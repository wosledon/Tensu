using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

/// <summary>
/// Service for invoking a dedicated LLM "routing model" to perform intent-based routing.
/// The routing model receives the user request and returns a JSON object with a recommended_model field.
/// Results are cached to reduce repeated routing costs.
/// </summary>
public class RoutingModelService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RoutingModelService> _logger;

    public RoutingModelService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RoutingModelService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Calls the routing model for a given route model and request body.
    /// Returns the recommended model name (Provider-Model or Model) or null if routing fails.
    /// The LLM is constrained to choose only from the supplied candidate models.
    /// </summary>
    public async Task<RoutingDecision?> RouteAsync(RouteModel routeModel, string requestBody, List<Model> candidates, string? originalModel = null)
    {
        if (routeModel.RoutingModelId == null) return null;
        if (candidates == null || candidates.Count == 0) return null;

        var cache = _serviceProvider.GetRequiredService<CacheService>();
        var messagesHash = ComputeMessagesHash(requestBody);
        var cacheKey = $"routing:{routeModel.Id}:{messagesHash}";

        // Try exact cache first
        var cached = cache.TryGet(cacheKey);
        if (cached.HasValue && cached.Value.hit)
        {
            _logger.LogDebug("Routing exact cache hit for route model {RouteModelId}", routeModel.Id);
            return JsonSerializer.Deserialize<RoutingDecision>(cached.Value.responseBody!);
        }

        // Try semantic cache for similar inputs
        var semanticHit = await cache.TryGetSemanticAsync($"routing:{routeModel.Id}", requestBody, 0.9f, CancellationToken.None);
        if (semanticHit.HasValue && semanticHit.Value.hit)
        {
            _logger.LogDebug("Routing semantic cache hit for route model {RouteModelId}", routeModel.Id);
            return JsonSerializer.Deserialize<RoutingDecision>(semanticHit.Value.responseBody!);
        }

        var db = _serviceProvider.GetRequiredService<TensuDbContext>();
        if (!routeModel.RoutingModelId.HasValue) return null;
        var routingModel = await db.Models
            .Include(m => m.Provider!).ThenInclude(p => p.Keys)
            .FirstOrDefaultAsync(m => m.Id == routeModel.RoutingModelId.Value);

        if (routingModel == null) return null;

        var providerService = _serviceProvider.GetRequiredService<ProviderService>();
        var loadBalancer = _serviceProvider.GetRequiredService<LoadBalancer>();
        var provider = routingModel.Provider;
        if (provider == null) return null;
        var selectedKey = loadBalancer.SelectKey(provider);
        if (selectedKey == null) return null;

        var routingPrompt = BuildRoutingPrompt(requestBody, candidates, originalModel);
        var routingRequestBody = new
        {
            model = routingModel.Name,
            messages = new[]
            {
                new { role = "system", content = BuildRoutingSystemPrompt(candidates) },
                new { role = "user", content = routingPrompt }
            },
            response_format = new { type = "json_object" }
        };

        var routingBodyJson = JsonSerializer.Serialize(routingRequestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var upstreamUrl = $"{provider.BaseUrl.TrimEnd('/')}/v1/chat/completions";

        var request = new HttpRequestMessage(HttpMethod.Post, upstreamUrl)
        {
            Content = new StringContent(routingBodyJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {providerService.DecryptKey(selectedKey.KeyValue)}");

        try
        {
            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Routing model returned status {Status}: {Body}", response.StatusCode, responseBody);
                return null;
            }

            int inputTokens = 0;
            int outputTokens = 0;
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("prompt_tokens", out var promptTokens) && promptTokens.TryGetInt32(out var pt))
                        inputTokens = pt;
                    if (usage.TryGetProperty("completion_tokens", out var completionTokens) && completionTokens.TryGetInt32(out var ct))
                        outputTokens = ct;
                }

                if (inputTokens == 0)
                    inputTokens = CompressionService.EstimateTokens(routingBodyJson);
                if (outputTokens == 0)
                    outputTokens = CompressionService.EstimateTokens(responseBody);
            }
            catch
            {
                inputTokens = CompressionService.EstimateTokens(routingBodyJson);
                outputTokens = CompressionService.EstimateTokens(responseBody);
            }

            var recommendation = ExtractRecommendation(responseBody);
            if (string.IsNullOrWhiteSpace(recommendation))
                return null;

            if (!IsValidCandidate(recommendation, candidates))
            {
                _logger.LogWarning("Routing model recommended '{Recommendation}' which is not in the candidate set; falling back to rule engine", recommendation);
                return null;
            }

            var decision = new RoutingDecision
            {
                RecommendedModel = recommendation.Trim(),
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                ProviderName = provider.Name,
                RoutingModelName = routingModel.Name
            };

            var cacheMinutes = _configuration.GetValue<int?>("Routing:CacheTtlMinutes") ?? 5;
            var cacheTtl = TimeSpan.FromMinutes(cacheMinutes);
            var serialized = JsonSerializer.Serialize(decision);
            cache.Set(cacheKey, serialized, isStream: false, cacheTtl);
            await cache.SetSemanticAsync($"routing:{routeModel.Id}", requestBody, serialized, isStream: false, cacheTtl, CancellationToken.None);
            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Routing model invocation failed for route model {RouteModelId}", routeModel.Id);
            return null;
        }
    }

    private static string BuildRoutingSystemPrompt(List<Model> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a routing assistant. Your job is to pick the single best model from the AVAILABLE_MODELS list below for the incoming user request.");
        sb.AppendLine("Respond with a JSON object containing only a 'recommended_model' field. The value must be one of the model identifiers listed exactly as shown.");
        sb.AppendLine();
        sb.AppendLine("AVAILABLE_MODELS:");
        foreach (var model in candidates)
        {
            var providerName = model.Provider?.Name ?? "unknown";
            var identifier = $"{providerName}-{model.Name}";
            sb.AppendLine($"- {identifier}");
        }
        sb.AppendLine();
        sb.AppendLine("Pick the model that best matches the request's requirements (e.g. coding, long context, multilingual, cost, speed). Return only the model identifier.");
        return sb.ToString();
    }

    private static string BuildRoutingPrompt(string requestBody, List<Model> candidates, string? originalModel)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(originalModel))
                sb.AppendLine($"Original model requested: {originalModel}");

            if (doc.RootElement.TryGetProperty("messages", out var messages))
                sb.AppendLine($"Messages: {messages.GetRawText()}");
            else
                sb.AppendLine($"Request body: {requestBody}");

            return sb.ToString();
        }
        catch
        {
            return requestBody;
        }
    }

    public static bool IsValidCandidate(string? recommendation, List<Model> candidates)
    {
        if (string.IsNullOrWhiteSpace(recommendation)) return false;
        var normalized = recommendation.Trim();
        return candidates.Any(m =>
        {
            var providerName = m.Provider?.Name ?? string.Empty;
            var withProvider = string.IsNullOrEmpty(providerName) ? m.Name : $"{providerName}-{m.Name}";
            return string.Equals(withProvider, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.Name, normalized, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string? ExtractRecommendation(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                var content = message.GetProperty("content").GetString() ?? "";
                using var contentDoc = JsonDocument.Parse(content);
                if (contentDoc.RootElement.TryGetProperty("recommended_model", out var recommendation))
                    return recommendation.GetString();
            }
        }
        catch
        {
            // Some routing models may return plain text
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("recommended_model", out var recommendation))
                    return recommendation.GetString();
            }
            catch { }
        }
        return null;
    }

    private static string ComputeMessagesHash(string requestBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            if (doc.RootElement.TryGetProperty("messages", out var messages))
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(messages.GetRawText()));
                return Convert.ToBase64String(hash)[..22];
            }
        }
        catch { }

        var fallback = SHA256.HashData(Encoding.UTF8.GetBytes(requestBody));
        return Convert.ToBase64String(fallback)[..22];
    }

    public class RoutingDecision
    {
        public string RecommendedModel { get; set; } = "";
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public string ProviderName { get; set; } = "";
        public string RoutingModelName { get; set; } = "";
    }
}
