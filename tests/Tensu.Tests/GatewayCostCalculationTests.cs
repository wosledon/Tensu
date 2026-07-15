using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Controllers;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Xunit;

namespace Tensu.Tests;

public class GatewayCostCalculationTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly GatewayController _controller;

    public GatewayCostCalculationTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();

        var auditChannel = new AuditChannel();
        var loadBalancer = new LoadBalancer();
        var rateLimiter = new RateLimiter(_db, new Mock<ILogger<RateLimiter>>().Object);
        var retryPolicy = new RetryPolicy(new Mock<ILogger<RetryPolicy>>().Object, loadBalancer);
        var compression = new CompressionService(new Mock<ILogger<CompressionService>>().Object, new CompressionChannel(), _db);
        var cache = new CacheService(new Mock<ILogger<CacheService>>().Object);
        var settingsService = new SettingsService(_db);
        var quotaService = new QuotaService(_db, rateLimiter, new Mock<ILogger<QuotaService>>().Object);
        var logger = new Mock<ILogger<GatewayController>>().Object;

        _controller = new GatewayController(
            null!, null!, null!, null!, null!,
            _db, auditChannel, loadBalancer, rateLimiter, retryPolicy,
            compression, cache, settingsService, quotaService, new IpWhitelistService(),
            new DesensitizationService(), logger, new Mock<IHttpClientFactory>().Object, new MetricsCollector());
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ComputeCost_UsesCurrentPricing()
    {
        var model = new Model
        {
            Name = "gpt-4o",
            Provider = new Provider { Name = "OpenAI" },
            Pricings =
            [
                new ModelPricing
                {
                    InputPricePerMillionTokens = 2.5m,
                    OutputPricePerMillionTokens = 10.0m,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1)
                }
            ]
        };

        var (inputCost, outputCost) = InvokeComputeCost(model, 1000, 500);

        Assert.Equal(0.0025m, inputCost);
        Assert.Equal(0.005m, outputCost);
    }

    [Fact]
    public void ComputeCost_NoPricing_ReturnsZero()
    {
        var model = new Model { Name = "unknown", Provider = new Provider { Name = "OpenAI" } };

        var (inputCost, outputCost) = InvokeComputeCost(model, 100, 100);

        Assert.Equal(0m, inputCost);
        Assert.Equal(0m, outputCost);
    }

    [Fact]
    public void EstimateOutputTokensFromResponse_OpenAIUsage_ParsesCompletionTokens()
    {
        var response = JsonSerializer.Serialize(new
        {
            usage = new { prompt_tokens = 100, completion_tokens = 50 },
            choices = new object[] { }
        });

        var tokens = InvokeEstimateOutputTokensFromResponse(response, isStream: false);

        Assert.Equal(50, tokens);
    }

    [Fact]
    public void EstimateOutputTokensFromResponse_Stream_AggregatesContent()
    {
        var sse = """
        data: {"choices":[{"delta":{"content":"Hello"}}]}
        data: {"choices":[{"delta":{"content":" world"}}]}
        data: [DONE]
        """;

        var tokens = InvokeEstimateOutputTokensFromResponse(sse, isStream: true);

        Assert.True(tokens > 0);
    }

    private static (decimal inputCost, decimal outputCost) InvokeComputeCost(Model model, int inputTokens, int outputTokens)
    {
        var method = typeof(GatewayController).GetMethod("ComputeCost", BindingFlags.NonPublic | BindingFlags.Static);
        return ((decimal inputCost, decimal outputCost))method!.Invoke(null, new object[] { model, inputTokens, outputTokens })!;
    }

    private int InvokeEstimateOutputTokensFromResponse(string responseBody, bool isStream)
    {
        var method = typeof(GatewayController).GetMethod("EstimateOutputTokensFromResponse", BindingFlags.NonPublic | BindingFlags.Instance);
        return (int)method!.Invoke(_controller, new object[] { responseBody, isStream })!;
    }
}
