using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Xunit;

namespace Tensu.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
        _service = new SettingsService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetAllAsync_MergesDefaults()
    {
        var all = await _service.GetAllAsync();

        Assert.Equal("true", all["compression.enabled"]);
        Assert.Equal("false", all["cache.enabled"]);
        Assert.Equal("60", all["rateLimit.defaultRpm"]);
        Assert.Equal("100000", all["rateLimit.defaultTpm"]);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsUpdatedValue()
    {
        await _service.SetAsync("compression.enabled", "false");

        var value = await _service.GetAsync("compression.enabled");
        Assert.Equal("false", value);
    }

    [Fact]
    public async Task SetAsync_Existing_KeepsCreatedAt()
    {
        var originalCreatedAt = DateTime.UtcNow.AddDays(-1);
        _db.Settings.Add(new Setting
        {
            Key = "compression.enabled",
            Value = "true",
            CreatedAt = originalCreatedAt,
            UpdatedAt = originalCreatedAt
        });
        await _db.SaveChangesAsync();

        await _service.SetAsync("compression.enabled", "false");

        var setting = await _db.Settings.FirstAsync(s => s.Key == "compression.enabled");
        Assert.Equal(originalCreatedAt, setting.CreatedAt);
        Assert.True(setting.UpdatedAt > setting.CreatedAt);
    }
}
