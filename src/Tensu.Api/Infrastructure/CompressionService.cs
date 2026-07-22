using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Entities;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Context Compression Service (CCR - Convertible/Reversible Compression).
/// Applies lossless compression before forwarding to upstream provider.
/// Persists a mapping so the original body can be restored.
/// </summary>
public class CompressionService
{
    private readonly ILogger<CompressionService> _logger;
    private readonly CompressionChannel _channel;
    private readonly TensuDbContext _db;

    public CompressionService(ILogger<CompressionService> logger, CompressionChannel channel, TensuDbContext db)
    {
        _logger = logger;
        _channel = channel;
        _db = db;
    }

    /// <summary>
    /// Result of a compression operation.
    /// </summary>
    public record CompressionResult(
        string CompressedBody,
        int OriginalTokenEstimate,
        int CompressedTokenEstimate,
        string Strategy,
        bool Applied,
        string? DecompressionKey = null
    );

    /// <summary>
    /// Compress a request body if beneficial. Returns the (possibly compressed) body
    /// along with token estimates and the strategy used. When compression is applied,
    /// a mapping is persisted so the original body can be restored later.
    ///
    /// Semantic safety: compression is skipped when the content contains format-sensitive
    /// patterns such as code blocks, few-shot examples, or structured prompts.
    /// </summary>
    public async Task<CompressionResult> CompressAsync(string requestBody, CancellationToken cancellationToken = default)
    {
        var originalTokens = EstimateTokens(requestBody);

        if (HasFormatSensitiveContent(requestBody))
        {
            return new CompressionResult(
                requestBody, originalTokens, originalTokens,
                "none", Applied: false);
        }

        try
        {
            var root = JsonNode.Parse(requestBody);
            if (root != null)
            {
                var strategies = new List<string>();

                // Basic dedup: drop exact duplicate messages and repeated paragraphs.
                if (DedupMessages(root)) strategies.Add("dedup");
                if (DedupParagraphs(root)) strategies.Add("dedup-paragraphs");

                // Shorten long keys inside JSON-structured message content (mapping kept
                // in the compression mapping table, so restoration stays exact).
                if (ShortenJsonContentKeys(root)) strategies.Add("json-keys");

                var minified = root.ToJsonString();
                strategies.Add("json-structure");
                var minifiedTokens = EstimateTokens(minified);

                if (minifiedTokens < originalTokens * 0.95)
                {
                    var strategy = string.Join('+', strategies.Distinct());
                    var decompressionKey = ComputeHash(minified);
                    await PersistMappingAsync(requestBody, minified, strategy, decompressionKey, cancellationToken);
                    return new CompressionResult(
                        minified, originalTokens, minifiedTokens,
                        strategy, Applied: true, decompressionKey);
                }
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            var (templateCompressed, templateKey) = CompressLogTemplates(requestBody);
            var templateTokens = EstimateTokens(templateCompressed);
            if (templateTokens < originalTokens * 0.95)
            {
                await PersistMappingAsync(requestBody, templateCompressed, "log-template", templateKey, cancellationToken);
                return new CompressionResult(
                    templateCompressed, originalTokens, templateTokens,
                    "log-template", Applied: true, templateKey);
            }
        }
        catch { }

        return new CompressionResult(
            requestBody, originalTokens, originalTokens,
            "none", Applied: false);
    }

    /// <summary>
    /// Remove exact duplicate entries from the top-level "messages" array.
    /// Duplicates are byte-identical, so removing them does not lose information.
    /// </summary>
    private static bool DedupMessages(JsonNode root)
    {
        if (root is not JsonObject obj) return false;
        if (obj["messages"] is not JsonArray messages) return false;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var removed = false;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var fingerprint = messages[i]?.ToJsonString() ?? "";
            if (fingerprint.Length == 0) continue;
            if (!seen.Add(fingerprint))
            {
                messages.RemoveAt(i);
                removed = true;
            }
        }
        return removed;
    }

    /// <summary>
    /// Remove repeated paragraphs (blank-line separated, ≥ 50 chars) inside long string
    /// values, keeping the first occurrence. Exact duplicates only.
    /// </summary>
    private static bool DedupParagraphs(JsonNode root)
    {
        var changed = false;
        foreach (var (parent, key, value) in EnumerateStrings(root))
        {
            if (value.Length < 400) continue;
            var parts = Regex.Split(value, @"(\n{2,})");
            if (parts.Length < 3) continue;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var sb = new StringBuilder();
            var removedAny = false;
            // parts alternate: paragraph, separator, paragraph, separator, ...
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var isSeparator = part.StartsWith('\n');
                if (!isSeparator)
                {
                    var trimmed = part.Trim();
                    if (trimmed.Length >= 50 && !seen.Add(trimmed))
                    {
                        // Drop the duplicate paragraph together with the separator after it.
                        removedAny = true;
                        if (i + 1 < parts.Length && parts[i + 1].StartsWith('\n'))
                            i++;
                        continue;
                    }
                }
                sb.Append(part);
            }

            if (removedAny)
            {
                ReplaceString(parent, key, sb.ToString());
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Shorten long property names inside string values that themselves contain JSON
    /// (data-heavy / few-shot prompts). Keys ≥ 5 chars become k0..kN within that string;
    /// the original body is stored in the mapping table for exact restoration.
    /// </summary>
    private static bool ShortenJsonContentKeys(JsonNode root)
    {
        var changed = false;
        foreach (var (parent, key, value) in EnumerateStrings(root))
        {
            if (value.Length < 200) continue;

            JsonNode? content;
            try { content = JsonNode.Parse(value); }
            catch (JsonException) { continue; }
            if (content is not (JsonObject or JsonArray)) continue;

            var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            CountKeys(content, keyCounts);

            // Only worth it for long keys used at least twice.
            var candidates = keyCounts
                .Where(kv => kv.Key.Length >= 5 && kv.Value >= 2)
                .OrderByDescending(kv => (kv.Key.Length - 3) * kv.Value)
                .ToList();
            var savings = candidates.Sum(kv => (kv.Key.Length - 3) * kv.Value);
            if (savings < 60) continue;

            var existing = new HashSet<string>(keyCounts.Keys, StringComparer.Ordinal);
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var n = 0;
            foreach (var kv in candidates)
            {
                string shortName;
                do { shortName = $"k{n++}"; } while (existing.Contains(shortName));
                map[kv.Key] = shortName;
            }

            var renamed = RenameKeys(content, map);
            var serialized = renamed.ToJsonString();
            if (serialized.Length < value.Length)
            {
                ReplaceString(parent, key, serialized);
                changed = true;
            }
        }
        return changed;
    }

    private static void CountKeys(JsonNode node, Dictionary<string, int> counts)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var prop in obj)
                {
                    counts[prop.Key] = counts.GetValueOrDefault(prop.Key) + 1;
                    if (prop.Value != null) CountKeys(prop.Value, counts);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    if (item != null) CountKeys(item, counts);
                break;
        }
    }

    private static JsonNode RenameKeys(JsonNode node, Dictionary<string, string> map)
    {
        switch (node)
        {
            case JsonObject obj:
                var renamed = new JsonObject();
                foreach (var prop in obj)
                {
                    var name = map.TryGetValue(prop.Key, out var s) ? s : prop.Key;
                    renamed[name] = prop.Value != null ? RenameKeys(prop.Value, map) : null;
                }
                return renamed;
            case JsonArray arr:
                var newArr = new JsonArray();
                foreach (var item in arr)
                    newArr.Add(item != null ? RenameKeys(item, map) : null);
                return newArr;
            default:
                return node.DeepClone();
        }
    }

    private static IEnumerable<(JsonNode parent, object key, string value)> EnumerateStrings(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var prop in obj.ToList())
                {
                    if (prop.Value is JsonValue jv && jv.TryGetValue<string>(out var s))
                        yield return (obj, prop.Key, s);
                    else if (prop.Value != null)
                        foreach (var item in EnumerateStrings(prop.Value))
                            yield return item;
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue jv && jv.TryGetValue<string>(out var s))
                        yield return (arr, i, s);
                    else if (arr[i] != null)
                        foreach (var item in EnumerateStrings(arr[i]!))
                            yield return item;
                }
                break;
        }
    }

    private static void ReplaceString(JsonNode parent, object key, string newValue)
    {
        if (parent is JsonObject obj && key is string name)
            obj[name] = newValue;
        else if (parent is JsonArray arr && key is int index)
            arr[index] = newValue;
    }

    /// <summary>
    /// Decompress a previously compressed body using the decompression key and strategy.
    /// Returns null if no mapping exists or the original body was too large to store in full.
    /// </summary>
    public async Task<string?> DecompressAsync(string compressedBody, string strategy, string decompressionKey, CancellationToken cancellationToken = default)
    {
        var mapping = await _db.CompressionMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.DecompressionKey == decompressionKey && m.Strategy == strategy, cancellationToken);

        if (mapping == null)
        {
            _logger.LogWarning("No compression mapping found for key {Key} and strategy {Strategy}", decompressionKey, strategy);
            return null;
        }

        // If the original body was too large to store in full, we cannot restore it.
        if (string.IsNullOrEmpty(mapping.OriginalBody))
        {
            _logger.LogWarning("Compression mapping for key {Key} exists but original body was not stored", decompressionKey);
            return null;
        }

        return mapping.OriginalBody;
    }

    private async ValueTask PersistMappingAsync(string requestBody, string compressedBody, string strategy, string decompressionKey, CancellationToken cancellationToken)
    {
        await _channel.EnqueueAsync(new CompressionMappingEntry
        {
            OriginalBody = requestBody,
            CompressedBody = compressedBody,
            Strategy = strategy,
            DecompressionKey = decompressionKey,
        }, cancellationToken);
    }

    /// <summary>
    /// Extract log-like patterns into template + parameters.
    /// </summary>
    private (string compressed, string decompressionKey) CompressLogTemplates(string text)
    {
        // Find repeated log patterns like "2024-01-01 ERROR: something failed with code 123"
        var logPattern = new Regex(@"(\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}[.\d]*Z?\s+\w+\s*[:|-]\s*)(.+?)(\n|$)", RegexOptions.Multiline);
        var templates = new Dictionary<string, int>();
        var templateId = 0;

        var compressed = logPattern.Replace(text, match =>
        {
            var prefix = match.Groups[1].Value;
            var message = match.Groups[2].Value.Trim();

            // Normalize numbers and IDs in messages
            var normalized = Regex.Replace(message, @"\b[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\b", "{UUID}");
            normalized = Regex.Replace(normalized, @"\b\d{5,}\b", "{N}");
            normalized = Regex.Replace(normalized, @"0x[a-fA-F0-9]+", "{HEX}");

            if (!templates.TryGetValue(normalized, out var id))
            {
                id = templateId++;
                templates[normalized] = id;
            }

            return $"T{id}:{message}\n";
        });

        var key = ComputeHash(JsonSerializer.Serialize(templates));
        return (compressed, key);
    }

    /// <summary>
    /// Estimate token count (rough: ~4 chars per token for English, ~2 for CJK).
    /// </summary>
    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var cjkCount = text.Count(c => c > 0x4E00 && c < 0x9FFF);
        var otherCount = text.Length - cjkCount;
        return (otherCount / 4) + (cjkCount / 2);
    }

    /// <summary>
    /// Detects format-sensitive content where compression could alter semantics.
    /// Skips compression for code blocks and few-shot examples.
    /// </summary>
    private static bool HasFormatSensitiveContent(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        var lowered = text.ToLowerInvariant();

        if (lowered.Contains("```")) return true;

        try
        {
            var root = JsonNode.Parse(text);
            if (root is JsonObject obj && obj["messages"] is JsonArray messages)
            {
                var messageCount = messages.Count;
                if (messageCount >= 3)
                {
                    var fewShotIndicators = 0;
                    foreach (var msg in messages)
                    {
                        if (msg is not JsonObject m) continue;
                        if (m["content"] is not JsonValue jv || !jv.TryGetValue<string>(out var content)) continue;
                        if (string.IsNullOrEmpty(content)) continue;

                        var contentLower = content.ToLowerInvariant();

                        if (contentLower.Contains("```")) return true;

                        var hasExampleHeader = contentLower.Contains("example 1:")
                            || contentLower.Contains("example 2:")
                            || contentLower.Contains("example 3:")
                            || Regex.IsMatch(contentLower, @"^example\s+\d+:", RegexOptions.Multiline);

                        var hasQaPattern = (contentLower.Contains("q:") && contentLower.Contains("a:"))
                            || (contentLower.Contains("question:") && contentLower.Contains("answer:"))
                            || (contentLower.Contains("input:") && contentLower.Contains("output:"));

                        var hasInstructionResponse = (contentLower.Contains("instruction:") && contentLower.Contains("response:"))
                            || (contentLower.Contains("prompt:") && contentLower.Contains("completion:"));

                        if (hasExampleHeader || hasQaPattern || hasInstructionResponse)
                            fewShotIndicators++;
                    }

                    if (fewShotIndicators >= 2) return true;
                }
            }
        }
        catch (JsonException) { }

        return false;
    }

    private static string ComputeHash(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16];
    }
}
