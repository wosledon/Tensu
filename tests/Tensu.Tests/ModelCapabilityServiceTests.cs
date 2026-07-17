using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tensu.Api.Data;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Tests;

public class ModelCapabilityServiceTests : IDisposable
{
    private readonly TensuDbContext _db;

    public ModelCapabilityServiceTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private ModelCapabilityService CreateService() => new ModelCapabilityService(_db);

    private async Task<Provider> CreateProviderAsync(string name = "OpenAI")
    {
        var provider = new Provider
        {
            Name = name,
            BaseUrl = "https://api.example.com",
            Protocol = ProtocolType.OpenAI,
            IsEnabled = true
        };
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

    [Fact]
    public async Task CreateAsync_ShouldAddCapability()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        var capability = new ModelCapability
        {
            ModelId = model.Id,
            Dimension = "Reasoning",
            Score = 92.5m,
            Source = "benchmark",
            Evidence = "MMLU benchmark"
        };

        var created = await service.CreateAsync(capability);

        Assert.NotEqual(0, created.Id);
        Assert.Equal("Reasoning", created.Dimension);
        Assert.Equal(92.5m, created.Score);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsCapability()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        var created = await service.CreateAsync(new ModelCapability
        {
            ModelId = model.Id,
            Dimension = "Code",
            Score = 88m,
            Source = "manual"
        });

        var fetched = await service.GetByIdAsync(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(88m, fetched!.Score);
    }

    [Fact]
    public async Task UpdateAsync_Existing_UpdatesScore()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        var created = await service.CreateAsync(new ModelCapability
        {
            ModelId = model.Id,
            Dimension = "Vision",
            Score = 80m,
            Source = "manual"
        });

        var updated = await service.UpdateAsync(created.Id, new ModelCapability
        {
            ModelId = model.Id,
            Dimension = "Vision",
            Score = 85m,
            Source = "benchmark",
            Evidence = "Updated evidence"
        });

        Assert.NotNull(updated);
        Assert.Equal(85m, updated!.Score);
        Assert.Equal("benchmark", updated.Source);
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesCapability()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        var created = await service.CreateAsync(new ModelCapability
        {
            ModelId = model.Id,
            Dimension = "Math",
            Score = 90m,
            Source = "manual"
        });

        var deleted = await service.DeleteAsync(created.Id);
        var fetched = await service.GetByIdAsync(created.Id);

        Assert.True(deleted);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetListAsync_WithFilter_ReturnsFilteredResults()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        await service.CreateAsync(new ModelCapability { ModelId = model.Id, Dimension = "Reasoning", Score = 90m, Source = "manual" });
        await service.CreateAsync(new ModelCapability { ModelId = model.Id, Dimension = "Code", Score = 85m, Source = "manual" });

        var result = await service.GetListAsync(new PagedRequest(), model.Id, dimension: "Code");

        Assert.Single(result.Items);
        Assert.Equal("Code", result.Items[0].Dimension);
    }

    [Fact]
    public async Task GetMatrixAsync_GroupsByDimensionAndComputesOverall()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        await service.CreateAsync(new ModelCapability { ModelId = model.Id, Dimension = "Reasoning", Score = 90m, Source = "manual" });
        await service.CreateAsync(new ModelCapability { ModelId = model.Id, Dimension = "Code", Score = 80m, Source = "manual" });
        await service.CreateAsync(new ModelCapability { ModelId = model.Id, Dimension = "Reasoning", Score = 100m, Source = "manual" }); // duplicate to test averaging

        var matrix = await service.GetMatrixAsync();

        Assert.Equal(2, matrix.Dimensions.Count);
        Assert.Single(matrix.Models);
        var item = matrix.Models[0];
        Assert.Equal("gpt-4o", item.ModelName);
        Assert.Equal(95m, item.Scores["Reasoning"]);
        Assert.Equal(80m, item.Scores["Code"]);
        Assert.Equal(87.5m, item.OverallScore);
    }

    [Fact]
    public async Task GetMatrixAsync_WithProviderFilter_ReturnsOnlyMatchingModels()
    {
        var openAi = await CreateProviderAsync("OpenAI");
        var anthropic = await CreateProviderAsync("Anthropic");
        var openAiModel = await CreateModelAsync(openAi, "gpt-4o");
        var anthropicModel = await CreateModelAsync(anthropic, "claude-3");
        var service = CreateService();

        await service.CreateAsync(new ModelCapability { ModelId = openAiModel.Id, Dimension = "Reasoning", Score = 90m, Source = "manual" });
        await service.CreateAsync(new ModelCapability { ModelId = anthropicModel.Id, Dimension = "Reasoning", Score = 95m, Source = "manual" });

        var matrix = await service.GetMatrixAsync(openAi.Id);

        Assert.Single(matrix.Models);
        Assert.Equal("gpt-4o", matrix.Models[0].ModelName);
    }

    [Fact]
    public async Task RunSyntheticEvaluationAsync_Latency_ComputesFromRequestLogs()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        _db.RequestLogs.AddRange(
            new RequestLog
            {
                RequestId = "r1",
                ModelName = model.Name,
                Status = RequestStatus.Success,
                TotalDurationMs = 1000,
                OutputTokensPerSecond = 50,
                InputTokens = 100,
                OutputTokens = 50,
                InputCost = 0.001m,
                OutputCost = 0.002m
            },
            new RequestLog
            {
                RequestId = "r2",
                ModelName = model.Name,
                Status = RequestStatus.Success,
                TotalDurationMs = 2000,
                OutputTokensPerSecond = 100,
                InputTokens = 100,
                OutputTokens = 50,
                InputCost = 0.001m,
                OutputCost = 0.002m
            });
        await _db.SaveChangesAsync();

        var result = await service.RunSyntheticEvaluationAsync(model.Id, new[] { "Latency", "Throughput" });

        Assert.Equal(model.Name, result.ModelName);
        var latency = result.Results.First(r => r.Dimension == "Latency");
        Assert.NotNull(latency.Score);
        // avg latency = 1500ms => 100 - 1500/5000*100 = 70
        Assert.Equal(70m, latency.Score!.Value);

        var throughput = result.Results.First(r => r.Dimension == "Throughput");
        Assert.NotNull(throughput.Score);
        // avg throughput = 75 => 75
        Assert.Equal(75m, throughput.Score!.Value);
    }

    [Fact]
    public async Task RunSyntheticEvaluationAsync_UnsupportedDimension_ReturnsNullScore()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        var result = await service.RunSyntheticEvaluationAsync(model.Id, new[] { "UnknownDimension" });

        var reasoning = result.Results.First(r => r.Dimension == "UnknownDimension");
        Assert.Null(reasoning.Score);
        Assert.Contains("No evaluation probes", reasoning.Message);
    }

    [Fact]
    public async Task RunSyntheticEvaluationAsync_LlmUnavailable_ReturnsNullScore()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService(); // no HTTP dependencies → LLM eval unavailable

        var result = await service.RunSyntheticEvaluationAsync(model.Id, new[] { "Reasoning" });

        var reasoning = result.Results.First(r => r.Dimension == "Reasoning");
        Assert.Null(reasoning.Score);
        Assert.Contains("not available", reasoning.Message);
    }

    private static string EncryptTestKey()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .Add(new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource
            {
                InitialData = new Dictionary<string, string?> { ["Encryption:Key"] = "TensuDefaultEncryptionKey32Bytes!" }
            })
            .Build();
        return new Api.Infrastructure.EncryptionService(config).Encrypt("test-upstream-key");
    }

    [Fact]
    public async Task RunSyntheticEvaluationAsync_LlmProbesPass_PersistsBenchmarkScore()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        provider.Keys.Add(new ProviderKey
        {
            ProviderId = provider.Id,
            Name = "k1",
            KeyValue = EncryptTestKey(),
            Status = KeyStatus.Active
        });
        await _db.SaveChangesAsync();

        // Mock upstream answers both math probes correctly.
        var handler = new StubHttpMessageHandler(req =>
        {
            var body = req.Content!.ReadAsStringAsync().Result;
            var answer = body.Contains("17 * 23") ? "391" : "36";
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"" + answer + "\"}}]}")
            };
        });
        var service = CreateLlmService(handler);

        var result = await service.RunSyntheticEvaluationAsync(model.Id, new[] { "Math" });

        var math = result.Results.First(r => r.Dimension == "Math");
        Assert.Equal(100m, math.Score);

        var capability = await service.GetByModelAndDimensionAsync(model.Id, "Math");
        Assert.NotNull(capability);
        Assert.Equal("benchmark", capability!.Source);
        Assert.Contains("LLM-evaluated", capability.Evidence);
    }

    [Fact]
    public async Task RunSyntheticEvaluationAsync_LlmProbesFail_ScoresZero()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        provider.Keys.Add(new ProviderKey
        {
            ProviderId = provider.Id,
            Name = "k1",
            KeyValue = EncryptTestKey(),
            Status = KeyStatus.Active
        });
        await _db.SaveChangesAsync();

        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"I don't know"}}]}""")
            });
        var service = CreateLlmService(handler);

        var result = await service.RunSyntheticEvaluationAsync(model.Id, new[] { "Math" });

        var math = result.Results.First(r => r.Dimension == "Math");
        Assert.Equal(0m, math.Score);
    }

    private ModelCapabilityService CreateLlmService(HttpMessageHandler handler)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .Add(new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource
            {
                InitialData = new Dictionary<string, string?> { ["Encryption:Key"] = "TensuDefaultEncryptionKey32Bytes!" }
            })
            .Build();
        var encryption = new Api.Infrastructure.EncryptionService(config);
        var providerService = new ProviderService(_db, encryption);
        var loadBalancer = new Api.Infrastructure.LoadBalancer();
        var factory = new StubHttpClientFactory(handler);
        return new ModelCapabilityService(_db, factory, loadBalancer, providerService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ModelCapabilityService>.Instance);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    [Fact]
    public async Task ImportAsync_UpdatesExistingOrCreatesNew()
    {
        var provider = await CreateProviderAsync();
        var model = await CreateModelAsync(provider, "gpt-4o");
        var service = CreateService();

        await service.CreateAsync(new ModelCapability { ModelId = model.Id, Dimension = "Reasoning", Score = 80m, Source = "manual" });

        var imported = await service.ImportAsync(new List<ModelCapability>
        {
            new ModelCapability { ModelId = model.Id, Dimension = "Reasoning", Score = 95m, Source = "benchmark" },
            new ModelCapability { ModelId = model.Id, Dimension = "Code", Score = 85m, Source = "benchmark" }
        });

        Assert.Equal(2, imported.Count);
        var reasoning = await service.GetByModelAndDimensionAsync(model.Id, "Reasoning");
        Assert.Equal(95m, reasoning!.Score);
        Assert.Equal("benchmark", reasoning.Source);

        var code = await service.GetByModelAndDimensionAsync(model.Id, "Code");
        Assert.Equal(85m, code!.Score);
    }
}
