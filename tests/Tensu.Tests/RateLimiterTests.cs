using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Xunit;

namespace Tensu.Tests;

public class RateLimiterTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly RateLimiter _limiter;

    public RateLimiterTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
        var logger = new Mock<ILogger<RateLimiter>>();
        _limiter = new RateLimiter(_db, logger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CheckRateLimit_AllowsWithinLimit()
    {
        var (allowed, _) = await _limiter.CheckRateLimitAsync(apiKeyId: 1, rpmLimit: 10, tpmLimit: null);
        Assert.True(allowed);
    }

    [Fact]
    public async Task CheckRateLimit_DeniesWhenExceeded()
    {
        for (var i = 0; i < 10; i++)
            _limiter.RecordRequest(apiKeyId: 2, tokensUsed: 0);

        var (allowed, retryAfter) = await _limiter.CheckRateLimitAsync(apiKeyId: 2, rpmLimit: 10, tpmLimit: null);
        Assert.False(allowed);
        Assert.True(retryAfter > 0);
    }

    [Fact]
    public async Task CheckRateLimit_NoLimit_AllowsAlways()
    {
        var (allowed, _) = await _limiter.CheckRateLimitAsync(apiKeyId: 3, rpmLimit: null, tpmLimit: null);
        Assert.True(allowed);
    }

    [Fact]
    public async Task RecordRequest_IncrementsCounters()
    {
        _limiter.RecordRequest(apiKeyId: 4, tokensUsed: 100);
        var (allowed, _) = await _limiter.CheckRateLimitAsync(apiKeyId: 4, rpmLimit: 5, tpmLimit: null);
        Assert.True(allowed);
    }

    [Fact]
    public async Task TryAcquireConcurrencyAsync_AllowsWithinLimit()
    {
        var acquired = await _limiter.TryAcquireConcurrencyAsync(modelId: 1, limit: 2);
        Assert.True(acquired);
        var acquired2 = await _limiter.TryAcquireConcurrencyAsync(modelId: 1, limit: 2);
        Assert.True(acquired2);
        var acquired3 = await _limiter.TryAcquireConcurrencyAsync(modelId: 1, limit: 2);
        Assert.False(acquired3);

        await _limiter.ReleaseConcurrencyAsync(1);
        var acquired4 = await _limiter.TryAcquireConcurrencyAsync(modelId: 1, limit: 2);
        Assert.True(acquired4);
    }

    [Fact]
    public async Task CheckRateLimit_DeniesWhenRpmExceeded()
    {
        for (var i = 0; i < 5; i++)
            _limiter.RecordRequest(apiKeyId: 10, tokensUsed: 0);

        var (allowed, retryAfter) = await _limiter.CheckRateLimitAsync(apiKeyId: 10, rpmLimit: 5, tpmLimit: null);
        Assert.False(allowed);
        Assert.True(retryAfter > 0);
    }

    [Fact]
    public async Task CheckRateLimit_DeniesWhenTpmExceeded()
    {
        _limiter.RecordRequest(apiKeyId: 11, tokensUsed: 1000);

        var (allowed, retryAfter) = await _limiter.CheckRateLimitAsync(apiKeyId: 11, rpmLimit: null, tpmLimit: 1000, estimatedTokens: 100);
        Assert.False(allowed);
        Assert.True(retryAfter > 0);
    }

    [Fact]
    public async Task CheckRateLimit_MultipleKeys_DoNotInterfere()
    {
        for (var i = 0; i < 5; i++)
            _limiter.RecordRequest(apiKeyId: 20, tokensUsed: 0);

        var (allowed1, _) = await _limiter.CheckRateLimitAsync(apiKeyId: 20, rpmLimit: 5, tpmLimit: null);
        Assert.False(allowed1);

        var (allowed2, _) = await _limiter.CheckRateLimitAsync(apiKeyId: 21, rpmLimit: 5, tpmLimit: null);
        Assert.True(allowed2);
    }
}
