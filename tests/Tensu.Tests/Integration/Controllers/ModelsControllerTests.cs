using System.Net.Http.Json;
using System.Text.Json;
using Tensu.Tests.Integration;
using Xunit;

namespace Tensu.Tests.Integration.Controllers;

public class ModelsControllerTests : IntegrationTestBase, IAsyncLifetime
{
    public ModelsControllerTests() : base() { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.CompletedTask;
    }

    private async Task<(int ProviderId, int ModelId)> SeedModelAsync()
    {
        await SetAuthHeaderAsync("testadmin", "Admin", 1);

        var providerResponse = await Client.PostAsJsonAsync("/api/admin/providers", new
        {
            name = "TestProvider",
            baseUrl = "https://api.test.com",
            protocol = "OpenAI",
            isEnabled = true,
        });
        providerResponse.EnsureSuccessStatusCode();
        var providerBody = await providerResponse.Content.ReadAsStringAsync();
        var providerResult = JsonSerializer.Deserialize<JsonElement>(providerBody);
        var providerId = providerResult.GetProperty("data").GetProperty("id").GetInt32();

        var modelResponse = await Client.PostAsJsonAsync("/api/admin/models", new
        {
            name = "test-model",
            providerId,
            isEnabled = true,
            inputContextSize = 8192,
            outputContextSize = 4096,
        });
        modelResponse.EnsureSuccessStatusCode();
        var modelBody = await modelResponse.Content.ReadAsStringAsync();
        var modelResult = JsonSerializer.Deserialize<JsonElement>(modelBody);
        var modelId = modelResult.GetProperty("data").GetProperty("id").GetInt32();

        return (providerId, modelId);
    }

    [Fact]
    public async Task AddPricing_ThenGetHistory_ReturnsPricingRecords()
    {
        await SetAuthHeaderAsync("testadmin", "Admin", 1);
        var (providerId, modelId) = await SeedModelAsync();

        await Client.PostAsJsonAsync($"/api/admin/models/{modelId}/pricing", new
        {
            inputPricePerMillionTokens = 10m,
            outputPricePerMillionTokens = 30m,
            currency = "USD",
            exchangeRate = 1.0m,
        });

        await Client.PostAsJsonAsync($"/api/admin/models/{modelId}/pricing", new
        {
            inputPricePerMillionTokens = 12m,
            outputPricePerMillionTokens = 35m,
            currency = "USD",
            exchangeRate = 1.0m,
            effectiveFrom = "2025-01-01T00:00:00Z",
        });

        var historyResponse = await Client.GetAsync($"/api/admin/models/{modelId}/pricing");
        historyResponse.EnsureSuccessStatusCode();

        var body = await historyResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        var items = result.GetProperty("data");
        Assert.True(items.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task GetCurrentPricing_ReturnsLatestActivePricing()
    {
        await SetAuthHeaderAsync("testadmin", "Admin", 1);
        var (providerId, modelId) = await SeedModelAsync();

        await Client.PostAsJsonAsync($"/api/admin/models/{modelId}/pricing", new
        {
            inputPricePerMillionTokens = 20m,
            outputPricePerMillionTokens = 60m,
            currency = "CNY",
            exchangeRate = 7.2m,
        });

        var response = await Client.GetAsync($"/api/admin/models/{modelId}/pricings/current");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        var data = result.GetProperty("data");
        Assert.Equal(20m, data.GetProperty("inputPricePerMillionTokens").GetDecimal());
        Assert.Equal("CNY", data.GetProperty("currency").GetString());
        Assert.Equal(7.2m, data.GetProperty("exchangeRate").GetDecimal());
    }
}
