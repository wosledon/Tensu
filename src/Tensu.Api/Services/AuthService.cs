using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class AuthService
{
    private readonly TensuDbContext _db;
    private readonly JwtService _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(TensuDbContext db, JwtService jwt, ILogger<AuthService> logger)
    {
        _db = db;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<(bool success, string? token, string? error)> LoginAsync(string username, string password)
    {
        var user = await _db.Users.Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, null, "Invalid username or password");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user.Id, user.Username, user.Role.ToString(), user.OrganizationId);
        return (true, token, null);
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _db.Users.Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<(bool success, string? error)> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found");

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
            return (false, "Invalid old password");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
