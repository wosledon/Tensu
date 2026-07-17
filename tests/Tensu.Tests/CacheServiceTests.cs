using Tensu.Api.Infrastructure;
using Xunit;

namespace Tensu.Tests;

public class CacheServiceTests
{
    private readonly CacheService _cache = new(
        new Microsoft.Extensions.Logging.Abstractions.NullLogger<CacheService>());

    [Fact]
    public void Set_ThenTryGet_ReturnsHit()
    {
        var key = "test-model:abc123:def456";
        _cache.Set(key, """{"choices":[]}""", isStream: false);

        var result = _cache.TryGet(key);
        Assert.NotNull(result);
        Assert.True(result!.Value.hit);
        Assert.Equal("""{"choices":[]}""", result.Value.responseBody);
    }

    [Fact]
    public void TryGet_Miss_ReturnsNull()
    {
        var result = _cache.TryGet("nonexistent:key");
        Assert.Null(result);
    }

    [Fact]
    public void Set_Expired_ReturnsNull()
    {
        var key = "expired:key";
        _cache.Set(key, "data", isStream: false, ttl: TimeSpan.FromMilliseconds(1));

        Thread.Sleep(10);
        var result = _cache.TryGet(key);
        Assert.Null(result);
    }

    [Fact]
    public void ComputeCacheKey_DifferentModels_DifferentKeys()
    {
        var key1 = CacheService.ComputeCacheKey("gpt-4o", """{"messages":[]}""");
        var key2 = CacheService.ComputeCacheKey("claude-3", """{"messages":[]}""");
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void ComputeCacheKey_SameInputs_SameKey()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"Hi"}]}""";
        var key1 = CacheService.ComputeCacheKey("gpt-4o", body);
        var key2 = CacheService.ComputeCacheKey("gpt-4o", body);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public async Task InvalidateModel_RemovesMatchingEntries()
    {
        _cache.Set("gpt-4o:hash1:hash2", "data1", false);
        _cache.Set("gpt-4o:hash3:hash4", "data2", false);
        _cache.Set("claude-3:hash5:hash6", "data3", false);

        await _cache.InvalidateModelAsync("gpt-4o");

        Assert.Null(_cache.TryGet("gpt-4o:hash1:hash2"));
        Assert.Null(_cache.TryGet("gpt-4o:hash3:hash4"));
        Assert.NotNull(_cache.TryGet("claude-3:hash5:hash6"));
    }

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        var cache = new CacheService(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<CacheService>());
        cache.Set("a:b:c", "data", false);
        cache.Set("d:e:f", "data", false);

        var (entries, _) = cache.GetStats();
        Assert.Equal(2, entries);
    }
}
