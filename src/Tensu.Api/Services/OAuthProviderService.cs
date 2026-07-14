using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

public class OAuthProviderService : BaseService
{
    private readonly TensuDbContext _db;
    private readonly EncryptionService _encryption;

    public OAuthProviderService(TensuDbContext db, EncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    public async Task<PagedResult<OAuthProviderDto>> GetListAsync(PagedRequest request)
    {
        var query = _db.OAuthProviders.AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(p => p.Name.Contains(request.Keyword)
                || (p.DisplayName != null && p.DisplayName.Contains(request.Keyword)));

        var (pagedQuery, total) = await ApplyPagingAsync(query, request);
        var items = await pagedQuery.ToListAsync();
        return ToPagedResult(items.Select(ToDto).ToList(), total, request);
    }

    public async Task<OAuthProviderDto?> GetByIdAsync(int id)
    {
        var provider = await _db.OAuthProviders.FindAsync(id);
        return provider == null ? null : ToDto(provider);
    }

    public async Task<OAuthProviderDto> CreateAsync(SaveOAuthProviderRequest request)
    {
        var entity = ToEntity(request);
        entity.ClientSecret = _encryption.Encrypt(entity.ClientSecret);
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _db.OAuthProviders.Add(entity);
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<OAuthProviderDto?> UpdateAsync(int id, SaveOAuthProviderRequest request)
    {
        var entity = await _db.OAuthProviders.FindAsync(id);
        if (entity == null) return null;

        entity.Name = request.Name;
        entity.DisplayName = request.DisplayName;
        entity.Protocol = request.Protocol;
        entity.ClientId = request.ClientId;
        if (!string.IsNullOrEmpty(request.ClientSecret))
            entity.ClientSecret = _encryption.Encrypt(request.ClientSecret);
        entity.AuthorizationEndpoint = request.AuthorizationEndpoint;
        entity.TokenEndpoint = request.TokenEndpoint;
        entity.UserInfoEndpoint = request.UserInfoEndpoint;
        entity.Issuer = request.Issuer;
        entity.Scope = request.Scope;
        entity.IsEnabled = request.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.OAuthProviders.FindAsync(id);
        if (entity == null) return false;
        _db.OAuthProviders.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<OAuthProvider?> GetEnabledByNameAsync(string name)
    {
        return await _db.OAuthProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == name && p.IsEnabled);
    }

    public async Task<IReadOnlyList<OAuthProviderDto>> GetEnabledAsync()
    {
        var items = await _db.OAuthProviders
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public string DecryptSecret(string encryptedSecret) => _encryption.Decrypt(encryptedSecret);

    private static string MaskSecret(string? secret) => string.IsNullOrEmpty(secret) ? string.Empty : "***";

    private OAuthProviderDto ToDto(OAuthProvider p) => new(
        p.Id,
        p.Name,
        p.DisplayName,
        p.Protocol,
        p.ClientId,
        MaskSecret(p.ClientSecret),
        p.AuthorizationEndpoint,
        p.TokenEndpoint,
        p.UserInfoEndpoint,
        p.Issuer,
        p.Scope,
        p.IsEnabled,
        p.CreatedAt,
        p.UpdatedAt);

    private OAuthProvider ToEntity(SaveOAuthProviderRequest r) => new()
    {
        Name = r.Name,
        DisplayName = r.DisplayName,
        Protocol = r.Protocol,
        ClientId = r.ClientId,
        ClientSecret = r.ClientSecret,
        AuthorizationEndpoint = r.AuthorizationEndpoint,
        TokenEndpoint = r.TokenEndpoint,
        UserInfoEndpoint = r.UserInfoEndpoint,
        Issuer = r.Issuer,
        Scope = r.Scope,
        IsEnabled = r.IsEnabled
    };
}

public record OAuthProviderDto(
    int Id,
    string Name,
    string? DisplayName,
    string Protocol,
    string ClientId,
    string ClientSecret,
    string? AuthorizationEndpoint,
    string? TokenEndpoint,
    string? UserInfoEndpoint,
    string? Issuer,
    string Scope,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SaveOAuthProviderRequest(
    string Name,
    string? DisplayName,
    string Protocol,
    string ClientId,
    string ClientSecret,
    string? AuthorizationEndpoint,
    string? TokenEndpoint,
    string? UserInfoEndpoint,
    string? Issuer,
    string Scope,
    bool IsEnabled);
