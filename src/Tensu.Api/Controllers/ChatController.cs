using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/chat")]
[Authorize(Policy = "Admin")]
public class ChatController : ControllerBase
{
    private readonly TensuDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProviderService _providerService;
    private readonly RouteModelService _routeModelService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(TensuDbContext db, IHttpClientFactory httpClientFactory,
        ProviderService providerService, RouteModelService routeModelService,
        ILogger<ChatController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _providerService = providerService;
        _routeModelService = routeModelService;
        _logger = logger;
    }

    public record ChatRequest(string Model, string[] Messages);

    [HttpPost("completions")]
    public async Task ChatCompletions([FromBody] ChatRequest request)
    {
        // 1. Try route model first
        var routeModel = await _routeModelService.ResolveAsync(request.Model);
        string resolvedModel;
        if (routeModel != null)
        {
            if (routeModel.Mode == RouteModelMode.Shadow)
            {
                var active = routeModel.Targets.FirstOrDefault(t => t.IsActive && t.Model != null && t.Model.IsEnabled)
                    ?? routeModel.Targets.FirstOrDefault(t => t.Model != null && t.Model.IsEnabled);
                if (active?.Model == null)
                {
                    await WriteError("No active target for shadow model");
                    return;
                }
                resolvedModel = $"{active.Model.Provider?.Name}-{active.Model.Name}";
            }
            else
            {
                var target = routeModel.Targets.FirstOrDefault(t => t.Model != null && t.Model.IsEnabled);
                if (target?.Model == null)
                {
                    await WriteError("No enabled target for route model");
                    return;
                }
                resolvedModel = $"{target.Model.Provider?.Name}-{target.Model.Name}";
            }
        }
        else
        {
            // 2. Parse as provider-model
            var dashIndex = request.Model.IndexOf('-');
            if (dashIndex <= 0 || dashIndex >= request.Model.Length - 1)
            {
                await WriteError("Model must be in format: provider-model or a valid route model name");
                return;
            }
            resolvedModel = request.Model;
        }

        // 3. Find model + provider + key
        var parts = resolvedModel.Split('-', 2);
        var modelEntity = await _db.Models
            .Include(m => m.Provider!).ThenInclude(p => p.Keys)
            .FirstOrDefaultAsync(m => m.Name == parts[1] && m.Provider!.Name == parts[0] && m.IsEnabled);

        if (modelEntity?.Provider == null)
        {
            await WriteError("Model not found or not enabled");
            return;
        }

        var key = modelEntity.Provider.Keys
            .Where(k => k.Status == KeyStatus.Active)
            .OrderByDescending(k => k.Weight)
            .FirstOrDefault();

        if (key == null)
        {
            await WriteError($"No active API key for provider '{modelEntity.Provider.Name}'. Please add an Active key in Provider settings.");
            return;
        }

        // 4. Build streaming upstream request
        // 去掉 baseUrl 末尾的 /v1（如果有），再由 path 统一补充
        var baseUrl = modelEntity.Provider.BaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            baseUrl = baseUrl[..^3].TrimEnd('/');
        var path = modelEntity.Provider.Protocol == ProtocolType.Anthropic ? "/v1/messages" : "/v1/chat/completions";
        var url = $"{baseUrl}{path}";

        var decryptedKey = _providerService.DecryptKey(key.KeyValue);
        var bodyObj = new Dictionary<string, object>
        {
            ["model"] = parts[1],
            ["stream"] = true,
            ["stream_options"] = new { include_usage = true },
        };

        if (modelEntity.Provider.Protocol == ProtocolType.Anthropic)
        {
            bodyObj["max_tokens"] = 4096;
            bodyObj["messages"] = request.Messages.Select(m => new { role = "user", content = m }).ToArray();
        }
        else
        {
            bodyObj["messages"] = request.Messages.Select(m => new { role = "user", content = m }).ToArray();
        }
        var upstreamBody = JsonSerializer.Serialize(bodyObj);

        _logger.LogInformation("Chat proxy: {Method} {Url} | model={Model} | provider={Provider}",
            "POST", url, parts[1], modelEntity.Provider.Name);

        using var httpClient = _httpClientFactory.CreateClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(upstreamBody, Encoding.UTF8, "application/json")
        };

        if (modelEntity.Provider.Protocol == ProtocolType.Anthropic)
            httpRequest.Headers.Add("x-api-key", decryptedKey);
        else
            httpRequest.Headers.Add("Authorization", $"Bearer {decryptedKey}");

        try
        {
            using var upstreamResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            if (!upstreamResponse.IsSuccessStatusCode)
            {
                var errorBody = await upstreamResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Chat upstream returned {StatusCode} for {Url}: {Error}",
                    (int)upstreamResponse.StatusCode, url, errorBody);
                Response.StatusCode = (int)upstreamResponse.StatusCode;
                Response.ContentType = "application/json";
                await Response.WriteAsync(errorBody);
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            using var stream = await upstreamResponse.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(Response.Body);
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat streaming failed for model {Model}", request.Model);
            await WriteError($"Upstream request failed: {ex.Message}");
        }
    }

    private async Task WriteError(string message)
    {
        Response.ContentType = "application/json";
        await Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
