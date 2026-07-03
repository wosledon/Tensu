using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Services;

namespace Tensu.Api.Services;

/// <summary>
/// Background service that periodically cleans up old audit logs and expired rate limit counters.
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

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var auditDeleted = await db.RequestLogs
            .Where(r => r.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);
        _logger.LogInformation("Data retention cleanup removed {Count} old audit logs older than {Cutoff:O}", auditDeleted, cutoff);

        var now = DateTime.UtcNow;
        var countersDeleted = await db.RateLimitCounters
            .Where(c => c.WindowStart < now.AddSeconds(-c.WindowSeconds))
            .ExecuteDeleteAsync(ct);
        _logger.LogInformation("Data retention cleanup removed {Count} expired rate limit counters", countersDeleted);
    }
}
