using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class UserService : BaseService
{
    private readonly TensuDbContext _db;

    public UserService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<User>> GetListAsync(PagedRequest request, int? orgId = null)
    {
        var query = _db.Users.Include(u => u.Organization).AsQueryable();

        if (orgId.HasValue)
            query = query.Where(u => u.OrganizationId == orgId.Value);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(u => u.Username.Contains(request.Keyword) || (u.Email != null && u.Email.Contains(request.Keyword)));

        query = ApplyPaging(query, request, out var total);
        var items = await query.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _db.Users.Include(u => u.Organization).FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User> CreateAsync(User user, string password)
    {
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<User?> UpdateAsync(int id, User updated)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return null;

        user.Email = updated.Email;
        user.DisplayName = updated.DisplayName;
        user.Role = updated.Role;
        user.IsActive = updated.IsActive;
        user.OrganizationId = updated.OrganizationId;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return false;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }
}
