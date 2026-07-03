using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "SuperAdmin")]
public class UsersController : AdminBaseController
{
    private readonly UserService _service;

    public UsersController(UserService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] int? orgId)
    {
        var result = await _service.GetListAsync(request, orgId);
        // Strip password hashes from response
        foreach (var user in result.Items) user.PasswordHash = "***";
        return Ok(ApiResponse<object>.Success(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await _service.GetByIdAsync(id);
        if (user == null) return NotFound(ApiResponse.Error(40401, "User not found"));
        user.PasswordHash = "***";
        return Ok(ApiResponse<object>.Success(user));
    }

    public record CreateUserRequest(string Username, string Password, string? Email, string? DisplayName, string Role, int OrganizationId);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Role = Enum.Parse<Tensu.Core.Enums.UserRole>(request.Role),
            OrganizationId = request.OrganizationId
        };
        var created = await _service.CreateAsync(user, request.Password);
        created.PasswordHash = "***";
        return Ok(ApiResponse<object>.Success(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] User user)
    {
        var updated = await _service.UpdateAsync(id, user);
        if (updated == null) return NotFound(ApiResponse.Error(40401, "User not found"));
        updated.PasswordHash = "***";
        return Ok(ApiResponse<object>.Success(updated));
    }

    public record ResetPasswordRequest(string NewPassword);

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        var success = await _service.ResetPasswordAsync(id, request.NewPassword);
        if (!success) return NotFound(ApiResponse.Error(40401, "User not found"));
        return Ok(ApiResponse.Success());
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse.Error(40401, "User not found"));
        return Ok(ApiResponse.Success());
    }
}
