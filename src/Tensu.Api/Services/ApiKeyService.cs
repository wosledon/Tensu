using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class ApiKeyService : BaseService
{
    private readonly TensuDbContext _db;
    private readonly EncryptionService _encryption;

    public ApiKeyService(TensuDbContext db, EncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    public async Task<PagedResult<object>> GetListAsync(PagedRequest request, int? orgId = null)
    {
        var query = _db.ApiKeys.Include(k => k.Organization).Include(k => k.User).AsQueryable();

        if (orgId.HasValue)
            query = query.Where(k => k.OrganizationId == orgId.Value);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(k => k.Name.Contains(request.Keyword) || k.KeyPrefix.Contains(request.Keyword));

        var (pagedQuery, total) = await ApplyPagingAsync(query, request);
        var items = await pagedQuery.ToListAsync();
        return ToPagedResult(items.Select(MapToListDto).ToList(), total, request);
    }

    public async Task<object?> GetByIdAsync(int id)
    {
        var key = await _db.ApiKeys.Include(k => k.Organization).Include(k => k.User)
            .FirstOrDefaultAsync(k => k.Id == id);
        return key == null ? null : MapToDetailDto(key);
    }

    public async Task<ApiKey?> GetEntityByIdAsync(int id)
    {
        return await _db.ApiKeys.FindAsync(id);
    }

    public async Task<bool> IsUserInOrganizationAsync(int userId, int orgId)
    {
        return await _db.Users.AnyAsync(u => u.Id == userId && u.OrganizationId == orgId);
    }

    internal static object MapToListDto(ApiKey k)
    {
        return new
        {
            k.Id,
            k.Name,
            k.KeyPrefix,
            k.ExpiresAt,
            k.AllowedModels,
            k.IpWhitelist,
            k.Status,
            k.RateLimitRpm,
            k.RateLimitTpm,
            k.OrganizationId,
            Organization = k.Organization == null ? null : new
            {
                k.Organization.Id,
                k.Organization.Name,
                k.Organization.Path
            },
            User = k.User == null ? null : new
            {
                k.User.Id,
                k.User.Username,
                k.User.DisplayName
            },
            k.CreatedAt,
            k.UpdatedAt
        };
    }

    public async Task<string?> RevealAsync(int id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return null;
        return _encryption.Decrypt(key.KeyValue);
    }

    private static object MapToDetailDto(ApiKey k)
    {
        return MapToListDto(k);
    }

    public async Task<(ApiKey key, string plainTextKey)> CreateAsync(ApiKey apiKey)
    {
        var plainTextKey = GenerateApiKey();
        apiKey.KeyValue = _encryption.Encrypt(plainTextKey);
        apiKey.KeyPrefix = plainTextKey[..Math.Min(8, plainTextKey.Length)];
        apiKey.CreatedAt = DateTime.UtcNow;
        apiKey.UpdatedAt = DateTime.UtcNow;
        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync();
        return (apiKey, plainTextKey);
    }

    public async Task<ApiKey?> UpdateAsync(int id, ApiKey updated)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return null;

        key.Name = updated.Name;
        key.UserId = updated.UserId;
        key.ExpiresAt = updated.ExpiresAt;
        key.AllowedModels = updated.AllowedModels;
        key.IpWhitelist = updated.IpWhitelist;
        key.Status = updated.Status;
        key.RateLimitRpm = updated.RateLimitRpm;
        key.RateLimitTpm = updated.RateLimitTpm;
        key.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return key;
    }

    public async Task<bool> RevokeAsync(int id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return false;
        key.Status = KeyStatus.Disabled;
        key.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EnableAsync(int id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return false;
        key.Status = KeyStatus.Active;
        key.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return false;
        _db.ApiKeys.Remove(key);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ApiKey?> ValidateKeyAsync(string plainTextKey)
    {
        // Try matching by prefix first for performance
        var prefix = plainTextKey[..Math.Min(8, plainTextKey.Length)];
        var candidates = await _db.ApiKeys
            .Include(k => k.Organization)
            .Where(k => k.KeyPrefix == prefix && k.Status == KeyStatus.Active)
            .ToListAsync();

        foreach (var candidate in candidates)
        {
            var decrypted = _encryption.Decrypt(candidate.KeyValue);
            if (decrypted == plainTextKey)
            {
                // Check expiry
                if (candidate.ExpiresAt.HasValue && candidate.ExpiresAt.Value < DateTime.UtcNow)
                    return null;
                return candidate;
            }
        }
        return null;
    }

    private static string GenerateApiKey()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        return $"tk-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
