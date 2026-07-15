using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Xunit;

namespace Tensu.Tests;

public class CompressionServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly CompressionService _compression;

    public CompressionServiceTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();

        var logger = new Mock<ILogger<CompressionService>>();
        _compression = new CompressionService(logger.Object, new CompressionChannel(), _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Compress_JsonBody_RemovesWhitespace()
    {
        var body = """
        {
            "model": "gpt-4o",
            "messages": [
                {
                    "role": "user",
                    "content": "Hello world"
                }
            ],
            "temperature": 0.7,
            "max_tokens": 100
        }
        """;

        var result = await _compression.CompressAsync(body);

        Assert.True(result.Applied);
        Assert.NotEqual(body, result.CompressedBody);
        Assert.True(result.CompressedBody.Length < body.Length);
        Assert.True(result.OriginalTokenEstimate > 0);
        Assert.True(result.CompressedTokenEstimate <= result.OriginalTokenEstimate);
    }

    [Fact]
    public async Task Compress_ShortBody_ReturnsValidResult()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"Hi"}]}""";
        var result = await _compression.CompressAsync(body);
        Assert.NotNull(result);
        Assert.True(result.OriginalTokenEstimate > 0);
    }

    [Fact]
    public async Task Compress_EmptyBody_DoesNotThrow()
    {
        var result = await _compression.CompressAsync("{}");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Compress_WhenApplied_PersistsMapping()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"This is a moderately long message to ensure compression is applied by the service."}]}""";
        var result = await _compression.CompressAsync(body);

        if (result.Applied)
        {
            Assert.NotNull(result.DecompressionKey);
            var mapping = await _db.CompressionMappings.FindAsync(1L);
            Assert.NotNull(mapping);
            Assert.Equal(result.DecompressionKey, mapping!.DecompressionKey);
        }
    }

    [Fact]
    public async Task Decompress_ExistingMapping_ReturnsOriginalBody()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"This is a moderately long message to ensure compression is applied and the mapping can be restored."}]}""";
        var result = await _compression.CompressAsync(body);

        if (result.Applied)
        {
            var original = await _compression.DecompressAsync(result.CompressedBody, result.Strategy, result.DecompressionKey!);
            Assert.Equal(body, original);
        }
    }

    [Fact]
    public void EstimateTokens_ReturnsPositiveForNonEmpty()
    {
        var tokens = CompressionService.EstimateTokens("Hello world, this is a test message.");
        Assert.True(tokens > 0);
    }

    [Fact]
    public void EstimateTokens_ReturnsZeroForEmpty()
    {
        Assert.Equal(0, CompressionService.EstimateTokens(""));
        Assert.Equal(0, CompressionService.EstimateTokens(null!));
    }
}
