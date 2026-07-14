using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/alert-rules")]
[Authorize(Policy = "Admin")]
public class AlertRulesController : AdminBaseController
{
    private readonly TensuDbContext _db;

    public AlertRulesController(TensuDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request)
    {
        var query = _db.AlertRules.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(r => r.Name.Contains(request.Keyword) || r.EventType.Contains(request.Keyword));

        query = query.OrderByDescending(r => r.CreatedAt);
        var total = await query.CountAsync();

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(ApiResponse<object>.Success(new PagedResult<AlertRule>
        {
            Items = items, Total = total, Page = page, PageSize = pageSize
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var rule = await _db.AlertRules.FindAsync(id);
        if (rule == null) return NotFound(ApiResponse.Error(40401, "Alert rule not found"));
        return Ok(ApiResponse<object>.Success(rule));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AlertRule rule)
    {
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;
        _db.AlertRules.Add(rule);
        await _db.SaveChangesAsync();
        await LogAdminAuditAsync("create", "AlertRule", rule.Id.ToString(), $"Name={rule.Name}");
        return Ok(ApiResponse<object>.Success(rule));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AlertRule updated)
    {
        var rule = await _db.AlertRules.FindAsync(id);
        if (rule == null) return NotFound(ApiResponse.Error(40401, "Alert rule not found"));

        rule.Name = updated.Name;
        rule.EventType = updated.EventType;
        rule.Severity = updated.Severity;
        rule.IsEnabled = updated.IsEnabled;
        rule.WebhookIds = updated.WebhookIds;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await LogAdminAuditAsync("update", "AlertRule", id.ToString(), $"Name={updated.Name}");
        return Ok(ApiResponse<object>.Success(rule));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await _db.AlertRules.FindAsync(id);
        if (rule == null) return NotFound(ApiResponse.Error(40401, "Alert rule not found"));
        _db.AlertRules.Remove(rule);
        await _db.SaveChangesAsync();
        await LogAdminAuditAsync("delete", "AlertRule", id.ToString());
        return Ok(ApiResponse.Success());
    }
}
