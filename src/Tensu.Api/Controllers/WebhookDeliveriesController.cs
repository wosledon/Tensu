using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/webhook-deliveries")]
[Authorize(Policy = "Admin")]
public class WebhookDeliveriesController : AdminBaseController
{
    private readonly TensuDbContext _db;

    public WebhookDeliveriesController(TensuDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] int? webhookId, [FromQuery] bool? success)
    {
        var query = _db.WebhookDeliveries.AsNoTracking().AsQueryable();

        if (webhookId.HasValue)
            query = query.Where(d => d.WebhookNotificationId == webhookId.Value);

        if (success.HasValue)
            query = query.Where(d => d.IsSuccess == success.Value);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(d => d.EventType.Contains(request.Keyword) || (d.LastErrorMessage != null && d.LastErrorMessage.Contains(request.Keyword)));

        query = query.OrderByDescending(d => d.CreatedAt);
        var total = await query.CountAsync();

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(ApiResponse<object>.Success(new PagedResult<WebhookDelivery>
        {
            Items = items, Total = total, Page = page, PageSize = pageSize
        }));
    }
}
