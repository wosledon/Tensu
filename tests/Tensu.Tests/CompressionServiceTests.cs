using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Infrastructure;
using Xunit;

namespace Tensu.Tests;

public class CompressionServiceTests
{
    private readonly CompressionService _compression;

    public CompressionServiceTests()
    {
        var logger = new Mock<ILogger<CompressionService>>();
        _compression = new CompressionService(logger.Object);
    }

    [Fact]
    public void Compress_JsonBody_RemovesWhitespace()
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

        var result = _compression.Compress(body);

        Assert.True(result.CompressedBody.Length < body.Length);
        Assert.True(result.OriginalTokenEstimate > 0);
        Assert.True(result.CompressedTokenEstimate <= result.OriginalTokenEstimate);
    }

    [Fact]
    public void Compress_ShortBody_ReturnsValidResult()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"Hi"}]}""";
        var result = _compression.Compress(body);
        Assert.NotNull(result);
        Assert.True(result.OriginalTokenEstimate > 0);
    }

    [Fact]
    public void Compress_EmptyBody_DoesNotThrow()
    {
        var result = _compression.Compress("{}");
        Assert.NotNull(result);
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
