using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize]
public class AuditController : AdminBaseController
{
    private readonly AuditService _service;

    public AuditController(AuditService service)
    {
        _service = service;
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] PagedRequest request,
        [FromQuery] string? modelName)
    {
        var orgId = IsSuperAdmin ? (int?)null : CurrentOrgId;
        var result = await _service.GetListAsync(request, orgId: orgId, modelName: modelName);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("logs/{requestId}")]
    public async Task<IActionResult> GetLog(string requestId)
    {
        var log = await _service.GetByRequestIdAsync(requestId);
        if (log == null) return NotFound(ApiResponse.Error(40401, "Request log not found"));
        if (!IsSuperAdmin && log.OrganizationId != CurrentOrgId)
            return Forbid();
        return Ok(ApiResponse<object>.Success(log));
    }

    [HttpGet("archived")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> GetArchived(
        [FromQuery] PagedRequest request,
        [FromQuery] string? modelName,
        [FromQuery] int? orgId)
    {
        var result = await _service.GetArchivedListAsync(request, orgId: orgId, modelName: modelName);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("archived/{requestId}")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> GetArchivedById(string requestId)
    {
        var log = await _service.GetArchivedByRequestIdAsync(requestId);
        if (log == null) return NotFound(ApiResponse.Error(40401, "Archived request log not found"));
        return Ok(ApiResponse<object>.Success(log));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? orgId)
    {
        if (from == default) from = DateTime.UtcNow.AddDays(-7);
        if (to == default) to = DateTime.UtcNow;

        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var summary = await _service.GetSummaryAsync(from, to, effectiveOrgId);
        return Ok(ApiResponse<object>.Success(summary));
    }
}
