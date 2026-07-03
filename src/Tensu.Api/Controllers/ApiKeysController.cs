using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/api-keys")]
[Authorize(Policy = "Admin")]
public class ApiKeysController : AdminBaseController
{
    private readonly ApiKeyService _service;

    public ApiKeysController(ApiKeyService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request)
    {
        var orgId = IsSuperAdmin ? (int?)null : CurrentOrgId;
        var result = await _service.GetListAsync(request, orgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var key = await _service.GetByIdAsync(id);
        if (key == null) return NotFound(ApiResponse.Error(40401, "API Key not found"));
        if (!IsSuperAdmin && ((dynamic)key).OrganizationId != CurrentOrgId)
            return Forbid();
        return Ok(ApiResponse<object>.Success(key));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ApiKey apiKey)
    {
        if (!IsSuperAdmin)
        {
            apiKey.OrganizationId = CurrentOrgId;
            apiKey.UserId = CurrentUserId;
        }
        var (created, plainTextKey) = await _service.CreateAsync(apiKey);
        return Ok(ApiResponse<object>.Success(new
        {
            created.Id,
            created.Name,
            Key = plainTextKey, // Only returned on creation
            created.KeyPrefix,
            created.OrganizationId,
            created.UserId,
            created.Status
        }));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ApiKey apiKey)
    {
        var existing = await _service.GetEntityByIdAsync(id);
        if (existing == null) return NotFound(ApiResponse.Error(40401, "API Key not found"));
        if (!IsSuperAdmin && existing.OrganizationId != CurrentOrgId)
            return Forbid();

        var updated = await _service.UpdateAsync(id, apiKey);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "API Key not found"));
        updated.KeyValue = "***";
        return Ok(ApiResponse<object>.Success(ApiKeyService.MapToListDto(updated)));
    }

    [HttpPost("{id}/revoke")]
    public async Task<IActionResult> Revoke(int id)
    {
        var existing = await _service.GetEntityByIdAsync(id);
        if (existing == null) return NotFound(ApiResponse.Error(40401, "API Key not found"));
        if (!IsSuperAdmin && existing.OrganizationId != CurrentOrgId)
            return Forbid();

        var revoked = await _service.RevokeAsync(id);
        if (!revoked) return NotFound(ApiResponse.Error(40401, "API Key not found"));
        return Ok(ApiResponse.Success());
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _service.GetEntityByIdAsync(id);
        if (existing == null) return NotFound(ApiResponse.Error(40401, "API Key not found"));
        if (!IsSuperAdmin && existing.OrganizationId != CurrentOrgId)
            return Forbid();

        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "API Key not found"));
        return Ok(ApiResponse.Success());
    }
}
