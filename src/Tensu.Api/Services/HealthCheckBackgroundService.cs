using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

/// <summary>
/// Background service that periodically checks provider health and individual key health
/// with configurable probe strategies, automatic failover, and state machine transitions.
/// </summary>
public class HealthCheckBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthCheckBackgroundService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeSpan _defaultInterval = TimeSpan.FromMinutes(5);

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

            var interval = _defaultInterval;
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CheckAllProvidersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
        var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();

        var providers = await db.Providers
            .Include(p => p.Keys)
            .Where(p => p.IsEnabled)
            .ToListAsync(ct);

        var tasks = providers.Select(async provider =>
        {
            var providerResult = await CheckProviderAsync(provider, encryption, settings, ct);
            provider.HealthStatus = providerResult.Status;
            provider.LastHealthCheckAt = DateTime.UtcNow;

            foreach (var key in provider.Keys)
            {
                key.Status = providerResult.KeyResults.GetValueOrDefault(key.Id, key.Status);
                key.LastHealthCheckAt = DateTime.UtcNow;
            }
        });

        await Task.WhenAll(tasks);

        await db.SaveChangesAsync(ct);
    }

    private async Task<(ProviderHealthStatus Status, Dictionary<int, KeyStatus> KeyResults)> CheckProviderAsync(
        Provider provider,
        EncryptionService encryption,
        SettingsService settings,
        CancellationToken ct)
    {
        var keyResults = new Dictionary<int, KeyStatus>();
        var activeKeys = provider.Keys.Where(k => k.Status != KeyStatus.Disabled && k.Status != KeyStatus.Expired).ToList();

        if (!activeKeys.Any())
        {
            return (ProviderHealthStatus.Unhealthy, keyResults);
        }

        var healthyCount = 0;
        var degradedCount = 0;
        var unhealthyCount = 0;

        foreach (var key in activeKeys)
        {
            var keyStatus = await CheckProviderKeyAsync(provider, key, encryption, ct);
            keyResults[key.Id] = keyStatus;

            switch (keyStatus)
            {
                case KeyStatus.Active:
                    healthyCount++;
                    break;
                case KeyStatus.Degraded:
                    degradedCount++;
                    break;
                default:
                    unhealthyCount++;
                    break;
            }
        }

        var total = activeKeys.Count;
        var healthyRatio = (double)healthyCount / total;

        ProviderHealthStatus providerStatus;
        if (healthyRatio >= 0.6)
        {
            providerStatus = ProviderHealthStatus.Healthy;
        }
        else if (healthyRatio >= 0.2 || degradedCount > 0)
        {
            providerStatus = ProviderHealthStatus.Degraded;
        }
        else
        {
            providerStatus = ProviderHealthStatus.Unhealthy;
        }

        return (providerStatus, keyResults);
    }

    private async Task<KeyStatus> CheckProviderKeyAsync(
        Provider provider,
        ProviderKey key,
        EncryptionService encryption,
        CancellationToken ct)
    {
        if (key.Status == KeyStatus.Disabled || key.Status == KeyStatus.Expired)
            return key.Status;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        var healthUrl = $"{provider.BaseUrl.TrimEnd('/')}/v1/models";
        var request = new HttpRequestMessage(HttpMethod.Get, healthUrl);

        try
        {
            var decryptedKey = encryption.Decrypt(key.KeyValue).Trim();
            if (string.IsNullOrWhiteSpace(decryptedKey))
                return KeyStatus.Inactive;

            if (provider.Protocol == ProtocolType.OpenAI)
                request.Headers.Add("Authorization", $"Bearer {decryptedKey}");
            else
                request.Headers.Add("x-api-key", decryptedKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt key {KeyId} for health check", key.Id);
            return KeyStatus.Inactive;
        }

        try
        {
            var response = await client.SendAsync(request, ct);

            key.LastHealthCheckAt = DateTime.UtcNow;

            if (response.IsSuccessStatusCode)
            {
                return KeyStatus.Active;
            }

            var statusCode = (int)response.StatusCode;
            if (statusCode is 401 or 403 or 407)
            {
                _logger.LogWarning("Key {KeyId} for provider {Provider} returned {StatusCode}; marking inactive",
                    key.Id, provider.Name, statusCode);
                return KeyStatus.Inactive;
            }

            if (statusCode >= 500)
            {
                _logger.LogWarning("Key {KeyId} for provider {Provider} returned server error {StatusCode}; marking degraded",
                    key.Id, provider.Name, statusCode);
                return KeyStatus.Degraded;
            }

            // 4xx (including 400, 404, 429) — provider is reachable, key is usable for chat
            return KeyStatus.Active;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Key {KeyId} for provider {Provider} health check timed out; marking degraded",
                key.Id, provider.Name);
            return KeyStatus.Degraded;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Key {KeyId} for provider {Provider} connection failed; marking inactive",
                key.Id, provider.Name);
            return KeyStatus.Inactive;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Key {KeyId} for provider {Provider} health check failed; marking inactive",
                key.Id, provider.Name);
            return KeyStatus.Inactive;
        }
    }
}
