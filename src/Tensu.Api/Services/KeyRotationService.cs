using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class KeyRotationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KeyRotationService> _logger;

    public KeyRotationService(
        IServiceScopeFactory scopeFactory,
        ILogger<KeyRotationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string GenerateNewKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)[..32];
    }

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

public class KeyRotationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KeyRotationBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public KeyRotationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<KeyRotationBackgroundService> logger)
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
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();
                var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
                var rotation = scope.ServiceProvider.GetRequiredService<KeyRotationService>();

                var enabled = await settings.GetAsync("keyRotation.enabled");
                if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(_interval, stoppingToken);
                    continue;
                }

                var warningDaysStr = await settings.GetAsync("keyRotation.expiryWarningDays");
                int.TryParse(warningDaysStr, out var warningDays);
                if (warningDays <= 0) warningDays = 7;

                var expiringKeys = await db.ApiKeys
                    .Where(k => k.Status == KeyStatus.Active && k.ExpiresAt.HasValue
                        && k.ExpiresAt.Value <= DateTime.UtcNow.AddDays(warningDays))
                    .ToListAsync(stoppingToken);

                foreach (var key in expiringKeys)
                {
                    _logger.LogWarning("API key {KeyId} ({Name}) expires in {Days} days; auto-rotation recommended",
                        key.Id, key.Name, (key.ExpiresAt!.Value - DateTime.UtcNow).TotalDays);
                }

                var staleProviderKeys = await db.ProviderKeys
                    .Include(k => k.Provider)
                    .Where(k => k.Status != KeyStatus.Disabled && k.Status != KeyStatus.Expired)
                    .ToListAsync(stoppingToken);

                foreach (var key in staleProviderKeys)
                {
                    if (key.LastHealthCheckAt.HasValue &&
                        (DateTime.UtcNow - key.LastHealthCheckAt.Value).TotalHours > 72)
                    {
                        _logger.LogWarning("Provider key {KeyId} for {Provider} not health checked in 72h; consider rotation",
                            key.Id, key.Provider.Name);
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Key rotation background check failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
