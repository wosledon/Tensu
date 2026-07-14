using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

public class DataDeletionService : BaseService
{
    private readonly TensuDbContext _db;
    private readonly ILogger<DataDeletionService> _logger;

    public DataDeletionService(TensuDbContext db, ILogger<DataDeletionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<DataDeletionRequest>> GetListAsync(PagedRequest request, int? orgId = null)
    {
        var query = _db.DataDeletionRequests.AsQueryable();

        if (orgId.HasValue)
            query = query.Where(d => d.OrganizationId == orgId.Value);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(d => d.Reason.Contains(request.Keyword!) || d.RequestId!.Contains(request.Keyword!));

        if (string.IsNullOrEmpty(request.SortBy))
            query = query.OrderByDescending(d => d.CreatedAt);

        var (pagedQuery, total) = await ApplyPagingAsync(query, request);
        var items = await pagedQuery.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<DataDeletionRequest?> GetByIdAsync(int id)
    {
        return await _db.DataDeletionRequests.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<DataDeletionRequest> CreateAsync(DataDeletionRequest request)
    {
        request.Status = "pending";
        request.CreatedAt = DateTime.UtcNow;
        request.RequestId = Guid.NewGuid().ToString("N");

        _db.DataDeletionRequests.Add(request);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created data deletion request {Id} for org {OrgId} / user {UserId}",
            request.Id, request.OrganizationId, request.UserId);

        return request;
    }

    public async Task<(bool Success, string? Error)> ProcessAsync(int id)
    {
        var request = await _db.DataDeletionRequests.FirstOrDefaultAsync(d => d.Id == id);
        if (request == null)
            return (false, "Deletion request not found");

        if (request.Status is "completed" or "processing")
            return (false, $"Request is already {request.Status}");

        request.Status = "processing";
        await _db.SaveChangesAsync();

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            int deletedRequestLogs = 0;
            int deletedArchivedLogs = 0;

            if (!string.IsNullOrEmpty(request.ApiKeyId))
            {
                var apiKeyId = int.Parse(request.ApiKeyId);
                deletedRequestLogs = await _db.RequestLogs
                    .Where(r => r.ApiKeyId == apiKeyId)
                    .ExecuteDeleteAsync();
                deletedArchivedLogs = await _db.ArchivedRequestLogs
                    .Where(r => r.ApiKeyId == apiKeyId)
                    .ExecuteDeleteAsync();
            }
            else if (request.UserId.HasValue)
            {
                deletedRequestLogs = await _db.RequestLogs
                    .Where(r => r.UserId == request.UserId.Value)
                    .ExecuteDeleteAsync();
                deletedArchivedLogs = await _db.ArchivedRequestLogs
                    .Where(r => r.UserId == request.UserId.Value)
                    .ExecuteDeleteAsync();
            }
            else if (request.OrganizationId.HasValue)
            {
                deletedRequestLogs = await _db.RequestLogs
                    .Where(r => r.OrganizationId == request.OrganizationId.Value)
                    .ExecuteDeleteAsync();
                deletedArchivedLogs = await _db.ArchivedRequestLogs
                    .Where(r => r.OrganizationId == request.OrganizationId.Value)
                    .ExecuteDeleteAsync();
            }

            await transaction.CommitAsync();

            request.Status = "completed";
            request.CompletedAt = DateTime.UtcNow;
            request.DeletedRequestLogs = deletedRequestLogs;
            request.DeletedArchivedLogs = deletedArchivedLogs;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Completed data deletion request {Id}: deleted {ReqLogs} request logs and {ArchivedLogs} archived logs",
                id, deletedRequestLogs, deletedArchivedLogs);

            return (true, null);
        }
        catch (Exception ex)
        {
            request.Status = "failed";
            request.ErrorMessage = ex.Message;
            request.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogError(ex, "Failed to process data deletion request {Id}", id);
            return (false, ex.Message);
        }
    }
}
