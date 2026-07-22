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

    private string? _backendMode;
    private DateTime _backendModeExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _backendModeLock = new(1, 1);

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
    /// Resolves the configured exact-cache backend ("memory" or "database").
    /// The setting is cached for 60 seconds to avoid a database lookup per request.
    /// </summary>
    public void InvalidateBackendCache()
    {
        _backendMode = null;
        _backendModeExpiresAt = DateTime.MinValue;
        _logger.LogInformation("Cache backend configuration cache invalidated");
    }

    private async Task<string> GetBackendAsync()
    {
        if (_backendMode != null && DateTime.UtcNow < _backendModeExpiresAt)
            return _backendMode;

        await _backendModeLock.WaitAsync();
        try
        {
            if (_backendMode != null && DateTime.UtcNow < _backendModeExpiresAt)
                return _backendMode;

            var mode = "memory";
            if (_scopeFactory != null)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
                    var setting = await db.Settings.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Key == "cache.backend");
                    if (string.Equals(setting?.Value, "database", StringComparison.OrdinalIgnoreCase))
                        mode = "database";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve cache backend, falling back to memory");
                }
            }

            _backendMode = mode;
            _backendModeExpiresAt = DateTime.UtcNow.AddSeconds(60);
            return mode;
        }
        finally
        {
            _backendModeLock.Release();
        }
    }

    /// <summary>
    /// Try to get a cached response, honoring the configured cache backend
    /// (in-memory for single instance, database for multi-instance sharing).
    /// </summary>
    public async Task<(bool hit, string? responseBody, bool isStream)?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (await GetBackendAsync() == "memory" || _scopeFactory == null)
            return TryGet(cacheKey);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();

        var entry = await db.ExactCacheEntries
            .FirstOrDefaultAsync(e => e.CacheKey == cacheKey, cancellationToken);

        if (entry == null) return null;

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            db.ExactCacheEntries.Remove(entry);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        entry.HitCount++;
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Cache HIT (database) for key {CacheKey}", cacheKey[..Math.Min(16, cacheKey.Length)]);
        return (true, entry.ResponseBody, entry.IsStream);
    }

    /// <summary>
    /// Store a response in the exact cache, honoring the configured cache backend.
    /// </summary>
    public async Task SetAsync(string cacheKey, string responseBody, bool isStream, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        if (await GetBackendAsync() == "memory" || _scopeFactory == null)
        {
            Set(cacheKey, responseBody, isStream, ttl);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();

        var count = await db.ExactCacheEntries.CountAsync(cancellationToken);
        if (count >= MaxCacheEntries)
        {
            var overflow = await db.ExactCacheEntries
                .OrderBy(e => e.CreatedAt)
                .Take(count - MaxCacheEntries + 1)
                .ToListAsync(cancellationToken);
            db.ExactCacheEntries.RemoveRange(overflow);
        }

        var existing = await db.ExactCacheEntries
            .FirstOrDefaultAsync(e => e.CacheKey == cacheKey, cancellationToken);

        if (existing != null)
        {
            existing.ResponseBody = responseBody;
            existing.IsStream = isStream;
            existing.ExpiresAt = DateTime.UtcNow + (ttl ?? TimeSpan.FromMinutes(10));
        }
        else
        {
            db.ExactCacheEntries.Add(new ExactCacheEntry
            {
                CacheKey = cacheKey,
                ResponseBody = responseBody,
                IsStream = isStream,
                ExpiresAt = DateTime.UtcNow + (ttl ?? TimeSpan.FromMinutes(10)),
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Cache SET (database) for key {CacheKey}", cacheKey[..Math.Min(16, cacheKey.Length)]);
    }

    /// <summary>
    /// Number of dimensions of the semantic embedding vectors.
    /// Vectors are produced by feature hashing (unigrams + bigrams), so all texts
    /// share the same vector space and are directly comparable.
    /// </summary>
    public const int EmbeddingDimensions = 256;

    /// <summary>
    /// Generate a semantic embedding for the given text using the feature hashing trick.
    /// Tokens (word unigrams + bigrams, CJK characters treated as unigrams) are hashed
    /// into a fixed-dimension vector with log-scaled term frequencies, then L2-normalized.
    /// Unlike a per-document vocabulary, hashed vectors from different texts are comparable.
    /// </summary>
    public static float[] GenerateEmbedding(string text)
    {
        var vector = new float[EmbeddingDimensions];
        var tokens = Tokenize(text).ToList();
        if (tokens.Count == 0) return vector;

        // Log-scaled term frequencies keep long documents from dominating.
        foreach (var group in tokens.GroupBy(t => t))
            AddFeature(vector, group.Key, (float)Math.Log(1 + group.Count()));

        var bigrams = new List<string>();
        for (var i = 0; i + 1 < tokens.Count; i++)
            bigrams.Add(tokens[i] + " " + tokens[i + 1]);
        foreach (var group in bigrams.GroupBy(b => b))
            AddFeature(vector, group.Key, 0.5f * (float)Math.Log(1 + group.Count()));

        return Normalize(vector);
    }

    private static void AddFeature(float[] vector, string feature, float weight)
    {
        var hash = StableHash(feature);
        var bucket = (int)(hash % EmbeddingDimensions);
        // Signed hashing reduces systematic collision bias.
        var sign = (hash & 0x8000_0000) == 0 ? 1f : -1f;
        vector[bucket] += sign * weight;
    }

    private static uint StableHash(string value)
    {
        // FNV-1a 32-bit: deterministic across processes (unlike string.GetHashCode).
        const uint fnvPrime = 16777619;
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= fnvPrime;
        }
        return hash;
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
    /// Compute a semantic embedding for the given request body.
    /// When embedding.providerId and embedding.model settings are configured,
    /// calls the real embedding API of the specified OpenAI-protocol provider.
    /// Otherwise falls back to local FNV feature hashing.
    /// </summary>
    public async Task<float[]> ComputeSemanticEmbeddingAsync(string requestBody)
    {
        if (_scopeFactory == null)
            return ComputeSemanticEmbedding(requestBody);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();

            var providerIdStr = await db.Settings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "embedding.providerId");
            var modelName = await db.Settings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "embedding.model");

            if (string.IsNullOrEmpty(providerIdStr?.Value) || string.IsNullOrEmpty(modelName?.Value))
                return ComputeSemanticEmbedding(requestBody);

            if (!int.TryParse(providerIdStr.Value, out var providerId))
                return ComputeSemanticEmbedding(requestBody);

            var provider = await db.Providers.AsNoTracking()
                .Include(p => p.Keys)
                .FirstOrDefaultAsync(p => p.Id == providerId && p.IsEnabled);

            if (provider == null || provider.Protocol != Tensu.Core.Enums.ProtocolType.OpenAI)
                return ComputeSemanticEmbedding(requestBody);

            var activeKey = provider.Keys
                .Where(k => k.Status == Tensu.Core.Enums.KeyStatus.Active)
                .OrderByDescending(k => k.UpdatedAt)
                .FirstOrDefault();

            if (activeKey == null)
                return ComputeSemanticEmbedding(requestBody);

            var encryptionService = scope.ServiceProvider.GetRequiredService<EncryptionService>();
            var apiKey = encryptionService.Decrypt(activeKey.KeyValue);
            var baseUrl = provider.BaseUrl.TrimEnd('/');

            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var text = ExtractTextContent(requestBody);
            if (string.IsNullOrWhiteSpace(text))
                return ComputeSemanticEmbedding(requestBody);

            var requestPayload = new { model = modelName.Value, input = text };
            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await httpClient.PostAsync($"{baseUrl}/embeddings", content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Embedding API returned {StatusCode}, falling back to feature hashing", (int)response.StatusCode);
                return ComputeSemanticEmbedding(requestBody);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(responseBody);

            if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0
                && data[0].TryGetProperty("embedding", out var embeddingElement))
            {
                var vector = new float[embeddingElement.GetArrayLength()];
                var i = 0;
                foreach (var val in embeddingElement.EnumerateArray())
                    vector[i++] = (float)val.GetDouble();
                return Normalize(vector);
            }

            _logger.LogWarning("Embedding API returned no data, falling back to feature hashing");
            return ComputeSemanticEmbedding(requestBody);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding API call failed, falling back to feature hashing");
            return ComputeSemanticEmbedding(requestBody);
        }
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

        var requestEmbedding = await ComputeSemanticEmbeddingAsync(requestBody);

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
            var entryEmbedding = DeserializeEmbedding(entry.Embedding);
            if (entryEmbedding == null) continue;
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

        var embedding = await ComputeSemanticEmbeddingAsync(requestBody);

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
    public async Task InvalidateModelAsync(string model, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith($"{model}:")).ToList();
        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);

        if (_scopeFactory != null && await GetBackendAsync() == "database")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
            var prefix = $"{model}:";
            var entries = await db.ExactCacheEntries
                .Where(e => e.CacheKey.StartsWith(prefix))
                .ToListAsync(cancellationToken);
            db.ExactCacheEntries.RemoveRange(entries);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Get exact cache statistics for the configured backend.
    /// </summary>
    public async Task<(int entries, int totalHits)> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (await GetBackendAsync() == "memory" || _scopeFactory == null)
            return (_cache.Count, _cache.Values.Sum(e => e.HitCount));

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
        var entries = await db.ExactCacheEntries.CountAsync(cancellationToken);
        var hits = await db.ExactCacheEntries.SumAsync(e => (int?)e.HitCount, cancellationToken) ?? 0;
        return (entries, hits);
    }

    /// <summary>
    /// Get exact cache statistics (in-memory only).
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
    /// Clear the exact cache (both backends).
    /// </summary>
    public async Task ClearExactAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
            var entries = await db.ExactCacheEntries.ToListAsync(cancellationToken);
            db.ExactCacheEntries.RemoveRange(entries);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Clear the in-memory exact cache.
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

    private static float[]? DeserializeEmbedding(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var embedding = JsonSerializer.Deserialize<float[]>(json);
            if (embedding == null || embedding.Length == 0) return null;
            return embedding;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        foreach (Match m in Regex.Matches(text.ToLowerInvariant(), @"\b\w+\b").Cast<Match>())
        {
            var word = m.Value;
            if (word.Length == 0) continue;
            // CJK characters have no word boundaries; emit them as single-char tokens
            // so similarity matching works at character granularity.
            if (word.Any(IsCjk))
            {
                var latin = new System.Text.StringBuilder();
                foreach (var c in word)
                {
                    if (IsCjk(c))
                    {
                        if (latin.Length > 0) { yield return latin.ToString(); latin.Clear(); }
                        yield return c.ToString();
                    }
                    else
                    {
                        latin.Append(c);
                    }
                }
                if (latin.Length > 0) yield return latin.ToString();
            }
            else
            {
                yield return word;
            }
        }
    }

    private static bool IsCjk(char c) =>
        c is >= '一' and <= '鿿' or >= '぀' and <= 'ヿ' or >= '가' and <= '힣';

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
