using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Xunit;

namespace Tensu.Tests;

public class QuotaServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly QuotaService _quotaService;
    private readonly RateLimiter _rateLimiter;

    public QuotaServiceTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();

        var rateLimiterLogger = new Mock<ILogger<RateLimiter>>();
        _rateLimiter = new RateLimiter(_db, rateLimiterLogger.Object);
        var quotaLogger = new Mock<ILogger<QuotaService>>();
        _quotaService = new QuotaService(_db, _rateLimiter, quotaLogger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CheckOrgQuotaAsync_AllowsWithinLimit()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, DailyTokenLimit = 100, MonthlyTokenLimit = 1000 });
        await _db.SaveChangesAsync();

        var (allowed, _) = await _quotaService.CheckOrgQuotaAsync(org.Id, 50);
        Assert.True(allowed);
    }

    [Fact]
    public async Task CheckOrgQuotaAsync_DeniesWhenDailyExceeded()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, DailyTokenLimit = 100 });
        await _db.SaveChangesAsync();

        await _quotaService.RecordOrgUsageAsync(org.Id, 90);
        var (allowed, retryAfter) = await _quotaService.CheckOrgQuotaAsync(org.Id, 20);
        Assert.False(allowed);
        Assert.True(retryAfter > 0);
    }

    [Fact]
    public async Task GetConcurrentRequestLimitAsync_ReturnsKeySpecificOrOrgValue()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, ConcurrentRequestLimit = 5 });
        await _db.SaveChangesAsync();

        var limit = await _quotaService.GetConcurrentRequestLimitAsync(org.Id);
        Assert.Equal(5, limit);
    }

    [Fact]
    public async Task CheckOrgQuotaAsync_DeniesWhenMonthlyExceeded()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, MonthlyTokenLimit = 100 });
        await _db.SaveChangesAsync();

        await _quotaService.RecordOrgUsageAsync(org.Id, 90);
        var (allowed, retryAfter) = await _quotaService.CheckOrgQuotaAsync(org.Id, 20);
        Assert.False(allowed);
        Assert.True(retryAfter > 0);
    }

    [Fact]
    public async Task CheckOrgQuotaAsync_ResetsAcrossDays()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, DailyTokenLimit = 100 });
        await _db.SaveChangesAsync();

        var yesterday = DateTime.UtcNow.AddDays(-1).Date;
        var dayScope = $"org:{org.Id}:day:{yesterday:yyyy-MM-dd}";
        await _rateLimiter.IncrementAsync(dayScope, 999);

        var (allowed, _) = await _quotaService.CheckOrgQuotaAsync(org.Id, 50);
        Assert.True(allowed);
    }

    [Fact]
    public async Task CheckOrgQuotaAsync_ResetsAcrossMonths()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, MonthlyTokenLimit = 100 });
        await _db.SaveChangesAsync();

        var lastMonth = DateTime.UtcNow.AddMonths(-1);
        var monthStart = new DateTime(lastMonth.Year, lastMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthScope = $"org:{org.Id}:month:{monthStart:yyyy-MM}";
        await _rateLimiter.IncrementAsync(monthScope, 999);

        var (allowed, _) = await _quotaService.CheckOrgQuotaAsync(org.Id, 50);
        Assert.True(allowed);
    }

    [Fact]
    public async Task ModelQuota_DailyLimit_DeniesWhenExceeded()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, ModelId = 7, DailyTokenLimit = 100 });
        await _db.SaveChangesAsync();

        var quota = await _quotaService.GetModelQuotaAsync(org.Id, 7);
        Assert.NotNull(quota);
        Assert.Equal("model", quota!.Scope);

        await _quotaService.RecordModelUsageAsync(7, 90);
        var (allowed, retryAfter) = await _quotaService.CheckModelTokenQuotaAsync(quota, 7, 20);
        Assert.False(allowed);
        Assert.True(retryAfter > 0);

        // A different model is unaffected.
        var (otherAllowed, _) = await _quotaService.CheckModelTokenQuotaAsync(quota, 8, 20);
        Assert.True(otherAllowed);
    }

    [Fact]
    public async Task GetConcurrentRequestLimitAsync_ModelQuotaTakesPrecedence()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, ConcurrentRequestLimit = 5 });
        _db.Quotas.Add(new Quota { OrganizationId = org.Id, ModelId = 3, ConcurrentRequestLimit = 2 });
        await _db.SaveChangesAsync();

        Assert.Equal(2, await _quotaService.GetConcurrentRequestLimitAsync(org.Id, modelId: 3));
        Assert.Equal(5, await _quotaService.GetConcurrentRequestLimitAsync(org.Id, modelId: 4));
        Assert.Equal(5, await _quotaService.GetConcurrentRequestLimitAsync(org.Id));
    }

    [Fact]
    public async Task GetListAsync_ScopeFilter_FiltersModelQuotas()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        _db.Quotas.Add(new Quota { OrganizationId = org.Id, DailyTokenLimit = 1 });
        _db.Quotas.Add(new Quota { OrganizationId = org.Id, ApiKeyId = 1, DailyTokenLimit = 1 });
        _db.Quotas.Add(new Quota { OrganizationId = org.Id, ModelId = 1, DailyTokenLimit = 1 });
        await _db.SaveChangesAsync();

        var model = await _quotaService.GetListAsync(new Tensu.Core.Common.PagedRequest(), scope: "model");
        Assert.Single(model.Items);
        Assert.Equal("model", model.Items[0].Scope);

        var key = await _quotaService.GetListAsync(new Tensu.Core.Common.PagedRequest(), scope: "key");
        Assert.Single(key.Items);
        Assert.Equal("key", key.Items[0].Scope);

        var orgOnly = await _quotaService.GetListAsync(new Tensu.Core.Common.PagedRequest(), scope: "org");
        Assert.Single(orgOnly.Items);
        Assert.Equal("org", orgOnly.Items[0].Scope);
    }

    [Fact]
    public async Task CreateAsync_ModelScope_ClearsApiKeyId()
    {
        _db.Organizations.Add(new Organization { Name = "Test", Path = "/" });
        await _db.SaveChangesAsync();
        var org = await _db.Organizations.FirstAsync();

        var created = await _quotaService.CreateAsync(new Quota
        {
            OrganizationId = org.Id,
            Scope = "model",
            ModelId = 5,
            ApiKeyId = 9,
            DailyTokenLimit = 100
        });

        Assert.Null(created.ApiKeyId);
        Assert.Equal(5, created.ModelId);
        Assert.Equal("model", created.Scope);
    }

    [Fact]
    public async Task ModelRateLimit_ScopePrefix_EnforcesRpm()
    {
        // model:9 RPM=1 — first check passes, second is denied.
        var (first, _) = await _rateLimiter.CheckRateLimitAsync("model:9", rpmLimit: 1, tpmLimit: null);
        Assert.True(first);

        await _rateLimiter.RecordRequestAsync("model:9", 0);

        var (second, retryAfter) = await _rateLimiter.CheckRateLimitAsync("model:9", rpmLimit: 1, tpmLimit: null);
        Assert.False(second);
        Assert.True(retryAfter > 0);
    }
}
