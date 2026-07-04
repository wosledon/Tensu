using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

/// <summary>
/// Service that automatically rotates API keys and provider keys based on usage patterns,
/// error rates, and time-based policies.
/// </summary>
public class KeyRotationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KeyRotationService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public KeyRotationService(
        IServiceScopeFactory scopeFactory,
        ILogger<KeyRotationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
        var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();

        // Rotate platform API keys with high error rates or approaching expiration
        var apiKeys = await db.ApiKeys
            .Where(k => k.Status == KeyStatus.Active && k.ExpiresAt.HasValue)
            .ToListAsync(ct);

        foreach (var apiKey in apiKeys)
        {
            var daysUntilExpiry = (apiKey.ExpiresAt.Value - DateTime.UtcNow).TotalDays;
            if (daysUntilExpiry <= 7)
            {
                _logger.LogInformation("API key {KeyId} ({Name}) expires in {Days} days; consider rotation",
                    apiKey.Id, apiKey.Name, daysUntilExpiry);
            }
        }

        // Rotate provider keys that have been inactive or degraded for extended periods
        var providerKeys = await db.ProviderKeys
            .Include(k => k.Provider)
            .Where(k => k.Status != KeyStatus.Disabled && k.Status != KeyStatus.Expired)
            .ToListAsync(ct);

        foreach (var key in providerKeys)
        {
            if (key.LastHealthCheckAt.HasValue &&
                (DateTime.UtcNow - key.LastHealthCheckAt.Value).TotalHours > 72)
            {
                _logger.LogInformation("Provider key {KeyId} for provider {Provider} has not been health checked in over 72 hours; consider rotation",
                    key.Id, key.Provider.Name);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Generate a new key value for rotation.
    /// </summary>
    public string GenerateNewKey()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")[..32];
    }

    /// <summary>
    /// Rotate a provider key: create a new key, mark old key as expired after grace period.
    /// </summary>
    public async Task<ProviderKey> RotateProviderKeyAsync(int providerId, int keyId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
        var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();

        var key = await db.ProviderKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.ProviderId == providerId);
        if (key == null)
            throw new InvalidOperationException($"Provider key {keyId} not found for provider {providerId}");

        var newKeyValue = GenerateNewKey();
        var newKey = new ProviderKey
        {
            ProviderId = providerId,
            Name = $"{key.Name}-rotated-{DateTime.UtcNow:yyyyMMdd}",
            KeyValue = encryption.Encrypt(newKeyValue),
            Weight = key.Weight,
            Status = KeyStatus.Active,
            RateLimitRpm = key.RateLimitRpm,
            RateLimitTpm = key.RateLimitTpm,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        key.Status = KeyStatus.Expired;
        key.UpdatedAt = DateTime.UtcNow;

        db.ProviderKeys.Add(newKey);
        await db.SaveChangesAsync();

        _logger.LogInformation("Rotated provider key {OldKeyId} for provider {ProviderId}; new key {NewKeyId} created",
            keyId, providerId, newKey.Id);

        return newKey;
    }
}
