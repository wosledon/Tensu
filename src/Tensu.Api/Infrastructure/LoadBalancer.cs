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
    private static readonly ConcurrentDictionary<int, double> _providerLatency = new(); // providerId -> avg latency ms
    private static readonly object _lock = new();

    /// <summary>
    /// Select a key from the provider's active keys based on the provider's load balance strategy.
    /// Degraded keys are excluded from normal selection and only used as a last resort.
    /// </summary>
    public ProviderKey? SelectKey(Provider provider)
    {
        var activeKeys = provider.Keys.Where(k => k.Status == KeyStatus.Active).ToList();
        if (activeKeys.Any()) return SelectKeyInternal(provider, activeKeys);

        // Fallback to degraded keys if no active keys remain
        var degradedKeys = provider.Keys.Where(k => k.Status == KeyStatus.Degraded).ToList();
        if (degradedKeys.Any()) return SelectKeyInternal(provider, degradedKeys);

        return null;
    }

    /// <summary>
    /// Select a key from the provider excluding the provided key ids.
    /// Degraded keys are excluded unless no active keys remain.
    /// </summary>
    public ProviderKey? SelectKey(Provider provider, HashSet<int> excludeKeyIds)
    {
        var activeKeys = provider.Keys.Where(k => k.Status == KeyStatus.Active && !excludeKeyIds.Contains(k.Id)).ToList();
        if (activeKeys.Any()) return SelectKeyInternal(provider, activeKeys);

        var degradedKeys = provider.Keys.Where(k => k.Status == KeyStatus.Degraded && !excludeKeyIds.Contains(k.Id)).ToList();
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

        // Try round-robin across providers
        var ordered = models.OrderBy(m => m.ProviderId).ToList();
        var counter = _roundRobinCounters.GetOrAdd(0, _ => 0);
        var startIndex = counter % ordered.Count;

        for (var i = 0; i < ordered.Count; i++)
        {
            var idx = (startIndex + i) % ordered.Count;
            var model = ordered[idx];
            var key = keySelector(model.Provider);
            if (key != null)
            {
                _roundRobinCounters.AddOrUpdate(0, 1, (_, v) => v + 1);
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
        // For keys within the same provider, just use round-robin
        // Lowest-latency is more useful across providers
        return keys[0];
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
