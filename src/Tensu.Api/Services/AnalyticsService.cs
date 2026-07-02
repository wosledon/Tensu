using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;

namespace Tensu.Api.Services;

/// <summary>
/// Aggregation service for computing analytics from request logs.
/// Provides usage, cost, performance, and cache statistics.
/// </summary>
public class AnalyticsService
{
    private readonly TensuDbContext _db;

    public AnalyticsService(TensuDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get dashboard overview: today's stats.
    /// </summary>
    public async Task<object> GetDashboardAsync(int? orgId = null)
    {
        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var query = _db.RequestLogs.Where(r => r.Timestamp >= today);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var todayLogs = await query.ToListAsync();

        var weekQuery = _db.RequestLogs.Where(r => r.Timestamp >= weekAgo);
        if (orgId.HasValue) weekQuery = weekQuery.Where(r => r.OrganizationId == orgId.Value);
        var weekLogs = await weekQuery.ToListAsync();

        // Daily trend (last 7 days)
        var dailyTrend = weekLogs
            .GroupBy(r => r.Timestamp.Date)
            .Select(g => new
            {
                date = g.Key.ToString("MM-dd"),
                requests = g.Count(),
                tokens = g.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0)),
                cost = g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)),
                successRate = g.Count() > 0 ? Math.Round((double)g.Count(r => r.Status == Core.Enums.RequestStatus.Success) / g.Count() * 100, 1) : 0,
                avgLatency = g.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average()
            })
            .OrderBy(x => x.date)
            .ToList();

        // Model distribution (last 7 days)
        var modelDistribution = weekLogs
            .GroupBy(r => r.ModelName)
            .Select(g => new { name = g.Key, value = g.Count() })
            .OrderByDescending(x => x.value)
            .Take(8)
            .ToList();

        // Provider health
        var providers = await _db.Providers
            .Select(p => new { p.Name, p.HealthStatus, p.IsEnabled })
            .ToListAsync();

        return new
        {
            today = new
            {
                requests = todayLogs.Count,
                tokens = todayLogs.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0)),
                cost = Math.Round(todayLogs.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 4),
                cacheHitRate = todayLogs.Count > 0 ? Math.Round((double)todayLogs.Count(r => r.CacheHit) / todayLogs.Count * 100, 1) : 0,
                avgLatency = todayLogs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average(),
                successRate = todayLogs.Count > 0 ? Math.Round((double)todayLogs.Count(r => r.Status == Core.Enums.RequestStatus.Success) / todayLogs.Count * 100, 1) : 0,
            },
            dailyTrend,
            modelDistribution,
            providers
        };
    }

    /// <summary>
    /// Get usage statistics over a time range.
    /// </summary>
    public async Task<object> GetUsageAsync(DateTime from, DateTime to, string granularity = "day", int? orgId = null)
    {
        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();

        var grouped = granularity switch
        {
            "hour" => logs.GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0)),
            "week" => logs.GroupBy(r => r.Timestamp.Date.AddDays(-(int)r.Timestamp.DayOfWeek)),
            "month" => logs.GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, 1)),
            _ => logs.GroupBy(r => r.Timestamp.Date)
        };

        return grouped.Select(g => new
        {
            time = g.Key.ToString(granularity == "hour" ? "MM-dd HH:mm" : "yyyy-MM-dd"),
            inputTokens = g.Sum(r => r.InputTokens ?? 0),
            outputTokens = g.Sum(r => r.OutputTokens ?? 0),
            totalTokens = g.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0)),
            requests = g.Count(),
            success = g.Count(r => r.Status == Core.Enums.RequestStatus.Success),
            failed = g.Count(r => r.Status == Core.Enums.RequestStatus.Failed),
            cacheHits = g.Count(r => r.CacheHit),
            compressionSaved = g.Sum(r => (r.InputTokens ?? 0) - (r.InputTokensAfterCompression ?? r.InputTokens ?? 0)),
        }).OrderBy(x => x.time).ToList();
    }

    /// <summary>
    /// Get cost statistics.
    /// </summary>
    public async Task<object> GetCostAsync(DateTime from, DateTime to, int? orgId = null)
    {
        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();

        var byModel = logs
            .GroupBy(r => r.ModelName)
            .Select(g => new
            {
                model = g.Key,
                totalCost = Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
                requests = g.Count(),
                avgCostPerRequest = g.Count() > 0 ? Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)) / g.Count(), 6) : 0,
            })
            .OrderByDescending(x => x.totalCost)
            .ToList();

        var byProvider = logs
            .GroupBy(r => r.ProviderName ?? "unknown")
            .Select(g => new
            {
                provider = g.Key,
                totalCost = Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
                requests = g.Count(),
            })
            .OrderByDescending(x => x.totalCost)
            .ToList();

        var daily = logs
            .GroupBy(r => r.Timestamp.Date)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                cost = Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
            })
            .OrderBy(x => x.date)
            .ToList();

        return new
        {
            totalCost = Math.Round(logs.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
            totalRequests = logs.Count,
            avgCostPerRequest = logs.Count > 0 ? Math.Round(logs.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)) / logs.Count, 6) : 0,
            compressionSavings = Math.Round(logs.Sum(r =>
            {
                var original = r.InputTokens ?? 0;
                var compressed = r.InputTokensAfterCompression ?? original;
                return (original - compressed) * 0.00001m; // rough estimate
            }), 6),
            byModel,
            byProvider,
            daily
        };
    }

    /// <summary>
    /// Get performance statistics.
    /// </summary>
    public async Task<object> GetPerformanceAsync(DateTime from, DateTime to, int? orgId = null)
    {
        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to && r.Status == Core.Enums.RequestStatus.Success);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();

        var latencies = logs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).OrderBy(x => x).ToList();
        var ttfts = logs.Where(r => r.TimeToFirstTokenMs.HasValue).Select(r => (double)r.TimeToFirstTokenMs!.Value).OrderBy(x => x).ToList();
        var speeds = logs.Where(r => r.OutputTokensPerSecond.HasValue).Select(r => r.OutputTokensPerSecond!.Value).OrderBy(x => x).ToList();

        var byModel = logs
            .GroupBy(r => r.ModelName)
            .Select(g => new
            {
                model = g.Key,
                avgLatency = Math.Round(g.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average(), 1),
                p95Latency = Percentile(g.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).OrderBy(x => x).ToList(), 95),
                avgSpeed = Math.Round(g.Where(r => r.OutputTokensPerSecond.HasValue).Select(r => r.OutputTokensPerSecond!.Value).DefaultIfEmpty(0).Average(), 1),
                requests = g.Count(),
            })
            .OrderByDescending(x => x.requests)
            .ToList();

        return new
        {
            overall = new
            {
                avgLatency = Math.Round(latencies.DefaultIfEmpty(0).Average(), 1),
                p50Latency = Percentile(latencies, 50),
                p95Latency = Percentile(latencies, 95),
                p99Latency = Percentile(latencies, 99),
                avgTtft = Math.Round(ttfts.DefaultIfEmpty(0).Average(), 1),
                p95Ttft = Percentile(ttfts, 95),
                avgSpeed = speeds.Count > 0 ? (double)Math.Round(speeds.Average(), 1) : 0,
            },
            byModel
        };
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public async Task<object> GetCacheStatsAsync(DateTime from, DateTime to, int? orgId = null)
    {
        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();
        var total = logs.Count;
        var hits = logs.Count(r => r.CacheHit);

        var byModel = logs
            .GroupBy(r => r.ModelName)
            .Select(g => new
            {
                model = g.Key,
                total = g.Count(),
                hits = g.Count(r => r.CacheHit),
                hitRate = g.Count() > 0 ? Math.Round((double)g.Count(r => r.CacheHit) / g.Count() * 100, 1) : 0,
            })
            .OrderByDescending(x => x.hits)
            .ToList();

        return new
        {
            totalRequests = total,
            cacheHits = hits,
            hitRate = total > 0 ? Math.Round((double)hits / total * 100, 1) : 0,
            byModel
        };
    }

    private static double Percentile(List<double> sorted, int percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return Math.Round(sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))], 1);
    }
}
