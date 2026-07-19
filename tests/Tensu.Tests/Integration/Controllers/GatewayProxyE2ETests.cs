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
/// End-to-end gateway proxy tests with a real loopback HTTP server as the mock upstream:
/// auth -> rate/quota -> compression -> cache -> forward -> audit.
/// </summary>
public class GatewayProxyE2ETests : IntegrationTestBase
{
    private IHost? _upstream;
    private string _upstreamUrl = "";
    private int _upstreamHits;
    private string _lastUpstreamBody = "";

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

    private async Task HandleUpstreamAsync(HttpContext ctx)
    {
        Interlocked.Increment(ref _upstreamHits);

        if (!ctx.Request.Path.StartsWithSegments("/v1/chat/completions"))
        {
            ctx.Response.StatusCode = 404;
            return;
        }

        if (ctx.Request.Headers.Authorization != "Bearer sk-e2e")
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsync("""{"error":{"message":"bad key"}}""");
            return;
        }

        _lastUpstreamBody = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        var doc = JsonDocument.Parse(_lastUpstreamBody);
        var isStream = doc.RootElement.TryGetProperty("stream", out var s) && s.GetBoolean();

        if (isStream)
        {
            ctx.Response.ContentType = "text/event-stream";
            await ctx.Response.WriteAsync("data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\n");
            await ctx.Response.WriteAsync("data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n");
            await ctx.Response.WriteAsync("data: [DONE]\n\n");
        }
        else
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"id":"chatcmpl-1","object":"chat.completion","choices":[{"message":{"role":"assistant","content":"Hello from mock"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":3,"total_tokens":8,"prompt_tokens_details":{"cached_tokens":2},"completion_tokens_details":{"reasoning_tokens":1}}}""");
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

    private async Task<(string apiKey, int modelId)> SeedGatewayAsync(string providerName, string modelName, string allowedModels = "[]")
    {
        await SetAuthHeaderAsync("e2eadmin", "SuperAdmin", 1);

        var providerResp = await Client.PostAsJsonAsync("/api/admin/providers", new
        {
            name = providerName,
            baseUrl = _upstreamUrl,
            protocol = "OpenAI",
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
            inputContextSize = 128000,
            outputContextSize = 4096,
        });
        modelResp.EnsureSuccessStatusCode();
        var modelId = (await modelResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetInt32();

        var apiKeyResp = await Client.PostAsJsonAsync("/api/admin/api-keys", new
        {
            name = $"e2e-{modelName}",
            organizationId = 1,
            allowedModels,
            status = KeyStatus.Active,
        });
        apiKeyResp.EnsureSuccessStatusCode();
        var apiKey = (await apiKeyResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("key").GetString()!;

        return (apiKey, modelId);
    }

    private HttpRequestMessage GatewayRequest(string path, string apiKey, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    [Fact]
    public async Task ProxyChain_NonStream_Stream_Cache_Audit_Auth()
    {
        await StartUpstreamAsync();
        // Gateway model ids follow the "{provider}-{model}" convention (split at the
        // first dash), so provider/model names themselves must not contain dashes.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var providerName = $"e2eprov{suffix}";
        var modelName = $"e2emodel{suffix}";
        var requestedModel = $"{providerName}-{modelName}";
        var (apiKey, _) = await SeedGatewayAsync(providerName, modelName);

        // ── 1. Non-stream proxy: response forwarded, extension headers present ──
        var resp1 = await Client.SendAsync(GatewayRequest("/v1/chat/completions", apiKey, new
        {
            model = requestedModel,
            messages = new[] { new { role = "user", content = "Say hello" } },
        }));
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        Assert.True(resp1.Headers.Contains("X-Request-Id"));
        Assert.Equal(modelName, resp1.Headers.GetValues("X-Route-Model").First());
        var body1 = await resp1.Content.ReadAsStringAsync();
        Assert.Contains("Hello from mock", body1);

        // Upstream received the platform-issued request with the provider key.
        Assert.Contains("Say hello", _lastUpstreamBody);

        // ── 2. Stream proxy: SSE chunks forwarded ──
        var resp2 = await Client.SendAsync(GatewayRequest("/v1/chat/completions", apiKey, new
        {
            model = requestedModel,
            messages = new[] { new { role = "user", content = "Say hello" } },
            stream = true,
        }));
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var body2 = await resp2.Content.ReadAsStringAsync();
        Assert.Contains("data:", body2);
        Assert.Contains("Hello", body2);
        Assert.Contains("[DONE]", body2);

        // ── 3. Exact cache: second identical request hits cache, upstream not called ──
        await SetAuthHeaderAsync("e2eadmin", "SuperAdmin", 1);
        var setResp = await Client.PutAsJsonAsync("/api/admin/settings/cache.enabled", new { value = "true" });
        setResp.EnsureSuccessStatusCode();

        var cacheBody = new
        {
            model = requestedModel,
            messages = new[] { new { role = "user", content = $"cachetest{suffix}" } },
        };
        var hitsBefore = _upstreamHits;
        var respA = await Client.SendAsync(GatewayRequest("/v1/chat/completions", apiKey, cacheBody));
        Assert.Equal(HttpStatusCode.OK, respA.StatusCode);
        Assert.Equal("MISS", respA.Headers.GetValues("X-Cache").First());

        var respB = await Client.SendAsync(GatewayRequest("/v1/chat/completions", apiKey, cacheBody));
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);
        Assert.Equal("HIT", respB.Headers.GetValues("X-Cache").First());
        Assert.Equal(1, _upstreamHits - hitsBefore); // only the first request reached upstream

        // ── 4. Audit: request log persisted via the async channel ──
        await SetAuthHeaderAsync("e2eadmin", "SuperAdmin", 1);
        JsonElement auditItem = default;
        var auditFound = false;
        for (var i = 0; i < 50 && !auditFound; i++)
        {
            var auditResp = await Client.GetAsync($"/api/admin/audit/logs?keyword={modelName}&pageSize=5");
            auditResp.EnsureSuccessStatusCode();
            var auditJson = await auditResp.Content.ReadFromJsonAsync<JsonElement>();
            var items = auditJson.GetProperty("data").GetProperty("items");
            var match = items.EnumerateArray().FirstOrDefault(r =>
                r.GetProperty("modelName").GetString() == modelName &&
                !r.GetProperty("cacheHit").GetBoolean() &&
                !r.GetProperty("isStream").GetBoolean());
            if (match.ValueKind != JsonValueKind.Undefined)
            {
                auditItem = match.Clone();
                auditFound = true;
            }
            else
            {
                await Task.Delay(100);
            }
        }
        Assert.True(auditFound, "audit log for the proxied request was not persisted");

        // Authoritative usage and pricing details are captured in the audit record.
        Assert.Equal(5, auditItem.GetProperty("inputTokens").GetInt32());
        Assert.Equal(3, auditItem.GetProperty("outputTokens").GetInt32());
        Assert.Equal(2, auditItem.GetProperty("cachedInputTokens").GetInt32());
        Assert.Equal(1, auditItem.GetProperty("reasoningTokens").GetInt32());

        // ── 5. Auth: no key -> 401; key without model permission -> 403 ──
        var noAuth = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent("""{"model":"x","messages":[]}""", Encoding.UTF8, "application/json")
        };
        var respNoAuth = await Client.SendAsync(noAuth);
        Assert.Equal(HttpStatusCode.Unauthorized, respNoAuth.StatusCode);

        var providerName2 = $"e2eprovb{suffix}";
        var modelName2 = $"e2emodelb{suffix}";
        var (restrictedKey, _) = await SeedGatewayAsync(providerName2, modelName2, allowedModels: "[\"some-other-model\"]");
        var respForbidden = await Client.SendAsync(GatewayRequest("/v1/chat/completions", restrictedKey, new
        {
            model = $"{providerName2}-{modelName2}",
            messages = new[] { new { role = "user", content = "hi" } },
        }));
        Assert.Equal(HttpStatusCode.Forbidden, respForbidden.StatusCode);
    }
}
