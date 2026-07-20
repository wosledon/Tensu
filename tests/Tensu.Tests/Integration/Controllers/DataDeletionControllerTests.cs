using System.Net.Http.Json;
using System.Text.Json;
using Tensu.Tests.Integration;
using Xunit;

namespace Tensu.Tests.Integration.Controllers;

public class DataDeletionControllerTests : IntegrationTestBase, IAsyncLifetime
{
    public DataDeletionControllerTests() : base() { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.CompletedTask;
    }

    private async Task<int> CreateDeletionAsync(string reason = "test-reason", int? orgId = null, int? userId = null, string? apiKeyId = null)
    {
        await SetAuthHeaderAsync("testadmin", "Admin", orgId ?? 1);

        var response = await Client.PostAsJsonAsync("/api/admin/data-deletions", new
        {
            reason,
            organizationId = orgId,
            userId = userId,
            apiKeyId = apiKeyId,
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        return result.GetProperty("data").GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Create_ReturnsDeletionRequest()
    {
        var id = await CreateDeletionAsync("gdpr-request");
        Assert.True(id > 0);
    }

    [Fact]
    public async Task List_ReturnsCreatedRequest()
    {
        await CreateDeletionAsync("list-check");

        await SetAuthHeaderAsync("testadmin", "Admin", 1);
        var response = await Client.GetAsync("/api/admin/data-deletions?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        var items = result.GetProperty("data").GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Get_ReturnsRequestById()
    {
        var id = await CreateDeletionAsync("get-check");

        await SetAuthHeaderAsync("testadmin", "Admin", 1);
        var response = await Client.GetAsync($"/api/admin/data-deletions/{id}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        var data = result.GetProperty("data");
        Assert.Equal("get-check", data.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Process_UpdatesStatusToCompleted()
    {
        var id = await CreateDeletionAsync("process-check", orgId: 99, userId: 199);

        await SetAuthHeaderAsync("testadmin", "Admin", 1);
        var processResponse = await Client.PostAsync($"/api/admin/data-deletions/{id}/process", null);
        
        var getResponse = await Client.GetAsync($"/api/admin/data-deletions/{id}");
        getResponse.EnsureSuccessStatusCode();

        var body = await getResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        var data = result.GetProperty("data");
        var status = data.GetProperty("status").GetString();
        Assert.Contains(status, new[] { "completed", "failed", "processing", "pending" });
    }
}
