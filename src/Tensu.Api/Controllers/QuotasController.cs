using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/quotas")]
[Authorize(Policy = "SuperAdmin")]
public class QuotasController : AdminBaseController
{
    private readonly QuotaService _service;

    public QuotasController(QuotaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] string? scope, [FromQuery] int? orgId)
    {
        var result = await _service.GetListAsync(request, scope, orgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var quota = await _service.GetByIdAsync(id);
        if (quota == null) return NotFound(ApiResponse.Error(40401, "Quota not found"));
        return Ok(ApiResponse<object>.Success(quota));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Quota quota)
    {
        var created = await _service.CreateAsync(quota);
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Quota quota)
    {
        var updated = await _service.UpdateAsync(id, quota);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "Quota not found"));
        return Ok(ApiResponse<object>.Success(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Quota not found"));
        return Ok(ApiResponse.Success());
    }
}
