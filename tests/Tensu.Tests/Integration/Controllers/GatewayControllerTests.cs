using System.Net.Http.Json;
using System.Text.Json;
using Tensu.Core.Enums;
using Tensu.Tests.Integration;
using Xunit;

namespace Tensu.Tests.Integration.Controllers;

public class GatewayControllerTests : IntegrationTestBase, IAsyncLifetime
{
    public GatewayControllerTests() : base() { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.CompletedTask;
    }

    private async Task<(string Key, int OrgId)> CreateApiKeyAsync()
    {
        await SetAuthHeaderAsync("testadmin", "SuperAdmin", 1);

        var response = await Client.PostAsJsonAsync("/api/admin/api-keys", new
        {
            name = "Gateway Test Key",
            organizationId = 1,
            allowedModels = "[]",
            status = KeyStatus.Active,
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        var key = result.GetProperty("data").GetProperty("key").GetString()!;
        return (key, 1);
    }

    private async Task SeedModelAsync(int orgId)
    {
        await SetAuthHeaderAsync();

        var providerResponse = await Client.PostAsJsonAsync("/api/admin/providers", new
        {
            name = "OpenAI",
            baseUrl = "https://api.openai.com",
            protocol = "OpenAI",
            isEnabled = true,
        });
        providerResponse.EnsureSuccessStatusCode();
        var providerBody = await providerResponse.Content.ReadAsStringAsync();
        var providerResult = JsonSerializer.Deserialize<JsonElement>(providerBody);
        var providerId = providerResult.GetProperty("data").GetProperty("id").GetInt32();

        await Client.PostAsJsonAsync($"/api/admin/providers/{providerId}/keys", new
        {
            name = "test-key",
            keyValue = "sk-test",
            status = KeyStatus.Active,
        });

        var modelResponse = await Client.PostAsJsonAsync("/api/admin/models", new
        {
            name = "gpt-4o",
            providerId,
            isEnabled = true,
            inputContextSize = 128000,
            outputContextSize = 4096,
        });
        modelResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ListModels_ReturnsOpenAiCompatibleModelList()
    {
        var (key, orgId) = await CreateApiKeyAsync();
        await SeedModelAsync(orgId);

        Client.DefaultRequestHeaders.Authorization = null;
        Client.DefaultRequestHeaders.Remove("Authorization");
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");

        var response = await Client.GetAsync("/v1/models");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal("list", result.GetProperty("object").GetString());
        var data = result.GetProperty("data");
        Assert.True(data.GetArrayLength() > 0);
        Assert.Equal("model", data[0].GetProperty("object").GetString());
        Assert.Equal("OpenAI-gpt-4o", data[0].GetProperty("id").GetString());
    }
}
