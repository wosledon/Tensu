using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/admin-audit")]
[Authorize(Policy = "SuperAdmin")]
public class AdminAuditController : AdminBaseController
{
    private readonly AdminAuditService _service;

    public AdminAuditController(AdminAuditService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] string? action, [FromQuery] string? entityType)
    {
        var result = await _service.GetListAsync(request, action, entityType);
        return Ok(ApiResponse<object>.Success(result));
    }
}
