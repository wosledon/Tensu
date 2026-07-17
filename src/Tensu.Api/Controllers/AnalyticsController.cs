using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize]
public class AnalyticsController : AdminBaseController
{
    private readonly AnalyticsService _service;
    private readonly AnomalyDetectionService _anomalyService;

    public AnalyticsController(AnalyticsService service, AnomalyDetectionService anomalyService)
    {
        _service = service;
        _anomalyService = anomalyService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] int? orgId)
    {
        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var result = await _service.GetDashboardAsync(effectiveOrgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("usage")]
    public async Task<IActionResult> Usage(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string granularity = "day",
        [FromQuery] int? orgId = null)
    {
        if (from == default) from = DateTime.UtcNow.AddDays(-7);
        if (to == default) to = DateTime.UtcNow;
        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var result = await _service.GetUsageAsync(from, to, granularity, effectiveOrgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("cost")]
    public async Task<IActionResult> Cost(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? orgId = null)
    {
        if (from == default) from = DateTime.UtcNow.AddDays(-30);
        if (to == default) to = DateTime.UtcNow;
        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var result = await _service.GetCostAsync(from, to, effectiveOrgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("performance")]
    public async Task<IActionResult> Performance(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? orgId = null)
    {
        if (from == default) from = DateTime.UtcNow.AddDays(-7);
        if (to == default) to = DateTime.UtcNow;
        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var result = await _service.GetPerformanceAsync(from, to, effectiveOrgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("cache")]
    public async Task<IActionResult> Cache(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? orgId = null)
    {
        if (from == default) from = DateTime.UtcNow.AddDays(-7);
        if (to == default) to = DateTime.UtcNow;
        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var result = await _service.GetCacheStatsAsync(from, to, effectiveOrgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health([FromQuery] int? orgId = null)
    {
        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var result = await _service.GetHealthAsync(effectiveOrgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("by-api-key")]
    public async Task<IActionResult> ByApiKey(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? orgId = null)
    {
        if (from == default) from = DateTime.UtcNow.AddDays(-7);
        if (to == default) to = DateTime.UtcNow;
        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var result = await _service.GetByApiKeyAsync(from, to, effectiveOrgId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("anomalies")]
    public async Task<IActionResult> Anomalies(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? orgId = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (from == default) from = DateTime.UtcNow.AddDays(-1);
        if (to == default) to = DateTime.UtcNow;
        var effectiveOrgId = IsSuperAdmin ? orgId : CurrentOrgId;
        var result = await _anomalyService.DetectAsync(from, to, effectiveOrgId);
        if (!string.IsNullOrWhiteSpace(severity))
        {
            var sev = severity.Trim();
            result = result.Where(a => a.Severity.Equals(sev, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim();
            result = result.Where(a => a.Type.Equals(t, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var total = result.Count;
        var items = result.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(ApiResponse<object>.Success(new { items, total, page, pageSize }));
    }
}
