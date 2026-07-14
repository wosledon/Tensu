using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/organizations")]
[Authorize(Policy = "SuperAdmin")]
public class OrganizationsController : AdminBaseController
{
    private readonly OrganizationService _service;

    public OrganizationsController(OrganizationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var tree = await _service.GetTreeAsync();
        return Ok(ApiResponse<object>.Success(tree));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var org = await _service.GetByIdAsync(id);
        if (org == null) return NotFound(ApiResponse.Error(40401, "Organization not found"));
        return Ok(ApiResponse<object>.Success(org));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Organization org, [FromQuery] int? parentId)
    {
        var created = await _service.CreateAsync(org, parentId);
        await LogAdminAuditAsync("create", "Organization", created.Id.ToString(), $"Name={created.Name}");
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Organization org)
    {
        var updated = await _service.UpdateAsync(id, org);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "Organization not found"));
        await LogAdminAuditAsync("update", "Organization", id.ToString(), $"Name={updated.Name}");
        return Ok(ApiResponse<object>.Success(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound(ApiResponse.Error(40401, "Organization not found"));
            await LogAdminAuditAsync("delete", "Organization", id.ToString());
            return Ok(ApiResponse.Success());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Error(40001, ex.Message));
        }
    }
}
