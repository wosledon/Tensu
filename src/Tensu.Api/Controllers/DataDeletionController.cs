using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/data-deletions")]
public class DataDeletionController : AdminBaseController
{
    private readonly DataDeletionService _service;

    public DataDeletionController(DataDeletionService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] int? orgId)
    {
        var result = await _service.GetListAsync(request, orgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null) return NotFound(ApiResponse.Error(40401, "Data deletion request not found"));
        return Ok(ApiResponse<object>.Success(item));
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create([FromBody] DataDeletionRequest request)
    {
        var created = await _service.CreateAsync(request);
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPost("{id:long}/process")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Process(long id)
    {
        var (success, error) = await _service.ProcessAsync((int)id);
        if (!success) return BadRequest(ApiResponse.Error(40001, error ?? "Failed to process deletion request"));
        return Ok(ApiResponse.Success());
    }
}
