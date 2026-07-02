using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Response cache service. Supports exact cache (hash-based) and semantic cache (placeholder).
/// Cache key: model + messages hash + parameters hash.
/// </summary>
public class CacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<CacheService> _logger;
    private readonly Timer _cleanupTimer;

    public CacheService(ILogger<CacheService> logger)
    {
        _logger = logger;
        // Cleanup expired entries every 5 minutes
        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Try to get a cached response for the given request.
    /// </summary>
    public (bool hit, string? responseBody, bool isStream)? TryGet(string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogDebug("Cache HIT for key {CacheKey}", cacheKey[..Math.Min(16, cacheKey.Length)]);
                return (true, entry.ResponseBody, entry.IsStream);
            }
            // Expired
            _cache.TryRemove(cacheKey, out _);
        }
        return null;
    }

    /// <summary>
    /// Store a response in the cache.
    /// </summary>
    public void Set(string cacheKey, string responseBody, bool isStream, TimeSpan? ttl = null)
    {
        var entry = new CacheEntry
        {
            ResponseBody = responseBody,
            IsStream = isStream,
            CachedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + (ttl ?? TimeSpan.FromMinutes(10))
        };
        _cache.AddOrUpdate(cacheKey, entry, (_, _) => entry);
        _logger.LogDebug("Cache SET for key {CacheKey}, TTL {TTL}", cacheKey[..Math.Min(16, cacheKey.Length)], ttl ?? TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// Compute cache key from model name, messages, and parameters.
    /// </summary>
    public static string ComputeCacheKey(string model, string requestBody)
    {
        try
        {
            var json = JsonDocument.Parse(requestBody);
            var root = json.RootElement;

            // Extract messages for hashing
            var messagesHash = "";
            if (root.TryGetProperty("messages", out var messages))
                messagesHash = ComputeHash(messages.GetRawText());

            // Extract parameters (exclude stream, model from hash)
            var paramParts = new List<string>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name is "messages" or "stream") continue;
                paramParts.Add($"{prop.Name}={prop.Value.GetRawText()}");
            }
            var paramsHash = ComputeHash(string.Join("&", paramParts));

            return $"{model}:{messagesHash}:{paramsHash}";
        }
        catch
        {
            // Fallback: hash entire body
            return $"{model}:{ComputeHash(requestBody)}";
        }
    }

    /// <summary>
    /// Invalidate cache entries for a specific model.
    /// </summary>
    public void InvalidateModel(string model)
    {
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith($"{model}:")).ToList();
        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public (int entries, int totalHits) GetStats()
    {
        return (_cache.Count, _cache.Values.Sum(e => e.HitCount));
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        var expired = _cache.Where(kv => kv.Value.ExpiresAt <= now).Select(kv => kv.Key).ToList();
        foreach (var key in expired)
            _cache.TryRemove(key, out _);

        if (expired.Count > 0)
            _logger.LogDebug("Cache cleanup: removed {Count} expired entries", expired.Count);
    }

    private static string ComputeHash(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash)[..22]; // Short but collision-resistant
    }

    private class CacheEntry
    {
        public string ResponseBody { get; set; } = "";
        public bool IsStream { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int HitCount { get; set; }
    }
}
