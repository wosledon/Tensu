using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Tensu.Core.Common;

namespace Tensu.Api.Controllers;

[ApiController]
public abstract class AdminBaseController : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    protected string CurrentUsername => User.FindFirstValue(ClaimTypes.Name) ?? "";
    protected string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "";
    protected int CurrentOrgId => int.Parse(User.FindFirstValue("org_id") ?? "0");
}
