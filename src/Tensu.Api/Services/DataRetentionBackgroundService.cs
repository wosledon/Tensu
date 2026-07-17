using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Services;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

/// <summary>
/// Background service that periodically archives old audit logs, prunes the archive,
/// and cleans up expired rate limit counters, compression mappings and semantic cache entries.
/// </summary>
public class DataRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public DataRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data retention cleanup cycle");
            }
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
        var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();

        var retentionValue = await settingsService.GetAsync("audit.dataRetentionDays");
        if (!int.TryParse(retentionValue, out var globalRetentionDays) || globalRetentionDays <= 0)
            globalRetentionDays = 30;

        var archiveBeforeDeleteValue = await settingsService.GetAsync("audit.archiveBeforeDelete");
        var archiveBeforeDelete = string.Equals(archiveBeforeDeleteValue, "true", StringComparison.OrdinalIgnoreCase);

        var archivedRetentionValue = await settingsService.GetAsync("audit.retentionArchivedDays");
        if (!int.TryParse(archivedRetentionValue, out var archivedRetentionDays) || archivedRetentionDays <= 0)
            archivedRetentionDays = 365;

        var archivedCutoff = DateTime.UtcNow.AddDays(-archivedRetentionDays);
        var now = DateTime.UtcNow;

        // Per-organization retention: orgs may configure 7~180 days (PRD §3.6.4);
        // logs without an organization (or with an unknown one) use the global setting.
        var orgRetentions = await db.Organizations
            .AsNoTracking()
            .Select(o => new { o.Id, o.DataRetentionDays })
            .ToListAsync(ct);

        var retentionByOrg = orgRetentions.ToDictionary(
            o => o.Id,
            o => o.DataRetentionDays > 0 ? Math.Clamp(o.DataRetentionDays, 7, 180) : globalRetentionDays);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        int totalArchived = 0;
        int totalDeleted = 0;

        foreach (var (orgId, retentionDays) in retentionByOrg)
        {
            var cutoff = now.AddDays(-retentionDays);
            var (archived, deleted) = await ArchiveAndDeleteBatchAsync(db, archiveBeforeDelete,
                db.RequestLogs.Where(r => r.OrganizationId == orgId && r.Timestamp < cutoff), ct);
            totalArchived += archived;
            totalDeleted += deleted;
        }

        // Logs with no organization or an organization that no longer exists.
        var orgIds = retentionByOrg.Keys.ToList();
        var globalCutoff = now.AddDays(-globalRetentionDays);
        var (globalArchived, globalDeleted) = await ArchiveAndDeleteBatchAsync(db, archiveBeforeDelete,
            db.RequestLogs.Where(r => r.Timestamp < globalCutoff &&
                (r.OrganizationId == null || !orgIds.Contains(r.OrganizationId.Value))), ct);
        totalArchived += globalArchived;
        totalDeleted += globalDeleted;

        var archivedDeleted = await db.ArchivedRequestLogs
            .Where(a => a.Timestamp < archivedCutoff)
            .ExecuteDeleteAsync(ct);

        var countersDeleted = await db.RateLimitCounters
            .Where(c => c.WindowStart < now.AddSeconds(-c.WindowSeconds))
            .ExecuteDeleteAsync(ct);

        var compressionMappingsDeleted = await db.CompressionMappings
            .Where(m => m.CreatedAt < now.AddDays(-30))
            .ExecuteDeleteAsync(ct);

        var semanticCacheDeleted = await db.SemanticCacheEntries
            .Where(e => e.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        var exactCacheDeleted = await db.ExactCacheEntries
            .Where(e => e.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        var dailyStatsDeleted = await db.DailyStats
            .Where(s => s.Date < now.AddDays(-archivedRetentionDays))
            .ExecuteDeleteAsync(ct);

        var adminAuditDeleted = await db.AdminAuditLogs
            .Where(a => a.Timestamp < now.AddDays(-180))
            .ExecuteDeleteAsync(ct);

        var dataDeletionDeleted = await db.DataDeletionRequests
            .Where(d => d.CreatedAt < now.AddDays(-archivedRetentionDays) && d.Status == "completed")
            .ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Data retention archived {Archived} and deleted {Deleted} audit logs (per-organization retention, global {GlobalDays} days)",
            totalArchived, totalDeleted, globalRetentionDays);
        _logger.LogInformation(
            "Data retention removed {Count} archived logs older than {ArchivedCutoff:O}",
            archivedDeleted, archivedCutoff);
        _logger.LogInformation(
            "Data retention removed {Count} expired rate limit counters", countersDeleted);
        _logger.LogInformation(
            "Data retention removed {Count} compression mappings older than 30 days", compressionMappingsDeleted);
        _logger.LogInformation(
            "Data retention removed {Count} expired semantic cache entries", semanticCacheDeleted);
        _logger.LogInformation(
            "Data retention removed {Count} expired exact cache entries", exactCacheDeleted);
        _logger.LogInformation(
            "Data retention removed {Count} daily stats older than {ArchivedCutoff:O}",
            dailyStatsDeleted, archivedCutoff);
        _logger.LogInformation(
            "Data retention removed {Count} admin audit logs older than 180 days", adminAuditDeleted);
        _logger.LogInformation(
            "Data retention removed {Count} completed data deletion requests", dataDeletionDeleted);
    }

    /// <summary>
    /// Archives (optional) then deletes all logs matching the given filter, in batches.
    /// Returns (archivedCount, deletedCount).
    /// </summary>
    private static async Task<(int archived, int deleted)> ArchiveAndDeleteBatchAsync(
        TensuDbContext db, bool archiveBeforeDelete, IQueryable<RequestLog> filter, CancellationToken ct)
    {
        if (!archiveBeforeDelete)
        {
            var deletedOnly = await filter.ExecuteDeleteAsync(ct);
            return (0, deletedOnly);
        }

        var totalArchived = 0;
        var totalDeleted = 0;
        const int batchSize = 1000;

        while (true)
        {
            var logs = await filter
                .OrderBy(r => r.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            if (logs.Count == 0)
                break;

            var requestIds = logs.Select(l => l.RequestId).ToList();
            var existingArchivedIds = await db.ArchivedRequestLogs
                .Where(a => requestIds.Contains(a.RequestId))
                .Select(a => a.RequestId)
                .ToListAsync(ct);

            var logsToArchive = logs
                .Where(l => !existingArchivedIds.Contains(l.RequestId))
                .ToList();

            if (logsToArchive.Count > 0)
            {
                db.ArchivedRequestLogs.AddRange(logsToArchive.Select(MapToArchive));
                await db.SaveChangesAsync(ct);
                totalArchived += logsToArchive.Count;
            }

            var idsToDelete = logs.Select(l => l.Id).ToList();
            var deleted = await db.RequestLogs
                .Where(r => idsToDelete.Contains(r.Id))
                .ExecuteDeleteAsync(ct);
            totalDeleted += deleted;
        }

        return (totalArchived, totalDeleted);
    }

    private static ArchivedRequestLog MapToArchive(RequestLog log)
    {
        return new ArchivedRequestLog
        {
            RequestId = log.RequestId,
            Timestamp = log.Timestamp,
            ApiKeyId = log.ApiKeyId,
            OrganizationId = log.OrganizationId,
            UserId = log.UserId,
            ModelName = log.ModelName,
            ResolvedModelName = log.ResolvedModelName,
            ProviderId = log.ProviderId,
            ProviderName = log.ProviderName,
            InputTokens = log.InputTokens,
            InputTokensAfterCompression = log.InputTokensAfterCompression,
            OutputTokens = log.OutputTokens,
            CacheHit = log.CacheHit,
            SemanticCacheHit = log.SemanticCacheHit,
            TimeToFirstTokenMs = log.TimeToFirstTokenMs,
            TotalDurationMs = log.TotalDurationMs,
            OutputTokensPerSecond = log.OutputTokensPerSecond,
            InputCost = log.InputCost,
            OutputCost = log.OutputCost,
            Currency = log.Currency,
            CompressionApplied = log.CompressionApplied,
            CompressionStrategy = log.CompressionStrategy,
            CompressionMappingKey = log.CompressionMappingKey,
            Status = log.Status,
            ErrorCode = log.ErrorCode,
            ErrorMessage = log.ErrorMessage,
            RetryCount = log.RetryCount,
            IsStream = log.IsStream,
            RequestContent = log.RequestContent,
            ResponseContent = log.ResponseContent
        };
    }
}
