using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Entities;

namespace Tensu.Api.Services;

/// <summary>
/// System settings service. Stores key-value settings in the database.
/// </summary>
public class SettingsService
{
    private readonly TensuDbContext _db;

    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["compression.enabled"] = "true",
        ["cache.enabled"] = "false",
        ["cache.ttlMinutes"] = "10",
        ["rateLimit.defaultRpm"] = "60",
        ["rateLimit.defaultTpm"] = "100000",
        ["audit.dataRetentionDays"] = "30",
        ["healthCheck.intervalMinutes"] = "5",
    };

    public SettingsService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        var stored = await _db.Settings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var merged = new Dictionary<string, string>(Defaults, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in stored)
            merged[kv.Key] = kv.Value;

        return merged;
    }

    public async Task<string?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var stored = await _db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);

        if (stored != null) return stored.Value;

        Defaults.TryGetValue(key, out var value);
        return value;
    }

    public async Task SetAsync(string key, string value)
    {
        if (!Defaults.ContainsKey(key))
            throw new ArgumentException($"Unknown setting key: {key}");

        var existing = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Settings.Add(new Setting
            {
                Key = key,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }
}
