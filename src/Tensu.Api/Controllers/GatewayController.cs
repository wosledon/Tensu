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
    private readonly RoutingModelService _routingModelService;
    private readonly ModelService _modelService;
    private readonly ProviderService _providerService;
    private readonly TensuDbContext _db;
    private readonly AuditChannel _auditChannel;
    private readonly LoadBalancer _loadBalancer;
    private readonly RateLimiter _rateLimiter;
    private readonly RetryPolicy _retryPolicy;
    private readonly CompressionService _compression;
    private readonly CacheService _cache;
    private readonly SettingsService _settingsService;
    private readonly QuotaService _quotaService;
    private readonly IpWhitelistService _ipWhitelistService;
    private readonly DesensitizationService _desensitizationService;
    private readonly ILogger<GatewayController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MetricsCollector _metrics;

    public GatewayController(
        ApiKeyService apiKeyService,
        RouteModelService routeModelService,
        RoutingModelService routingModelService,
        ModelService modelService,
        ProviderService providerService,
        TensuDbContext db,
        AuditChannel auditChannel,
        LoadBalancer loadBalancer,
        RateLimiter rateLimiter,
        RetryPolicy retryPolicy,
        CompressionService compression,
        CacheService cache,
        SettingsService settingsService,
        QuotaService quotaService,
        IpWhitelistService ipWhitelistService,
        DesensitizationService desensitizationService,
        ILogger<GatewayController> logger,
        IHttpClientFactory httpClientFactory,
        MetricsCollector metrics)
    {
        _apiKeyService = apiKeyService;
        _routeModelService = routeModelService;
        _routingModelService = routingModelService;
        _modelService = modelService;
        _providerService = providerService;
        _db = db;
        _auditChannel = auditChannel;
        _loadBalancer = loadBalancer;
        _rateLimiter = rateLimiter;
        _retryPolicy = retryPolicy;
        _compression = compression;
        _cache = cache;
        _settingsService = settingsService;
        _quotaService = quotaService;
        _ipWhitelistService = ipWhitelistService;
        _desensitizationService = desensitizationService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _metrics = metrics;
    }

    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions()
        => await ProxyRequestAsync("openai", "/v1/chat/completions");

    [HttpPost("messages")]
    public async Task<IActionResult> Messages()
        => await ProxyRequestAsync("anthropic", "/v1/messages");

    [HttpGet("models")]
    public async Task<IActionResult> ListModels()
    {
        var (apiKey, error) = await GetApiKeyAsync();
        if (apiKey == null) return error!;

        var models = await _db.Models
            .AsNoTracking()
            .Include(m => m.Provider)
            .Where(m => m.IsEnabled)
            .ToListAsync();

        var allowedModels = GetAllowedModels(apiKey);
        var data = models
            .Where(m => allowedModels == null || allowedModels.Contains($"{m.Provider?.Name}-{m.Name}", StringComparer.OrdinalIgnoreCase))
            .Select(m => new
            {
                id = $"{m.Provider?.Name}-{m.Name}",
                @object = "model",
                created = new DateTimeOffset(m.CreatedAt).ToUnixTimeSeconds(),
                owned_by = m.Provider?.Name ?? "unknown"
            })
            .ToList();

        return Ok(new
        {
            @object = "list",
            data
        });
    }

    private async Task<(ApiKey? ApiKey, IActionResult? Error)> GetApiKeyAsync()
    {
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return (null, Unauthorized(new { error = new { message = "Missing or invalid authorization header", type = "authentication_error" } }));

        var platformKey = authHeader["Bearer ".Length..].Trim();
        var apiKey = await _apiKeyService.ValidateKeyAsync(platformKey);
        if (apiKey == null)
            return (null, Unauthorized(new { error = new { message = "Invalid API key", type = "authentication_error" } }));

        var clientIp = _ipWhitelistService.GetClientIp(HttpContext);
        if (!_ipWhitelistService.IsAllowed(apiKey.IpWhitelist, clientIp))
        {
            await EnqueueIpWhitelistAuditLog(Guid.NewGuid().ToString("N"), apiKey, clientIp);
            return (null, StatusCode(403, new { error = new { message = "IP not whitelisted", type = "forbidden_error" } }));
        }

        return (apiKey, null);
    }

    private async Task<IActionResult> ProxyRequestAsync(string protocol, string path)
    {
        var sw = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N");
        HttpContext.Response.Headers["X-Request-Id"] = requestId;

        // ── 1. Authenticate ──
        var (apiKey, authError) = await GetApiKeyAsync();
        if (apiKey == null) return authError!;

        // ── 2. Rate limit check ──
        var apiKeyRpm = apiKey.RateLimitRpm;
        var apiKeyTpm = apiKey.RateLimitTpm;
        if (!apiKeyRpm.HasValue || apiKeyRpm.Value <= 0)
        {
            var defaultRpm = await _settingsService.GetAsync("rateLimit.defaultRpm");
            if (int.TryParse(defaultRpm, out var parsedDefaultRpm) && parsedDefaultRpm > 0)
                apiKeyRpm = parsedDefaultRpm;
        }
        if (!apiKeyTpm.HasValue || apiKeyTpm.Value <= 0)
        {
            var defaultTpm = await _settingsService.GetAsync("rateLimit.defaultTpm");
            if (int.TryParse(defaultTpm, out var parsedDefaultTpm) && parsedDefaultTpm > 0)
                apiKeyTpm = parsedDefaultTpm;
        }
        var (allowed, retryAfter) = await _rateLimiter.CheckRateLimitAsync(apiKey.Id, apiKeyRpm, apiKeyTpm);
        if (!allowed)
        {
            HttpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
            return StatusCode(429, new { error = new { message = "Rate limit exceeded", type = "rate_limit_error", retry_after = retryAfter } });
        }

        // ── 3. Read & parse request ──
        const int maxBodySize = 4 * 1024 * 1024;
        string requestBody;
        using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
        {
            var buffer = new char[maxBodySize + 1];
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (charsRead > maxBodySize)
                return BadRequest(new { error = new { message = "Request body too large", type = "invalid_request_error" } });
            requestBody = new string(buffer, 0, charsRead);
        }

        var requestJson = JsonDocument.Parse(requestBody);
        var requestedModel = requestJson.RootElement.TryGetProperty("model", out var modelProp) ? modelProp.GetString() : null;
        var isStream = requestJson.RootElement.TryGetProperty("stream", out var streamProp) && streamProp.GetBoolean();

        if (string.IsNullOrEmpty(requestedModel))
            return BadRequest(new { error = new { message = "Missing 'model' field", type = "invalid_request_error" } });

        // ── Allowed models check ──
        if (!IsModelAllowed(apiKey, requestedModel))
        {
            return StatusCode(403, new { error = new { message = $"Model '{requestedModel}' is not allowed for this API key", type = "forbidden_error" } });
        }

        // ── 4. Organization quota check ──
        var estimatedTokens = CompressionService.EstimateTokens(requestBody);
        var (quotaAllowed, quotaRetryAfter) = await _quotaService.CheckOrgQuotaAsync(apiKey.OrganizationId, estimatedTokens, apiKey.Id);
        if (!quotaAllowed)
        {
            HttpContext.Response.Headers["Retry-After"] = quotaRetryAfter.ToString();
            return StatusCode(429, new { error = new { message = "Token quota exceeded", type = "rate_limit_error", retry_after = quotaRetryAfter } });
        }

        // ── 5. Route ──
        var resolvedModelName = await ResolveModelAsync(requestedModel, requestBody, apiKey, requestId);

        // ── 6. Find models ──
        var candidates = await FindModelsAsync(resolvedModelName);

        if (!candidates.Any())
            return NotFound(new { error = new { message = $"Model '{resolvedModelName}' not found", type = "not_found_error" } });

        // ── 7. Try providers with failover ──
        var attemptedProviderIds = new HashSet<int>();
        IActionResult? lastError = null;

        while (candidates.Any())
        {
            var selection = _loadBalancer.SelectModelWithKey(candidates, p => _loadBalancer.SelectKey(p));
            if (selection == null)
                break;

            var (model, selectedKey) = selection.Value;
            if (attemptedProviderIds.Contains(model.ProviderId))
            {
                candidates.Remove(model);
                continue;
            }

            attemptedProviderIds.Add(model.ProviderId);
            candidates.Remove(model);

            var result = await TryForwardToProviderAsync(model, selectedKey, protocol, path, requestId, apiKey, requestBody, requestedModel, estimatedTokens, isStream, sw);
            if (result is ObjectResult { StatusCode: >= 500 } or StatusCodeResult { StatusCode: >= 500 })
            {
                lastError = result;
                continue;
            }

            return result;
        }

        return lastError ?? StatusCode(503, new { error = new { message = "No available providers or keys for this model", type = "service_unavailable_error" } });

        async Task<IActionResult> TryForwardToProviderAsync(
            Model model, ProviderKey selectedKey, string protocol, string path,
            string requestId, ApiKey apiKey, string requestBody, string requestedModel, int estimatedTokens, bool isStream, Stopwatch sw)
        {
            var provider = model.Provider;
            HttpContext.Response.Headers["X-Upstream-Provider"] = provider.Name;

            // ── Concurrency check ──
            var concurrentLimit = await _quotaService.GetConcurrentRequestLimitAsync(apiKey.OrganizationId, apiKey.Id);
            bool concurrencyAcquired = false;
            if (concurrentLimit.HasValue)
            {
                concurrencyAcquired = await _rateLimiter.TryAcquireConcurrencyAsync(model.Id, concurrentLimit.Value);
                if (!concurrencyAcquired)
                {
                    return StatusCode(429, new { error = new { message = "Model concurrency limit exceeded", type = "rate_limit_error" } });
                }
            }

            try
            {
                // ── Token estimation & compression ──
                var inputTokens = CompressionService.EstimateTokens(requestBody);
                var compressionResult = await _compression.CompressAsync(requestBody);
                var compressionEnabled = await IsCompressionEnabledAsync(model.Id, apiKey.OrganizationId);
                var compressionApplied = compressionResult.Applied && compressionEnabled;
                var compressionStrategy = compressionApplied ? compressionResult.Strategy : "none";
                var inputTokensAfterCompression = compressionApplied ? compressionResult.CompressedTokenEstimate : inputTokens;
                var bodyToSend = compressionApplied ? compressionResult.CompressedBody : requestBody;
                var compressionMappingKey = compressionApplied ? compressionResult.DecompressionKey : null;

                // ── Cache check ──
                var cacheEnabled = await IsCacheEnabledAsync();
                var semanticCacheEnabled = await IsSemanticCacheEnabledAsync();
                var semanticCacheThreshold = await GetSemanticCacheThresholdAsync();
                var semanticCacheTtl = await GetSemanticCacheTtlAsync();
                var cacheKey = CacheService.ComputeCacheKey(requestedModel, requestBody);
                var cacheTtl = await GetCacheTtlAsync();

                if (cacheEnabled)
                {
                    var cacheHit = _cache.TryGet(cacheKey);
                    if (cacheHit.HasValue && cacheHit.Value.hit)
                    {
                        HttpContext.Response.Headers["X-Cache"] = "HIT";
                        await _rateLimiter.RecordRequestAsync(apiKey.Id, 0);
                        await _quotaService.RecordOrgUsageAsync(apiKey.OrganizationId, estimatedTokens, apiKey.Id);

                        var cachedResponse = cacheHit.Value.responseBody!;
                        var cachedOutputTokens = EstimateOutputTokensFromResponse(cachedResponse, cacheHit.Value.isStream);
                        var (inputCost, outputCost) = ComputeCost(model, inputTokens, cachedOutputTokens);

                        var responseToReturn = cacheHit.Value.isStream && !isStream
                            ? CacheService.ConvertStreamToNonStream(cachedResponse)
                            : cachedResponse;

                        if (isStream && cacheHit.Value.isStream)
                        {
                            await WriteStreamCacheResponseAsync(cachedResponse, requestId, model, apiKey, sw,
                                inputTokens, inputTokensAfterCompression, cachedOutputTokens, inputCost, outputCost,
                                compressionStrategy, compressionApplied, requestBody, compressionMappingKey: compressionMappingKey);
                            _metrics.RecordRequest(model.Name, provider.Name, success: true, cacheHit: true, sw.ElapsedMilliseconds, inputTokens, cachedOutputTokens, rateLimited: false);
                            return new EmptyResult();
                        }

                        await EnqueueAuditLog(requestId, model, apiKey, responseToReturn, sw.ElapsedMilliseconds, isStream,
                            RequestStatus.Success, inputTokens, inputTokensAfterCompression, cachedOutputTokens,
                            inputCost, outputCost, compressionStrategy, compressionApplied, requestBody, compressionMappingKey: compressionMappingKey, cacheHit: true);
                        Response.ContentType = isStream ? "text/event-stream" : "application/json";
                        _metrics.RecordRequest(model.Name, provider.Name, success: true, cacheHit: true, sw.ElapsedMilliseconds, inputTokens, cachedOutputTokens, rateLimited: false);
                        return Content(responseToReturn, Response.ContentType);
                    }

                    if (semanticCacheEnabled)
                    {
                        var semanticHit = await _cache.TryGetSemanticAsync(requestedModel, requestBody, semanticCacheThreshold);
                        if (semanticHit.HasValue && semanticHit.Value.hit)
                        {
                            HttpContext.Response.Headers["X-Cache"] = "HIT_SEMANTIC";
                            await _rateLimiter.RecordRequestAsync(apiKey.Id, 0);
                            await _quotaService.RecordOrgUsageAsync(apiKey.OrganizationId, estimatedTokens, apiKey.Id);

                            var cachedResponse = semanticHit.Value.responseBody!;
                            var cachedOutputTokens = EstimateOutputTokensFromResponse(cachedResponse, semanticHit.Value.isStream);
                            var (inputCost, outputCost) = ComputeCost(model, inputTokens, cachedOutputTokens);

                            var responseToReturn = semanticHit.Value.isStream && !isStream
                                ? CacheService.ConvertStreamToNonStream(cachedResponse)
                                : cachedResponse;

                            if (isStream && semanticHit.Value.isStream)
                            {
                                await WriteStreamCacheResponseAsync(cachedResponse, requestId, model, apiKey, sw,
                                    inputTokens, inputTokensAfterCompression, cachedOutputTokens, inputCost, outputCost,
                                    compressionStrategy, compressionApplied, requestBody, compressionMappingKey: compressionMappingKey);
                                _metrics.RecordRequest(model.Name, provider.Name, success: true, cacheHit: true, sw.ElapsedMilliseconds, inputTokens, cachedOutputTokens, rateLimited: false);
                                return new EmptyResult();
                            }

                            await EnqueueAuditLog(requestId, model, apiKey, responseToReturn, sw.ElapsedMilliseconds, isStream,
                                RequestStatus.Success, inputTokens, inputTokensAfterCompression, cachedOutputTokens,
                                inputCost, outputCost, compressionStrategy, compressionApplied, requestBody, compressionMappingKey: compressionMappingKey, cacheHit: true, semanticCacheHit: true);
                            Response.ContentType = isStream ? "text/event-stream" : "application/json";
                            _metrics.RecordRequest(model.Name, provider.Name, success: true, cacheHit: true, sw.ElapsedMilliseconds, inputTokens, cachedOutputTokens, rateLimited: false);
                            return Content(responseToReturn, Response.ContentType);
                        }
                    }
                }

                HttpContext.Response.Headers["X-Cache"] = "MISS";

                // ── Forward with retry ──
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                var upstreamUrl = $"{provider.BaseUrl.TrimEnd('/')}{path}";

                try
                {
                    HttpResponseMessage? response;

                    if (isStream)
                    {
                        var upstreamRequest = BuildUpstreamRequest(upstreamUrl, bodyToSend, provider, selectedKey);
                        response = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead);
                        var streamResult = await HandleStreamResponse(response, requestId, model, apiKey, sw, requestBody, inputTokens, inputTokensAfterCompression, compressionStrategy, compressionApplied, cacheKey, cacheTtl, cacheEnabled, semanticCacheEnabled, semanticCacheThreshold, semanticCacheTtl, requestedModel, compressionMappingKey, provider, selectedKey);
                        await _quotaService.RecordOrgUsageAsync(apiKey.OrganizationId, estimatedTokens, apiKey.Id);
                        return streamResult;
                    }
                    else
                    {
                        var maxRetriesValue = await _settingsService.GetAsync("retry.maxRetries");
                        var baseDelayMsValue = await _settingsService.GetAsync("retry.baseDelayMs");
                        var maxDelayMsValue = await _settingsService.GetAsync("retry.maxDelayMs");
                        int.TryParse(maxRetriesValue, out var parsedMaxRetries);
                        int.TryParse(baseDelayMsValue, out var parsedBaseDelayMs);
                        int.TryParse(maxDelayMsValue, out var parsedMaxDelayMs);

                        response = await _retryPolicy.ExecuteWithRetryAsync(
                            async (key) =>
                            {
                                var req = BuildUpstreamRequest(upstreamUrl, bodyToSend, provider, key);
                                return await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                            },
                            provider,
                            selectedKey,
                            isStream: false,
                            maxRetries: parsedMaxRetries > 0 ? parsedMaxRetries : null,
                            baseDelayMs: parsedBaseDelayMs > 0 ? parsedBaseDelayMs : null,
                            maxDelayMs: parsedMaxDelayMs > 0 ? parsedMaxDelayMs : null);

                        sw.Stop();

                        if (response == null)
                        {
                            _metrics.RecordRequest(model.Name, provider.Name, success: false, cacheHit: false, sw.ElapsedMilliseconds, inputTokens, 0, rateLimited: false);
                            return StatusCode(502, new { error = new { message = "All retry attempts failed", type = "proxy_error" } });
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorBody = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning("Upstream error {StatusCode} after retries: {Body}", response.StatusCode, errorBody);
                            await EnqueueAuditLog(requestId, model, apiKey, null, sw.ElapsedMilliseconds, false,
                                RequestStatus.Failed, inputTokens, inputTokensAfterCompression, 0, 0, 0, compressionStrategy, compressionApplied, requestBody,
                                ((int)response.StatusCode).ToString(), errorBody, compressionMappingKey: compressionMappingKey);
                            _metrics.RecordRequest(model.Name, provider.Name, success: false, cacheHit: false, sw.ElapsedMilliseconds, inputTokens, 0, rateLimited: false);
                            return StatusCode((int)response.StatusCode,
                                new { error = new { message = "Upstream provider error", type = "upstream_error", upstream_status = (int)response.StatusCode } });
                        }

                        var responseBody = await response.Content.ReadAsStringAsync();

                        ForwardHeaders(response);

                        await _rateLimiter.RecordRequestAsync(apiKey.Id, 0);
                        _loadBalancer.RecordLatency(provider.Id, sw.ElapsedMilliseconds);
                        _loadBalancer.RecordKeyLatency(selectedKey.Id, sw.ElapsedMilliseconds);

                        var outputTokens = EstimateOutputTokensFromResponse(responseBody, isStream: false);
                        var (inputCost, outputCost) = ComputeCost(model, inputTokens, outputTokens);

                        if (cacheEnabled)
                            _cache.Set(cacheKey, responseBody, isStream: false, cacheTtl);

                        if (semanticCacheEnabled)
                            await _cache.SetSemanticAsync(requestedModel, requestBody, responseBody, isStream: false, semanticCacheTtl, CancellationToken.None);

                        await _quotaService.RecordOrgUsageAsync(apiKey.OrganizationId, inputTokens + outputTokens, apiKey.Id);

                        await EnqueueAuditLog(requestId, model, apiKey, responseBody, sw.ElapsedMilliseconds, false,
                            RequestStatus.Success, inputTokens, inputTokensAfterCompression, outputTokens, inputCost, outputCost,
                            compressionStrategy, compressionApplied, requestBody, compressionMappingKey: compressionMappingKey);

                        Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
                        _metrics.RecordRequest(model.Name, provider.Name, success: true, cacheHit: false, sw.ElapsedMilliseconds, inputTokens, outputTokens, rateLimited: false);
                        return Content(responseBody, "application/json");
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    var errorCode = ErrorCodeMapper.Map(ex);
                    var errorMessage = ex is TimeoutException or TaskCanceledException or OperationCanceledException ? "Request timed out" : "Proxy error";
                    var statusCode = ex is TimeoutException or TaskCanceledException or OperationCanceledException ? 504 : 502;
                    _logger.LogError(ex, "Error proxying request {RequestId}", requestId);
                    await EnqueueAuditLog(requestId, model, apiKey, null, sw.ElapsedMilliseconds, false,
                        RequestStatus.Failed, inputTokens, inputTokensAfterCompression, 0, 0, 0, compressionStrategy, compressionApplied, requestBody,
                        errorCode, errorMessage, compressionMappingKey: compressionMappingKey);
                    _metrics.RecordRequest(model.Name, provider.Name, success: false, cacheHit: false, sw.ElapsedMilliseconds, inputTokens, 0, rateLimited: false);
                    return StatusCode(statusCode, ErrorCodeMapper.BuildErrorResponse(errorCode, errorMessage, statusCode == 504 ? "timeout_error" : "proxy_error"));
                }
            }
            finally
            {
                if (concurrencyAcquired)
                    await _rateLimiter.ReleaseConcurrencyAsync(model.Id);
            }
        }
    }

    private async Task<string> ResolveModelAsync(string requestedModel, string requestBody, ApiKey apiKey, string requestId)
    {
        var routeModel = await _routeModelService.ResolveAsync(requestedModel);
        if (routeModel == null) return requestedModel;

        HttpContext.Response.Headers["X-Route-Model"] = routeModel.Name;

        if (routeModel.Mode == RouteModelMode.Shadow)
        {
            var activeTarget = routeModel.Targets
                .FirstOrDefault(t => t.IsActive && t.Model != null && t.Model.IsEnabled);
            // 如果没有活跃目标，尝试第一个可用目标
            activeTarget ??= routeModel.Targets
                .FirstOrDefault(t => t.Model != null && t.Model.IsEnabled);
            if (activeTarget?.Model == null) return requestedModel;
            return $"{activeTarget.Model.Provider?.Name}-{activeTarget.Model.Name}";
        }

        // Route mode: build candidate pool from direct targets + rules + fallback
        var candidates = await BuildCandidateModelsAsync(routeModel);

        // Try LLM-based routing first, then fall back to rule engine
        if (routeModel.Mode == RouteModelMode.Route && routeModel.RoutingModelId.HasValue && candidates.Any())
        {
            var decision = await _routingModelService.RouteAsync(routeModel, requestBody, candidates, requestedModel);
            if (decision != null)
            {
                HttpContext.Response.Headers["X-Routing-Model-Used"] = "true";
                await EnqueueRoutingAuditLog(requestId, apiKey, routeModel, decision);
                return decision.RecommendedModel;
            }
        }

        HttpContext.Response.Headers["X-Routing-Model-Used"] = "false";

        // Rule-based fallback: configured rules (algorithmic or legacy) first
        foreach (var rule in routeModel.Rules.Where(r => r.IsEnabled).OrderBy(r => r.Priority))
        {
            if (EvaluateRule(rule, requestBody))
            {
                var target = await _modelService.GetByIdAsync(rule.TargetModelId);
                if (target == null) continue;
                var providerName = GetNestedValue(target, "Provider.Name") ?? string.Empty;
                var modelName = GetNestedValue(target, "Name") ?? string.Empty;
                if (!string.IsNullOrEmpty(providerName) && !string.IsNullOrEmpty(modelName))
                    return $"{providerName}-{modelName}";
            }
        }

        // Built-in algorithmic routing: when no configured rule matches, pick from candidates by request characteristics
        if (candidates.Any())
        {
            var algorithmicPick = AlgorithmicRoute(candidates, requestBody);
            if (algorithmicPick != null)
                return $"{algorithmicPick.Provider.Name}-{algorithmicPick.Name}";
        }

        if (routeModel.FallbackModelId.HasValue)
        {
            var fallback = await _modelService.GetByIdAsync(routeModel.FallbackModelId.Value);
            if (fallback == null) return requestedModel;
            var providerName = GetNestedValue(fallback, "Provider.Name") ?? string.Empty;
            var modelName = GetNestedValue(fallback, "Name") ?? string.Empty;
            return string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(modelName) ? requestedModel : $"{providerName}-{modelName}";
        }

        return requestedModel;
    }

    private async Task<List<Model>> BuildCandidateModelsAsync(RouteModel routeModel)
    {
        var candidateIds = new HashSet<int>();

        // 直接挂载的目标
        foreach (var t in routeModel.Targets.Where(t => t.Model != null && t.Model.IsEnabled))
            candidateIds.Add(t.ModelId);

        // 规则中的目标
        foreach (var r in routeModel.Rules.Where(r => r.IsEnabled))
            candidateIds.Add(r.TargetModelId);

        // 回退模型
        if (routeModel.FallbackModelId.HasValue)
            candidateIds.Add(routeModel.FallbackModelId.Value);

        if (candidateIds.Count == 0) return new List<Model>();

        var candidates = await _db.Models
            .Include(m => m.Provider)
            .Where(m => candidateIds.Contains(m.Id) && m.IsEnabled)
            .ToListAsync();

        return candidates;
    }

    private static string? GetNestedValue(object? source, string path)
    {
        if (source == null) return null;
        var current = source;
        foreach (var part in path.Split('.'))
        {
            if (current == null) return null;
            var type = current.GetType();
            var prop = type.GetProperty(part, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop == null) return null;
            current = prop.GetValue(current);
        }
        return current?.ToString();
    }

    private static bool IsModelAllowed(ApiKey apiKey, string modelName)
    {
        var allowed = GetAllowedModels(apiKey);
        if (allowed == null) return true;
        return allowed.Contains(modelName, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string>? GetAllowedModels(ApiKey apiKey)
    {
        if (string.IsNullOrEmpty(apiKey.AllowedModels)) return null;

        try
        {
            var allowed = JsonSerializer.Deserialize<List<string>>(apiKey.AllowedModels);
            if (allowed == null || !allowed.Any()) return null;
            return allowed;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Malformed AllowedModels JSON for API key {apiKey.Id}: {ex.Message}");
            return new List<string>();
        }
    }

    private async Task<List<Model>> FindModelsAsync(string resolvedModelName)
    {
        var dashIndex = resolvedModelName.IndexOf('-');
        if (dashIndex <= 0 || dashIndex >= resolvedModelName.Length - 1)
        {
            return await _db.Models
                .Include(m => m.Provider).ThenInclude(p => p.Keys)
                .Include(m => m.Pricings)
                .Where(m => m.Name == resolvedModelName && m.IsEnabled)
                .ToListAsync();
        }

        var providerName = resolvedModelName[..dashIndex];
        var modelName = resolvedModelName[(dashIndex + 1)..];

        var exact = await _db.Models
            .Include(m => m.Provider).ThenInclude(p => p.Keys)
            .Include(m => m.Pricings)
            .Where(m => m.Name == modelName && m.Provider.Name == providerName && m.IsEnabled)
            .ToListAsync();

        if (exact.Any()) return exact;

        return await _db.Models
            .Include(m => m.Provider).ThenInclude(p => p.Keys)
            .Include(m => m.Pricings)
            .Where(m => m.Name == modelName && m.IsEnabled)
            .ToListAsync();
    }

    private async Task EnqueueRoutingAuditLog(string requestId, ApiKey apiKey, RouteModel routeModel, RoutingModelService.RoutingDecision decision)
    {
        try
        {
            await _auditChannel.EnqueueAsync(new RequestLog
            {
                RequestId = $"{requestId}:routing",
                Timestamp = DateTime.UtcNow,
                ApiKeyId = apiKey.Id,
                OrganizationId = apiKey.OrganizationId,
                UserId = apiKey.UserId,
                ModelName = $"routing:{routeModel.Name}",
                ResolvedModelName = $"{decision.ProviderName}-{decision.RoutingModelName}",
                ProviderName = decision.ProviderName,
                InputTokens = decision.InputTokens,
                OutputTokens = decision.OutputTokens,
                Status = Core.Enums.RequestStatus.Success,
                IsStream = false,
                CacheHit = false,
                CompressionApplied = false,
                CompressionStrategy = "none"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue routing audit log for {RequestId}", requestId);
        }
    }

    private async Task EnqueueIpWhitelistAuditLog(string requestId, ApiKey apiKey, string? clientIp)
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
                ModelName = "unknown",
                ProviderName = null,
                InputTokens = 0,
                OutputTokens = 0,
                Status = Core.Enums.RequestStatus.Forbidden,
                ErrorCode = "IP_NOT_WHITELISTED",
                ErrorMessage = "IP not whitelisted",
                IsStream = false,
                CacheHit = false,
                CompressionApplied = false,
                CompressionStrategy = "none",
                RequestContent = clientIp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue IP whitelist audit log for {RequestId}", requestId);
        }
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

    private static readonly HashSet<string> AllowedResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "x-ratelimit-limit-requests",
        "x-ratelimit-limit-tokens",
        "x-ratelimit-remaining-requests",
        "x-ratelimit-remaining-tokens",
        "x-ratelimit-reset-requests",
        "x-ratelimit-reset-tokens",
        "openai-model",
        "openai-organization",
        "openai-processing-ms",
        "openai-version",
        "anthropic-ratelimit-requests-limit",
        "anthropic-ratelimit-requests-remaining",
        "anthropic-ratelimit-requests-reset",
        "anthropic-ratelimit-tokens-limit",
        "anthropic-ratelimit-tokens-remaining",
        "anthropic-ratelimit-tokens-reset",
    };

    private void ForwardHeaders(HttpResponseMessage response)
    {
        foreach (var header in response.Headers)
            if (AllowedResponseHeaders.Contains(header.Key))
                Response.Headers[header.Key] = string.Join(", ", header.Value);
    }

    private async Task<bool> IsCompressionEnabledAsync(int? modelId = null, int? orgId = null)
    {
        var globalValue = await _settingsService.GetAsync("compression.enabled");
        if (string.Equals(globalValue, "false", StringComparison.OrdinalIgnoreCase))
            return false;

        if (orgId.HasValue)
        {
            var org = await _db.Organizations.FindAsync(orgId.Value);
            if (org != null && !org.CompressionEnabled)
                return false;
        }

        if (modelId.HasValue)
        {
            var model = await _modelService.GetByIdAsync(modelId.Value) as Tensu.Core.Entities.Model;
            if (model != null && !model.CompressionEnabled)
                return false;
        }

        return true;
    }

    private async Task<bool> IsCacheEnabledAsync()
    {
        var value = await _settingsService.GetAsync("cache.enabled");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsSemanticCacheEnabledAsync()
    {
        var value = await _settingsService.GetAsync("semanticCache.enabled");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<float> GetSemanticCacheThresholdAsync()
    {
        var value = await _settingsService.GetAsync("semanticCache.threshold");
        if (value != null && float.TryParse(value, out var threshold) && threshold is >= 0 and <= 1)
            return threshold;
        return 0.9f;
    }

    private async Task<TimeSpan?> GetSemanticCacheTtlAsync()
    {
        var value = await _settingsService.GetAsync("semanticCache.ttlMinutes");
        if (value != null && int.TryParse(value, out var minutes) && minutes > 0)
            return TimeSpan.FromMinutes(minutes);
        return TimeSpan.FromMinutes(30);
    }

    private async Task<TimeSpan?> GetCacheTtlAsync()
    {
        var value = await _settingsService.GetAsync("cache.ttlMinutes");
        if (value != null && int.TryParse(value, out var minutes) && minutes > 0)
            return TimeSpan.FromMinutes(minutes);
        return TimeSpan.FromMinutes(10);
    }

    private int EstimateOutputTokensFromResponse(string responseBody, bool isStream)
    {
        if (string.IsNullOrEmpty(responseBody)) return 0;

        if (!isStream)
        {
            try
            {
                var json = JsonDocument.Parse(responseBody);
                if (json.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("completion_tokens", out var completionTokensProp) &&
                        completionTokensProp.TryGetInt32(out var completionTokens))
                        return completionTokens;
                }
            }
            catch { }
        }

        // Fallback: estimate from content
        if (isStream)
            return CompressionService.EstimateTokens(CacheService.ConvertStreamToNonStream(responseBody));

        try
        {
            var json = JsonDocument.Parse(responseBody);
            var content = new StringBuilder();
            if (json.RootElement.TryGetProperty("choices", out var choices))
            {
                foreach (var choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var msgContent))
                    {
                        content.Append(msgContent.GetString());
                    }
                    else if (choice.TryGetProperty("text", out var text))
                    {
                        content.Append(text.GetString());
                    }
                    else if (choice.TryGetProperty("delta", out var delta) &&
                             delta.TryGetProperty("content", out var deltaContent))
                    {
                        content.Append(deltaContent.GetString());
                    }
                }
            }
            else if (json.RootElement.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in contentArray.EnumerateArray())
                {
                    if (block.TryGetProperty("text", out var blockText))
                        content.Append(blockText.GetString());
                }
            }
            return CompressionService.EstimateTokens(content.ToString());
        }
        catch
        {
            return CompressionService.EstimateTokens(responseBody);
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

    private async Task<IActionResult> HandleStreamResponse(
        HttpResponseMessage response, string requestId, Model model, ApiKey apiKey, Stopwatch sw,
        string requestBody, int inputTokens, int inputTokensAfterCompression, string compressionStrategy, bool compressionApplied,
        string cacheKey, TimeSpan? cacheTtl, bool cacheEnabled, bool semanticCacheEnabled, float semanticCacheThreshold, TimeSpan? semanticCacheTtl, string requestedModel, string? compressionMappingKey, Provider provider, ProviderKey selectedKey)
    {
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            await EnqueueAuditLog(requestId, model, apiKey, null, sw.ElapsedMilliseconds, false,
                RequestStatus.Failed, inputTokens, inputTokensAfterCompression, 0, 0, 0, compressionStrategy, compressionApplied, requestBody,
                ((int)response.StatusCode).ToString(), errorBody, compressionMappingKey: compressionMappingKey);
            _metrics.RecordRequest(model.Name, provider.Name, success: false, cacheHit: false, sw.ElapsedMilliseconds, inputTokens, 0, rateLimited: false);
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
        var aggregatedSse = new StringBuilder();

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                aggregatedSse.AppendLine(line);

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
            await _rateLimiter.RecordRequestAsync(apiKey.Id, 0);
            _loadBalancer.RecordLatency(model.ProviderId, sw.ElapsedMilliseconds);
            _loadBalancer.RecordKeyLatency(selectedKey.Id, sw.ElapsedMilliseconds);

            var outputTokens = CompressionService.EstimateTokens(aggregatedContent.ToString());
            var (inputCost, outputCost) = ComputeCost(model, inputTokens, outputTokens);
            var totalDurationSec = sw.ElapsedMilliseconds / 1000.0;
            double? outputTokensPerSecond = totalDurationSec > 0 ? outputTokens / totalDurationSec : null;

            var aggregatedSseBody = aggregatedSse.ToString();
            if (cacheEnabled)
                _cache.Set(cacheKey, aggregatedSseBody, isStream: true, cacheTtl);

            if (semanticCacheEnabled)
                await _cache.SetSemanticAsync(requestedModel, requestBody, aggregatedSseBody, isStream: true, semanticCacheTtl, CancellationToken.None);

            await EnqueueAuditLog(requestId, model, apiKey, aggregatedContent.ToString(), sw.ElapsedMilliseconds, true,
                RequestStatus.Success, inputTokens, inputTokensAfterCompression, outputTokens, inputCost, outputCost,
                compressionStrategy, compressionApplied, requestBody, ttftMs: ttftMs, outputTokensPerSecond: outputTokensPerSecond, compressionMappingKey: compressionMappingKey);

            _metrics.RecordRequest(model.Name, provider.Name, success: true, cacheHit: false, sw.ElapsedMilliseconds, inputTokens, outputTokens, rateLimited: false);
            return new EmptyResult();
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            sw.Stop();
            var outputTokens = CompressionService.EstimateTokens(aggregatedContent.ToString());
            var (inputCost, outputCost) = ComputeCost(model, inputTokens, outputTokens);
            var totalDurationSec = sw.ElapsedMilliseconds / 1000.0;
            double? outputTokensPerSecond = totalDurationSec > 0 ? outputTokens / totalDurationSec : null;

            var aggregatedSseBody = aggregatedSse.ToString();
            if (cacheEnabled && aggregatedSseBody.Length > 0)
                _cache.Set(cacheKey, aggregatedSseBody, isStream: true, cacheTtl);

            if (semanticCacheEnabled && aggregatedSseBody.Length > 0)
                await _cache.SetSemanticAsync(requestedModel, requestBody, aggregatedSseBody, isStream: true, semanticCacheTtl, CancellationToken.None);

            await EnqueueAuditLog(requestId, model, apiKey, aggregatedContent.ToString(), sw.ElapsedMilliseconds, true,
                RequestStatus.Interrupted, inputTokens, inputTokensAfterCompression, outputTokens, inputCost, outputCost,
                compressionStrategy, compressionApplied, requestBody, "INTERRUPTED", ex.Message, ttftMs: ttftMs, outputTokensPerSecond: outputTokensPerSecond, compressionMappingKey: compressionMappingKey);
            _metrics.RecordRequest(model.Name, provider.Name, success: false, cacheHit: false, sw.ElapsedMilliseconds, inputTokens, outputTokens, rateLimited: false);
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
                case RouteRuleType.Algorithm:
                    if (condition.RootElement.TryGetProperty("algorithm", out var algorithm))
                        return EvaluateAlgorithm(algorithm.GetString() ?? string.Empty, requestBody, condition.RootElement);
                    break;
            }
        }
        catch { }
        return false;
    }

    private static bool EvaluateAlgorithm(string algorithm, string requestBody, JsonElement condition)
    {
        return algorithm.ToLowerInvariant() switch
        {
            "codedetection" => ContainsCode(requestBody),
            "chinesedetection" => ContainsChinese(requestBody),
            "contextsize" => condition.TryGetProperty("maxTokens", out var maxTokens)
                && CompressionService.EstimateTokens(requestBody) <= maxTokens.GetInt32(),
            "mathdetection" => ContainsMath(requestBody),
            "translationdetection" => ContainsTranslation(requestBody),
            _ => false
        };
    }

    private static Model? AlgorithmicRoute(List<Model> candidates, string requestBody)
    {
        if (candidates.Count == 0) return null;

        var estimatedTokens = CompressionService.EstimateTokens(requestBody);
        var containsVisionContent = ContainsVisionContent(requestBody);
        var containsCode = ContainsCode(requestBody);
        var containsChinese = ContainsChinese(requestBody);
        var containsMath = ContainsMath(requestBody);
        var containsTools = ContainsToolUse(requestBody);

        // Vision requests require a vision-capable model
        if (containsVisionContent)
        {
            var vision = candidates.Where(m => m.SupportsVision).OrderByDescending(m => m.InputContextSize).FirstOrDefault();
            if (vision != null) return vision;
        }

        // Tool-use requests prefer a model that supports function calling
        if (containsTools)
        {
            var toolUse = candidates.Where(m => m.SupportsToolUse).OrderByDescending(m => m.InputContextSize).FirstOrDefault();
            if (toolUse != null) return toolUse;
        }

        // Code / reasoning requests prefer reasoning or thinking models
        if (containsCode || containsMath)
        {
            var reasoning = candidates
                .Where(m => m.SupportsReasoning || m.SupportsThinking)
                .OrderByDescending(m => m.InputContextSize)
                .FirstOrDefault();
            if (reasoning != null) return reasoning;
        }

        // Chinese content can benefit from larger context windows; prefer the largest
        if (containsChinese)
        {
            var chinese = candidates.OrderByDescending(m => m.InputContextSize).FirstOrDefault();
            if (chinese != null) return chinese;
        }

        // Long context requests need a model with enough input context
        var maxContext = candidates.Max(m => m.InputContextSize);
        if (maxContext > 0 && estimatedTokens > maxContext * 0.5)
        {
            var longContext = candidates
                .Where(m => m.InputContextSize >= estimatedTokens)
                .OrderBy(m => m.InputContextSize)
                .FirstOrDefault();
            if (longContext != null) return longContext;
        }

        // Default: pick the model with the largest context window as a safe general choice
        return candidates.OrderByDescending(m => m.InputContextSize).FirstOrDefault();
    }

    private static bool ContainsVisionContent(string requestBody)
    {
        return requestBody.Contains("image_url") || requestBody.Contains("data:image/") || requestBody.Contains("base64,");
    }

    private static bool ContainsToolUse(string requestBody)
    {
        return requestBody.Contains("\"tools\"") || requestBody.Contains("\"functions\"");
    }

    private static bool ContainsCode(string requestBody)
    {
        var codeIndicators = new[] { "```", "def ", "function ", "class ", "import ", "#include", "public static", "const ", "let ", "var " };
        return codeIndicators.Any(indicator => requestBody.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsChinese(string requestBody)
    {
        return requestBody.Any(c => c >= 0x4E00 && c <= 0x9FFF);
    }

    private static bool ContainsMath(string requestBody)
    {
        var mathIndicators = new[] { "\u003e", "\u003c", "=", "+", "-", "*", "/", "^", "sqrt", "integral", "derivative", "sum", "equation" };
        return mathIndicators.Any(indicator => requestBody.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTranslation(string requestBody)
    {
        var translationIndicators = new[] { "translate", "translation", "translate to", "翻译成", "翻訳" };
        return translationIndicators.Any(indicator => requestBody.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteStreamCacheResponseAsync(
        string cachedSseBody, string requestId, Model model, ApiKey apiKey, Stopwatch sw,
        int inputTokens, int inputTokensAfterCompression, int outputTokens, decimal inputCost, decimal outputCost,
        string compressionStrategy, bool compressionApplied, string requestBody, string? compressionMappingKey = null)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        var ttftSw = Stopwatch.StartNew();
        long ttftMs = 0;

        using var reader = new StringReader(cachedSseBody);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (ttftMs == 0) { ttftSw.Stop(); ttftMs = ttftSw.ElapsedMilliseconds; }
            await Response.WriteAsync(line + "\n");
            await Response.Body.FlushAsync();
        }

        sw.Stop();
        var totalDurationSec = sw.ElapsedMilliseconds / 1000.0;
        double? outputTokensPerSecond = totalDurationSec > 0 ? outputTokens / totalDurationSec : null;

        await EnqueueAuditLog(requestId, model, apiKey, cachedSseBody, sw.ElapsedMilliseconds, true,
            RequestStatus.Success, inputTokens, inputTokensAfterCompression, outputTokens, inputCost, outputCost,
            compressionStrategy, compressionApplied, requestBody, compressionMappingKey: compressionMappingKey, ttftMs: ttftMs, outputTokensPerSecond: outputTokensPerSecond, cacheHit: true);
    }

    private async Task EnqueueAuditLog(
        string requestId, Model model, ApiKey apiKey,
        string? responseContent, long totalDurationMs, bool isStream,
        RequestStatus status,
        int inputTokens, int inputTokensAfterCompression, int outputTokens,
        decimal inputCost, decimal outputCost,
        string compressionStrategy, bool compressionApplied,
        string? requestContent = null,
        string? errorCode = null, string? errorMessage = null, long ttftMs = 0,
        double? outputTokensPerSecond = null, bool cacheHit = false, string? compressionMappingKey = null, bool semanticCacheHit = false)
    {
        try
        {
            var org = await _db.Organizations.FindAsync(apiKey.OrganizationId);
            var sanitizedRequest = _desensitizationService.Desensitize(
                requestContent?.Length > 10000 ? requestContent[..10000] : requestContent,
                org ?? new Organization { EnableContentLogging = true });
            var sanitizedResponse = _desensitizationService.Desensitize(
                responseContent?.Length > 10000 ? responseContent[..10000] : responseContent,
                org ?? new Organization { EnableContentLogging = true });

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
                InputTokens = inputTokens,
                InputTokensAfterCompression = inputTokensAfterCompression,
                OutputTokens = outputTokens,
                CacheHit = cacheHit,
                SemanticCacheHit = semanticCacheHit,
                TimeToFirstTokenMs = ttftMs > 0 ? ttftMs : null,
                TotalDurationMs = totalDurationMs,
                OutputTokensPerSecond = outputTokensPerSecond,
                InputCost = inputCost,
                OutputCost = outputCost,
                Currency = "USD",
                CompressionApplied = compressionApplied,
                CompressionStrategy = compressionStrategy,
                CompressionMappingKey = compressionMappingKey,
                Status = status,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                IsStream = isStream,
                RequestContent = sanitizedRequest,
                ResponseContent = sanitizedResponse
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue audit log for {RequestId}", requestId);
        }
    }
}
