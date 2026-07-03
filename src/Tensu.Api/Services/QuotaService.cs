using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

/// <summary>
/// Manages organization and API key token quotas and concurrent request limits.
/// </summary>
public class QuotaService : BaseService
{
    private readonly TensuDbContext _db;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger<QuotaService> _logger;

    public QuotaService(TensuDbContext db, RateLimiter rateLimiter, ILogger<QuotaService> logger)
    {
        _db = db;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<PagedResult<Quota>> GetListAsync(PagedRequest request, string? scope = null, int? orgId = null)
    {
        var query = _db.Quotas
            .Include(q => q.Organization)
            .Include(q => q.ApiKey)
            .AsQueryable();

        if (orgId.HasValue)
            query = query.Where(q => q.OrganizationId == orgId.Value);

        if (!string.IsNullOrEmpty(scope))
        {
            var lower = scope.ToLowerInvariant();
            if (lower == "org")
                query = query.Where(q => q.ApiKeyId == null);
            else if (lower == "key")
                query = query.Where(q => q.ApiKeyId != null);
        }

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(q => q.Organization.Name.Contains(request.Keyword));

        query = ApplyPaging(query, request, out var total);
        var items = await query.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<Quota?> GetByIdAsync(int id)
    {
        return await _db.Quotas
            .Include(q => q.Organization)
            .Include(q => q.ApiKey)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<Quota> CreateAsync(Quota quota)
    {
        quota.CreatedAt = DateTime.UtcNow;
        quota.UpdatedAt = DateTime.UtcNow;
        _db.Quotas.Add(quota);
        await _db.SaveChangesAsync();
        return quota;
    }

    public async Task<Quota?> UpdateAsync(int id, Quota updated)
    {
        var quota = await _db.Quotas.FindAsync(id);
        if (quota == null) return null;

        quota.OrganizationId = updated.OrganizationId;
        quota.ApiKeyId = updated.ApiKeyId;
        quota.Rpm = updated.Rpm;
        quota.Tpm = updated.Tpm;
        quota.ConcurrentRequestLimit = updated.ConcurrentRequestLimit;
        quota.DailyTokenLimit = updated.DailyTokenLimit;
        quota.MonthlyTokenLimit = updated.MonthlyTokenLimit;
        quota.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return quota;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var quota = await _db.Quotas.FindAsync(id);
        if (quota == null) return false;
        _db.Quotas.Remove(quota);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks whether the organization is within its daily and monthly token quota.
    /// Returns (allowed, retryAfterSeconds).
    /// </summary>
    public async Task<(bool allowed, int retryAfterSeconds)> CheckOrgQuotaAsync(int orgId, int estimatedTokens)
    {
        var quota = await GetOrgQuotaAsync(orgId);
        if (quota == null) return (true, 0);

        var today = DateTime.UtcNow.Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (quota.DailyTokenLimit.HasValue && quota.DailyTokenLimit.Value > 0)
        {
            var dayScope = GetOrgScope(orgId, "day", today.ToString("yyyy-MM-dd"));
            var used = await GetCounterValueAsync(dayScope);
            if (used + estimatedTokens > quota.DailyTokenLimit.Value)
            {
                var retryAfter = (int)(today.AddDays(1) - DateTime.UtcNow).TotalSeconds;
                _logger.LogWarning("Org {OrgId} daily token quota exceeded: {Used}+{Estimated}/{Limit}", orgId, used, estimatedTokens, quota.DailyTokenLimit.Value);
                return (false, retryAfter);
            }
        }

        if (quota.MonthlyTokenLimit.HasValue && quota.MonthlyTokenLimit.Value > 0)
        {
            var monthScope = GetOrgScope(orgId, "month", thisMonth.ToString("yyyy-MM"));
            var used = await GetCounterValueAsync(monthScope);
            if (used + estimatedTokens > quota.MonthlyTokenLimit.Value)
            {
                var retryAfter = (int)(thisMonth.AddMonths(1) - DateTime.UtcNow).TotalSeconds + 1;
                _logger.LogWarning("Org {OrgId} monthly token quota exceeded: {Used}+{Estimated}/{Limit}", orgId, used, estimatedTokens, quota.MonthlyTokenLimit.Value);
                return (false, retryAfter);
            }
        }

        return (true, 0);
    }

    /// <summary>
    /// Records organization token usage for the current day and month.
    /// </summary>
    public async Task RecordOrgUsageAsync(int orgId, int tokens)
    {
        if (tokens <= 0) return;

        var today = DateTime.UtcNow.Date;
        var dayScope = GetOrgScope(orgId, "day", today.ToString("yyyy-MM-dd"));
        await _rateLimiter.IncrementAsync(dayScope, tokens);

        var thisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthScope = GetOrgScope(orgId, "month", thisMonth.ToString("yyyy-MM"));
        await _rateLimiter.IncrementAsync(monthScope, tokens);
    }

    /// <summary>
    /// Returns the most specific concurrent request limit for the organization or API key.
    /// </summary>
    public async Task<int?> GetConcurrentRequestLimitAsync(int orgId, int? apiKeyId = null)
    {
        if (apiKeyId.HasValue)
        {
            var keyQuota = await _db.Quotas
                .Where(q => q.OrganizationId == orgId && q.ApiKeyId == apiKeyId.Value && q.ConcurrentRequestLimit.HasValue)
                .OrderByDescending(q => q.Id)
                .FirstOrDefaultAsync();
            if (keyQuota != null) return keyQuota.ConcurrentRequestLimit;
        }

        var orgQuota = await _db.Quotas
            .Where(q => q.OrganizationId == orgId && q.ApiKeyId == null && q.ConcurrentRequestLimit.HasValue)
            .OrderByDescending(q => q.Id)
            .FirstOrDefaultAsync();
        return orgQuota?.ConcurrentRequestLimit;
    }

    private async Task<Quota?> GetOrgQuotaAsync(int orgId)
    {
        return await _db.Quotas
            .Where(q => q.OrganizationId == orgId && q.ApiKeyId == null)
            .OrderByDescending(q => q.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<long> GetCounterValueAsync(string scope)
    {
        return await _db.RateLimitCounters
                   .Where(c => c.Scope == scope)
                   .SumAsync(c => (long?)c.Value) ?? 0;
    }

    private static string GetOrgScope(int orgId, string bucketType, string bucketValue) =>
        $"org:{orgId}:{bucketType}:{bucketValue}";
}
