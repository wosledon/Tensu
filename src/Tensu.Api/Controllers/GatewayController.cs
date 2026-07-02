using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("v1")]
public class GatewayController : ControllerBase
{
    private readonly ApiKeyService _apiKeyService;
    private readonly RouteModelService _routeModelService;
    private readonly ModelService _modelService;
    private readonly ProviderService _providerService;
    private readonly TensuDbContext _db;
    private readonly AuditChannel _auditChannel;
    private readonly LoadBalancer _loadBalancer;
    private readonly RateLimiter _rateLimiter;
    private readonly RetryPolicy _retryPolicy;
    private readonly CompressionService _compression;
    private readonly CacheService _cache;
    private readonly ILogger<GatewayController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public GatewayController(
        ApiKeyService apiKeyService,
        RouteModelService routeModelService,
        ModelService modelService,
        ProviderService providerService,
        TensuDbContext db,
        AuditChannel auditChannel,
        LoadBalancer loadBalancer,
        RateLimiter rateLimiter,
        RetryPolicy retryPolicy,
        CompressionService compression,
        CacheService cache,
        ILogger<GatewayController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _apiKeyService = apiKeyService;
        _routeModelService = routeModelService;
        _modelService = modelService;
        _providerService = providerService;
        _db = db;
        _auditChannel = auditChannel;
        _loadBalancer = loadBalancer;
        _rateLimiter = rateLimiter;
        _retryPolicy = retryPolicy;
        _compression = compression;
        _cache = cache;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions()
        => await ProxyRequestAsync("openai", "/v1/chat/completions");

    [HttpPost("messages")]
    public async Task<IActionResult> Messages()
        => await ProxyRequestAsync("anthropic", "/v1/messages");

    private async Task<IActionResult> ProxyRequestAsync(string protocol, string path)
    {
        var sw = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N");
        HttpContext.Response.Headers["X-Request-Id"] = requestId;

        // ── 1. Authenticate ──
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized(new { error = new { message = "Missing or invalid authorization header", type = "authentication_error" } });

        var platformKey = authHeader["Bearer ".Length..].Trim();
        var apiKey = await _apiKeyService.ValidateKeyAsync(platformKey);
        if (apiKey == null)
            return Unauthorized(new { error = new { message = "Invalid API key", type = "authentication_error" } });

        // ── 2. Rate limit check ──
        var (allowed, retryAfter) = _rateLimiter.CheckRateLimit(apiKey.Id, apiKey.RateLimitRpm, apiKey.RateLimitTpm);
        if (!allowed)
        {
            HttpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
            return StatusCode(429, new { error = new { message = "Rate limit exceeded", type = "rate_limit_error", retry_after = retryAfter } });
        }

        // ── 3. Read & parse request ──
        string requestBody;
        using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
            requestBody = await reader.ReadToEndAsync();

        var requestJson = JsonDocument.Parse(requestBody);
        var requestedModel = requestJson.RootElement.TryGetProperty("model", out var modelProp) ? modelProp.GetString() : null;
        var isStream = requestJson.RootElement.TryGetProperty("stream", out var streamProp) && streamProp.GetBoolean();

        if (string.IsNullOrEmpty(requestedModel))
            return BadRequest(new { error = new { message = "Missing 'model' field", type = "invalid_request_error" } });

        // ── 4. Cache check ──
        var cacheKey = CacheService.ComputeCacheKey(requestedModel, requestBody);
        var cacheHit = _cache.TryGet(cacheKey);
        if (cacheHit.HasValue && cacheHit.Value.hit)
        {
            HttpContext.Response.Headers["X-Cache"] = "HIT";
            Response.ContentType = "application/json";
            _rateLimiter.RecordRequest(apiKey.Id, 0);
            await EnqueueAuditLog(Guid.NewGuid().ToString("N"), new Model { Name = requestedModel, Provider = new Provider { Name = "cache" } },
                apiKey, cacheHit.Value.responseBody, 0, cacheHit.Value.isStream, RequestStatus.Success, cacheHit: true);
            return Content(cacheHit.Value.responseBody, "application/json");
        }
        HttpContext.Response.Headers["X-Cache"] = "MISS";

        // ── 5. Route ──
        var resolvedModelName = await ResolveModelAsync(requestedModel, requestBody);

        // ── 5. Find model + load balance ──
        var parts = resolvedModelName.Split('-', 2);
        if (parts.Length != 2)
            return BadRequest(new { error = new { message = $"Invalid model format: {resolvedModelName}. Expected 'Provider-Model'", type = "invalid_request_error" } });

        // Support same-model across multiple providers
        var candidates = await _db.Models
            .Include(m => m.Provider).ThenInclude(p => p.Keys)
            .Include(m => m.Pricings)
            .Where(m => m.Name == parts[1] && m.Provider.Name == parts[0] && m.IsEnabled)
            .ToListAsync();

        // Also check for same model name across different providers (same-name LB)
        if (!candidates.Any())
        {
            candidates = await _db.Models
                .Include(m => m.Provider).ThenInclude(p => p.Keys)
                .Include(m => m.Pricings)
                .Where(m => m.Name == parts[1] && m.IsEnabled)
                .ToListAsync();
        }

        if (!candidates.Any())
            return NotFound(new { error = new { message = $"Model '{resolvedModelName}' not found", type = "not_found_error" } });

        // Use load balancer to select model + key
        var selection = _loadBalancer.SelectModelWithKey(candidates, p => _loadBalancer.SelectKey(p));
        if (selection == null)
            return StatusCode(503, new { error = new { message = "No available providers or keys for this model", type = "service_unavailable_error" } });

        var (model, selectedKey) = selection.Value;
        var provider = model.Provider;
        HttpContext.Response.Headers["X-Upstream-Provider"] = provider.Name;

        // ── 6. Compression (before forwarding) ──
        var compressionResult = _compression.Compress(requestBody);
        var bodyToSend = compressionResult.Applied ? compressionResult.CompressedBody : requestBody;

        // ── 7. Forward with retry ──
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        var upstreamUrl = $"{provider.BaseUrl.TrimEnd('/')}{path}";

        try
        {
            HttpResponseMessage? response;

            if (isStream)
            {
                // Streaming: no retry, direct forward
                var upstreamRequest = BuildUpstreamRequest(upstreamUrl, bodyToSend, provider, selectedKey);
                response = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead);
                return await HandleStreamResponse(response, requestId, model, apiKey, sw);
            }
            else
            {
                // Non-streaming: with retry
                response = await _retryPolicy.ExecuteWithRetryAsync(
                    async (key) =>
                    {
                        var req = BuildUpstreamRequest(upstreamUrl, bodyToSend, provider, key);
                        return await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    },
                    selectedKey,
                    isStream: false);

                sw.Stop();

                if (response == null)
                    return StatusCode(502, new { error = new { message = "All retry attempts failed", type = "proxy_error" } });

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Upstream error {StatusCode} after retries: {Body}", response.StatusCode, errorBody);
                    await EnqueueAuditLog(requestId, model, apiKey, null, sw.ElapsedMilliseconds, false,
                        RequestStatus.Failed, ((int)response.StatusCode).ToString(), errorBody);
                    return StatusCode((int)response.StatusCode,
                        new { error = new { message = "Upstream provider error", type = "upstream_error", upstream_status = (int)response.StatusCode } });
                }

                var responseBody = await response.Content.ReadAsStringAsync();

                // Forward response headers
                ForwardHeaders(response);

                // Record rate limit usage
                _rateLimiter.RecordRequest(apiKey.Id, 0);
                _loadBalancer.RecordLatency(provider.Id, sw.ElapsedMilliseconds);

                // Cache the response
                _cache.Set(cacheKey, responseBody, isStream: false);

                // Audit
                await EnqueueAuditLog(requestId, model, apiKey, responseBody, sw.ElapsedMilliseconds, false, RequestStatus.Success);

                Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
                return Content(responseBody, "application/json");
            }
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            await EnqueueAuditLog(requestId, model, apiKey, null, sw.ElapsedMilliseconds, false,
                RequestStatus.Timeout, "TIMEOUT", "Request timed out");
            return StatusCode(408, new { error = new { message = "Request timed out", type = "timeout_error" } });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error proxying request {RequestId}", requestId);
            await EnqueueAuditLog(requestId, model, apiKey, null, sw.ElapsedMilliseconds, false,
                RequestStatus.Failed, "INTERNAL_ERROR", ex.Message);
            return StatusCode(502, new { error = new { message = "Bad gateway", type = "proxy_error" } });
        }
    }

    private async Task<string> ResolveModelAsync(string requestedModel, string requestBody)
    {
        var routeModel = await _routeModelService.ResolveAsync(requestedModel);
        if (routeModel == null) return requestedModel;

        HttpContext.Response.Headers["X-Route-Model"] = routeModel.Name;

        if (routeModel.Mode == RouteModelMode.Shadow)
        {
            if (routeModel.TargetModelId == null) return requestedModel;
            var target = await _modelService.GetByIdAsync(routeModel.TargetModelId.Value);
            return target != null ? $"{target.Provider.Name}-{target.Name}" : requestedModel;
        }

        // Route mode: rule-based
        foreach (var rule in routeModel.Rules.Where(r => r.IsEnabled).OrderBy(r => r.Priority))
        {
            if (EvaluateRule(rule, requestBody))
            {
                var target = await _modelService.GetByIdAsync(rule.TargetModelId);
                if (target != null) return $"{target.Provider.Name}-{target.Name}";
            }
        }

        if (routeModel.FallbackModelId.HasValue)
        {
            var fallback = await _modelService.GetByIdAsync(routeModel.FallbackModelId.Value);
            if (fallback != null) return $"{fallback.Provider.Name}-{fallback.Name}";
        }

        return requestedModel;
    }

    private HttpRequestMessage BuildUpstreamRequest(string url, string body, Provider provider, ProviderKey key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        var decryptedKey = _providerService.DecryptKey(key.KeyValue);
        if (provider.Protocol == ProtocolType.OpenAI)
            request.Headers.Add("Authorization", $"Bearer {decryptedKey}");
        else
            request.Headers.Add("x-api-key", decryptedKey);

        foreach (var header in HttpContext.Request.Headers)
        {
            if (header.Key.StartsWith("anthropic-", StringComparison.OrdinalIgnoreCase) && provider.Protocol == ProtocolType.Anthropic)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
        }

        return request;
    }

    private void ForwardHeaders(HttpResponseMessage response)
    {
        foreach (var header in response.Headers)
            if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                Response.Headers[header.Key] = string.Join(", ", header.Value);
        foreach (var header in response.Content.Headers)
            if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                Response.Headers[header.Key] = string.Join(", ", header.Value);
    }

    private async Task<IActionResult> HandleStreamResponse(
        HttpResponseMessage response, string requestId, Model model, ApiKey apiKey, Stopwatch sw)
    {
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            await EnqueueAuditLog(requestId, model, apiKey, null, sw.ElapsedMilliseconds, false,
                RequestStatus.Failed, ((int)response.StatusCode).ToString(), errorBody);
            return StatusCode((int)response.StatusCode,
                new { error = new { message = "Upstream provider error", type = "upstream_error" } });
        }

        ForwardHeaders(response);
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        var ttftSw = Stopwatch.StartNew();
        long ttftMs = 0;
        var aggregatedContent = new StringBuilder();

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                if (line.StartsWith("data: ") && line.Length > 6)
                {
                    if (ttftMs == 0) { ttftSw.Stop(); ttftMs = ttftSw.ElapsedMilliseconds; }

                    var data = line[6..];
                    if (data == "[DONE]") { await Response.WriteAsync(line + "\n"); break; }

                    try
                    {
                        var chunkJson = JsonDocument.Parse(data);
                        if (chunkJson.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var delta = choices[0].GetProperty("delta");
                            if (delta.TryGetProperty("content", out var content))
                                aggregatedContent.Append(content.GetString());
                        }
                    }
                    catch { }
                }

                await Response.WriteAsync(line + "\n");
                await Response.Body.FlushAsync();
            }

            sw.Stop();
            _rateLimiter.RecordRequest(apiKey.Id, 0);
            _loadBalancer.RecordLatency(model.ProviderId, sw.ElapsedMilliseconds);
            await EnqueueAuditLog(requestId, model, apiKey, aggregatedContent.ToString(), sw.ElapsedMilliseconds, true,
                RequestStatus.Success, ttftMs: ttftMs);

            return new EmptyResult();
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            sw.Stop();
            await EnqueueAuditLog(requestId, model, apiKey, aggregatedContent.ToString(), sw.ElapsedMilliseconds, true,
                RequestStatus.Interrupted, "INTERRUPTED", ex.Message, ttftMs: ttftMs);
            return new EmptyResult();
        }
    }

    private bool EvaluateRule(RouteRule rule, string requestBody)
    {
        try
        {
            var condition = JsonDocument.Parse(rule.Condition);
            switch (rule.Type)
            {
                case RouteRuleType.Keyword:
                    if (condition.RootElement.TryGetProperty("keywords", out var keywords))
                        foreach (var kw in keywords.EnumerateArray())
                            if (requestBody.Contains(kw.GetString()!, StringComparison.OrdinalIgnoreCase)) return true;
                    break;
                case RouteRuleType.Regex:
                    if (condition.RootElement.TryGetProperty("pattern", out var pattern))
                        return System.Text.RegularExpressions.Regex.IsMatch(requestBody, pattern.GetString()!);
                    break;
                case RouteRuleType.ContextSize:
                    if (condition.RootElement.TryGetProperty("maxTokens", out var maxTokens))
                        return requestBody.Length / 4 <= maxTokens.GetInt32();
                    break;
            }
        }
        catch { }
        return false;
    }

    private async Task EnqueueAuditLog(
        string requestId, Model model, ApiKey apiKey,
        string? responseContent, long totalDurationMs, bool isStream,
        RequestStatus status, string? errorCode = null, string? errorMessage = null, long ttftMs = 0,
        bool cacheHit = false, bool compressionApplied = false)
    {
        try
        {
            await _auditChannel.EnqueueAsync(new RequestLog
            {
                RequestId = requestId,
                Timestamp = DateTime.UtcNow,
                ApiKeyId = apiKey.Id,
                OrganizationId = apiKey.OrganizationId,
                UserId = apiKey.UserId,
                ModelName = model.Name,
                ResolvedModelName = $"{model.Provider.Name}-{model.Name}",
                ProviderId = model.ProviderId,
                ProviderName = model.Provider.Name,
                InputTokensAfterCompression = null,
                CacheHit = cacheHit,
                CompressionApplied = compressionApplied,
                TimeToFirstTokenMs = ttftMs > 0 ? ttftMs : null,
                TotalDurationMs = totalDurationMs,
                Status = status,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                IsStream = isStream,
                ResponseContent = responseContent?.Length > 10000 ? responseContent[..10000] : responseContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue audit log for {RequestId}", requestId);
        }
    }
}
