using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class StatsAggregationService
{
    private readonly TensuDbContext _db;

    public StatsAggregationService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task AggregateDayAsync(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        var groups = await _db.RequestLogs.AsNoTracking()
            .Where(r => r.Timestamp >= dayStart && r.Timestamp < dayEnd)
            .GroupBy(r => new { r.OrganizationId, r.ModelName, r.ProviderName })
            .Select(g => new
            {
                g.Key.OrganizationId,
                g.Key.ModelName,
                g.Key.ProviderName,
                TotalRequests = g.Count(),
                SuccessRequests = g.Count(r => r.Status == Tensu.Core.Enums.RequestStatus.Success),
                FailedRequests = g.Count(r => r.Status == Tensu.Core.Enums.RequestStatus.Failed || r.Status == Tensu.Core.Enums.RequestStatus.Timeout),
                RateLimitedRequests = g.Count(r => r.Status == Tensu.Core.Enums.RequestStatus.RateLimited),
                CacheHits = g.Count(r => r.CacheHit),
                TotalInputTokens = g.Sum(r => (long)(r.InputTokens ?? 0)),
                TotalOutputTokens = g.Sum(r => (long)(r.OutputTokens ?? 0)),
                TotalInputTokensAfterCompression = g.Sum(r => (long)(r.InputTokensAfterCompression ?? r.InputTokens ?? 0)),
                TotalInputCost = g.Sum(r => r.InputCost ?? 0),
                TotalOutputCost = g.Sum(r => r.OutputCost ?? 0),
                AvgLatencyMs = (long?)g.Where(r => r.TotalDurationMs.HasValue).Average(r => (double?)r.TotalDurationMs!.Value),
                AvgTtftMs = (long?)g.Where(r => r.TimeToFirstTokenMs.HasValue).Average(r => (double?)r.TimeToFirstTokenMs!.Value),
                AvgOutputTokensPerSecond = g.Where(r => r.OutputTokensPerSecond.HasValue).Average(r => (double?)r.OutputTokensPerSecond),
            })
            .ToListAsync();

        if (!groups.Any()) return;

        foreach (var g in groups)
        {
            var existing = await _db.DailyStats
                .FirstOrDefaultAsync(s => s.Date == dayStart
                    && s.OrganizationId == g.OrganizationId
                    && s.ModelName == g.ModelName
                    && s.ProviderName == g.ProviderName);

            var stat = existing ?? new DailyStat
            {
                Date = dayStart,
                OrganizationId = g.OrganizationId,
                ModelName = g.ModelName,
                ProviderName = g.ProviderName,
                CreatedAt = DateTime.UtcNow
            };

            stat.TotalRequests = g.TotalRequests;
            stat.SuccessRequests = g.SuccessRequests;
            stat.FailedRequests = g.FailedRequests;
            stat.RateLimitedRequests = g.RateLimitedRequests;
            stat.CacheHits = g.CacheHits;
            stat.TotalInputTokens = g.TotalInputTokens;
            stat.TotalOutputTokens = g.TotalOutputTokens;
            stat.TotalInputTokensAfterCompression = g.TotalInputTokensAfterCompression;
            stat.TotalInputCost = g.TotalInputCost;
            stat.TotalOutputCost = g.TotalOutputCost;
            stat.AvgLatencyMs = g.AvgLatencyMs.HasValue ? (long)g.AvgLatencyMs.Value : null;
            stat.AvgTtftMs = g.AvgTtftMs.HasValue ? (long)g.AvgTtftMs.Value : null;
            stat.AvgOutputTokensPerSecond = g.AvgOutputTokensPerSecond;

            if (existing == null)
                _db.DailyStats.Add(stat);
        }

        await _db.SaveChangesAsync();
    }
}

public class StatsAggregationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatsAggregationBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

    public StatsAggregationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<StatsAggregationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
                var service = new StatsAggregationService(db);

                var yesterday = DateTime.UtcNow.Date.AddDays(-1);
                await service.AggregateDayAsync(yesterday);

                var today = DateTime.UtcNow.Date;
                await service.AggregateDayAsync(today);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stats aggregation failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
