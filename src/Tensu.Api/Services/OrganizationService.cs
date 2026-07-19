using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

public class OrganizationService : BaseService
{
    private readonly TensuDbContext _db;

    public OrganizationService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task<List<object>> GetTreeAsync()
    {
        var orgs = await _db.Organizations.OrderBy(o => o.Path).ToListAsync();
        return orgs.Select(MapToTreeDto).ToList();
    }

    public async Task<object?> GetByIdAsync(int id)
    {
        var org = await _db.Organizations.Include(o => o.Children).FirstOrDefaultAsync(o => o.Id == id);
        return org == null ? null : MapToDetailDto(org);
    }

    private static object MapToTreeDto(Organization org)
    {
        return new
        {
            org.Id,
            org.ParentId,
            org.Name,
            org.Path,
            org.Description,
            org.EnableContentLogging,
            org.CompressionEnabled,
            org.CacheEnabled,
            org.DataRetentionDays,
            org.CreatedAt,
            org.UpdatedAt
        };
    }

    private static object MapToDetailDto(Organization org)
    {
        return new
        {
            org.Id,
            org.ParentId,
            org.Name,
            org.Path,
            org.Description,
            org.EnableContentLogging,
            org.CompressionEnabled,
            org.CacheEnabled,
            org.DataRetentionDays,
            org.CreatedAt,
            org.UpdatedAt,
            Children = org.Children.Select(MapToTreeDto).ToList()
        };
    }

    public async Task<Organization> CreateAsync(Organization org, int? parentId = null)
    {
        org.ParentId = parentId;
        org.CreatedAt = DateTime.UtcNow;
        org.UpdatedAt = DateTime.UtcNow;
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();

        org.Path = parentId.HasValue
            ? $"{(await _db.Organizations.FindAsync(parentId))?.Path}{org.Id}/"
            : $"/{org.Id}/";
        await _db.SaveChangesAsync();
        return org;
    }

    public async Task<Organization?> UpdateAsync(int id, Organization updated)
    {
        var org = await _db.Organizations.FindAsync(id);
        if (org == null) return null;

        org.Name = updated.Name;
        org.Description = updated.Description;
        org.EnableContentLogging = updated.EnableContentLogging;
        org.CompressionEnabled = updated.CompressionEnabled;
        org.CacheEnabled = updated.CacheEnabled;
        org.DataRetentionDays = updated.DataRetentionDays;
        org.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return org;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var org = await _db.Organizations.Include(o => o.Children).Include(o => o.Users).FirstOrDefaultAsync(o => o.Id == id);
        if (org == null) return false;

        if (org.Children.Any() || org.Users.Any())
            throw new InvalidOperationException("Cannot delete organization with children or users.");

        _db.Organizations.Remove(org);
        await _db.SaveChangesAsync();
        return true;
    }
}
