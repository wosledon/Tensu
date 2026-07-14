using System.Collections.Concurrent;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Load balancer for selecting provider keys and models.
/// Supports round-robin, weighted, and lowest-latency strategies.
/// </summary>
public class LoadBalancer
{
    private static readonly ConcurrentDictionary<int, int> _roundRobinCounters = new();
    private static readonly ConcurrentDictionary<int, double> _providerLatency = new();
    private static readonly ConcurrentDictionary<int, double> _keyLatency = new();

    /// <summary>
    /// Select a key from the provider based on health status.
    /// Healthy keys are preferred; degraded keys are used as fallback when no healthy keys remain.
    /// Unhealthy/inactive keys are excluded.
    /// </summary>
    public ProviderKey? SelectKey(Provider provider)
    {
        var healthyKeys = provider.Keys.Where(k => k.Status == KeyStatus.Active).ToList();
        if (healthyKeys.Any()) return SelectKeyInternal(provider, healthyKeys);

        var degradedKeys = provider.Keys.Where(k => k.Status == KeyStatus.Degraded).ToList();
        if (degradedKeys.Any()) return SelectKeyInternal(provider, degradedKeys);

        return null;
    }

    /// <summary>
    /// Select a key from the provider excluding the provided key ids.
    /// Healthy keys are preferred; degraded keys are used as fallback when no healthy keys remain.
    /// </summary>
    public ProviderKey? SelectKey(Provider provider, HashSet<int> excludeKeyIds)
    {
        var healthyKeys = provider.Keys
            .Where(k => k.Status == KeyStatus.Active && !excludeKeyIds.Contains(k.Id))
            .ToList();
        if (healthyKeys.Any()) return SelectKeyInternal(provider, healthyKeys);

        var degradedKeys = provider.Keys
            .Where(k => k.Status == KeyStatus.Degraded && !excludeKeyIds.Contains(k.Id))
            .ToList();
        if (degradedKeys.Any()) return SelectKeyInternal(provider, degradedKeys);

        return null;
    }

    private ProviderKey? SelectKeyInternal(Provider provider, List<ProviderKey> keys)
    {
        if (!keys.Any()) return null;

        return provider.KeyLoadBalanceStrategy switch
        {
            LoadBalanceStrategy.Weighted => SelectWeighted(keys),
            LoadBalanceStrategy.LowestLatency => SelectLowestLatency(keys),
            LoadBalanceStrategy.Failover => SelectFailover(keys, provider.Id),
            _ => SelectRoundRobin(keys, provider.Id),
        };
    }

    /// <summary>
    /// Select a model from multiple providers offering the same model name.
    /// Uses round-robin across providers, with fallback on failure.
    /// </summary>
    public (Model model, ProviderKey key)? SelectModelWithKey(List<Model> models, Func<Provider, ProviderKey?> keySelector)
    {
        if (!models.Any()) return null;

        var ordered = models.OrderBy(m => m.ProviderId).ToList();
        var counter = _roundRobinCounters.AddOrUpdate(0, 1, (_, v) => v + 1);
        var startIndex = (counter - 1) % ordered.Count;

        for (var i = 0; i < ordered.Count; i++)
        {
            var idx = (startIndex + i) % ordered.Count;
            var model = ordered[idx];
            var key = keySelector(model.Provider);
            if (key != null)
            {
                return (model, key);
            }
        }

        return null;
    }

    /// <summary>
    /// Record latency for a provider to improve future lowest-latency selection.
    /// </summary>
    public void RecordLatency(int providerId, double latencyMs)
    {
        _providerLatency.AddOrUpdate(providerId, latencyMs, (_, existing) =>
            existing * 0.7 + latencyMs * 0.3); // exponential moving average
    }

    /// <summary>
    /// Record latency for a specific key to improve key-level lowest-latency selection.
    /// </summary>
    public void RecordKeyLatency(int keyId, double latencyMs)
    {
        _keyLatency.AddOrUpdate(keyId, latencyMs, (_, existing) =>
            existing * 0.7 + latencyMs * 0.3);
    }

    private ProviderKey SelectRoundRobin(List<ProviderKey> keys, int providerId)
    {
        var counter = _roundRobinCounters.AddOrUpdate(providerId, 1, (_, v) => v + 1);
        var index = counter % keys.Count;
        return keys[index];
    }

    private ProviderKey SelectFailover(List<ProviderKey> keys, int providerId)
    {
        // Failover: prefer the first healthy key in the list, cycling on retries
        return SelectRoundRobin(keys, providerId);
    }

    private ProviderKey SelectWeighted(List<ProviderKey> keys)
    {
        var totalWeight = keys.Sum(k => k.Weight);
        if (totalWeight <= 0) return keys[0];

        var random = Random.Shared.Next(totalWeight);
        var cumulative = 0;
        foreach (var key in keys)
        {
            cumulative += key.Weight;
            if (random < cumulative) return key;
        }
        return keys[^1];
    }

    private ProviderKey SelectLowestLatency(List<ProviderKey> keys)
    {
        if (keys.Count == 1) return keys[0];

        var best = keys[0];
        var bestLatency = _keyLatency.TryGetValue(best.Id, out var l0) ? l0 : double.MaxValue;

        foreach (var key in keys.Skip(1))
        {
            var latency = _keyLatency.TryGetValue(key.Id, out var l) ? l : double.MaxValue;
            if (latency < bestLatency)
            {
                best = key;
                bestLatency = latency;
            }
        }

        return best;
    }

    /// <summary>
    /// Get providers ordered by latency for same-model load balancing.
    /// </summary>
    public List<Model> OrderByLatency(List<Model> models)
    {
        return models.OrderBy(m =>
            _providerLatency.TryGetValue(m.ProviderId, out var latency) ? latency : double.MaxValue
        ).ToList();
    }
}
