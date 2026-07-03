using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;
using Xunit;

namespace Tensu.Tests;

public class RetryPolicyTests
{
    private readonly Mock<ILogger<RetryPolicy>> _logger = new();
    private readonly LoadBalancer _loadBalancer = new();

    [Fact]
    public async Task ExecuteWithRetryAsync_FirstFailsSecondSucceeds_WithDifferentKey()
    {
        var provider = new Provider
        {
            Id = 1,
            KeyLoadBalanceStrategy = LoadBalanceStrategy.RoundRobin,
            Keys =
            [
                new ProviderKey { Id = 1, Status = KeyStatus.Active },
                new ProviderKey { Id = 2, Status = KeyStatus.Active }
            ]
        };

        var policy = new RetryPolicy(_logger.Object, _loadBalancer) { BaseDelayMs = 10 };
        var attempts = 0;

        var result = await policy.ExecuteWithRetryAsync(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            },
            provider,
            provider.Keys.First(),
            isStream: false);

        Assert.Equal(2, attempts);
        Assert.Equal(HttpStatusCode.OK, result!.StatusCode);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_RetrySelectsDifferentKey()
    {
        var provider = new Provider
        {
            Id = 2,
            KeyLoadBalanceStrategy = LoadBalanceStrategy.RoundRobin,
            Keys =
            [
                new ProviderKey { Id = 1, Status = KeyStatus.Active },
                new ProviderKey { Id = 2, Status = KeyStatus.Active }
            ]
        };

        var policy = new RetryPolicy(_logger.Object, _loadBalancer) { BaseDelayMs = 10 };
        var usedKeys = new List<int>();

        var result = await policy.ExecuteWithRetryAsync(
            key =>
            {
                usedKeys.Add(key.Id);
                if (key.Id == 1)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            },
            provider,
            provider.Keys.First(),
            isStream: false);

        Assert.Equal(2, usedKeys.Count);
        Assert.Contains(1, usedKeys);
        Assert.Contains(2, usedKeys);
        Assert.Equal(2, usedKeys.Last());
        Assert.Equal(HttpStatusCode.OK, result!.StatusCode);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_AllKeysFail_Throws()
    {
        var provider = new Provider
        {
            Id = 3,
            Keys =
            [
                new ProviderKey { Id = 1, Status = KeyStatus.Active },
                new ProviderKey { Id = 2, Status = KeyStatus.Active }
            ]
        };

        var policy = new RetryPolicy(_logger.Object, _loadBalancer) { MaxRetries = 2, BaseDelayMs = 10 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteWithRetryAsync(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            provider,
            provider.Keys.First(),
            isStream: false));
    }
}
