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
    /// </summary>
    public async Task<RoutingDecision?> RouteAsync(RouteModel routeModel, string requestBody, string? originalModel = null)
    {
        if (routeModel.RoutingModelId == null) return null;

        var cache = _serviceProvider.GetRequiredService<CacheService>();
        var messagesHash = ComputeMessagesHash(requestBody);
        var cacheKey = $"routing:{routeModel.Id}:{messagesHash}";

        var cached = cache.TryGet(cacheKey);
        if (cached.HasValue && cached.Value.hit)
        {
            _logger.LogDebug("Routing cache hit for route model {RouteModelId}", routeModel.Id);
            return JsonSerializer.Deserialize<RoutingDecision>(cached.Value.responseBody!);
        }

        var db = _serviceProvider.GetRequiredService<TensuDbContext>();
        var routingModel = await db.Models
            .Include(m => m.Provider).ThenInclude(p => p.Keys)
            .FirstOrDefaultAsync(m => m.Id == routeModel.RoutingModelId.Value);

        if (routingModel == null) return null;

        var providerService = _serviceProvider.GetRequiredService<ProviderService>();
        var loadBalancer = _serviceProvider.GetRequiredService<LoadBalancer>();
        var provider = routingModel.Provider;
        var selectedKey = loadBalancer.SelectKey(provider);
        if (selectedKey == null) return null;

        var routingPrompt = BuildRoutingPrompt(requestBody, originalModel);
        var routingRequestBody = new
        {
            model = routingModel.Name,
            messages = new[]
            {
                new { role = "system", content = "You are a routing assistant. Given a user request, recommend the best model. Respond with a JSON object containing only a 'recommended_model' field. The value should be in the format 'Provider-Model' or just 'Model'." },
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

            var decision = new RoutingDecision
            {
                RecommendedModel = recommendation.Trim(),
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                ProviderName = provider.Name,
                RoutingModelName = routingModel.Name
            };

            var cacheMinutes = _configuration.GetValue<int?>("Routing:CacheTtlMinutes") ?? 5;
            cache.Set(cacheKey, JsonSerializer.Serialize(decision), isStream: false, TimeSpan.FromMinutes(cacheMinutes));
            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Routing model invocation failed for route model {RouteModelId}", routeModel.Id);
            return null;
        }
    }

    private static string BuildRoutingPrompt(string requestBody, string? originalModel)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(originalModel))
                parts.Add($"Original model requested: {originalModel}");

            if (doc.RootElement.TryGetProperty("messages", out var messages))
                parts.Add($"Messages: {messages.GetRawText()}");

            return string.Join("\n", parts);
        }
        catch
        {
            return requestBody;
        }
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
