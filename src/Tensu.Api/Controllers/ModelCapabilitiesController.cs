using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/model-capabilities")]
public class ModelCapabilitiesController : AdminBaseController
{
    private readonly ModelCapabilityService _service;

    public ModelCapabilitiesController(ModelCapabilityService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "ReadOnly")]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] int? modelId, [FromQuery] string? dimension)
    {
        var result = await _service.GetListAsync(request, modelId, dimension);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = "ReadOnly")]
    public async Task<IActionResult> Get(long id)
    {
        var capability = await _service.GetByIdAsync(id);
        if (capability == null) return NotFound(ApiResponse.Error(40401, "Model capability not found"));
        return Ok(ApiResponse<object>.Success(capability));
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create([FromBody] ModelCapability capability)
    {
        var created = await _service.CreateAsync(capability);
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Update(long id, [FromBody] ModelCapability capability)
    {
        var updated = await _service.UpdateAsync(id, capability);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "Model capability not found"));
        return Ok(ApiResponse<object>.Success(updated));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Model capability not found"));
        return Ok(ApiResponse.Success());
    }

    [HttpPost("import")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Import([FromBody] List<ModelCapability> capabilities)
    {
        var imported = await _service.ImportAsync(capabilities);
        return Ok(ApiResponse<object>.Success(imported));
    }

    [HttpGet("matrix")]
    [Authorize(Policy = "ReadOnly")]
    public async Task<IActionResult> Matrix([FromQuery] int? providerId)
    {
        var matrix = await _service.GetMatrixAsync(providerId);
        return Ok(ApiResponse<object>.Success(matrix));
    }

    [HttpPost("{modelId:int}/auto-evaluate")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> AutoEvaluate(int modelId, [FromBody] string[] dimensions)
    {
        var result = await _service.RunSyntheticEvaluationAsync(modelId, dimensions);
        return Ok(ApiResponse<object>.Success(result));
    }
}
