using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Entities;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Response cache service. Supports exact cache (hash-based, in-memory) and semantic cache (database-backed, simple bag-of-words similarity).
/// Cache key: model + messages hash + parameters hash.
/// </summary>
public class CacheService : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<CacheService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly Timer _cleanupTimer;
    private bool _disposed;
    private const int MaxCacheEntries = 10000;

    public CacheService(ILogger<CacheService> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public CacheService(ILogger<CacheService> logger, IServiceScopeFactory scopeFactory) : this(logger)
    {
        _scopeFactory = scopeFactory;
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
                entry.HitCount++;
                _logger.LogDebug("Cache HIT for key {CacheKey}", cacheKey[..Math.Min(16, cacheKey.Length)]);
                return (true, entry.ResponseBody, entry.IsStream);
            }
            // Expired
            _cache.TryRemove(cacheKey, out _);
        }
        return null;
    }

    /// <summary>
    /// Store a response in the exact cache.
    /// </summary>
    public void Set(string cacheKey, string responseBody, bool isStream, TimeSpan? ttl = null)
    {
        if (_cache.Count >= MaxCacheEntries && !_cache.ContainsKey(cacheKey))
        {
            var oldest = _cache
                .Where(kv => kv.Value.ExpiresAt > DateTime.UtcNow)
                .OrderBy(kv => kv.Value.CachedAt)
                .Take(_cache.Count - MaxCacheEntries + 1)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in oldest)
                _cache.TryRemove(key, out _);
        }

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
    /// Generate a simple bag-of-words embedding for the given text.
    /// Tokenizes by whitespace and punctuation, lowercases, counts frequencies,
    /// and returns a normalized 100-dimensional vector using the top 100 most frequent words.
    /// </summary>
    public static float[] GenerateEmbedding(string text)
    {
        const int Dimensions = 100;
        var wordCounts = Tokenize(text)
            .GroupBy(w => w)
            .ToDictionary(g => g.Key, g => g.Count());

        var vocabulary = wordCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(Dimensions)
            .Select(kv => kv.Key)
            .ToList();

        var vector = vocabulary.Count == 0
            ? new float[Dimensions]
            : BuildVector(text, vocabulary).ToArray();

        // Pad or truncate to ensure exactly 100 dimensions.
        if (vector.Length < Dimensions)
        {
            var padded = new float[Dimensions];
            vector.CopyTo(padded, 0);
            vector = padded;
        }
        else if (vector.Length > Dimensions)
        {
            vector = vector[..Dimensions];
        }

        return Normalize(vector);
    }

    /// <summary>
    /// Extract text content from a request body and generate its semantic embedding.
    /// </summary>
    public static float[] ComputeSemanticEmbedding(string requestBody)
    {
        var text = ExtractTextContent(requestBody);
        return GenerateEmbedding(text);
    }

    /// <summary>
    /// Try to get a semantically similar cached response for the given request.
    /// </summary>
    public async Task<(bool hit, string? responseBody, bool isStream)?> TryGetSemanticAsync(
        string model, string requestBody, float threshold = 0.9f, CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null)
        {
            _logger.LogWarning("Semantic cache is not available because no service scope factory was provided");
            return null;
        }

        var requestEmbedding = ComputeSemanticEmbedding(requestBody);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();

        var now = DateTime.UtcNow;
        var entries = await db.SemanticCacheEntries
            .AsNoTracking()
            .Where(e => e.Model == model && e.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        SemanticCacheEntry? bestEntry = null;
        var bestSimilarity = 0f;

        foreach (var entry in entries)
        {
            var entryEmbedding = ComputeSemanticEmbedding(entry.RequestBody);
            var similarity = CosineSimilarity(requestEmbedding, entryEmbedding);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestEntry = entry;
            }
        }

        if (bestEntry != null && bestSimilarity >= threshold)
        {
            _logger.LogDebug(
                "Semantic cache HIT for model {Model} with similarity {Similarity}",
                model, bestSimilarity);
            return (true, bestEntry.ResponseBody, bestEntry.IsStream);
        }

        return null;
    }

    /// <summary>
    /// Store a response in the semantic cache.
    /// </summary>
    public async Task SetSemanticAsync(
        string model, string requestBody, string responseBody, bool isStream,
        TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null)
        {
            _logger.LogWarning("Semantic cache is not available because no service scope factory was provided");
            return;
        }

        var embedding = ComputeSemanticEmbedding(requestBody);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();

        var effectiveTtl = ttl ?? TimeSpan.FromMinutes(30);
        var maxEntries = await GetSemanticMaxEntriesAsync(db);

        var count = await db.SemanticCacheEntries.CountAsync(cancellationToken);
        while (count >= maxEntries)
        {
            var oldest = await db.SemanticCacheEntries
                .OrderBy(e => e.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (oldest == null) break;

            db.SemanticCacheEntries.Remove(oldest);
            await db.SaveChangesAsync(cancellationToken);
            count--;
        }

        db.SemanticCacheEntries.Add(new SemanticCacheEntry
        {
            Model = model,
            Embedding = JsonSerializer.Serialize(embedding),
            RequestBody = requestBody,
            ResponseBody = responseBody,
            IsStream = isStream,
            ExpiresAt = DateTime.UtcNow + effectiveTtl,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Semantic cache SET for model {Model}", model);
    }

    /// <summary>
    /// Convert a cached SSE stream body into a non-stream JSON response.
    /// </summary>
    public static string ConvertStreamToNonStream(string sseBody)
    {
        var aggregatedContent = new StringBuilder();
        var finishReason = "stop";

        using var reader = new StringReader(sseBody);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            try
            {
                var json = JsonDocument.Parse(data);
                if (json.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var content))
                    {
                        aggregatedContent.Append(content.GetString());
                    }
                    else if (first.TryGetProperty("message", out var message) &&
                             message.TryGetProperty("content", out var msgContent))
                    {
                        aggregatedContent.Append(msgContent.GetString());
                    }

                    if (first.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
                    {
                        finishReason = fr.GetString() ?? finishReason;
                    }
                }
            }
            catch { }
        }

        var response = new
        {
            choices = new[]
            {
                new
                {
                    message = new { role = "assistant", content = aggregatedContent.ToString() },
                    finish_reason = finishReason
                }
            }
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
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
    /// Get exact cache statistics.
    /// </summary>
    public (int entries, int totalHits) GetStats()
    {
        return (_cache.Count, _cache.Values.Sum(e => e.HitCount));
    }

    /// <summary>
    /// Get exact cache entry count.
    /// </summary>
    public int GetExactEntryCount() => _cache.Count;

    /// <summary>
    /// Clear the exact cache.
    /// </summary>
    public void ClearExact()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Clear the semantic cache (database-backed).
    /// </summary>
    public async Task ClearSemanticAsync(CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
        var entries = await db.SemanticCacheEntries.ToListAsync(cancellationToken);
        db.SemanticCacheEntries.RemoveRange(entries);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Get semantic cache entry count.
    /// </summary>
    public async Task<int> GetSemanticEntryCountAsync(CancellationToken cancellationToken = default)
    {
        if (_scopeFactory == null) return 0;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
        return await db.SemanticCacheEntries.CountAsync(cancellationToken);
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

    private static IEnumerable<string> Tokenize(string text)
    {
        return Regex.Matches(text.ToLowerInvariant(), @"\b\w+\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(w => w.Length > 0);
    }

    private static float[] BuildVector(string text, List<string> vocabulary)
    {
        var wordCounts = Tokenize(text)
            .GroupBy(w => w)
            .ToDictionary(g => g.Key, g => g.Count());

        return vocabulary
            .Select(word => wordCounts.TryGetValue(word, out var count) ? (float)count : 0f)
            .ToArray();
    }

    private static float[] Normalize(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude == 0) return vector;
        return vector.Select(v => v / magnitude).ToArray();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;

        var dot = a.Zip(b, (x, y) => x * y).Sum();
        var magA = MathF.Sqrt(a.Sum(x => x * x));
        var magB = MathF.Sqrt(b.Sum(y => y * y));

        if (magA == 0 || magB == 0) return 0;
        return dot / (magA * magB);
    }

    private static string ExtractTextContent(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody)) return string.Empty;

        try
        {
            var json = JsonDocument.Parse(requestBody);
            var root = json.RootElement;
            var content = new StringBuilder();

            if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                foreach (var message in messages.EnumerateArray())
                {
                    AppendContent(message, content);
                }
            }
            else if (root.TryGetProperty("content", out var topLevelContent))
            {
                AppendContentValue(topLevelContent, content);
            }

            return content.Length > 0 ? content.ToString() : requestBody;
        }
        catch
        {
            return requestBody;
        }
    }

    private static void AppendContent(JsonElement message, StringBuilder content)
    {
        if (!message.TryGetProperty("content", out var contentElement)) return;
        AppendContentValue(contentElement, content);
    }

    private static void AppendContentValue(JsonElement contentElement, StringBuilder content)
    {
        switch (contentElement.ValueKind)
        {
            case JsonValueKind.String:
                content.Append(contentElement.GetString()).Append(' ');
                break;
            case JsonValueKind.Array:
                foreach (var item in contentElement.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                    {
                        content.Append(text.GetString()).Append(' ');
                    }
                    else if (item.ValueKind == JsonValueKind.String)
                    {
                        content.Append(item.GetString()).Append(' ');
                    }
                }
                break;
        }
    }

    private static async Task<int> GetSemanticMaxEntriesAsync(TensuDbContext db)
    {
        var setting = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "semanticCache.maxEntries");
        return int.TryParse(setting?.Value, out var maxEntries) ? maxEntries : 1000;
    }

    private class CacheEntry
    {
        public string ResponseBody { get; set; } = "";
        public bool IsStream { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int HitCount { get; set; }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
