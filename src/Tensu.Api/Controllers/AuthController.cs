using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tensu.Api.Services;
using Tensu.Core.Common;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly OAuthService _oauthService;
    private readonly IConfiguration _configuration;

    public AuthController(AuthService authService, OAuthService oauthService, IConfiguration configuration)
    {
        _authService = authService;
        _oauthService = oauthService;
        _configuration = configuration;
    }

    public record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (success, token, error) = await _authService.LoginAsync(request.Username, request.Password);
        if (!success)
            return Unauthorized(ApiResponse.Error(40101, error!));

        return Ok(ApiResponse<object>.Success(new { token }));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null) return NotFound(ApiResponse.Error(40401, "User not found"));

        return Ok(ApiResponse<object>.Success(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.PictureUrl,
            Role = user.Role.ToString(),
            user.OrganizationId,
            Organization = user.Organization?.Name
        }));
    }

    public record ChangePasswordRequest(string OldPassword, string NewPassword);

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var (success, error) = await _authService.ChangePasswordAsync(userId, request.OldPassword, request.NewPassword);
        if (!success) return BadRequest(ApiResponse.Error(40001, error!));
        return Ok(ApiResponse.Success());
    }

    [AllowAnonymous]
    [HttpGet("oauth/providers")]
    public async Task<IActionResult> GetOAuthProviders()
    {
        var providers = await _oauthService.GetLoginProvidersAsync();
        return Ok(ApiResponse<object>.Success(providers));
    }

    [AllowAnonymous]
    [HttpGet("oauth/{providerName}")]
    public async Task<IActionResult> OAuthChallenge(string providerName)
    {
        var frontendBaseUrl = _configuration["OAuth:FrontendBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var redirectUriBase = frontendBaseUrl.TrimEnd('/');

        var (success, url, error) = await _oauthService.ChallengeAsync(providerName, redirectUriBase);
        if (!success)
            return BadRequest(ApiResponse.Error(40001, error!));

        return Redirect(url!);
    }

    [AllowAnonymous]
    [HttpGet("oauth/callback")]
    public async Task<IActionResult> OAuthCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        var frontendBaseUrl = _configuration["OAuth:FrontendBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var redirectUriBase = frontendBaseUrl.TrimEnd('/');

        var (success, token, err) = await _oauthService.CallbackAsync(code, state, error, redirectUriBase);
        if (!success)
        {
            var errorUrl = $"{redirectUriBase}/oauth/callback?error={Uri.EscapeDataString(err ?? "OAuth callback failed")}";
            return Redirect(errorUrl);
        }

        var successUrl = $"{redirectUriBase}/oauth/callback?token={Uri.EscapeDataString(token!)}";
        return Redirect(successUrl);
    }
}
