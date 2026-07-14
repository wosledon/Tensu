using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

public class AdminAuditService
{
    private readonly TensuDbContext _db;

    public AdminAuditService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(int userId, string username, string action, string entityType, string? entityId = null, string? details = null, string? ipAddress = null, string? userAgent = null)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            UserId = userId,
            Username = username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task<PagedResult<AdminAuditLog>> GetListAsync(PagedRequest request, string? action = null, string? entityType = null)
    {
        var query = _db.AdminAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action == action);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(l => l.EntityType == entityType);
        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(l => l.Details != null && l.Details.Contains(request.Keyword) || l.Username.Contains(request.Keyword));

        query = query.OrderByDescending(l => l.Timestamp);
        var total = await query.CountAsync();

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<AdminAuditLog>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
