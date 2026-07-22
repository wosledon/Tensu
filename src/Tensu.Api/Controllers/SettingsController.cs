using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;
using Tensu.Core.Common;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = "SuperAdmin")]
public class SettingsController : AdminBaseController
{
    private readonly SettingsService _service;
    private readonly CacheService _cacheService;

    public SettingsController(SettingsService service, CacheService cacheService)
    {
        _service = service;
        _cacheService = cacheService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await _service.GetAllAsync();
        return Ok(ApiResponse<object>.Success(settings));
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var value = await _service.GetAsync(key);
        if (value == null) return NotFound(ApiResponse.Error(40401, "Setting not found"));
        return Ok(ApiResponse<object>.Success(new { key, value }));
    }

    public record SetSettingRequest(string Value);

    [HttpPost("reload")]
    public async Task<IActionResult> Reload()
    {
        _cacheService.InvalidateBackendCache();
        await LogAdminAuditAsync("reload", "Settings", "", "Configuration reload triggered");
        return Ok(ApiResponse.Success());
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(string key, [FromBody] SetSettingRequest request)
    {
        try
        {
            await _service.SetAsync(key, request.Value);
            await LogAdminAuditAsync("set", "Setting", key, $"Value={request.Value}");
            return Ok(ApiResponse.Success());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Error(40001, ex.Message));
        }
    }
}
