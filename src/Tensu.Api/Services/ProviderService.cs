using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

public class ProviderService : BaseService
{
    private readonly TensuDbContext _db;
    private readonly EncryptionService _encryption;

    public ProviderService(TensuDbContext db, EncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    public async Task<PagedResult<Provider>> GetListAsync(PagedRequest request)
    {
        var query = _db.Providers.Include(p => p.Keys).Include(p => p.Models).AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(p => p.Name.Contains(request.Keyword) || (p.Description != null && p.Description.Contains(request.Keyword)));

        query = ApplyPaging(query, request, out var total);
        var items = await query.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<Provider?> GetByIdAsync(int id)
    {
        return await _db.Providers.Include(p => p.Keys).Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Provider> CreateAsync(Provider provider)
    {
        provider.CreatedAt = DateTime.UtcNow;
        provider.UpdatedAt = DateTime.UtcNow;
        _db.Providers.Add(provider);
        await _db.SaveChangesAsync();
        return provider;
    }

    public async Task<Provider?> UpdateAsync(int id, Provider updated)
    {
        var provider = await _db.Providers.FindAsync(id);
        if (provider == null) return null;

        provider.Name = updated.Name;
        provider.Protocol = updated.Protocol;
        provider.BaseUrl = updated.BaseUrl;
        provider.Description = updated.Description;
        provider.IsEnabled = updated.IsEnabled;
        provider.KeyLoadBalanceStrategy = updated.KeyLoadBalanceStrategy;
        provider.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return provider;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var provider = await _db.Providers.Include(p => p.Models).FirstOrDefaultAsync(p => p.Id == id);
        if (provider == null) return false;

        if (provider.Models.Any())
            throw new InvalidOperationException("Cannot delete provider with existing models. Remove models first.");

        _db.Providers.Remove(provider);
        await _db.SaveChangesAsync();
        return true;
    }

    // Provider Keys
    public async Task<ProviderKey> AddKeyAsync(int providerId, ProviderKey key)
    {
        key.ProviderId = providerId;
        key.KeyValue = _encryption.Encrypt(key.KeyValue);
        key.CreatedAt = DateTime.UtcNow;
        key.UpdatedAt = DateTime.UtcNow;
        _db.ProviderKeys.Add(key);
        await _db.SaveChangesAsync();
        return key;
    }

    public async Task<bool> DeleteKeyAsync(int providerId, int keyId)
    {
        var key = await _db.ProviderKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.ProviderId == providerId);
        if (key == null) return false;
        _db.ProviderKeys.Remove(key);
        await _db.SaveChangesAsync();
        return true;
    }

    public string DecryptKey(string encryptedKey) => _encryption.Decrypt(encryptedKey);
}
