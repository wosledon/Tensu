using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tensu.Core.Enums;
using Xunit;

namespace Tensu.Tests.Integration.Controllers;

/// <summary>
/// End-to-end gateway proxy tests for the Anthropic protocol (/v1/messages),
/// including SSE event passthrough and authoritative usage extraction.
/// </summary>
public class GatewayAnthropicE2ETests : IntegrationTestBase
{
    private IHost? _upstream;
    private string _upstreamUrl = "";

    private async Task StartUpstreamAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Run(HandleUpstreamAsync);
        await app.StartAsync();
        _upstream = app;
        _upstreamUrl = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
    }

    private static async Task HandleUpstreamAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/v1/messages"))
        {
            ctx.Response.StatusCode = 404;
            return;
        }

        if (ctx.Request.Headers["x-api-key"] != "sk-e2e")
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsync("""{"type":"error","error":{"type":"authentication_error","message":"bad key"}}""");
            return;
        }

        var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        var doc = JsonDocument.Parse(body);
        var isStream = doc.RootElement.TryGetProperty("stream", out var s) && s.GetBoolean();

        if (isStream)
        {
            ctx.Response.ContentType = "text/event-stream";
            await ctx.Response.WriteAsync("event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"content\":[],\"usage\":{\"input_tokens\":5,\"output_tokens\":1}}}\n\n");
            await ctx.Response.WriteAsync("event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n");
            await ctx.Response.WriteAsync("event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello from anthropic\"}}\n\n");
            await ctx.Response.WriteAsync("event: message_delta\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":3}}\n\n");
            await ctx.Response.WriteAsync("event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n");
        }
        else
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"id":"msg_1","type":"message","role":"assistant","content":[{"type":"text","text":"Hello from anthropic mock"}],"stop_reason":"end_turn","usage":{"input_tokens":5,"output_tokens":3}}""");
        }
    }

    public override async Task DisposeAsync()
    {
        if (_upstream != null)
        {
            await _upstream.StopAsync();
            _upstream.Dispose();
        }
        await base.DisposeAsync();
    }

    private async Task<string> SeedAnthropicGatewayAsync(string providerName, string modelName)
    {
        await SetAuthHeaderAsync("e2eadmin", "SuperAdmin", 1);

        var providerResp = await Client.PostAsJsonAsync("/api/admin/providers", new
        {
            name = providerName,
            baseUrl = _upstreamUrl,
            protocol = "Anthropic",
            isEnabled = true,
        });
        providerResp.EnsureSuccessStatusCode();
        var providerId = (await providerResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetInt32();

        var keyResp = await Client.PostAsJsonAsync($"/api/admin/providers/{providerId}/keys", new
        {
            name = "e2e-key",
            keyValue = "sk-e2e",
            status = KeyStatus.Active,
        });
        keyResp.EnsureSuccessStatusCode();

        var modelResp = await Client.PostAsJsonAsync("/api/admin/models", new
        {
            name = modelName,
            providerId,
            isEnabled = true,
            inputContextSize = 200000,
            outputContextSize = 8192,
        });
        modelResp.EnsureSuccessStatusCode();

        var apiKeyResp = await Client.PostAsJsonAsync("/api/admin/api-keys", new
        {
            name = $"e2e-{modelName}",
            organizationId = 1,
            allowedModels = "[]",
            status = KeyStatus.Active,
        });
        apiKeyResp.EnsureSuccessStatusCode();
        return (await apiKeyResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("key").GetString()!;
    }

    private static HttpRequestMessage GatewayRequest(string path, string apiKey, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        return request;
    }

    private async Task<JsonElement> WaitForAuditAsync(string modelName, bool isStream)
    {
        await SetAuthHeaderAsync("e2eadmin", "SuperAdmin", 1);
        for (var i = 0; i < 50; i++)
        {
            var auditResp = await Client.GetAsync($"/api/admin/audit/logs?keyword={modelName}&pageSize=10");
            auditResp.EnsureSuccessStatusCode();
            var auditJson = await auditResp.Content.ReadFromJsonAsync<JsonElement>();
            var match = auditJson.GetProperty("data").GetProperty("items").EnumerateArray()
                .FirstOrDefault(r => r.GetProperty("modelName").GetString() == modelName &&
                                     r.GetProperty("isStream").GetBoolean() == isStream);
            if (match.ValueKind != JsonValueKind.Undefined) return match.Clone();
            await Task.Delay(100);
        }
        throw new Xunit.Sdk.XunitException($"audit log not found for {modelName} (isStream={isStream})");
    }

    [Fact]
    public async Task AnthropicProxy_NonStream_And_Stream_WithUsage()
    {
        await StartUpstreamAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var providerName = $"e2eclaude{suffix}";
        var modelName = $"e2esonne{suffix}";
        var requestedModel = $"{providerName}-{modelName}";
        var apiKey = await SeedAnthropicGatewayAsync(providerName, modelName);

        // ── Non-stream: Anthropic message forwarded, usage is authoritative ──
        var resp1 = await Client.SendAsync(GatewayRequest("/v1/messages", apiKey, new
        {
            model = requestedModel,
            max_tokens = 64,
            messages = new[] { new { role = "user", content = "Say hello" } },
        }));
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        var body1 = await resp1.Content.ReadAsStringAsync();
        Assert.Contains("Hello from anthropic mock", body1);
        Assert.Equal(modelName, resp1.Headers.GetValues("X-Route-Model").First());

        var audit1 = await WaitForAuditAsync(modelName, isStream: false);
        Assert.Equal(5, audit1.GetProperty("inputTokens").GetInt32());
        Assert.Equal(3, audit1.GetProperty("outputTokens").GetInt32());

        // ── Stream: Anthropic SSE events forwarded; usage captured from events ──
        var resp2 = await Client.SendAsync(GatewayRequest("/v1/messages", apiKey, new
        {
            model = requestedModel,
            max_tokens = 64,
            stream = true,
            messages = new[] { new { role = "user", content = "Say hello" } },
        }));
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var body2 = await resp2.Content.ReadAsStringAsync();
        Assert.Contains("message_start", body2);
        Assert.Contains("content_block_delta", body2);
        Assert.Contains("Hello from anthropic", body2);
        Assert.Contains("message_stop", body2);

        var audit2 = await WaitForAuditAsync(modelName, isStream: true);
        Assert.Equal(5, audit2.GetProperty("inputTokens").GetInt32());
        Assert.Equal(3, audit2.GetProperty("outputTokens").GetInt32());
        var ttft = audit2.GetProperty("timeToFirstTokenMs");
        Assert.True(ttft.ValueKind == JsonValueKind.Null || ttft.GetInt64() >= 0);
    }
}
