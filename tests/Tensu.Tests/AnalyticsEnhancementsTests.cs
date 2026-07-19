using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Tensu.Core.Enums;
using Xunit;

namespace Tensu.Tests;

public class AnalyticsEnhancementsTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly AnalyticsService _service;

    public AnalyticsEnhancementsTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
        _service = new AnalyticsService(_db, new SettingsService(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static JsonElement Serialize(object result)
    {
        var json = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private async Task SeedCurrencyAsync()
    {
        _db.Settings.AddRange(
            new Setting { Key = "currency.default", Value = "USD" },
            new Setting { Key = "currency.rate.USD", Value = "1.0" },
            new Setting { Key = "currency.rate.CNY", Value = "7.2" });
        await _db.SaveChangesAsync();
    }

    private void AddLog(int? orgId = 1, bool cacheHit = false, string currency = "USD",
        int inputTokens = 100, int outputTokens = 50, decimal inputCost = 0.001m, decimal outputCost = 0.002m,
        DateTime? timestamp = null)
    {
        _db.RequestLogs.Add(new RequestLog
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Timestamp = timestamp ?? DateTime.UtcNow,
            OrganizationId = orgId,
            ModelName = "m",
            Status = RequestStatus.Success,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            InputCost = inputCost,
            OutputCost = outputCost,
            CacheHit = cacheHit,
            Currency = currency,
        });
    }

    [Fact]
    public async Task GetCostAsync_ConvertsToDefaultCurrency()
    {
        await SeedCurrencyAsync();
        AddLog(currency: "USD", inputCost: 1m, outputCost: 0m);   // 1 USD
        AddLog(currency: "CNY", inputCost: 7.2m, outputCost: 0m); // 7.2 CNY = 1 USD
        await _db.SaveChangesAsync();

        var root = Serialize(await _service.GetCostAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));

        Assert.True(root.GetProperty("currencyConversionApplied").GetBoolean());
        Assert.Equal("USD", root.GetProperty("defaultCurrency").GetString());
        Assert.Equal(2m, root.GetProperty("totalCostInDefaultCurrency").GetDecimal());
        Assert.Equal(2, root.GetProperty("byCurrency").GetArrayLength());
    }

    [Fact]
    public async Task GetCacheStatsAsync_ReportsSavedTokensAndCost()
    {
        await SeedCurrencyAsync();
        AddLog(cacheHit: true, inputTokens: 200, outputTokens: 100, inputCost: 0.5m, outputCost: 0.5m);
        AddLog(cacheHit: true, currency: "CNY", inputTokens: 100, outputTokens: 0, inputCost: 7.2m, outputCost: 0m);
        AddLog(cacheHit: false);
        await _db.SaveChangesAsync();

        var root = Serialize(await _service.GetCacheStatsAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));

        Assert.Equal(2, root.GetProperty("cacheHits").GetInt32());
        Assert.Equal(400, root.GetProperty("savedTokens").GetInt64());
        // 1 USD + 7.2 CNY/7.2 = 2 USD
        Assert.Equal(2m, root.GetProperty("savedCost").GetDecimal());
        Assert.Equal("USD", root.GetProperty("defaultCurrency").GetString());
    }

    [Fact]
    public async Task GetByOrganizationAsync_GroupsUsagePerOrg()
    {
        await SeedCurrencyAsync();
        _db.Organizations.AddRange(
            new Organization { Name = "OrgA", Path = "/a" },
            new Organization { Name = "OrgB", Path = "/b" });
        await _db.SaveChangesAsync();
        var orgA = await _db.Organizations.FirstAsync(o => o.Name == "OrgA");
        var orgB = await _db.Organizations.FirstAsync(o => o.Name == "OrgB");

        AddLog(orgId: orgA.Id, inputTokens: 100, outputTokens: 50);
        AddLog(orgId: orgA.Id, cacheHit: true);
        AddLog(orgId: orgB.Id, currency: "CNY", inputCost: 7.2m, outputCost: 0m);
        await _db.SaveChangesAsync();

        var root = Serialize(await _service.GetByOrganizationAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));
        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());

        var a = items.EnumerateArray().First(x => x.GetProperty("organizationId").GetInt32() == orgA.Id);
        Assert.Equal("OrgA", a.GetProperty("organizationName").GetString());
        Assert.Equal(2, a.GetProperty("requests").GetInt32());
        Assert.Equal(1, a.GetProperty("cacheHits").GetInt32());

        var b = items.EnumerateArray().First(x => x.GetProperty("organizationId").GetInt32() == orgB.Id);
        Assert.Equal(1m, b.GetProperty("totalCost").GetDecimal()); // 7.2 CNY converted to 1 USD
    }

    [Fact]
    public async Task GetUsageAsync_MinuteGranularity_GroupsByMinute()
    {
        var t0 = new DateTime(2026, 7, 1, 10, 30, 10, DateTimeKind.Utc);
        AddLog(timestamp: t0);
        AddLog(timestamp: t0.AddSeconds(20));
        AddLog(timestamp: t0.AddMinutes(1));
        await _db.SaveChangesAsync();

        var result = await _service.GetUsageAsync(t0.AddHours(-1), t0.AddHours(1), "minute");
        var root = Serialize(result);

        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(2, root[0].GetProperty("requests").GetInt32());
        Assert.Equal(1, root[1].GetProperty("requests").GetInt32());
    }
}
