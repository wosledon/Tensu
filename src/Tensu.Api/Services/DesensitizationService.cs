using System.Text.Json;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

/// <summary>
/// Provides content desensitization based on organization configuration.
/// Supports masking API keys, tokens, and custom patterns.
/// </summary>
public class DesensitizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Desensitize request/response content based on organization settings.
    /// </summary>
    public string? Desensitize(string? content, Organization org)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        if (!org.EnableContentLogging)
            return "[Content logging disabled by organization policy]";

        var result = content;

        // Mask API keys (long alphanumeric strings that look like keys)
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\b[A-Za-z0-9]{32,}\b",
            "[REDACTED_KEY]");

        // Mask common token patterns
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\b(sk-|pk-|api-|key-|token-)[A-Za-z0-9]{20,}\b",
            "[REDACTED_TOKEN]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Mask email addresses
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            "[REDACTED_EMAIL]");

        // Mask IP addresses
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\b(\d{1,3}\.){3}\d{1,3}\b",
            "[REDACTED_IP]");

        return result;
    }

    /// <summary>
    /// Desensitize a JSON object by redacting sensitive fields.
    /// </summary>
    public string DesensitizeJson(string jsonContent, HashSet<string> sensitiveFields)
    {
        if (string.IsNullOrEmpty(jsonContent))
            return jsonContent;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var redacted = RedactJsonElement(doc.RootElement, sensitiveFields);
            return JsonSerializer.Serialize(redacted, JsonOptions);
        }
        catch
        {
            // If JSON parsing fails, fall back to string-level desensitization
            return Desensitize(jsonContent, new Organization { EnableContentLogging = true }) ?? jsonContent;
        }
    }

    private object RedactJsonElement(JsonElement element, HashSet<string> sensitiveFields)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>();
                foreach (var prop in element.EnumerateObject())
                {
                    if (sensitiveFields.Contains(prop.Name))
                    {
                        dict[prop.Name] = "[REDACTED]";
                    }
                    else
                    {
                        dict[prop.Name] = RedactJsonElement(prop.Value, sensitiveFields);
                    }
                }
                return dict;

            case JsonValueKind.Array:
                return element.EnumerateArray().Select(e => RedactJsonElement(e, sensitiveFields)).ToList();

            case JsonValueKind.String:
                return Desensitize(element.GetString() ?? string.Empty, new Organization { EnableContentLogging = true }) ?? element.GetString();

            default:
                return element.GetRawText();
        }
    }

    /// <summary>
    /// Default sensitive field patterns for JSON desensitization.
    /// </summary>
    public static readonly HashSet<string> DefaultSensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "api_key",
        "apikey",
        "key",
        "secret",
        "password",
        "token",
        "access_token",
        "refresh_token",
        "authorization",
        "x-api-key",
        "auth"
    };
}
