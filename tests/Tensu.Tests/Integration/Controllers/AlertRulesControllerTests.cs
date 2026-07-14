using System.Net.Http.Json;
using System.Text.Json;
using Tensu.Tests.Integration;
using Xunit;

namespace Tensu.Tests.Integration.Controllers;

public class AlertRulesControllerTests : IntegrationTestBase, IAsyncLifetime
{
    public AlertRulesControllerTests() : base()
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SetAuthHeaderAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.CompletedTask;
    }

    private async Task<int> SeedAlertRuleAsync(string name = "Seed Rule", string eventType = "provider.unhealthy")
    {
        var createResponse = await Client.PostAsJsonAsync("/api/admin/alert-rules", new
        {
            name,
            eventType,
            severity = "Medium",
            isEnabled = true,
            webhookIds = "[]"
        });
        createResponse.EnsureSuccessStatusCode();
        var body = await createResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        return result.GetProperty("data").GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Create_ReturnsCreatedRule()
    {
        var rule = new
        {
            name = "Test Rule",
            eventType = "anomaly.detected",
            severity = "High",
            isEnabled = true,
            webhookIds = "[]"
        };

        var response = await Client.PostAsJsonAsync("/api/admin/alert-rules", rule);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(0, result.GetProperty("code").GetInt32());
        Assert.True(result.GetProperty("data").GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task List_ReturnsPagedResult()
    {
        await SeedAlertRuleAsync("List Rule");

        var response = await Client.GetAsync("/api/admin/alert-rules?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(0, result.GetProperty("code").GetInt32());
        Assert.True(result.GetProperty("data").GetProperty("total").GetInt32() > 0);
    }

    [Fact]
    public async Task Get_ReturnsRule_WhenExists()
    {
        var id = await SeedAlertRuleAsync("Get Rule");

        var response = await Client.GetAsync($"/api/admin/alert-rules/{id}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(0, result.GetProperty("code").GetInt32());
        Assert.Equal("Get Rule", result.GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_ReturnsUpdatedRule()
    {
        var id = await SeedAlertRuleAsync("Update Rule");

        var updated = new
        {
            name = "Updated Rule",
            eventType = "quota.exceeded",
            severity = "Critical",
            isEnabled = false,
            webhookIds = "[]"
        };

        var response = await Client.PutAsJsonAsync($"/api/admin/alert-rules/{id}", updated);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(0, result.GetProperty("code").GetInt32());
        Assert.Equal("Updated Rule", result.GetProperty("data").GetProperty("name").GetString());
        Assert.Equal("Critical", result.GetProperty("data").GetProperty("severity").GetString());
    }

    [Fact]
    public async Task Delete_ReturnsSuccess()
    {
        var id = await SeedAlertRuleAsync("Delete Rule");

        var response = await Client.DeleteAsync($"/api/admin/alert-rules/{id}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(0, result.GetProperty("code").GetInt32());
    }
}
