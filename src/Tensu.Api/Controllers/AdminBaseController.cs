using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Enums;

namespace Tensu.Api.Controllers;

[ApiController]
public abstract class AdminBaseController : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    protected string CurrentUsername => User.FindFirstValue(ClaimTypes.Name) ?? "";
    protected string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "";
    protected int CurrentOrgId => int.Parse(User.FindFirstValue("org_id") ?? "0");

    protected bool IsSuperAdmin => CurrentRole == UserRole.SuperAdmin.ToString();
    protected bool IsAdminOrSuper => IsSuperAdmin || CurrentRole == UserRole.Admin.ToString();

    protected UserRole? CurrentUserRole
    {
        get
        {
            if (Enum.TryParse<UserRole>(CurrentRole, out var role)) return role;
            return null;
        }
    }

    protected async Task LogAdminAuditAsync(string action, string entityType, string? entityId = null, string? details = null)
    {
        var audit = HttpContext?.RequestServices.GetRequiredService<AdminAuditService>();
        var ipAddress = HttpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext?.Request.Headers["User-Agent"].ToString();
        await audit.LogAsync(CurrentUserId, CurrentUsername, action, entityType, entityId, details, ipAddress, userAgent);
    }
}
