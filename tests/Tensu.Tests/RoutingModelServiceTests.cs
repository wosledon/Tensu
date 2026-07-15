using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Tensu.Core.Enums;
using Xunit;

namespace Tensu.Tests;

public class RoutingModelServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly ServiceProvider _provider;
    private readonly CacheService _cache;
    private readonly EncryptionService _encryption;

    public RoutingModelServiceTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();

        _encryption = new EncryptionService(new ConfigurationBuilder().Build());

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton<CacheService>(sp => new CacheService(new Mock<ILogger<CacheService>>().Object));
        services.AddSingleton<LoadBalancer>();
        services.AddHttpClient();
        services.AddScoped<ProviderService>(sp => new ProviderService(_db, _encryption));
        services.AddScoped<ModelService>(sp => new ModelService(_db, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<ProviderService>()));
        _provider = services.BuildServiceProvider();
        _cache = _provider.GetRequiredService<CacheService>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RouteAsync_UpstreamReturnsRecommendation_ParsesDecision()
    {
        var provider = await CreateProviderAsync();
        var routingModel = await CreateModelAsync(provider, "routing-model");
        var candidate = await CreateModelAsync(provider, "gpt-4o");
        var routeModel = new RouteModel
        {
            Name = "virtual",
            Mode = RouteModelMode.Route,
            RoutingModelId = routingModel.Id,
            IsEnabled = true
        };
        _db.RouteModels.Add(routeModel);
        await _db.SaveChangesAsync();

        var responseBody = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = JsonSerializer.Serialize(new { recommended_model = "OpenAI-gpt-4o" }) } }
            },
            usage = new { prompt_tokens = 100, completion_tokens = 20 }
        });

        var service = CreateService(responseBody);
        var candidates = new List<Model> { candidate };
        var decision = await service.RouteAsync(routeModel, """{"messages":[]}""", candidates, "virtual");

        Assert.NotNull(decision);
        Assert.Equal("OpenAI-gpt-4o", decision!.RecommendedModel);
        Assert.Equal("OpenAI", decision.ProviderName);
        Assert.Equal("routing-model", decision.RoutingModelName);
        Assert.Equal(100, decision.InputTokens);
        Assert.Equal(20, decision.OutputTokens);
    }

    [Fact]
    public async Task RouteAsync_UpstreamFails_ReturnsNull()
    {
        var provider = await CreateProviderAsync();
        var routingModel = await CreateModelAsync(provider, "routing-model");
        var candidate = await CreateModelAsync(provider, "gpt-4o");
        var routeModel = new RouteModel
        {
            Name = "virtual",
            Mode = RouteModelMode.Route,
            RoutingModelId = routingModel.Id,
            IsEnabled = true
        };
        _db.RouteModels.Add(routeModel);
        await _db.SaveChangesAsync();

        var service = CreateService(statusCode: HttpStatusCode.InternalServerError);
        var candidates = new List<Model> { candidate };
        var decision = await service.RouteAsync(routeModel, """{"messages":[]}""", candidates, "virtual");

        Assert.Null(decision);
    }

    [Fact]
    public async Task RouteAsync_CacheHit_DoesNotCallUpstream()
    {
        var provider = await CreateProviderAsync();
        var routingModel = await CreateModelAsync(provider, "routing-model");
        var candidate = await CreateModelAsync(provider, "gpt-4o");
        var routeModel = new RouteModel
        {
            Name = "virtual",
            Mode = RouteModelMode.Route,
            RoutingModelId = routingModel.Id,
            IsEnabled = true
        };
        _db.RouteModels.Add(routeModel);
        await _db.SaveChangesAsync();

        var cacheKey = $"routing:{routeModel.Id}:{ComputeMessagesHash("""{"messages":[]}""")}";
        var cachedDecision = new RoutingModelService.RoutingDecision
        {
            RecommendedModel = "OpenAI-gpt-4o",
            InputTokens = 10,
            OutputTokens = 5,
            ProviderName = "OpenAI",
            RoutingModelName = "routing-model"
        };
        _cache.Set(cacheKey, JsonSerializer.Serialize(cachedDecision), isStream: false);

        var handler = new TestHandler();
        var service = CreateService(handler);
        var candidates = new List<Model> { candidate };
        var result = await service.RouteAsync(routeModel, """{"messages":[]}""", candidates, "virtual");

        Assert.NotNull(result);
        Assert.Equal("OpenAI-gpt-4o", result!.RecommendedModel);
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task RouteAsync_RecommendationNotInCandidateSet_ReturnsNull()
    {
        var provider = await CreateProviderAsync();
        var routingModel = await CreateModelAsync(provider, "routing-model");
        var candidate = await CreateModelAsync(provider, "gpt-4o");
        var routeModel = new RouteModel
        {
            Name = "virtual",
            Mode = RouteModelMode.Route,
            RoutingModelId = routingModel.Id,
            IsEnabled = true
        };
        _db.RouteModels.Add(routeModel);
        await _db.SaveChangesAsync();

        var responseBody = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = JsonSerializer.Serialize(new { recommended_model = "OpenAI-unknown-model" }) } }
            }
        });

        var service = CreateService(responseBody);
        var candidates = new List<Model> { candidate };
        var decision = await service.RouteAsync(routeModel, """{"messages":[]}""", candidates, "virtual");

        Assert.Null(decision);
    }

    [Fact]
    public void IsValidCandidate_MatchesProviderModelAndModelName()
    {
        var provider = new Provider { Name = "OpenAI" };
        var model = new Model { Name = "gpt-4o", Provider = provider };
        var candidates = new List<Model> { model };

        Assert.True(RoutingModelService.IsValidCandidate("OpenAI-gpt-4o", candidates));
        Assert.True(RoutingModelService.IsValidCandidate("gpt-4o", candidates));
        Assert.False(RoutingModelService.IsValidCandidate("OpenAI-gpt-4", candidates));
        Assert.False(RoutingModelService.IsValidCandidate(null, candidates));
    }

    private RoutingModelService CreateService(string? responseBody = null, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new TestHandler(responseBody, statusCode);
        return CreateService(handler);
    }

    private RoutingModelService CreateService(TestHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger<RoutingModelService>>().Object;
        return new RoutingModelService(_provider, factoryMock.Object, config, logger);
    }

    private async Task<Provider> CreateProviderAsync()
    {
        var provider = new Provider
        {
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com",
            Protocol = ProtocolType.OpenAI,
            IsEnabled = true
        };
        provider.Keys.Add(new ProviderKey
        {
            Name = "k1",
            KeyValue = _encryption.Encrypt("sk-test"),
            Status = KeyStatus.Active
        });
        _db.Providers.Add(provider);
        await _db.SaveChangesAsync();
        return provider;
    }

    private async Task<Model> CreateModelAsync(Provider provider, string name)
    {
        var model = new Model { Name = name, ProviderId = provider.Id, IsEnabled = true };
        _db.Models.Add(model);
        await _db.SaveChangesAsync();
        return model;
    }

    private static string ComputeMessagesHash(string requestBody)
    {
        var method = typeof(RoutingModelService).GetMethod("ComputeMessagesHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)method!.Invoke(null, new object[] { requestBody })!;
    }

    private class TestHandler : HttpMessageHandler
    {
        private readonly string? _responseBody;
        private readonly HttpStatusCode _statusCode;

        public bool WasCalled { get; private set; }

        public TestHandler(string? responseBody = null, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody ?? "", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
