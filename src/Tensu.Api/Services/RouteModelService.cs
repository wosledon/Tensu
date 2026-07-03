using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class RouteModelService : BaseService
{
    private readonly TensuDbContext _db;

    public RouteModelService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<RouteModel>> GetListAsync(PagedRequest request)
    {
        var query = _db.RouteModels
            .Include(r => r.TargetModel)
            .Include(r => r.FallbackModel)
            .Include(r => r.RoutingModel)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(r => r.Name.Contains(request.Keyword));

        query = ApplyPaging(query, request, out var total);
        var items = await query.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<RouteModel?> GetByIdAsync(int id)
    {
        return await _db.RouteModels
            .Include(r => r.TargetModel).ThenInclude(m => m!.Provider)
            .Include(r => r.FallbackModel)
            .Include(r => r.RoutingModel)
            .Include(r => r.Rules).ThenInclude(r => r.TargetModel)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<RouteModel> CreateAsync(RouteModel routeModel)
    {
        routeModel.CreatedAt = DateTime.UtcNow;
        routeModel.UpdatedAt = DateTime.UtcNow;
        _db.RouteModels.Add(routeModel);
        await _db.SaveChangesAsync();
        return routeModel;
    }

    public async Task<RouteModel?> UpdateAsync(int id, RouteModel updated)
    {
        var rm = await _db.RouteModels.FindAsync(id);
        if (rm == null) return null;

        rm.Name = updated.Name;
        rm.Description = updated.Description;
        rm.Mode = updated.Mode;
        rm.TargetModelId = updated.TargetModelId;
        rm.FallbackModelId = updated.FallbackModelId;
        rm.RoutingModelId = updated.RoutingModelId;
        rm.IsEnabled = updated.IsEnabled;
        rm.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return rm;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var rm = await _db.RouteModels.FindAsync(id);
        if (rm == null) return false;
        _db.RouteModels.Remove(rm);
        await _db.SaveChangesAsync();
        return true;
    }

    // Rules
    public async Task<RouteRule> AddRuleAsync(int routeModelId, RouteRule rule)
    {
        rule.RouteModelId = routeModelId;
        rule.CreatedAt = DateTime.UtcNow;
        _db.RouteRules.Add(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task<bool> DeleteRuleAsync(int routeModelId, int ruleId)
    {
        var rule = await _db.RouteRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.RouteModelId == routeModelId);
        if (rule == null) return false;
        _db.RouteRules.Remove(rule);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<RouteModel?> ResolveAsync(string modelName)
    {
        return await _db.RouteModels
            .Include(r => r.TargetModel).ThenInclude(m => m!.Provider)
            .Include(r => r.FallbackModel)
            .Include(r => r.RoutingModel)
            .Include(r => r.Rules).ThenInclude(r => r.TargetModel).ThenInclude(m => m.Provider)
            .FirstOrDefaultAsync(r => r.Name == modelName && r.IsEnabled);
    }

    public async Task<RouteModel?> UpdateShadowTargetAsync(int routeModelId, int? targetModelId)
    {
        var rm = await _db.RouteModels.FindAsync(routeModelId);
        if (rm == null || rm.Mode != RouteModelMode.Shadow) return null;

        rm.TargetModelId = targetModelId;
        rm.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return rm;
    }
}
