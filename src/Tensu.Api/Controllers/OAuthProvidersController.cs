using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/oauth-providers")]
[Authorize(Policy = "SuperAdmin")]
public class OAuthProvidersController : AdminBaseController
{
    private readonly OAuthProviderService _service;

    public OAuthProvidersController(OAuthProviderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request)
    {
        var result = await _service.GetListAsync(request);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var provider = await _service.GetByIdAsync(id);
        if (provider == null) return NotFound(ApiResponse.Error(40401, "OAuth provider not found"));
        return Ok(ApiResponse<object>.Success(provider));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveOAuthProviderRequest request)
    {
        var created = await _service.CreateAsync(request);
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveOAuthProviderRequest request)
    {
        var updated = await _service.UpdateAsync(id, request);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "OAuth provider not found"));
        return Ok(ApiResponse<object>.Success(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "OAuth provider not found"));
        return Ok(ApiResponse.Success());
    }
}
