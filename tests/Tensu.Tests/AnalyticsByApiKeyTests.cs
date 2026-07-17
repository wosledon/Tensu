using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Tensu.Core.Enums;
using Xunit;

namespace Tensu.Tests;

public class AnalyticsByApiKeyTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly AnalyticsService _service;

    public AnalyticsByApiKeyTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();
        _service = new AnalyticsService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(Organization org1, Organization org2, ApiKey key1, ApiKey key2, ApiKey key3)> SeedAsync()
    {
        var org1 = new Organization { Name = "Org1", Path = "/org1" };
        var org2 = new Organization { Name = "Org2", Path = "/org2" };
        _db.Organizations.AddRange(org1, org2);
        await _db.SaveChangesAsync();

        var alice = new User { Username = "alice", DisplayName = "Alice A", OrganizationId = org1.Id, PasswordHash = "x" };
        _db.Users.Add(alice);
        await _db.SaveChangesAsync();

        var key1 = new ApiKey { Name = "prod-key", KeyPrefix = "tk-aaaa", KeyValue = "enc", OrganizationId = org1.Id, UserId = alice.Id };
        var key2 = new ApiKey { Name = "org-key", KeyPrefix = "tk-bbbb", KeyValue = "enc", OrganizationId = org1.Id };
        var key3 = new ApiKey { Name = "org2-key", KeyPrefix = "tk-cccc", KeyValue = "enc", OrganizationId = org2.Id };
        _db.ApiKeys.AddRange(key1, key2, key3);
        await _db.SaveChangesAsync();
        return (org1, org2, key1, key2, key3);
    }

    private void AddLog(int? apiKeyId, int? orgId, RequestStatus status = RequestStatus.Success,
        int inputTokens = 100, int outputTokens = 50, decimal inputCost = 0.001m, decimal outputCost = 0.002m,
        DateTime? timestamp = null)
    {
        _db.RequestLogs.Add(new RequestLog
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Timestamp = timestamp ?? DateTime.UtcNow,
            ApiKeyId = apiKeyId,
            OrganizationId = orgId,
            ModelName = "m",
            Status = status,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            InputCost = inputCost,
            OutputCost = outputCost,
        });
    }

    private static JsonElement Serialize(object result)
    {
        var json = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task GetByApiKeyAsync_AggregatesRequestsTokensCost()
    {
        var (org1, _, key1, key2, _) = await SeedAsync();
        AddLog(key1.Id, org1.Id, RequestStatus.Success);
        AddLog(key1.Id, org1.Id, RequestStatus.Success, inputTokens: 200, outputTokens: 100);
        AddLog(key1.Id, org1.Id, RequestStatus.Failed);
        AddLog(key2.Id, org1.Id, RequestStatus.Success);
        await _db.SaveChangesAsync();

        var root = Serialize(await _service.GetByApiKeyAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));
        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());

        var first = items[0];
        Assert.Equal(key1.Id, first.GetProperty("apiKeyId").GetInt32());
        Assert.Equal("prod-key", first.GetProperty("name").GetString());
        Assert.Equal("Alice A", first.GetProperty("user").GetString());
        Assert.Equal(3, first.GetProperty("requests").GetInt32());
        Assert.Equal(66.7, first.GetProperty("successRate").GetDouble());
        Assert.Equal(400, first.GetProperty("inputTokens").GetInt32());
        Assert.Equal(200, first.GetProperty("outputTokens").GetInt32());
        Assert.Equal(600, first.GetProperty("totalTokens").GetInt32());
        Assert.Equal(0.009m, first.GetProperty("totalCost").GetDecimal());

        var second = items[1];
        Assert.Equal(key2.Id, second.GetProperty("apiKeyId").GetInt32());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("user").ValueKind);
        Assert.Equal(1, second.GetProperty("requests").GetInt32());
    }

    [Fact]
    public async Task GetByApiKeyAsync_FiltersByOrganization()
    {
        var (org1, org2, key1, _, key3) = await SeedAsync();
        AddLog(key1.Id, org1.Id);
        AddLog(key3.Id, org2.Id);
        await _db.SaveChangesAsync();

        var root = Serialize(await _service.GetByApiKeyAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), org1.Id));
        var items = root.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(key1.Id, items[0].GetProperty("apiKeyId").GetInt32());
    }

    [Fact]
    public async Task GetByApiKeyAsync_ExcludesLogsWithoutApiKey()
    {
        var (org1, _, key1, _, _) = await SeedAsync();
        AddLog(key1.Id, org1.Id);
        AddLog(null, null); // 测试聊天等无密钥日志
        await _db.SaveChangesAsync();

        var root = Serialize(await _service.GetByApiKeyAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));
        var items = root.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(1, items[0].GetProperty("requests").GetInt32());
    }

    [Fact]
    public async Task GetByApiKeyAsync_FallsBackToKeyIdWhenKeyDeleted()
    {
        var (org1, _, _, _, _) = await SeedAsync();
        AddLog(999, org1.Id);
        await _db.SaveChangesAsync();

        var root = Serialize(await _service.GetByApiKeyAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));
        var items = root.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("key #999", items[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetByApiKeyAsync_RespectsTimeRange()
    {
        var (org1, _, key1, _, _) = await SeedAsync();
        AddLog(key1.Id, org1.Id, timestamp: DateTime.UtcNow.AddDays(-10));
        await _db.SaveChangesAsync();

        var root = Serialize(await _service.GetByApiKeyAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task IsUserInOrganizationAsync_ValidatesMembership()
    {
        var (org1, org2, _, _, _) = await SeedAsync();
        var apiKeyService = new ApiKeyService(_db, null!);
        var alice = await _db.Users.FirstAsync(u => u.Username == "alice");

        Assert.True(await apiKeyService.IsUserInOrganizationAsync(alice.Id, org1.Id));
        Assert.False(await apiKeyService.IsUserInOrganizationAsync(alice.Id, org2.Id));
        Assert.False(await apiKeyService.IsUserInOrganizationAsync(999, org1.Id));
    }
}
