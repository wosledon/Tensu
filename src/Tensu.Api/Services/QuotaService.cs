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
            .Include(q => q.Model!)
            .ThenInclude(m => m.Provider)
            .AsQueryable();

        if (orgId.HasValue)
            query = query.Where(q => q.OrganizationId == orgId.Value);

        if (!string.IsNullOrEmpty(scope))
        {
            var lower = scope.ToLowerInvariant();
            if (lower == "org")
                query = query.Where(q => q.ApiKeyId == null && q.ModelId == null);
            else if (lower == "key")
                query = query.Where(q => q.ApiKeyId != null);
            else if (lower == "model")
                query = query.Where(q => q.ModelId != null);
        }

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(q => q.Organization != null && q.Organization.Name.Contains(request.Keyword));

        var (pagedQuery, total) = await ApplyPagingAsync(query, request);
        var items = await pagedQuery.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<Quota?> GetByIdAsync(int id)
    {
        return await _db.Quotas
            .Include(q => q.Organization)
            .Include(q => q.ApiKey)
            .Include(q => q.Model!)
            .ThenInclude(m => m.Provider)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<Quota> CreateAsync(Quota quota)
    {
        NormalizeScope(quota);
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

        NormalizeScope(updated);
        quota.OrganizationId = updated.OrganizationId;
        quota.ApiKeyId = updated.ApiKeyId;
        quota.ModelId = updated.ModelId;
        quota.Rpm = updated.Rpm;
        quota.Tpm = updated.Tpm;
        quota.ConcurrentRequestLimit = updated.ConcurrentRequestLimit;
        quota.DailyTokenLimit = updated.DailyTokenLimit;
        quota.MonthlyTokenLimit = updated.MonthlyTokenLimit;
        quota.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return quota;
    }

    /// <summary>
    /// Normalizes FK fields according to the requested scope so exactly one scope is active:
    /// model scope keeps only ModelId, key scope keeps only ApiKeyId, org scope clears both.
    /// </summary>
    private static void NormalizeScope(Quota quota)
    {
        switch (quota.Scope?.ToLowerInvariant())
        {
            case "model":
                quota.ApiKeyId = null;
                break;
            case "key":
                quota.ModelId = null;
                break;
            case "org":
                quota.ApiKeyId = null;
                quota.ModelId = null;
                break;
            default:
                // No explicit scope: derive from populated FKs (model wins over key).
                if (quota.ModelId.HasValue)
                    quota.ApiKeyId = null;
                break;
        }
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
    public async Task<(bool allowed, int retryAfterSeconds)> CheckOrgQuotaAsync(int orgId, int estimatedTokens, int? apiKeyId = null)
    {
        var quota = await GetOrgQuotaAsync(orgId);
        var keyQuota = apiKeyId.HasValue ? await GetApiKeyQuotaAsync(apiKeyId.Value) : null;
        var effectiveQuota = keyQuota ?? quota;
        if (effectiveQuota == null) return (true, 0);

        var today = DateTime.UtcNow.Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (effectiveQuota.DailyTokenLimit.HasValue && effectiveQuota.DailyTokenLimit.Value > 0)
        {
            var scope = apiKeyId.HasValue
                ? GetKeyScope(apiKeyId.Value, "day", today.ToString("yyyy-MM-dd"))
                : GetOrgScope(orgId, "day", today.ToString("yyyy-MM-dd"));
            var used = await GetCounterValueAsync(scope);
            if (used + estimatedTokens > effectiveQuota.DailyTokenLimit.Value)
            {
                var retryAfter = (int)(today.AddDays(1) - DateTime.UtcNow).TotalSeconds;
                _logger.LogWarning("Quota exceeded for {Scope}: {Used}+{Estimated}/{Limit}", scope, used, estimatedTokens, effectiveQuota.DailyTokenLimit.Value);
                return (false, retryAfter);
            }
        }

        if (effectiveQuota.MonthlyTokenLimit.HasValue && effectiveQuota.MonthlyTokenLimit.Value > 0)
        {
            var scope = apiKeyId.HasValue
                ? GetKeyScope(apiKeyId.Value, "month", thisMonth.ToString("yyyy-MM"))
                : GetOrgScope(orgId, "month", thisMonth.ToString("yyyy-MM"));
            var used = await GetCounterValueAsync(scope);
            if (used + estimatedTokens > effectiveQuota.MonthlyTokenLimit.Value)
            {
                var retryAfter = (int)(thisMonth.AddMonths(1) - DateTime.UtcNow).TotalSeconds + 1;
                _logger.LogWarning("Quota exceeded for {Scope}: {Used}+{Estimated}/{Limit}", scope, used, estimatedTokens, effectiveQuota.MonthlyTokenLimit.Value);
                return (false, retryAfter);
            }
        }

        return (true, 0);
    }

    /// <summary>
    /// Records organization token usage for the current day and month.
    /// </summary>
    public async Task RecordOrgUsageAsync(int orgId, int tokens, int? apiKeyId = null)
    {
        if (tokens <= 0) return;

        var today = DateTime.UtcNow.Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dayOrgScope = GetOrgScope(orgId, "day", today.ToString("yyyy-MM-dd"));
        await _rateLimiter.IncrementAsync(dayOrgScope, tokens);
        var monthOrgScope = GetOrgScope(orgId, "month", thisMonth.ToString("yyyy-MM"));
        await _rateLimiter.IncrementAsync(monthOrgScope, tokens);

        if (apiKeyId.HasValue)
        {
            var dayKeyScope = GetKeyScope(apiKeyId.Value, "day", today.ToString("yyyy-MM-dd"));
            await _rateLimiter.IncrementAsync(dayKeyScope, tokens);
            var monthKeyScope = GetKeyScope(apiKeyId.Value, "month", thisMonth.ToString("yyyy-MM"));
            await _rateLimiter.IncrementAsync(monthKeyScope, tokens);
        }
    }

    /// <summary>
    /// Returns the most specific concurrent request limit: model quota > key quota > org quota.
    /// </summary>
    public async Task<int?> GetConcurrentRequestLimitAsync(int orgId, int? apiKeyId = null, int? modelId = null)
    {
        if (modelId.HasValue)
        {
            var modelQuota = await _db.Quotas
                .Where(q => q.OrganizationId == orgId && q.ModelId == modelId.Value && q.ConcurrentRequestLimit.HasValue)
                .OrderByDescending(q => q.Id)
                .FirstOrDefaultAsync();
            if (modelQuota != null) return modelQuota.ConcurrentRequestLimit;
        }

        if (apiKeyId.HasValue)
        {
            var keyQuota = await _db.Quotas
                .Where(q => q.OrganizationId == orgId && q.ApiKeyId == apiKeyId.Value && q.ConcurrentRequestLimit.HasValue)
                .OrderByDescending(q => q.Id)
                .FirstOrDefaultAsync();
            if (keyQuota != null) return keyQuota.ConcurrentRequestLimit;
        }

        var orgQuota = await _db.Quotas
            .Where(q => q.OrganizationId == orgId && q.ApiKeyId == null && q.ModelId == null && q.ConcurrentRequestLimit.HasValue)
            .OrderByDescending(q => q.Id)
            .FirstOrDefaultAsync();
        return orgQuota?.ConcurrentRequestLimit;
    }

    /// <summary>
    /// Returns the model-scoped quota for the given organization and model, if any.
    /// </summary>
    public async Task<Quota?> GetModelQuotaAsync(int orgId, int modelId)
    {
        return await _db.Quotas
            .Where(q => q.OrganizationId == orgId && q.ModelId == modelId)
            .OrderByDescending(q => q.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Checks whether a model-scoped quota permits the estimated token usage.
    /// Returns (allowed, retryAfterSeconds).
    /// </summary>
    public async Task<(bool allowed, int retryAfterSeconds)> CheckModelTokenQuotaAsync(Quota modelQuota, int modelId, int estimatedTokens)
    {
        var today = DateTime.UtcNow.Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (modelQuota.DailyTokenLimit.HasValue && modelQuota.DailyTokenLimit.Value > 0)
        {
            var scope = GetModelScope(modelId, "day", today.ToString("yyyy-MM-dd"));
            var used = await GetCounterValueAsync(scope);
            if (used + estimatedTokens > modelQuota.DailyTokenLimit.Value)
            {
                var retryAfter = (int)(today.AddDays(1) - DateTime.UtcNow).TotalSeconds;
                _logger.LogWarning("Model quota exceeded for {Scope}: {Used}+{Estimated}/{Limit}", scope, used, estimatedTokens, modelQuota.DailyTokenLimit.Value);
                return (false, retryAfter);
            }
        }

        if (modelQuota.MonthlyTokenLimit.HasValue && modelQuota.MonthlyTokenLimit.Value > 0)
        {
            var scope = GetModelScope(modelId, "month", thisMonth.ToString("yyyy-MM"));
            var used = await GetCounterValueAsync(scope);
            if (used + estimatedTokens > modelQuota.MonthlyTokenLimit.Value)
            {
                var retryAfter = (int)(thisMonth.AddMonths(1) - DateTime.UtcNow).TotalSeconds + 1;
                _logger.LogWarning("Model quota exceeded for {Scope}: {Used}+{Estimated}/{Limit}", scope, used, estimatedTokens, modelQuota.MonthlyTokenLimit.Value);
                return (false, retryAfter);
            }
        }

        return (true, 0);
    }

    /// <summary>
    /// Records token usage against the model-scoped daily and monthly counters.
    /// </summary>
    public async Task RecordModelUsageAsync(int modelId, int tokens)
    {
        if (tokens <= 0) return;

        var today = DateTime.UtcNow.Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        await _rateLimiter.IncrementAsync(GetModelScope(modelId, "day", today.ToString("yyyy-MM-dd")), tokens);
        await _rateLimiter.IncrementAsync(GetModelScope(modelId, "month", thisMonth.ToString("yyyy-MM")), tokens);
    }

    private async Task<Quota?> GetOrgQuotaAsync(int orgId)
    {
        return await _db.Quotas
            .Where(q => q.OrganizationId == orgId && q.ApiKeyId == null)
            .OrderByDescending(q => q.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<Quota?> GetApiKeyQuotaAsync(int apiKeyId)
    {
        return await _db.Quotas
            .Where(q => q.ApiKeyId == apiKeyId)
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

    private static string GetKeyScope(int apiKeyId, string bucketType, string bucketValue) =>
        $"key:{apiKeyId}:{bucketType}:{bucketValue}";

    private static string GetModelScope(int modelId, string bucketType, string bucketValue) =>
        $"model:{modelId}:{bucketType}:{bucketValue}";
}
