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

    public AnalyticsController(AnalyticsService service)
    {
        _service = service;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] int? orgId)
    {
        var result = await _service.GetDashboardAsync(orgId);
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
        var result = await _service.GetUsageAsync(from, to, granularity, orgId);
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
        var result = await _service.GetCostAsync(from, to, orgId);
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
        var result = await _service.GetPerformanceAsync(from, to, orgId);
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
        var result = await _service.GetCacheStatsAsync(from, to, orgId);
        return Ok(ApiResponse<object>.Success(result));
    }
}
