using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        var created = await _service.CreateAsync(model);
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Model model)
    {
        var updated = await _service.UpdateAsync(id, model);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "Model not found"));
        return Ok(ApiResponse<object>.Success(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "Model not found"));
        return Ok(ApiResponse.Success());
    }

    [HttpPost("{id}/pricings")]
    public async Task<IActionResult> AddPricing(int id, [FromBody] ModelPricing pricing)
    {
        var created = await _service.AddPricingAsync(id, pricing);
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpGet("{id}/pricings/current")]
    public async Task<IActionResult> GetCurrentPricing(int id)
    {
        var pricing = await _service.GetCurrentPricingAsync(id);
        return Ok(ApiResponse<object>.Success(pricing));
    }

    [HttpGet("all-enabled")]
    public async Task<IActionResult> GetAllEnabled()
    {
        var models = await _service.GetAllEnabledAsync();
        return Ok(ApiResponse<object>.Success(models));
    }
}
