using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Infrastructure;
using Xunit;

namespace Tensu.Tests;

public class RateLimiterTests
{
    private readonly RateLimiter _limiter;

    public RateLimiterTests()
    {
        var logger = new Mock<ILogger<RateLimiter>>();
        _limiter = new RateLimiter(logger.Object);
    }

    [Fact]
    public void CheckRateLimit_AllowsWithinLimit()
    {
        var (allowed, _) = _limiter.CheckRateLimit(apiKeyId: 1, rpmLimit: 10, tpmLimit: null);
        Assert.True(allowed);
    }

    [Fact]
    public void CheckRateLimit_DeniesWhenExceeded()
    {
        // Fill up the RPM limit
        for (var i = 0; i < 10; i++)
            _limiter.RecordRequest(apiKeyId: 2, tokensUsed: 0);

        var (allowed, retryAfter) = _limiter.CheckRateLimit(apiKeyId: 2, rpmLimit: 10, tpmLimit: null);
        Assert.False(allowed);
        Assert.True(retryAfter > 0);
    }

    [Fact]
    public void CheckRateLimit_NoLimit_AllowsAlways()
    {
        var (allowed, _) = _limiter.CheckRateLimit(apiKeyId: 3, rpmLimit: null, tpmLimit: null);
        Assert.True(allowed);
    }

    [Fact]
    public void RecordRequest_IncrementsCounters()
    {
        _limiter.RecordRequest(apiKeyId: 4, tokensUsed: 100);
        // Should still be allowed with limit of 5
        var (allowed, _) = _limiter.CheckRateLimit(apiKeyId: 4, rpmLimit: 5, tpmLimit: null);
        Assert.True(allowed);
    }
}
