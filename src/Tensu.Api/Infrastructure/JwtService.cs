using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Tensu.Core.Enums;

namespace Tensu.Api.Infrastructure;

public class JwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(int userId, string username, string role, int organizationId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"] ?? "TensuDefaultJwtSecretKey32Chars!!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim("org_id", organizationId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiresMinutes = int.TryParse(_configuration["Jwt:ExpiresInMinutes"], out var m) ? m : 480;

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "Tensu",
            audience: _configuration["Jwt:Audience"] ?? "Tensu",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (int userId, string username, string role, int orgId)? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"] ?? "TensuDefaultJwtSecretKey32Chars!!"));

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "Tensu",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "Tensu",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true
            }, out _);

            var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var username = principal.FindFirst(ClaimTypes.Name)!.Value;
            var role = principal.FindFirst(ClaimTypes.Role)!.Value;
            var orgId = int.Parse(principal.FindFirst("org_id")!.Value);

            return (userId, username, role, orgId);
        }
        catch
        {
            return null;
        }
    }
}
