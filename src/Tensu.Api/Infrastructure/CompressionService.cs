using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    /// </summary>
    public async Task<CompressionResult> CompressAsync(string requestBody, CancellationToken cancellationToken = default)
    {
        var originalTokens = EstimateTokens(requestBody);

        try
        {
            var json = JsonDocument.Parse(requestBody);
            var minified = MinifyJson(json.RootElement);
            var minifiedTokens = EstimateTokens(minified);

            if (minifiedTokens < originalTokens * 0.95)
            {
                var decompressionKey = ComputeHash(minified);
                await PersistMappingAsync(requestBody, minified, "json-structure", decompressionKey, cancellationToken);
                return new CompressionResult(
                    minified, originalTokens, minifiedTokens,
                    "json-structure", Applied: true, decompressionKey);
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

    private string MinifyJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        });

        WriteElement(element, writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(property.Value, writer);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteElement(item, writer);
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString() ?? "");
                break;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                    writer.WriteNumberValue(l);
                else
                    writer.WriteNumberValue(element.GetDouble());
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
        }
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
        // Simple heuristic
        var cjkCount = text.Count(c => c > 0x4E00 && c < 0x9FFF);
        var otherCount = text.Length - cjkCount;
        return (otherCount / 4) + (cjkCount / 2);
    }

    private static string ComputeHash(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16];
    }
}
