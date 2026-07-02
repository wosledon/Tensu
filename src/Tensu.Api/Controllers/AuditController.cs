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
        var result = await _service.GetListAsync(request, orgId: null, modelName: modelName);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("logs/{requestId}")]
    public async Task<IActionResult> GetLog(string requestId)
    {
        var log = await _service.GetByRequestIdAsync(requestId);
        if (log == null) return NotFound(ApiResponse.Error(40401, "Request log not found"));
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

        var summary = await _service.GetSummaryAsync(from, to, orgId);
        return Ok(ApiResponse<object>.Success(summary));
    }
}
