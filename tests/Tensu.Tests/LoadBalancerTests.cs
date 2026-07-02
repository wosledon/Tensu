using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;
using Xunit;

namespace Tensu.Tests;

public class LoadBalancerTests
{
    private readonly LoadBalancer _lb = new();

    [Fact]
    public void SelectKey_RoundRobin_DistributesAcrossKeys()
    {
        var provider = new Provider
        {
            Id = 1,
            KeyLoadBalanceStrategy = LoadBalanceStrategy.RoundRobin,
            Keys =
            [
                new ProviderKey { Id = 1, Status = KeyStatus.Active, Weight = 1 },
                new ProviderKey { Id = 2, Status = KeyStatus.Active, Weight = 1 },
                new ProviderKey { Id = 3, Status = KeyStatus.Active, Weight = 1 },
            ]
        };

        var results = new List<int>();
        for (var i = 0; i < 9; i++)
            results.Add(_lb.SelectKey(provider)!.Id);

        // All three keys should appear in the results
        Assert.Contains(1, results);
        Assert.Contains(2, results);
        Assert.Contains(3, results);
        Assert.Equal(9, results.Count);
    }

    [Fact]
    public void SelectKey_Weighted_RespectsWeight()
    {
        var provider = new Provider
        {
            Id = 2,
            KeyLoadBalanceStrategy = LoadBalanceStrategy.Weighted,
            Keys =
            [
                new ProviderKey { Id = 1, Status = KeyStatus.Active, Weight = 10 },
                new ProviderKey { Id = 2, Status = KeyStatus.Active, Weight = 0 },
            ]
        };

        // With weight 10 vs 0, key 1 should always be selected
        for (var i = 0; i < 10; i++)
            Assert.Equal(1, _lb.SelectKey(provider)!.Id);
    }

    [Fact]
    public void SelectKey_SkipsDisabledKeys()
    {
        var provider = new Provider
        {
            Id = 3,
            KeyLoadBalanceStrategy = LoadBalanceStrategy.RoundRobin,
            Keys =
            [
                new ProviderKey { Id = 1, Status = KeyStatus.Disabled, Weight = 1 },
                new ProviderKey { Id = 2, Status = KeyStatus.Active, Weight = 1 },
            ]
        };

        for (var i = 0; i < 5; i++)
            Assert.Equal(2, _lb.SelectKey(provider)!.Id);
    }

    [Fact]
    public void SelectKey_NoActiveKeys_ReturnsNull()
    {
        var provider = new Provider
        {
            Id = 4,
            Keys = [new ProviderKey { Id = 1, Status = KeyStatus.Disabled }]
        };

        Assert.Null(_lb.SelectKey(provider));
    }

    [Fact]
    public void RecordLatency_AffectsOrdering()
    {
        _lb.RecordLatency(1, 100);
        _lb.RecordLatency(2, 50);

        var models = new List<Model>
        {
            new() { Id = 1, ProviderId = 1, Provider = new Provider { Id = 1 } },
            new() { Id = 2, ProviderId = 2, Provider = new Provider { Id = 2 } },
        };

        var ordered = _lb.OrderByLatency(models);
        Assert.Equal(2, ordered[0].ProviderId); // Lower latency first
    }
}
