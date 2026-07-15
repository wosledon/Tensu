using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/route-models")]
[Authorize(Policy = "Admin")]
public class RouteModelsController : AdminBaseController
{
    private readonly RouteModelService _service;

    public RouteModelsController(RouteModelService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request)
    {
        var result = await _service.GetListAsync(request);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var rm = await _service.GetByIdAsync(id);
        if (rm == null) return NotFound(ApiResponse.Error(40401, "Route model not found"));
        return Ok(ApiResponse<object>.Success(rm));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RouteModel routeModel)
    {
        try
        {
            var created = await _service.CreateAsync(routeModel);
            await LogAdminAuditAsync("create", "RouteModel", created.Id.ToString(), $"Name={created.Name}");
            return Ok(ApiResponse<object>.Success(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Error(40001, ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] RouteModel routeModel)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, routeModel);
            if (updated == null) return NotFound(ApiResponse.Error(40401, "Route model not found"));
            await LogAdminAuditAsync("update", "RouteModel", id.ToString(), $"Name={updated.Name}");
            return Ok(ApiResponse<object>.Success(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Error(40001, ex.Message));
        }
    }

    [HttpGet("batch-test")]
    public async Task<IActionResult> BatchTest([FromQuery] int[] ids)
    {
        if (ids == null || ids.Length == 0) return Ok(ApiResponse<object>.Success(new { results = Array.Empty<object>() }));
        var results = new List<object>();
        foreach (var id in ids)
        {
            var rm = await _service.GetByIdAsync(id);
            results.Add(new
            {
                id,
                exists = rm != null,
                name = rm?.Name,
                mode = rm?.Mode.ToString(),
                isEnabled = rm?.IsEnabled ?? false,
                ruleCount = rm?.Rules?.Count ?? 0,
                targetCount = rm?.Targets?.Count ?? 0,
                hasFallback = rm?.FallbackModelId.HasValue ?? false
            });
        }
        return Ok(ApiResponse<object>.Success(new { results }));
    }

    public record BatchIdsRequest(int[] Ids);

    [HttpDelete("batch")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchIdsRequest request)
    {
        var ids = request.Ids ?? Array.Empty<int>();
        var deleted = 0;
        foreach (var id in ids.Distinct())
        {
            if (await _service.DeleteAsync(id))
            {
                deleted++;
                await LogAdminAuditAsync("batch_delete", "RouteModel", id.ToString());
            }
        }
        return Ok(ApiResponse<object>.Success(new { ids = deleted }));
    }

    [HttpDelete("batch/delete")]
    public async Task<IActionResult> BatchDeleteAlias([FromBody] BatchIdsRequest request)
    {
        return await BatchDelete(request);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!int.TryParse(id, out var modelId)) return NotFound(ApiResponse.Error(40401, "Route model not found"));
        var deleted = await _service.DeleteAsync(modelId);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Route model not found"));
        await LogAdminAuditAsync("delete", "RouteModel", modelId.ToString());
        return Ok(ApiResponse.Success());
    }

    [HttpPost("batch/enable")]
    public async Task<IActionResult> BatchEnable([FromBody] int[] ids)
    {
        if (ids == null || ids.Length == 0) return BadRequest(ApiResponse.Error(40001, "No ids provided"));
        var results = new List<object>();
        foreach (var id in ids.Distinct())
        {
            try
            {
                var rm = await _service.GetByIdAsync(id);
                if (rm == null) { results.Add(new { id, success = false, error = "Not found" }); continue; }
                rm.IsEnabled = true;
                await _service.UpdateAsync(id, rm);
                await LogAdminAuditAsync("batch_enable", "RouteModel", id.ToString());
                results.Add(new { id, success = true });
            }
            catch (Exception ex)
            {
                results.Add(new { id, success = false, error = ex.Message });
            }
        }
        return Ok(ApiResponse<object>.Success(results));
    }

    [HttpPost("batch/disable")]
    public async Task<IActionResult> BatchDisable([FromBody] int[] ids)
    {
        if (ids == null || ids.Length == 0) return BadRequest(ApiResponse.Error(40001, "No ids provided"));
        var results = new List<object>();
        foreach (var id in ids.Distinct())
        {
            try
            {
                var rm = await _service.GetByIdAsync(id);
                if (rm == null) { results.Add(new { id, success = false, error = "Not found" }); continue; }
                rm.IsEnabled = false;
                await _service.UpdateAsync(id, rm);
                await LogAdminAuditAsync("batch_disable", "RouteModel", id.ToString());
                results.Add(new { id, success = true });
            }
            catch (Exception ex)
            {
                results.Add(new { id, success = false, error = ex.Message });
            }
        }
        return Ok(ApiResponse<object>.Success(results));
    }

    [HttpPost("{id}/rules")]
    public async Task<IActionResult> AddRule(int id, [FromBody] RouteRule rule)
    {
        var created = await _service.AddRuleAsync(id, rule);
        await LogAdminAuditAsync("add_rule", "RouteRule", created.Id.ToString(), $"RouteModelId={id}");
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpDelete("{routeModelId}/rules/{ruleId}")]
    public async Task<IActionResult> DeleteRule(int routeModelId, int ruleId)
    {
        var deleted = await _service.DeleteRuleAsync(routeModelId, ruleId);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Rule not found"));
        await LogAdminAuditAsync("delete_rule", "RouteRule", ruleId.ToString(), $"RouteModelId={routeModelId}");
        return Ok(ApiResponse.Success());
    }

    // ── Targets ──

    public record AddTargetRequest(int ModelId);

    [HttpPost("{id}/targets")]
    public async Task<IActionResult> AddTarget(int id, [FromBody] AddTargetRequest request)
    {
        var target = await _service.AddTargetAsync(id, request.ModelId);
        await LogAdminAuditAsync("add_target", "RouteModelTarget", target.Id.ToString(), $"RouteModelId={id}, ModelId={request.ModelId}");
        return Ok(ApiResponse<object>.Success(target));
    }

    [HttpDelete("{routeModelId}/targets/{targetId}")]
    public async Task<IActionResult> RemoveTarget(int routeModelId, int targetId)
    {
        var deleted = await _service.RemoveTargetAsync(routeModelId, targetId);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Target not found"));
        await LogAdminAuditAsync("remove_target", "RouteModelTarget", targetId.ToString(), $"RouteModelId={routeModelId}");
        return Ok(ApiResponse.Success());
    }

    public record SetActiveTargetRequest(int TargetId);

    /// <summary>影子模式：设置活跃目标</summary>
    [HttpPost("{id}/targets/active")]
    public async Task<IActionResult> SetActiveTarget(int id, [FromBody] SetActiveTargetRequest request)
    {
        try
        {
            var targets = await _service.SetActiveTargetAsync(id, request.TargetId);
            await LogAdminAuditAsync("set_active_target", "RouteModel", id.ToString(), $"TargetId={request.TargetId}");
            return Ok(ApiResponse<object>.Success(targets));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Error(40001, ex.Message));
        }
    }

    public record UpdateTargetPriorityRequest(int TargetId, int Priority);

    [HttpPut("{id}/targets/priority")]
    public async Task<IActionResult> UpdateTargetPriority(int id, [FromBody] UpdateTargetPriorityRequest request)
    {
        await _service.UpdateTargetPriorityAsync(id, request.TargetId, request.Priority);
        return Ok(ApiResponse.Success());
    }

    public record ShadowTargetRequest(int? TargetModelId);

    [HttpPost("{id}/shadow-target")]
    public async Task<IActionResult> UpdateShadowTarget(int id, [FromBody] ShadowTargetRequest request)
    {
        var updated = await _service.UpdateShadowTargetAsync(id, request.TargetModelId);
        if (updated == null) return BadRequest(ApiResponse.Error(40001, "Route model not found or not in shadow mode"));
        await LogAdminAuditAsync("update_shadow_target", "RouteModel", id.ToString(), $"TargetModelId={request.TargetModelId}");
        return Ok(ApiResponse<object>.Success(updated));
    }
}
