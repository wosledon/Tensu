using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/providers")]
[Authorize(Policy = "Admin")]
public class ProvidersController : AdminBaseController
{
    private readonly ProviderService _service;

    public ProvidersController(ProviderService service)
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
        if (provider == null) return NotFound(ApiResponse.Error(40401, "Provider not found"));
        return Ok(ApiResponse<object>.Success(provider));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Provider provider)
    {
        var created = await _service.CreateAsync(provider);
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Provider provider)
    {
        var updated = await _service.UpdateAsync(id, provider);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "Provider not found"));
        return Ok(ApiResponse<object>.Success(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound(ApiResponse.Error(40401, "Provider not found"));
            return Ok(ApiResponse.Success());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Error(40001, ex.Message));
        }
    }

    [HttpPost("{id}/keys")]
    public async Task<IActionResult> AddKey(int id, [FromBody] ProviderKey key)
    {
        var created = await _service.AddKeyAsync(id, key);
        // Don't return the encrypted key value
        created.KeyValue = "***";
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpDelete("{providerId}/keys/{keyId}")]
    public async Task<IActionResult> DeleteKey(int providerId, int keyId)
    {
        var deleted = await _service.DeleteKeyAsync(providerId, keyId);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Key not found"));
        return Ok(ApiResponse.Success());
    }
}
