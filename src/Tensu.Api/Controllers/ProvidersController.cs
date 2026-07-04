using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/providers")]
[Authorize(Policy = "Admin")]
public class ProvidersController : AdminBaseController
{
    private readonly ProviderService _service;
    private readonly KeyRotationService _keyRotationService;

    public ProvidersController(ProviderService service, KeyRotationService keyRotationService)
    {
        _service = service;
        _keyRotationService = keyRotationService;
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
        return Ok(ApiResponse<object>.Success(_service.MapToDetailDto(provider)));
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

    public record AddKeyRequest(string Name, string KeyValue, int Weight = 1, int? RateLimitRpm = null, int? RateLimitTpm = null);

    [HttpPost("{id}/keys")]
    public async Task<IActionResult> AddKey(int id, [FromBody] AddKeyRequest request)
    {
        var provider = await _service.GetByIdAsync(id);
        if (provider == null) return NotFound(ApiResponse.Error(40401, "Provider not found"));

        var key = new ProviderKey
        {
            ProviderId = id,
            Name = request.Name,
            KeyValue = request.KeyValue,
            Weight = request.Weight,
            RateLimitRpm = request.RateLimitRpm,
            RateLimitTpm = request.RateLimitTpm,
            Status = KeyStatus.Active
        };

        var created = await _service.AddKeyAsync(id, key);
        // Don't return the encrypted key value and break object cycles
        var response = new
        {
            created.Id,
            created.ProviderId,
            created.Name,
            created.Weight,
            created.Status,
            created.RateLimitRpm,
            created.RateLimitTpm,
            created.CreatedAt,
            created.UpdatedAt,
            created.LastHealthCheckAt,
            KeyValue = "***"
        };
        return Ok(ApiResponse<object>.Success(response));
    }

    public record UpdateKeyRequest(string Name, string? Status = null, int? Weight = null, int? RateLimitRpm = null, int? RateLimitTpm = null);

    [HttpPut("{providerId}/keys/{keyId}")]
    public async Task<IActionResult> UpdateKey(int providerId, int keyId, [FromBody] UpdateKeyRequest request)
    {
        var provider = await _service.GetByIdAsync(providerId);
        if (provider == null) return NotFound(ApiResponse.Error(40401, "Provider not found"));

        var updated = await _service.UpdateKeyFieldsAsync(providerId, keyId, request.Name, request.Status, request.Weight, request.RateLimitRpm, request.RateLimitTpm);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "Key not found"));

        var response = new
        {
            updated.Id,
            updated.ProviderId,
            updated.Name,
            updated.Weight,
            updated.Status,
            updated.RateLimitRpm,
            updated.RateLimitTpm,
            updated.CreatedAt,
            updated.UpdatedAt,
            updated.LastHealthCheckAt,
            KeyValue = "***"
        };
        return Ok(ApiResponse<object>.Success(response));
    }

    [HttpDelete("{providerId}/keys/{keyId}")]
    public async Task<IActionResult> DeleteKey(int providerId, int keyId)
    {
        var deleted = await _service.DeleteKeyAsync(providerId, keyId);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Key not found"));
        return Ok(ApiResponse.Success());
    }

    [HttpPost("{providerId}/keys/{keyId}/rotate")]
    public async Task<IActionResult> RotateKey(int providerId, int keyId)
    {
        try
        {
            var newKey = await _keyRotationService.RotateProviderKeyAsync(providerId, keyId);
            var response = new
            {
                newKey.Id,
                newKey.ProviderId,
                newKey.Name,
                newKey.Weight,
                newKey.Status,
                newKey.RateLimitRpm,
                newKey.RateLimitTpm,
                newKey.CreatedAt,
                newKey.UpdatedAt,
                newKey.LastHealthCheckAt,
                KeyValue = newKey.KeyValue
            };
            return Ok(ApiResponse<object>.Success(response));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse.Error(40401, ex.Message));
        }
    }

    public record BatchIdsRequest(int[] Ids);

    [HttpDelete("batch")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchIdsRequest request)
    {
        var ids = request.Ids ?? Array.Empty<int>();
        if (ids.Length == 0) return BadRequest(ApiResponse.Error(40001, "No ids provided"));
        var results = new List<object>();
        foreach (var id in ids.Distinct())
        {
            try
            {
                var deleted = await _service.DeleteAsync(id);
                results.Add(new { id, success = deleted });
            }
            catch (Exception ex)
            {
                results.Add(new { id, success = false, error = ex.Message });
            }
        }
        return Ok(ApiResponse<object>.Success(results));
    }

    [HttpPost("batch/enable")]
    public async Task<IActionResult> BatchEnable([FromBody] int[] ids)
    {
        if (ids == null || ids.Length == 0) return BadRequest(ApiResponse.Error(40001, "No ids provided"));
        var results = new List<object>();
        foreach (var id in ids.Distinct())
        {
            try
            {
                var provider = await _service.GetByIdAsync(id);
                if (provider == null) { results.Add(new { id, success = false, error = "Not found" }); continue; }
                provider.IsEnabled = true;
                await _service.UpdateAsync(id, provider);
                results.Add(new { id, success = true });
            }
            catch (Exception ex)
            {
                results.Add(new { id, success = false, error = ex.Message });
            }
        }
        return Ok(ApiResponse<object>.Success(results));
    }

    [HttpPost("batch/disable")]
    public async Task<IActionResult> BatchDisable([FromBody] int[] ids)
    {
        if (ids == null || ids.Length == 0) return BadRequest(ApiResponse.Error(40001, "No ids provided"));
        var results = new List<object>();
        foreach (var id in ids.Distinct())
        {
            try
            {
                var provider = await _service.GetByIdAsync(id);
                if (provider == null) { results.Add(new { id, success = false, error = "Not found" }); continue; }
                provider.IsEnabled = false;
                await _service.UpdateAsync(id, provider);
                results.Add(new { id, success = true });
            }
            catch (Exception ex)
            {
                results.Add(new { id, success = false, error = ex.Message });
            }
        }
        return Ok(ApiResponse<object>.Success(results));
    }
}
