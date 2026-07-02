using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize]
public class SettingsController : AdminBaseController
{
    private readonly SettingsService _service;

    public SettingsController(SettingsService service)
    {
        _service = service;
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

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(string key, [FromBody] SetSettingRequest request)
    {
        try
        {
            await _service.SetAsync(key, request.Value);
            return Ok(ApiResponse.Success());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Error(40001, ex.Message));
        }
    }
}
