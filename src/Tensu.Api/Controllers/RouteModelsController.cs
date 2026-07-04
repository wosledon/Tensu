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
        var created = await _service.CreateAsync(routeModel);
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] RouteModel routeModel)
    {
        var updated = await _service.UpdateAsync(id, routeModel);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "Route model not found"));
        return Ok(ApiResponse<object>.Success(updated));
    }

    [HttpGet("batch-test")]
    public async Task<IActionResult> BatchTest()
    {
        return Ok(ApiResponse<object>.Success(new { test = true }));
    }

    public record BatchIdsRequest(int[] Ids);

    [HttpDelete("batch")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchIdsRequest request)
    {
        var ids = request.Ids ?? Array.Empty<int>();
        return Ok(ApiResponse<object>.Success(new { ids = ids.Length }));
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
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpDelete("{routeModelId}/rules/{ruleId}")]
    public async Task<IActionResult> DeleteRule(int routeModelId, int ruleId)
    {
        var deleted = await _service.DeleteRuleAsync(routeModelId, ruleId);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Rule not found"));
        return Ok(ApiResponse.Success());
    }

    public record ShadowTargetRequest(int? TargetModelId);

    [HttpPost("{id}/shadow-target")]
    public async Task<IActionResult> UpdateShadowTarget(int id, [FromBody] ShadowTargetRequest request)
    {
        var updated = await _service.UpdateShadowTargetAsync(id, request.TargetModelId);
        if (updated == null) return BadRequest(ApiResponse.Error(40001, "Route model not found or not in shadow mode"));
        return Ok(ApiResponse<object>.Success(updated));
    }
}
