using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Xunit;

namespace Tensu.Tests;

public class SemanticCacheServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly ServiceProvider _provider;
    private readonly CacheService _cache;

    public SemanticCacheServiceTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton<CacheService>(sp => new CacheService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CacheService>.Instance,
            sp.GetRequiredService<IServiceScopeFactory>()));
        _provider = services.BuildServiceProvider();
        _cache = _provider.GetRequiredService<CacheService>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SetSemantic_ThenTryGetSemantic_ReturnsHit()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"What is the capital of France?"}]}""";
        _cache.Set("exact:key", "ignored", false);
        await _cache.SetSemanticAsync("gpt-4o", body, """{"choices":[{"message":{"content":"Paris"}}]}""", false, TimeSpan.FromMinutes(10));

        var result = await _cache.TryGetSemanticAsync("gpt-4o", body);

        Assert.NotNull(result);
        Assert.True(result!.Value.hit);
        Assert.Contains("Paris", result.Value.responseBody);
    }

    [Fact]
    public async Task TryGetSemantic_DifferentModel_ReturnsNull()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"Hello"}]}""";
        await _cache.SetSemanticAsync("gpt-4o", body, "response", false, TimeSpan.FromMinutes(10));

        var result = await _cache.TryGetSemanticAsync("claude-3", body);

        Assert.Null(result);
    }

    [Fact]
    public async Task ClearSemantic_RemovesAllEntries()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"Hello"}]}""";
        await _cache.SetSemanticAsync("gpt-4o", body, "response", false, TimeSpan.FromMinutes(10));

        await _cache.ClearSemanticAsync();
        var count = await _cache.GetSemanticEntryCountAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public void ComputeSemanticEmbedding_ReturnsNormalizedVector()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"hello world hello"}]}""";
        var embedding = CacheService.ComputeSemanticEmbedding(body);

        Assert.Equal(CacheService.EmbeddingDimensions, embedding.Length);
        var magnitude = MathF.Sqrt(embedding.Sum(v => v * v));
        Assert.True(MathF.Abs(magnitude - 1f) < 0.001f);
    }

    [Fact]
    public void GenerateEmbedding_SimilarTexts_HigherSimilarityThanDissimilar()
    {
        var a = CacheService.GenerateEmbedding("what is the capital of france");
        var similar = CacheService.GenerateEmbedding("what is the capital city of france");
        var dissimilar = CacheService.GenerateEmbedding("how do i bake chocolate chip cookies");

        var simSimilar = Cosine(a, similar);
        var simDissimilar = Cosine(a, dissimilar);

        Assert.True(simSimilar > 0.7f, $"expected high similarity, got {simSimilar}");
        Assert.True(simSimilar > simDissimilar + 0.3f, $"similar {simSimilar} should clearly exceed dissimilar {simDissimilar}");
    }

    [Fact]
    public void GenerateEmbedding_DifferentTexts_ComparableVectorSpace()
    {
        // With per-document vocabularies these would be incomparable; hashed
        // vectors must yield meaningful similarity across different texts.
        var a = CacheService.GenerateEmbedding("the quick brown fox jumps over the lazy dog");
        var b = CacheService.GenerateEmbedding("the quick brown fox leaps over the lazy dog");

        Assert.True(Cosine(a, b) > 0.8f);
    }

    [Fact]
    public void GenerateEmbedding_ChineseText_ProducesUsefulSimilarity()
    {
        var a = CacheService.GenerateEmbedding("中国的首都是北京");
        var similar = CacheService.GenerateEmbedding("北京是中国的首都");
        var dissimilar = CacheService.GenerateEmbedding("如何做巧克力蛋糕");

        var simSimilar = Cosine(a, similar);
        var simDissimilar = Cosine(a, dissimilar);

        Assert.True(simSimilar > 0.5f, $"expected high similarity for reordered Chinese, got {simSimilar}");
        Assert.True(simSimilar > simDissimilar);
    }

    private static float Cosine(float[] x, float[] y)
    {
        var dot = x.Zip(y, (u, v) => u * v).Sum();
        var magX = MathF.Sqrt(x.Sum(u => u * u));
        var magY = MathF.Sqrt(y.Sum(u => u * u));
        return magX == 0 || magY == 0 ? 0 : dot / (magX * magY);
    }
}