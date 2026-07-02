using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

public class AuditService : BaseService
{
    private readonly TensuDbContext _db;

    public AuditService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<RequestLog>> GetListAsync(PagedRequest request, int? orgId = null, string? modelName = null)
    {
        var query = _db.RequestLogs.AsQueryable();

        if (orgId.HasValue)
            query = query.Where(r => r.OrganizationId == orgId.Value);

        if (!string.IsNullOrEmpty(modelName))
            query = query.Where(r => r.ModelName == modelName);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(r => r.RequestId.Contains(request.Keyword) || r.ModelName.Contains(request.Keyword));

        // Default sort by timestamp desc
        if (string.IsNullOrEmpty(request.SortBy))
            query = query.OrderByDescending(r => r.Timestamp);

        query = ApplyPaging(query, request, out var total);
        var items = await query.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<RequestLog?> GetByRequestIdAsync(string requestId)
    {
        return await _db.RequestLogs.FirstOrDefaultAsync(r => r.RequestId == requestId);
    }

    public async Task<object> GetSummaryAsync(DateTime from, DateTime to, int? orgId = null)
    {
        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();

        return new
        {
            TotalRequests = logs.Count,
            SuccessRequests = logs.Count(r => r.Status == Core.Enums.RequestStatus.Success),
            FailedRequests = logs.Count(r => r.Status == Core.Enums.RequestStatus.Failed),
            TotalInputTokens = logs.Sum(r => r.InputTokens ?? 0),
            TotalOutputTokens = logs.Sum(r => r.OutputTokens ?? 0),
            TotalCost = logs.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)),
            CacheHitRate = logs.Count > 0 ? (double)logs.Count(r => r.CacheHit) / logs.Count * 100 : 0,
            AvgLatencyMs = logs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average(),
            CompressionSavedTokens = logs.Sum(r => (r.InputTokens ?? 0) - (r.InputTokensAfterCompression ?? r.InputTokens ?? 0))
        };
    }
}
