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

    public SettingsService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        // Using a simple approach: store settings as Organization-level or a dedicated table
        // For now, return defaults and allow override via API
        return new Dictionary<string, string>
        {
            ["compression.enabled"] = "true",
            ["cache.enabled"] = "false",
            ["cache.ttlMinutes"] = "10",
            ["rateLimit.defaultRpm"] = "60",
            ["rateLimit.defaultTpm"] = "100000",
            ["audit.dataRetentionDays"] = "30",
            ["healthCheck.intervalMinutes"] = "5",
        };
    }

    public async Task<string?> GetAsync(string key)
    {
        var all = await GetAllAsync();
        return all.TryGetValue(key, out var value) ? value : null;
    }

    public async Task SetAsync(string key, string value)
    {
        // In a full implementation, this would write to a Settings table
        // For now, this is a placeholder that validates the key exists
        var all = await GetAllAsync();
        if (!all.ContainsKey(key))
            throw new ArgumentException($"Unknown setting key: {key}");
        // TODO: Persist to database
    }
}
