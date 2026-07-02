using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

/// <summary>
/// Background service that periodically checks provider health by pinging their base URLs.
/// </summary>
public class HealthCheckBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthCheckBackgroundService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public HealthCheckBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthCheckBackgroundService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllProvidersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check cycle");
            }
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CheckAllProvidersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
        var providers = await db.Providers.Where(p => p.IsEnabled).ToListAsync(ct);
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        foreach (var provider in providers)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await client.GetAsync(provider.BaseUrl, ct);
                sw.Stop();

                provider.LastHealthCheckAt = DateTime.UtcNow;
                provider.HealthStatus = response.IsSuccessStatusCode
                    ? ProviderHealthStatus.Healthy
                    : ProviderHealthStatus.Degraded;

                _logger.LogDebug("Health check for {Provider}: {Status} ({Latency}ms)",
                    provider.Name, provider.HealthStatus, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                provider.LastHealthCheckAt = DateTime.UtcNow;
                provider.HealthStatus = ProviderHealthStatus.Unhealthy;
                _logger.LogWarning(ex, "Health check failed for provider {Provider}", provider.Name);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
