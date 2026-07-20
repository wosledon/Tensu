using System.Text.Json;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;
using Xunit;

namespace Tensu.Tests;

public class CostCalculatorTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static Model CreateModel(
        decimal inputPrice = 10m,
        decimal outputPrice = 30m,
        decimal? cachedPrice = null,
        decimal? thinkingPrice = null)
    {
        return new Model
        {
            Name = "test-model",
            Pricings =
            [
                new ModelPricing
                {
                    InputPricePerMillionTokens = inputPrice,
                    OutputPricePerMillionTokens = outputPrice,
                    CachedInputPricePerMillionTokens = cachedPrice,
                    ThinkingPricePerMillionTokens = thinkingPrice,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                }
            ]
        };
    }

    [Fact]
    public void ExtractUsage_OpenAI_ParsesDetails()
    {
        var root = Parse("""{"usage":{"prompt_tokens":100,"completion_tokens":50,"prompt_tokens_details":{"cached_tokens":40},"completion_tokens_details":{"reasoning_tokens":20}}}""");

        var usage = CostCalculator.ExtractUsage(root, ProtocolType.OpenAI);

        Assert.Equal(100, usage.InputTokens);
        Assert.Equal(50, usage.OutputTokens);
        Assert.Equal(40, usage.CachedInputTokens);
        Assert.Equal(20, usage.ReasoningTokens);
        Assert.True(usage.HasAny);
    }

    [Fact]
    public void ExtractUsage_Anthropic_ParsesCacheRead()
    {
        var root = Parse("""{"usage":{"input_tokens":80,"output_tokens":10,"cache_read_input_tokens":30}}""");

        var usage = CostCalculator.ExtractUsage(root, ProtocolType.Anthropic);

        Assert.Equal(80, usage.InputTokens);
        Assert.Equal(10, usage.OutputTokens);
        Assert.Equal(30, usage.CachedInputTokens);
        Assert.Equal(0, usage.ReasoningTokens);
    }

    [Fact]
    public void ExtractUsage_NoUsage_ReturnsEmpty()
    {
        var root = Parse("""{"choices":[]}""");
        var usage = CostCalculator.ExtractUsage(root, ProtocolType.OpenAI);
        Assert.False(usage.HasAny);
    }

    [Fact]
    public void ExtractStreamUsage_OpenAI_UsageChunk()
    {
        var chunk = Parse("""{"choices":[],"usage":{"prompt_tokens":7,"completion_tokens":9}}""");
        var usage = CostCalculator.ExtractStreamUsage(chunk, ProtocolType.OpenAI);
        Assert.NotNull(usage);
        Assert.Equal(7, usage!.Value.InputTokens);
        Assert.Equal(9, usage.Value.OutputTokens);
    }

    [Fact]
    public void ExtractStreamUsage_Anthropic_MessageStartAndDelta()
    {
        var start = Parse("""{"type":"message_start","message":{"usage":{"input_tokens":5,"output_tokens":1}}}""");
        var startUsage = CostCalculator.ExtractStreamUsage(start, ProtocolType.Anthropic);
        Assert.NotNull(startUsage);
        Assert.Equal(5, startUsage!.Value.InputTokens);

        var delta = Parse("""{"type":"message_delta","usage":{"output_tokens":3}}""");
        var deltaUsage = CostCalculator.ExtractStreamUsage(delta, ProtocolType.Anthropic);
        Assert.NotNull(deltaUsage);
        Assert.Equal(3, deltaUsage!.Value.OutputTokens);
        Assert.Null(deltaUsage.Value.InputTokens);

        var other = Parse("""{"type":"content_block_delta","delta":{"text":"x"}}""");
        Assert.Null(CostCalculator.ExtractStreamUsage(other, ProtocolType.Anthropic));
    }

    [Fact]
    public void Compute_BasicPricing()
    {
        var model = CreateModel(inputPrice: 10m, outputPrice: 30m);
        var (inputCost, outputCost) = CostCalculator.Compute(model, 1_000_000, 100_000);
        Assert.Equal(10m, inputCost);
        Assert.Equal(3m, outputCost);
    }

    [Fact]
    public void Compute_CachedInputTokens_UseCachedPrice()
    {
        var model = CreateModel(inputPrice: 10m, cachedPrice: 1m);
        // 1M tokens, 500k cached: 500k*10 + 500k*1 = 5.5
        var (inputCost, _) = CostCalculator.Compute(model, 1_000_000, 0, cachedInputTokens: 500_000);
        Assert.Equal(5.5m, inputCost);
    }

    [Fact]
    public void Compute_ReasoningTokens_UseThinkingPrice()
    {
        var model = CreateModel(outputPrice: 30m, thinkingPrice: 60m);
        // 100k output, 40k reasoning: 60k*30 + 40k*60 = 1.8 + 2.4 = 4.2
        var (_, outputCost) = CostCalculator.Compute(model, 0, 100_000, reasoningTokens: 40_000);
        Assert.Equal(4.2m, outputCost);
    }

    [Fact]
    public void Compute_TierPricesFallBackToBasePrices()
    {
        var model = CreateModel(inputPrice: 10m, outputPrice: 30m);
        var (inputCost, outputCost) = CostCalculator.Compute(model, 1000, 1000, cachedInputTokens: 500, reasoningTokens: 500);
        var (baseInput, baseOutput) = CostCalculator.Compute(model, 1000, 1000);
        Assert.Equal(baseInput, inputCost);
        Assert.Equal(baseOutput, outputCost);
    }

    [Fact]
    public void Compute_NoPricing_ReturnsZero()
    {
        var model = new Model { Name = "m", Pricings = [] };
        Assert.Equal((0m, 0m), CostCalculator.Compute(model, 1000, 1000));
    }

    [Fact]
    public void Compute_ExchangeRate_ConvertsCostToUsd()
    {
        var model = new Model
        {
            Name = "cny-model",
            Pricings =
            [
                new ModelPricing
                {
                    InputPricePerMillionTokens = 18m,
                    OutputPricePerMillionTokens = 60m,
                    Currency = "CNY",
                    ExchangeRate = 7.2m,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1)
                }
            ]
        };

        var (inputCost, outputCost) = CostCalculator.Compute(model, 1_000_000, 100_000);
        Assert.Equal(18m * 1_000_000m / 1_000_000m / 7.2m, inputCost);
        Assert.Equal(60m * 100_000m / 1_000_000m / 7.2m, outputCost);
    }

    [Fact]
    public void Compute_ExchangeRate_DefaultUsd_NoConversion()
    {
        var model = new Model
        {
            Name = "usd-model",
            Pricings =
            [
                new ModelPricing
                {
                    InputPricePerMillionTokens = 10m,
                    OutputPricePerMillionTokens = 30m,
                    Currency = "USD",
                    ExchangeRate = 1.0m,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1)
                }
            ]
        };

        var (inputCost, outputCost) = CostCalculator.Compute(model, 1_000_000, 100_000);
        Assert.Equal(10m, inputCost);
        Assert.Equal(3m, outputCost);
    }

    [Fact]
    public void Compute_ExchangeRate_WithCachedAndReasoningTokens()
    {
        var model = new Model
        {
            Name = "eur-model",
            Pricings =
            [
                new ModelPricing
                {
                    InputPricePerMillionTokens = 9.2m,
                    OutputPricePerMillionTokens = 36.8m,
                    CachedInputPricePerMillionTokens = 0.92m,
                    ThinkingPricePerMillionTokens = 73.6m,
                    Currency = "EUR",
                    ExchangeRate = 0.92m,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1)
                }
            ]
        };

        var (inputCost, outputCost) = CostCalculator.Compute(model, 1_000_000, 100_000, cachedInputTokens: 400_000, reasoningTokens: 30_000);
        var expectedInput = ((600_000 * 9.2m + 400_000 * 0.92m) / 1_000_000m) / 0.92m;
        var expectedOutput = ((70_000 * 36.8m + 30_000 * 73.6m) / 1_000_000m) / 0.92m;
        Assert.Equal(expectedInput, inputCost);
        Assert.Equal(expectedOutput, outputCost);
    }
}
