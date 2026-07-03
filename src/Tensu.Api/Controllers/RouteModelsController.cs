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

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Route model not found"));
        return Ok(ApiResponse.Success());
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
