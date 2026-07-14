using System.Net.Http.Json;
using System.Text.Json;
using Tensu.Tests.Integration;
using Xunit;

namespace Tensu.Tests.Integration.Controllers;

public class WebhookDeliveriesControllerTests : IntegrationTestBase, IAsyncLifetime
{
    public WebhookDeliveriesControllerTests() : base()
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

    [Fact]
    public async Task List_ReturnsPagedResult()
    {
        await SetAuthHeaderAsync();

        var response = await Client.GetAsync("/api/admin/webhook-deliveries?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(0, result.GetProperty("code").GetInt32());
        Assert.True(result.GetProperty("data").GetProperty("total").GetInt32() >= 0);
    }

    [Fact]
    public async Task List_FiltersByWebhookId()
    {
        await SetAuthHeaderAsync();

        var response = await Client.GetAsync("/api/admin/webhook-deliveries?page=1&pageSize=10&webhookId=1");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(0, result.GetProperty("code").GetInt32());
        var items = result.GetProperty("data").GetProperty("items");
        foreach (var item in items.EnumerateArray())
        {
            Assert.Equal(1, item.GetProperty("webhookNotificationId").GetInt32());
        }
    }

    [Fact]
    public async Task List_FiltersBySuccess()
    {
        await SetAuthHeaderAsync();

        var response = await Client.GetAsync("/api/admin/webhook-deliveries?page=1&pageSize=10&success=true");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(0, result.GetProperty("code").GetInt32());
        var items = result.GetProperty("data").GetProperty("items");
        foreach (var item in items.EnumerateArray())
        {
            Assert.True(item.GetProperty("isSuccess").GetBoolean());
        }
    }
}
