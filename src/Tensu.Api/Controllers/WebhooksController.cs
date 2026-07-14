using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/webhooks")]
[Authorize(Policy = "Admin")]
public class WebhooksController : AdminBaseController
{
    private readonly TensuDbContext _db;

    public WebhooksController(TensuDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request)
    {
        var query = _db.WebhookNotifications.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(w => w.Name.Contains(request.Keyword));

        query = query.OrderByDescending(w => w.CreatedAt);
        var total = await query.CountAsync();

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(ApiResponse<object>.Success(new PagedResult<WebhookNotification>
        {
            Items = items, Total = total, Page = page, PageSize = pageSize
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var webhook = await _db.WebhookNotifications.FindAsync(id);
        if (webhook == null) return NotFound(ApiResponse.Error(40401, "Webhook not found"));
        return Ok(ApiResponse<object>.Success(webhook));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WebhookNotification webhook)
    {
        webhook.CreatedAt = DateTime.UtcNow;
        webhook.UpdatedAt = DateTime.UtcNow;
        _db.WebhookNotifications.Add(webhook);
        await _db.SaveChangesAsync();
        await LogAdminAuditAsync("create", "WebhookNotification", webhook.Id.ToString(), $"Name={webhook.Name}, Url={webhook.Url}");
        return Ok(ApiResponse<object>.Success(webhook));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] WebhookNotification updated)
    {
        var webhook = await _db.WebhookNotifications.FindAsync(id);
        if (webhook == null) return NotFound(ApiResponse.Error(40401, "Webhook not found"));

        webhook.Name = updated.Name;
        webhook.Url = updated.Url;
        webhook.Secret = updated.Secret;
        webhook.IsEnabled = updated.IsEnabled;
        webhook.Events = updated.Events;
        webhook.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogAdminAuditAsync("update", "WebhookNotification", id.ToString(), $"Name={updated.Name}");
        return Ok(ApiResponse<object>.Success(webhook));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var webhook = await _db.WebhookNotifications.FindAsync(id);
        if (webhook == null) return NotFound(ApiResponse.Error(40401, "Webhook not found"));
        _db.WebhookNotifications.Remove(webhook);
        await _db.SaveChangesAsync();
        await LogAdminAuditAsync("delete", "WebhookNotification", id.ToString());
        return Ok(ApiResponse.Success());
    }
}
