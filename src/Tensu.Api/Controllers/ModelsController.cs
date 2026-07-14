using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/models")]
[Authorize(Policy = "Admin")]
public class ModelsController : AdminBaseController
{
    private readonly ModelService _service;

    public ModelsController(ModelService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] int? providerId)
    {
        var result = await _service.GetListAsync(request, providerId);
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var model = await _service.GetByIdAsync(id);
        if (model == null) return NotFound(ApiResponse.Error(40401, "Model not found"));
        return Ok(ApiResponse<object>.Success(model));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Model model)
    {
        var created = await _service.CreateAsync(model) as Model;
        if (created == null) return BadRequest(ApiResponse.Error(40001, "Failed to create model"));
        await LogAdminAuditAsync("create", "Model", created.Id.ToString(), $"Name={created.Name}");
        return Ok(ApiResponse<object>.Success(created));
    }

    public record UpdateModelRequest(string Name, string? DisplayName = null, string? Description = null, bool SupportsVision = false, bool SupportsReasoning = false, bool SupportsToolUse = false, bool SupportsThinking = false, string? ThinkingStrengths = null, int InputContextSize = 0, int OutputContextSize = 0, bool IsEnabled = true, bool CompressionEnabled = true);

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateModelRequest request)
    {
        var model = await _service.GetByIdAsync(id);
        if (model == null) return NotFound(ApiResponse.Error(40401, "Model not found"));

        var updated = await _service.UpdateAsync(id, new Model
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            SupportsVision = request.SupportsVision,
            SupportsReasoning = request.SupportsReasoning,
            SupportsToolUse = request.SupportsToolUse,
            SupportsThinking = request.SupportsThinking,
            ThinkingStrengths = request.ThinkingStrengths,
            InputContextSize = request.InputContextSize,
            OutputContextSize = request.OutputContextSize,
            IsEnabled = request.IsEnabled,
            CompressionEnabled = request.CompressionEnabled,
        });
        if (updated == null) return NotFound(ApiResponse.Error(40401, "Model not found"));
        await LogAdminAuditAsync("update", "Model", id.ToString(), $"Name={request.Name}");
        return Ok(ApiResponse<object>.Success(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Model not found"));
        await LogAdminAuditAsync("delete", "Model", id.ToString());
        return Ok(ApiResponse.Success());
    }

    [HttpPost("sync/{providerId}")]
    public async Task<IActionResult> Sync(int providerId)
    {
        try
        {
            var result = await _service.SyncModelsFromProviderAsync(providerId);
            await LogAdminAuditAsync("sync", "Model", providerId.ToString(), $"added={result.Added}, existing={result.Existing}");
            return Ok(ApiResponse<object>.Success(new { added = result.Added, existing = result.Existing, total = result.Total }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Error(40001, ex.Message));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ApiResponse.Error(40002, ex.Message));
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(ApiResponse.Error(40003, ex.Message));
        }
    }

    [HttpPost("{modelId}/pricing")]
    public async Task<IActionResult> AddPricing(int modelId, [FromBody] ModelPricing pricing)
    {
        var created = await _service.AddPricingAsync(modelId, pricing);
        await LogAdminAuditAsync("add_pricing", "ModelPricing", created.Id.ToString(), $"ModelId={modelId}");
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpGet("{modelId}/pricing")]
    public async Task<IActionResult> GetPricingHistory(int modelId)
    {
        var history = await _service.GetPricingHistoryAsync(modelId);
        return Ok(ApiResponse<object>.Success(history));
    }

    [HttpGet("{id}/pricings/current")]
    public async Task<IActionResult> GetCurrentPricing(int id)
    {
        var pricing = await _service.GetCurrentPricingAsync(id);
        if (pricing == null) return NotFound(ApiResponse.Error(40401, "No active pricing found"));
        return Ok(ApiResponse<object>.Success(pricing));
    }

    [HttpGet("all-enabled")]
    public async Task<IActionResult> GetAllEnabled()
    {
        var models = await _service.GetAllEnabledAsync();
        return Ok(ApiResponse<object>.Success(models));
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
                if (deleted) await LogAdminAuditAsync("batch_delete", "Model", id.ToString());
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
                var modelObj = await _service.GetByIdAsync(id);
                if (modelObj is not Model model) { results.Add(new { id, success = false, error = "Not found" }); continue; }
                model.IsEnabled = true;
                await _service.UpdateAsync(id, model);
                await LogAdminAuditAsync("batch_enable", "Model", id.ToString());
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
                var modelObj = await _service.GetByIdAsync(id);
                if (modelObj is not Model model) { results.Add(new { id, success = false, error = "Not found" }); continue; }
                model.IsEnabled = false;
                await _service.UpdateAsync(id, model);
                await LogAdminAuditAsync("batch_disable", "Model", id.ToString());
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
