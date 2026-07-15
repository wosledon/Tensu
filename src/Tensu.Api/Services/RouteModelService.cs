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
            .Include(r => r.Targets).ThenInclude(t => t.Model).ThenInclude(m => m!.Provider)
            .Include(r => r.FallbackModel)
            .Include(r => r.RoutingModel)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(r => r.Name.Contains(request.Keyword));

        var (pagedQuery, total) = await ApplyPagingAsync(query, request);
        var items = await pagedQuery.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<RouteModel?> GetByIdAsync(int id)
    {
        return await _db.RouteModels
            .Include(r => r.Targets).ThenInclude(t => t.Model).ThenInclude(m => m!.Provider)
            .Include(r => r.FallbackModel)
            .Include(r => r.RoutingModel)
            .Include(r => r.Rules).ThenInclude(r => r.TargetModel)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<RouteModel> CreateAsync(RouteModel routeModel)
    {
        var exists = await _db.RouteModels.AnyAsync(r => r.Name == routeModel.Name);
        if (exists) throw new InvalidOperationException($"Route model name '{routeModel.Name}' already exists");

        routeModel.CreatedAt = DateTime.UtcNow;
        routeModel.UpdatedAt = DateTime.UtcNow;
        _db.RouteModels.Add(routeModel);
        await _db.SaveChangesAsync();
        return routeModel;
    }

    public async Task<RouteModel?> UpdateAsync(int id, RouteModel updated)
    {
        var rm = await _db.RouteModels
            .Include(r => r.Targets)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (rm == null) return null;

        if (updated.Name != rm.Name)
        {
            var exists = await _db.RouteModels.AnyAsync(r => r.Name == updated.Name && r.Id != id);
            if (exists) throw new InvalidOperationException($"Route model name '{updated.Name}' already exists");
        }

        rm.Name = updated.Name;
        rm.Description = updated.Description;
        rm.Mode = updated.Mode;
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

    // ── Targets ──

    public async Task<RouteModelTarget> AddTargetAsync(int routeModelId, int modelId)
    {
        var existing = await _db.RouteModelTargets
            .FirstOrDefaultAsync(t => t.RouteModelId == routeModelId && t.ModelId == modelId);
        if (existing != null) return existing;

        var target = new RouteModelTarget
        {
            RouteModelId = routeModelId,
            ModelId = modelId,
            Priority = await _db.RouteModelTargets.CountAsync(t => t.RouteModelId == routeModelId)
        };
        _db.RouteModelTargets.Add(target);
        await _db.SaveChangesAsync();
        return target;
    }

    public async Task<bool> RemoveTargetAsync(int routeModelId, int targetId)
    {
        var target = await _db.RouteModelTargets
            .FirstOrDefaultAsync(t => t.Id == targetId && t.RouteModelId == routeModelId);
        if (target == null) return false;
        _db.RouteModelTargets.Remove(target);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<RouteModelTarget>> SetActiveTargetAsync(int routeModelId, int targetId)
    {
        var rm = await _db.RouteModels.FindAsync(routeModelId);
        if (rm == null || rm.Mode != RouteModelMode.Shadow)
            throw new InvalidOperationException("Only shadow mode supports active target switching");

        var targets = await _db.RouteModelTargets
            .Where(t => t.RouteModelId == routeModelId)
            .ToListAsync();

        var target = targets.FirstOrDefault(t => t.Id == targetId);
        if (target == null)
            throw new InvalidOperationException("Target not found");

        foreach (var t in targets)
            t.IsActive = t.Id == targetId;

        rm.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return targets;
    }

    public async Task UpdateTargetPriorityAsync(int routeModelId, int targetId, int priority)
    {
        var target = await _db.RouteModelTargets
            .FirstOrDefaultAsync(t => t.Id == targetId && t.RouteModelId == routeModelId);
        if (target == null) return;

        target.Priority = priority;
        await _db.SaveChangesAsync();
    }

    // ── Rules ──

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
            .Include(r => r.Targets).ThenInclude(t => t.Model).ThenInclude(m => m!.Provider)
            .Include(r => r.FallbackModel)
            .Include(r => r.RoutingModel)
            .Include(r => r.Rules).ThenInclude(r => r.TargetModel).ThenInclude(m => m!.Provider)
            .FirstOrDefaultAsync(r => r.Name == modelName && r.IsEnabled);
    }

    /// <summary>影子模式：切换活跃目标。供管理员后台使用。</summary>
    public async Task<RouteModel?> UpdateShadowTargetAsync(int routeModelId, int? targetModelId)
    {
        var rm = await _db.RouteModels
            .Include(r => r.Targets)
            .FirstOrDefaultAsync(r => r.Id == routeModelId);
        if (rm == null || rm.Mode != RouteModelMode.Shadow) return null;

        // 如果传了 targetModelId，在 targets 中找到对应项设为活跃
        // 如果传 null，清除所有活跃标记
        foreach (var t in rm.Targets)
            t.IsActive = t.ModelId == targetModelId;

        rm.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return rm;
    }
}
