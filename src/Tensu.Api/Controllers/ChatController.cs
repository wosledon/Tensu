using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/chat")]
[Authorize(Policy = "Admin")]
public class ChatController : AdminBaseController
{
    private readonly TensuDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProviderService _providerService;
    private readonly RouteModelService _routeModelService;
    private readonly AuditChannel _auditChannel;
    private readonly MetricsCollector _metrics;
    private readonly DesensitizationService _desensitizationService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(TensuDbContext db, IHttpClientFactory httpClientFactory,
        ProviderService providerService, RouteModelService routeModelService,
        AuditChannel auditChannel, MetricsCollector metrics, DesensitizationService desensitizationService,
        ILogger<ChatController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _providerService = providerService;
        _routeModelService = routeModelService;
        _auditChannel = auditChannel;
        _metrics = metrics;
        _desensitizationService = desensitizationService;
        _logger = logger;
    }

    public record ChatRequest(string Model, string[] Messages);

    [HttpPost("completions")]
    public async Task ChatCompletions([FromBody] ChatRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N");
        Response.Headers["X-Request-Id"] = requestId;
        var sw = Stopwatch.StartNew();
        var requestContent = JsonSerializer.Serialize(request.Messages);

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
                    await FailRequestAsync(requestId, sw, request.Model, null, null,
                        "NO_ACTIVE_TARGET", "No active target for shadow model", requestContent);
                    return;
                }
                resolvedModel = $"{active.Model.Provider?.Name}-{active.Model.Name}";
            }
            else
            {
                var target = routeModel.Targets.FirstOrDefault(t => t.Model != null && t.Model.IsEnabled);
                if (target?.Model == null)
                {
                    await FailRequestAsync(requestId, sw, request.Model, null, null,
                        "NO_ENABLED_TARGET", "No enabled target for route model", requestContent);
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
                await FailRequestAsync(requestId, sw, request.Model, null, null,
                    "INVALID_MODEL_FORMAT", "Model must be in format: provider-model or a valid route model name", requestContent);
                return;
            }
            resolvedModel = request.Model;
        }

        // 3. Find model + provider + key
        var parts = resolvedModel.Split('-', 2);
        var modelEntity = await _db.Models
            .Include(m => m.Provider!).ThenInclude(p => p.Keys)
            .Include(m => m.Pricings)
            .FirstOrDefaultAsync(m => m.Name == parts[1] && m.Provider!.Name == parts[0] && m.IsEnabled);

        if (modelEntity?.Provider == null)
        {
            await FailRequestAsync(requestId, sw, request.Model, resolvedModel, null,
                "MODEL_NOT_FOUND", "Model not found or not enabled", requestContent);
            return;
        }

        var key = modelEntity.Provider.Keys
            .Where(k => k.Status == KeyStatus.Active)
            .OrderByDescending(k => k.Weight)
            .FirstOrDefault();

        if (key == null)
        {
            await FailRequestAsync(requestId, sw, request.Model, resolvedModel, modelEntity,
                "NO_ACTIVE_KEY", $"No active API key for provider '{modelEntity.Provider.Name}'. Please add an Active key in Provider settings.", requestContent);
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
        var inputTokens = CompressionService.EstimateTokens(upstreamBody);

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

                sw.Stop();
                await EnqueueAuditLog(requestId, request.Model, resolvedModel, modelEntity, null,
                    sw.ElapsedMilliseconds, RequestStatus.Failed, inputTokens, 0, 0, 0,
                    upstreamBody, ((int)upstreamResponse.StatusCode).ToString(), Truncate(errorBody, 2000));
                _metrics.RecordRequest(modelEntity.Name, modelEntity.Provider.Name, success: false, cacheHit: false,
                    sw.ElapsedMilliseconds, inputTokens, 0, rateLimited: false);
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var ttftSw = Stopwatch.StartNew();
            long ttftMs = 0;
            var aggregatedContent = new StringBuilder();

            try
            {
                using var stream = await upstreamResponse.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    if (line.StartsWith("data:", StringComparison.Ordinal) && line.Length > 5)
                    {
                        if (ttftMs == 0) { ttftSw.Stop(); ttftMs = ttftSw.ElapsedMilliseconds; }

                        var data = line[5..].TrimStart();
                        if (data != "[DONE]")
                        {
                            try
                            {
                                using var chunkJson = JsonDocument.Parse(data);
                                var root = chunkJson.RootElement;
                                // OpenAI: choices[0].delta.content；Anthropic: content_block_delta → delta.text
                                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
                                    && choices[0].TryGetProperty("delta", out var delta)
                                    && delta.TryGetProperty("content", out var content))
                                {
                                    aggregatedContent.Append(content.GetString());
                                }
                                else if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "content_block_delta"
                                    && root.TryGetProperty("delta", out var anthropicDelta)
                                    && anthropicDelta.TryGetProperty("text", out var text))
                                {
                                    aggregatedContent.Append(text.GetString());
                                }
                            }
                            catch { }
                        }
                    }

                    await Response.WriteAsync(line + "\n");
                    await Response.Body.FlushAsync();
                }

                sw.Stop();
                var outputTokens = CompressionService.EstimateTokens(aggregatedContent.ToString());
                var (inputCost, outputCost) = ComputeCost(modelEntity, inputTokens, outputTokens);
                var totalDurationSec = sw.ElapsedMilliseconds / 1000.0;
                double? outputTokensPerSecond = totalDurationSec > 0 ? outputTokens / totalDurationSec : null;

                await EnqueueAuditLog(requestId, request.Model, resolvedModel, modelEntity,
                    aggregatedContent.ToString(), sw.ElapsedMilliseconds, RequestStatus.Success,
                    inputTokens, outputTokens, inputCost, outputCost, upstreamBody,
                    ttftMs: ttftMs, outputTokensPerSecond: outputTokensPerSecond);
                _metrics.RecordRequest(modelEntity.Name, modelEntity.Provider.Name, success: true, cacheHit: false,
                    sw.ElapsedMilliseconds, inputTokens, outputTokens, rateLimited: false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                sw.Stop();
                var outputTokens = CompressionService.EstimateTokens(aggregatedContent.ToString());
                var (inputCost, outputCost) = ComputeCost(modelEntity, inputTokens, outputTokens);
                var totalDurationSec = sw.ElapsedMilliseconds / 1000.0;
                double? outputTokensPerSecond = totalDurationSec > 0 ? outputTokens / totalDurationSec : null;

                await EnqueueAuditLog(requestId, request.Model, resolvedModel, modelEntity,
                    aggregatedContent.ToString(), sw.ElapsedMilliseconds, RequestStatus.Interrupted,
                    inputTokens, outputTokens, inputCost, outputCost, upstreamBody,
                    "INTERRUPTED", ex.Message, ttftMs, outputTokensPerSecond);
                _metrics.RecordRequest(modelEntity.Name, modelEntity.Provider.Name, success: false, cacheHit: false,
                    sw.ElapsedMilliseconds, inputTokens, outputTokens, rateLimited: false);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Chat streaming failed for model {Model}", request.Model);
            await EnqueueAuditLog(requestId, request.Model, resolvedModel, modelEntity, null,
                sw.ElapsedMilliseconds, RequestStatus.Failed, inputTokens, 0, 0, 0,
                upstreamBody, "UPSTREAM_ERROR", ex.Message);
            _metrics.RecordRequest(modelEntity.Name, modelEntity.Provider.Name, success: false, cacheHit: false,
                sw.ElapsedMilliseconds, inputTokens, 0, rateLimited: false);
            await WriteError($"Upstream request failed: {ex.Message}");
        }
    }

    private async Task FailRequestAsync(string requestId, Stopwatch sw, string requestedModel,
        string? resolvedModel, Model? model, string errorCode, string errorMessage, string? requestContent)
    {
        sw.Stop();
        await EnqueueAuditLog(requestId, requestedModel, resolvedModel, model, null,
            sw.ElapsedMilliseconds, RequestStatus.Failed, 0, 0, 0, 0,
            requestContent, errorCode, errorMessage);
        _metrics.RecordRequest(resolvedModel ?? requestedModel, model?.Provider?.Name ?? "unknown",
            success: false, cacheHit: false, sw.ElapsedMilliseconds, 0, 0, rateLimited: false);
        await WriteError(errorMessage);
    }

    private async Task EnqueueAuditLog(
        string requestId, string requestedModel, string? resolvedModel, Model? model,
        string? responseContent, long totalDurationMs, RequestStatus status,
        int inputTokens, int outputTokens, decimal inputCost, decimal outputCost,
        string? requestContent = null, string? errorCode = null, string? errorMessage = null,
        long ttftMs = 0, double? outputTokensPerSecond = null)
    {
        try
        {
            // 测试聊天无组织上下文，按默认策略（启用内容记录）脱敏
            var defaultOrg = new Organization { EnableContentLogging = true };
            var sanitizedRequest = _desensitizationService.Desensitize(Truncate(requestContent, 10000), defaultOrg);
            var sanitizedResponse = _desensitizationService.Desensitize(Truncate(responseContent, 10000), defaultOrg);

            await _auditChannel.EnqueueAsync(new RequestLog
            {
                RequestId = requestId,
                Timestamp = DateTime.UtcNow,
                ApiKeyId = null,
                OrganizationId = CurrentOrgId > 0 ? CurrentOrgId : null,
                UserId = CurrentUserId > 0 ? CurrentUserId : null,
                ModelName = requestedModel,
                ResolvedModelName = resolvedModel,
                ProviderId = model?.ProviderId,
                ProviderName = model?.Provider?.Name,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TimeToFirstTokenMs = ttftMs > 0 ? ttftMs : null,
                TotalDurationMs = totalDurationMs,
                OutputTokensPerSecond = outputTokensPerSecond,
                InputCost = inputCost,
                OutputCost = outputCost,
                Currency = "USD",
                Status = status,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                IsStream = true,
                RequestContent = sanitizedRequest,
                ResponseContent = sanitizedResponse
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue audit log for {RequestId}", requestId);
        }
    }

    private static (decimal inputCost, decimal outputCost) ComputeCost(Model model, int inputTokens, int outputTokens)
    {
        var now = DateTime.UtcNow;
        var pricing = (model.Pricings ?? [])
            .Where(p => p.EffectiveFrom <= now && (p.EffectiveTo == null || p.EffectiveTo >= now))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefault();

        if (pricing == null) return (0, 0);

        var inputCost = inputTokens * pricing.InputPricePerMillionTokens / 1_000_000m;
        var outputCost = outputTokens * pricing.OutputPricePerMillionTokens / 1_000_000m;
        return (inputCost, outputCost);
    }

    private static string? Truncate(string? value, int maxLength)
        => value?.Length > maxLength ? value[..maxLength] : value;

    private async Task WriteError(string message)
    {
        Response.ContentType = "application/json";
        await Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
