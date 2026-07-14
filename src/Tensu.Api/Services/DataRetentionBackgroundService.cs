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
        if (!int.TryParse(retentionValue, out var retentionDays) || retentionDays <= 0)
            retentionDays = 30;

        var archiveBeforeDeleteValue = await settingsService.GetAsync("audit.archiveBeforeDelete");
        var archiveBeforeDelete = string.Equals(archiveBeforeDeleteValue, "true", StringComparison.OrdinalIgnoreCase);

        var archivedRetentionValue = await settingsService.GetAsync("audit.retentionArchivedDays");
        if (!int.TryParse(archivedRetentionValue, out var archivedRetentionDays) || archivedRetentionDays <= 0)
            archivedRetentionDays = 365;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var archivedCutoff = DateTime.UtcNow.AddDays(-archivedRetentionDays);
        var now = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        int totalArchived = 0;
        int totalDeleted = 0;

        if (archiveBeforeDelete)
        {
            const int batchSize = 1000;
            while (true)
            {
                var logs = await db.RequestLogs
                    .Where(r => r.Timestamp < cutoff)
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
        }
        else
        {
            totalDeleted = await db.RequestLogs
                .Where(r => r.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);
        }

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
            "Data retention archived {Archived} and deleted {Deleted} audit logs older than {Cutoff:O}",
            totalArchived, totalDeleted, cutoff);
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
            "Data retention removed {Count} daily stats older than {ArchivedCutoff:O}",
            dailyStatsDeleted, archivedCutoff);
        _logger.LogInformation(
            "Data retention removed {Count} admin audit logs older than 180 days", adminAuditDeleted);
        _logger.LogInformation(
            "Data retention removed {Count} completed data deletion requests", dataDeletionDeleted);
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
