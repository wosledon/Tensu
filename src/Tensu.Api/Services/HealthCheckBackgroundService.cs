using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

/// <summary>
/// Background service that periodically checks provider health and individual key health.
/// Updates Provider and ProviderKey status based on response latency and status codes.
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
        var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();
        var providers = await db.Providers
            .Include(p => p.Keys)
            .Where(p => p.IsEnabled)
            .ToListAsync(ct);

        foreach (var provider in providers)
        {
            var (providerStatus, latencyMs) = await CheckProviderBaseUrlAsync(provider, ct);
            provider.LastHealthCheckAt = DateTime.UtcNow;
            provider.HealthStatus = providerStatus;

            foreach (var key in provider.Keys)
            {
                var keyStatus = await CheckProviderKeyAsync(provider, key, encryption, ct);
                key.Status = keyStatus;
                key.LastHealthCheckAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<(ProviderHealthStatus status, long latencyMs)> CheckProviderBaseUrlAsync(Provider provider, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.GetAsync(provider.BaseUrl, ct);
            sw.Stop();

            var latencyMs = sw.ElapsedMilliseconds;
            var status = response.IsSuccessStatusCode
                ? ProviderHealthStatus.Healthy
                : ProviderHealthStatus.Degraded;

            _logger.LogDebug("Health check for {Provider}: {Status} ({Latency}ms)",
                provider.Name, status, latencyMs);

            return (status, latencyMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for provider {Provider}", provider.Name);
            return (ProviderHealthStatus.Unhealthy, 0);
        }
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
            var decryptedKey = encryption.Decrypt(key.KeyValue);
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.SendAsync(request, ct);
            sw.Stop();

            key.LastHealthCheckAt = DateTime.UtcNow;

            if (response.IsSuccessStatusCode)
            {
                return sw.ElapsedMilliseconds > 5000 ? KeyStatus.Degraded : KeyStatus.Active;
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

            return KeyStatus.Degraded;
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
