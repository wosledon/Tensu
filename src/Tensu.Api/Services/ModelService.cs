using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

public class ModelService : BaseService
{
    private readonly TensuDbContext _db;

    public ModelService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Model>> GetListAsync(PagedRequest request, int? providerId = null)
    {
        var query = _db.Models.Include(m => m.Provider).Include(m => m.Pricings).AsQueryable();

        if (providerId.HasValue)
            query = query.Where(m => m.ProviderId == providerId.Value);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(m => m.Name.Contains(request.Keyword) || (m.DisplayName != null && m.DisplayName.Contains(request.Keyword)));

        query = ApplyPaging(query, request, out var total);
        var items = await query.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<Model?> GetByIdAsync(int id)
    {
        return await _db.Models.Include(m => m.Provider).Include(m => m.Pricings)
            .FirstOrDefaultAsync(m => m.Id == id);
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

    public async Task<List<Model>> GetAllEnabledAsync()
    {
        return await _db.Models.Where(m => m.IsEnabled)
            .Include(m => m.Provider)
            .ToListAsync();
    }
}
