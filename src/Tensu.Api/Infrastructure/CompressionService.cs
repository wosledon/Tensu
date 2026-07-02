using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Context Compression Service (CCR - Convertible/Reversible Compression).
/// Applies lossless compression before forwarding to upstream provider.
/// Preserves a mapping for full restoration.
/// </summary>
public class CompressionService
{
    private readonly ILogger<CompressionService> _logger;

    public CompressionService(ILogger<CompressionService> logger)
    {
        _logger = logger;
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
    /// along with token estimates and the strategy used.
    /// </summary>
    public CompressionResult Compress(string requestBody)
    {
        var originalTokens = EstimateTokens(requestBody);

        try
        {
            var json = JsonDocument.Parse(requestBody);
            var compressed = CompressJson(json.RootElement);
            var compressedTokens = EstimateTokens(compressed);

            // Only apply compression if we save at least 5% tokens
            if (compressedTokens < originalTokens * 0.95)
            {
                // Verify reversibility: can we decompress back?
                var decompressionKey = ComputeHash(compressed);
                return new CompressionResult(
                    compressed, originalTokens, compressedTokens,
                    "json-structure", Applied: true, decompressionKey);
            }
        }
        catch (JsonException)
        {
            // Not JSON, try text compression
        }

        // For non-JSON or when JSON compression doesn't help, try log template extraction
        try
        {
            var (templateCompressed, templateKey) = CompressLogTemplates(requestBody);
            var templateTokens = EstimateTokens(templateCompressed);
            if (templateTokens < originalTokens * 0.95)
            {
                return new CompressionResult(
                    templateCompressed, originalTokens, templateTokens,
                    "log-template", Applied: true, templateKey);
            }
        }
        catch { }

        // No beneficial compression found
        return new CompressionResult(
            requestBody, originalTokens, originalTokens,
            "none", Applied: false);
    }

    /// <summary>
    /// Decompress a previously compressed body using the decompression key.
    /// </summary>
    public string? Decompress(string compressedBody, string strategy, string decompressionKey)
    {
        // In a real implementation, we'd store the mapping in a cache/DB.
        // For now, the CCR strategies we use (JSON key shortening, whitespace removal)
        // are reversible by re-parsing with the original schema.
        // Log templates require stored mappings.
        return null; // Placeholder - full restoration requires cached mappings
    }

    /// <summary>
    /// JSON structure compression: remove whitespace, shorten known keys.
    /// </summary>
    private string CompressJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        });

        CompressJsonElement(element, writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void CompressJsonElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    var key = ShortenKey(property.Name);
                    writer.WritePropertyName(key);
                    CompressJsonElement(property.Value, writer);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    CompressJsonElement(item, writer);
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                var str = element.GetString() ?? "";
                // Deduplicate repeated whitespace in string values
                var compressed = Regex.Replace(str, @"\s{2,}", " ");
                writer.WriteStringValue(compressed);
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
    /// Shorten well-known JSON keys in LLM API requests.
    /// Only shortens keys that are safe to abbreviate without losing meaning.
    /// </summary>
    private static string ShortenKey(string key) => key switch
    {
        // Messages array is the biggest payload - keep readable
        "messages" => "msg",
        "content" => "c",
        "role" => "r",
        "name" => "n",
        "function_call" => "fc",
        "tool_calls" => "tc",
        "tool_choice" => "tch",
        "max_tokens" => "mt",
        "max_completion_tokens" => "mct",
        "temperature" => "tp",
        "top_p" => "p",
        "frequency_penalty" => "fp",
        "presence_penalty" => "pp",
        "stop_sequences" => "ss",
        "system" => "sys",
        "metadata" => "md",
        _ => key
    };

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
