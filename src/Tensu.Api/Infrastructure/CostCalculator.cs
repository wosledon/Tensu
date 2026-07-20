using System.Text.Json;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Extracts authoritative token usage from upstream responses and computes request
/// cost honoring cached-input and thinking (reasoning) pricing tiers.
/// </summary>
public static class CostCalculator
{
    public readonly record struct UpstreamUsage(
        int? InputTokens,
        int? OutputTokens,
        int CachedInputTokens,
        int ReasoningTokens)
    {
        public bool HasAny => InputTokens.HasValue || OutputTokens.HasValue;
    }

    /// <summary>
    /// Extract usage from a non-stream response body root element.
    /// OpenAI: usage.{prompt_tokens, completion_tokens, prompt_tokens_details.cached_tokens,
    /// completion_tokens_details.reasoning_tokens}.
    /// Anthropic: usage.{input_tokens, output_tokens, cache_read_input_tokens}.
    /// </summary>
    public static UpstreamUsage ExtractUsage(JsonElement root, ProtocolType protocol)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return default;

        if (protocol == ProtocolType.Anthropic)
        {
            int? input = GetInt(usage, "input_tokens");
            int? output = GetInt(usage, "output_tokens");
            var cached = GetInt(usage, "cache_read_input_tokens") ?? 0;
            return new UpstreamUsage(input, output, cached, 0);
        }

        int? prompt = GetInt(usage, "prompt_tokens");
        int? completion = GetInt(usage, "completion_tokens");
        var cachedTokens = 0;
        if (usage.TryGetProperty("prompt_tokens_details", out var ptd) && ptd.ValueKind == JsonValueKind.Object)
            cachedTokens = GetInt(ptd, "cached_tokens") ?? 0;
        var reasoningTokens = 0;
        if (usage.TryGetProperty("completion_tokens_details", out var ctd) && ctd.ValueKind == JsonValueKind.Object)
            reasoningTokens = GetInt(ctd, "reasoning_tokens") ?? 0;
        return new UpstreamUsage(prompt, completion, cachedTokens, reasoningTokens);
    }

    /// <summary>
    /// Extract usage from a single stream chunk. Returns null when the chunk carries none.
    /// OpenAI: a chunk with a top-level "usage" object (sent when include_usage is set).
    /// Anthropic: message_start (message.usage) and message_delta (usage) events.
    /// </summary>
    public static UpstreamUsage? ExtractStreamUsage(JsonElement chunkRoot, ProtocolType protocol)
    {
        if (protocol == ProtocolType.Anthropic)
        {
            if (!chunkRoot.TryGetProperty("type", out var typeProp)) return null;
            var type = typeProp.GetString();
            if (type == "message_start" &&
                chunkRoot.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.Object)
            {
                var usage = ExtractUsage(message, protocol);
                return usage.HasAny ? usage : null;
            }
            if (type == "message_delta")
            {
                var usage = ExtractUsage(chunkRoot, protocol);
                return usage.HasAny ? usage : null;
            }
            return null;
        }

        var openAiUsage = ExtractUsage(chunkRoot, protocol);
        return openAiUsage.HasAny ? openAiUsage : null;
    }

    /// <summary>
    /// Compute (inputCost, outputCost) using the currently effective pricing tier.
    /// Cached input tokens are billed at CachedInputPricePerMillionTokens (falling back to
    /// the input price) and reasoning tokens at ThinkingPricePerMillionTokens (falling
    /// back to the output price).
    /// </summary>
    public static (decimal inputCost, decimal outputCost) Compute(
        Model model, int inputTokens, int outputTokens, int cachedInputTokens = 0, int reasoningTokens = 0)
    {
        var now = DateTime.UtcNow;
        var pricing = (model.Pricings ?? [])
            .Where(p => p.EffectiveFrom <= now && (p.EffectiveTo == null || p.EffectiveTo >= now))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefault();

        if (pricing == null) return (0, 0);

        var cachedInput = Math.Clamp(cachedInputTokens, 0, inputTokens);
        var reasoning = Math.Clamp(reasoningTokens, 0, outputTokens);

        var cachedPrice = pricing.CachedInputPricePerMillionTokens ?? pricing.InputPricePerMillionTokens;
        var thinkingPrice = pricing.ThinkingPricePerMillionTokens ?? pricing.OutputPricePerMillionTokens;

        var inputCost = ((inputTokens - cachedInput) * pricing.InputPricePerMillionTokens
            + cachedInput * cachedPrice) / 1_000_000m;
        var outputCost = ((outputTokens - reasoning) * pricing.OutputPricePerMillionTokens
            + reasoning * thinkingPrice) / 1_000_000m;

        if (pricing.ExchangeRate > 0)
        {
            inputCost /= pricing.ExchangeRate;
            outputCost /= pricing.ExchangeRate;
        }

        return (inputCost, outputCost);
    }

    private static int? GetInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) && prop.TryGetInt32(out var value) ? value : null;
    }
}
