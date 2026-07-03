using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Tests;

public class AnomalyDetectionServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly SettingsService _settings;
    private readonly AnomalyDetectionService _service;

    public AnomalyDetectionServiceTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
        _settings = new SettingsService(_db);
        _service = new AnomalyDetectionService(_db, _settings);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private DateTime CurrentWindowStart => new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc);
    private DateTime CurrentWindowEnd => new DateTime(2026, 7, 3, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public async Task DetectAsync_UsageSpike_ReturnsUsageSpikeAnomaly()
    {
        // Baseline: 70 requests over 7 days ago => average 10/day
        await AddLogsAsync(CurrentWindowStart.AddDays(-7), CurrentWindowEnd.AddDays(-7), 70, status: RequestStatus.Success, model: "gpt-4o");
        // Current: 200 requests today
        await AddLogsAsync(CurrentWindowStart, CurrentWindowEnd, 200, status: RequestStatus.Success, model: "gpt-4o");

        var results = await _service.DetectAsync(CurrentWindowStart, CurrentWindowEnd);

        var spike = results.FirstOrDefault(r => r.Type == "UsageSpike" && r.Dimension == "model:gpt-4o");
        Assert.NotNull(spike);
        Assert.True(spike.CurrentValue > spike.BaselineValue * 2);
    }

    [Fact]
    public async Task DetectAsync_CostSpike_ReturnsCostSpikeAnomaly()
    {
        // Baseline: $70 over 7 days ago => average $10/day
        await AddLogsAsync(CurrentWindowStart.AddDays(-7), CurrentWindowEnd.AddDays(-7), 10, status: RequestStatus.Success, model: "gpt-4o", cost: 7.0m);
        // Current: $200 today
        await AddLogsAsync(CurrentWindowStart, CurrentWindowEnd, 10, status: RequestStatus.Success, model: "gpt-4o", cost: 20.0m);

        var results = await _service.DetectAsync(CurrentWindowStart, CurrentWindowEnd);

        var costSpike = results.FirstOrDefault(r => r.Type == "CostSpike" && r.Dimension == "model:gpt-4o");
        Assert.NotNull(costSpike);
        Assert.True(costSpike.CurrentValue > costSpike.BaselineValue * 2);
    }

    [Fact]
    public async Task DetectAsync_ErrorRateSpike_ReturnsErrorRateAnomaly()
    {
        // Current: 15 failed out of 20 requests => 75% error rate
        await AddLogsAsync(CurrentWindowStart, CurrentWindowEnd, 5, status: RequestStatus.Success, model: "claude-3");
        await AddLogsAsync(CurrentWindowStart, CurrentWindowEnd, 15, status: RequestStatus.Failed, model: "claude-3");

        var results = await _service.DetectAsync(CurrentWindowStart, CurrentWindowEnd);

        var errorRateSpike = results.FirstOrDefault(r => r.Type == "ErrorRateSpike" && r.Dimension == "model:claude-3");
        Assert.NotNull(errorRateSpike);
        Assert.True(errorRateSpike.CurrentValue >= 0.1);
    }

    [Fact]
    public async Task DetectAsync_ProviderSuccessRateDrops_ReturnsProviderDegradedAnomaly()
    {
        // Current: 20 requests, 5 success, 15 failed => 25% success rate
        await AddLogsAsync(CurrentWindowStart, CurrentWindowEnd, 5, status: RequestStatus.Success, provider: "OpenAI", model: "gpt-4o");
        await AddLogsAsync(CurrentWindowStart, CurrentWindowEnd, 15, status: RequestStatus.Failed, provider: "OpenAI", model: "gpt-4o");

        var results = await _service.DetectAsync(CurrentWindowStart, CurrentWindowEnd);

        var degraded = results.FirstOrDefault(r => r.Type == "ProviderDegraded" && r.Dimension == "provider:OpenAI");
        Assert.NotNull(degraded);
        Assert.Equal("Critical", degraded.Severity);
        Assert.True(degraded.CurrentValue < 0.95);
    }

    private async Task AddLogsAsync(DateTime from, DateTime to, int count, RequestStatus status, string model = "gpt-4o", string provider = "OpenAI", decimal? cost = null)
    {
        var random = new Random(42);
        var duration = to - from;
        for (var i = 0; i < count; i++)
        {
            var fraction = count == 1 ? 0.5 : (double)i / (count - 1);
            var timestamp = from.AddSeconds(duration.TotalSeconds * fraction);
            _db.RequestLogs.Add(new RequestLog
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Timestamp = timestamp,
                ModelName = model,
                ProviderName = provider,
                Status = status,
                InputTokens = 10,
                OutputTokens = 10,
                InputCost = cost * 0.5m ?? 0,
                OutputCost = cost * 0.5m ?? 0,
                TotalDurationMs = random.Next(100, 1000),
                CacheHit = false,
                OrganizationId = 1,
                ApiKeyId = 1
            });
        }

        await _db.SaveChangesAsync();
    }
}
