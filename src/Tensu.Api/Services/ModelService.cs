using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class ModelService : BaseService
{
    private readonly TensuDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProviderService _providerService;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public ModelService(TensuDbContext db, IHttpClientFactory httpClientFactory, ProviderService providerService)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _providerService = providerService;
    }

    public async Task<PagedResult<object>> GetListAsync(PagedRequest request, int? providerId = null)
    {
        var query = _db.Models.Include(m => m.Provider).Include(m => m.Pricings).AsQueryable();

        if (providerId.HasValue)
            query = query.Where(m => m.ProviderId == providerId.Value);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(m => m.Name.Contains(request.Keyword) || (m.DisplayName != null && m.DisplayName.Contains(request.Keyword)));

        var (pagedQuery, total) = await ApplyPagingAsync(query, request);
        var items = await pagedQuery.ToListAsync();
        return ToPagedResult(items.Select(MapToListDto).ToList(), total, request);
    }

    public static object MapToListDto(Model m)
    {
        return new
        {
            m.Id,
            m.ProviderId,
            m.Name,
            m.DisplayName,
            m.Description,
            m.SupportsVision,
            m.SupportsReasoning,
            m.SupportsToolUse,
            m.SupportsThinking,
            m.ThinkingStrengths,
            m.InputContextSize,
            m.OutputContextSize,
            m.IsEnabled,
            m.CompressionEnabled,
            m.CreatedAt,
            m.UpdatedAt,
            Provider = m.Provider == null ? null : new
            {
                m.Provider.Id,
                m.Provider.Name,
                m.Provider.Protocol,
                m.Provider.BaseUrl,
                m.Provider.HealthStatus,
                m.Provider.IsEnabled,
                m.Provider.KeyLoadBalanceStrategy,
                m.Provider.CreatedAt,
                m.Provider.UpdatedAt
            },
            Pricings = m.Pricings?.Select(p => new
            {
                p.Id,
                p.ModelId,
                p.InputPricePerMillionTokens,
                p.OutputPricePerMillionTokens,
                p.CachedInputPricePerMillionTokens,
                p.ThinkingPricePerMillionTokens,
                p.Currency,
                p.ExchangeRate,
                p.EffectiveFrom,
                p.EffectiveTo
            }).ToList()
        };
    }

    public async Task<object?> GetByIdAsync(int id)
    {
        var model = await _db.Models.Include(m => m.Provider).Include(m => m.Pricings)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (model == null) return null;
        return MapToListDto(model);
    }

    public async Task<Model> CreateAsync(Model model)
    {
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        _db.Models.Add(model);
        await _db.SaveChangesAsync();
        return model;
    }

    public async Task<Model?> UpdateAsync(int id, Model updated)
    {
        var model = await _db.Models.FindAsync(id);
        if (model == null) return null;

        model.Name = updated.Name;
        model.ProviderId = updated.ProviderId;
        model.DisplayName = updated.DisplayName;
        model.Description = updated.Description;
        model.SupportsVision = updated.SupportsVision;
        model.SupportsReasoning = updated.SupportsReasoning;
        model.SupportsToolUse = updated.SupportsToolUse;
        model.SupportsThinking = updated.SupportsThinking;
        model.ThinkingStrengths = updated.ThinkingStrengths;
        model.InputContextSize = updated.InputContextSize;
        model.OutputContextSize = updated.OutputContextSize;
        model.IsEnabled = updated.IsEnabled;
        model.CompressionEnabled = updated.CompressionEnabled;
        model.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return model;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var model = await _db.Models.FindAsync(id);
        if (model == null) return false;
        _db.Models.Remove(model);
        await _db.SaveChangesAsync();
        return true;
    }

    // Pricing
    public async Task<ModelPricing> AddPricingAsync(int modelId, ModelPricing pricing)
    {
        pricing.ModelId = modelId;
        pricing.CreatedAt = DateTime.UtcNow;
        _db.ModelPricings.Add(pricing);
        await _db.SaveChangesAsync();
        return pricing;
    }

    public async Task<ModelPricing?> GetCurrentPricingAsync(int modelId)
    {
        return await _db.ModelPricings
            .Where(p => p.ModelId == modelId && (p.EffectiveTo == null || p.EffectiveTo > DateTime.UtcNow))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync();
    }

    public async Task<List<object>> GetPricingHistoryAsync(int modelId)
    {
        return await _db.ModelPricings
            .Where(p => p.ModelId == modelId)
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => new
            {
                p.Id,
                p.ModelId,
                p.InputPricePerMillionTokens,
                p.OutputPricePerMillionTokens,
                p.CachedInputPricePerMillionTokens,
                p.ThinkingPricePerMillionTokens,
                p.Currency,
                p.ExchangeRate,
                p.EffectiveFrom,
                p.EffectiveTo,
                p.CreatedAt
            })
            .Cast<object>()
            .ToListAsync();
    }

    public async Task<List<object>> GetAllEnabledAsync()
    {
        return await _db.Models.Where(m => m.IsEnabled)
            .Include(m => m.Provider)
            .Select(m => new
            {
                m.Id,
                m.ProviderId,
                m.Name,
                m.DisplayName,
                m.Description,
                m.SupportsVision,
                m.SupportsReasoning,
                m.SupportsToolUse,
                m.SupportsThinking,
                m.InputContextSize,
                m.OutputContextSize,
                m.IsEnabled,
                m.CompressionEnabled,
                Provider = m.Provider == null ? null : new
                {
                    m.Provider.Id,
                    m.Provider.Name,
                    m.Provider.Protocol,
                    m.Provider.BaseUrl,
                    m.Provider.HealthStatus,
                    m.Provider.IsEnabled,
                    m.Provider.KeyLoadBalanceStrategy,
                    m.Provider.CreatedAt,
                    m.Provider.UpdatedAt
                }
            })
            .Cast<object>()
            .ToListAsync();
    }

    public async Task<SyncModelsResult> SyncModelsFromProviderAsync(int providerId)
    {
        var provider = await _db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId)
            ?? throw new InvalidOperationException("Provider not found.");

        if (provider.Protocol == ProtocolType.Anthropic)
            throw new NotSupportedException("Anthropic does not expose a standard models list API. Please add models manually.");

        var key = await _providerService.GetActiveKeyAsync(providerId)
            ?? throw new InvalidOperationException("Provider has no active key.");

        var decryptedKey = _providerService.DecryptKey(key.KeyValue);

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", decryptedKey);

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var modelsPath = "/v1/models";
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            baseUrl = baseUrl[..^3];
        var response = await client.GetAsync($"{baseUrl}{modelsPath}");

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to fetch models from provider: {(int)response.StatusCode} {response.StatusCode} at {baseUrl}{modelsPath}. {body}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<OpenAiModelsResponse>(json, JsonOptions)
            ?? throw new HttpRequestException("Invalid models response from provider.");

        var existingNames = await _db.Models
            .Where(m => m.ProviderId == providerId)
            .Select(m => m.Name)
            .ToHashSetAsync();

        var added = 0;
        var existing = 0;

        foreach (var item in payload.Data)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                continue;

            if (existingNames.Contains(item.Id))
            {
                existing++;
                continue;
            }

            var nameLower = item.Id.ToLowerInvariant();

            var model = new Model
            {
                ProviderId = providerId,
                Name = item.Id,
                DisplayName = item.Id,
                IsEnabled = true,
                CompressionEnabled = true,
                SupportsVision = nameLower.Contains("vision"),
                SupportsReasoning = nameLower.Contains("o1") || nameLower.Contains("reasoning"),
                SupportsToolUse = nameLower.Contains("tool"),
                SupportsThinking = nameLower.Contains("thinking"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.Models.Add(model);
            existingNames.Add(item.Id);
            added++;
        }

        await _db.SaveChangesAsync();

        return new SyncModelsResult(added, existing, payload.Data.Count);
    }
}

public class OpenAiModelsResponse
{
    public List<OpenAiModelEntry> Data { get; set; } = [];
}

public class OpenAiModelEntry
{
    public string Id { get; set; } = string.Empty;
}

public record SyncModelsResult(int Added, int Existing, int Total);
