using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Xunit;

namespace Tensu.Tests;

public class CompressionServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly CompressionService _compression;
    private readonly CompressionBackgroundService _background;
    private readonly CancellationTokenSource _cts = new();

    public CompressionServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbRoot = new InMemoryDatabaseRoot();
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(dbName, dbRoot)
            .Options);
        _db.Database.EnsureCreated();

        // Run the real background persistence so decompression mappings land in the DB.
        var channel = new CompressionChannel();
        var services = new ServiceCollection();
        services.AddDbContext<TensuDbContext>(o => o.UseInMemoryDatabase(dbName, dbRoot));
        var provider = services.BuildServiceProvider();
        _background = new CompressionBackgroundService(
            channel,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CompressionBackgroundService>.Instance);
        _background.StartAsync(_cts.Token).GetAwaiter().GetResult();

        var logger = new Mock<ILogger<CompressionService>>();
        _compression = new CompressionService(logger.Object, channel, _db);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _background.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _cts.Dispose();
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

    [Fact]
    public async Task Compress_DuplicateMessages_RemovesDuplicatesAndRestores()
    {
        var msg = string.Concat(Enumerable.Repeat("Repeated user message with enough content to matter. ", 4));
        var body = $$"""{"model":"gpt-4o","messages":[{"role":"user","content":"{{msg}}"},{"role":"user","content":"{{msg}}"},{"role":"assistant","content":"ok"}]}""";

        var result = await _compression.CompressAsync(body);

        Assert.True(result.Applied);
        Assert.Contains("dedup", result.Strategy);

        var doc = System.Text.Json.JsonDocument.Parse(result.CompressedBody);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());

        // Restoration via mapping is byte-exact.
        var restored = await WaitForMappingAndDecompress(result);
        Assert.Equal(body, restored);
    }

    [Fact]
    public async Task Compress_DuplicateParagraphs_RemovesThem()
    {
        var paragraph = string.Concat(Enumerable.Repeat("This is a long repeated paragraph with plenty of content. ", 6));
        var content = $"{paragraph}\n\n{paragraph}\n\nunique tail";
        var body = $$"""{"model":"gpt-4o","messages":[{"role":"user","content":{{System.Text.Json.JsonSerializer.Serialize(content)}}}]}""";

        var result = await _compression.CompressAsync(body);

        Assert.True(result.Applied);
        Assert.Contains("dedup-paragraphs", result.Strategy);

        var doc = System.Text.Json.JsonDocument.Parse(result.CompressedBody);
        var compressedContent = doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Equal($"{paragraph}\n\nunique tail", compressedContent);

        var restored = await WaitForMappingAndDecompress(result);
        Assert.Equal(body, restored);
    }

    [Fact]
    public async Task Compress_JsonContentWithLongKeys_ShortensKeysAndRestores()
    {
        var item = """{"customer_order_identifier":"A-1001","customer_delivery_address":"Street 1","customer_contact_phone":"123"}""";
        var jsonContent = "[" + string.Join(",", Enumerable.Repeat(item, 8)) + "]";
        var body = $$"""{"model":"gpt-4o","messages":[{"role":"user","content":{{System.Text.Json.JsonSerializer.Serialize(jsonContent)}}}]}""";

        var result = await _compression.CompressAsync(body);

        Assert.True(result.Applied);
        Assert.Contains("json-keys", result.Strategy);
        Assert.DoesNotContain("customer_order_identifier", result.CompressedBody);

        var restored = await WaitForMappingAndDecompress(result);
        Assert.Equal(body, restored);
    }

    [Fact]
    public async Task Compress_ProtocolKeys_NeverShortened()
    {
        var body = """{"model":"gpt-4o","messages":[{"role":"user","content":"Hi"}],"max_tokens":100}""";
        var result = await _compression.CompressAsync(body);

        if (result.Applied)
        {
            var doc = System.Text.Json.JsonDocument.Parse(result.CompressedBody);
            Assert.True(doc.RootElement.TryGetProperty("messages", out _));
            Assert.True(doc.RootElement.TryGetProperty("max_tokens", out _));
        }
    }

    [Fact]
    public async Task Compress_SkipsCodeBlocks()
    {
        var body = $$"""{"model":"gpt-4o","messages":[{"role":"user","content":"Here is code:\n```python\nprint('hello')\n```\nExplain it."}]}""";
        var result = await _compression.CompressAsync(body);
        Assert.False(result.Applied);
        Assert.Equal("none", result.Strategy);
    }

    [Fact]
    public async Task Compress_SkipsFewShotExamples()
    {
        var body = "{\"model\":\"gpt-4o\",\"messages\":["
            + "{\"role\":\"user\",\"content\":\"Example 1:\\nInput: hello\\nOutput: world\\n\\nExample 2:\\nInput: foo\\nOutput: bar\"}"
            + ",{\"role\":\"assistant\",\"content\":\"I understand the pattern.\"}"
            + "]}";
        var result = await _compression.CompressAsync(body);
        Assert.False(result.Applied);
        Assert.Equal("none", result.Strategy);
    }

    [Fact]
    public async Task Compress_SkipsQaPattern()
    {
        var body = "{\"model\":\"gpt-4o\",\"messages\":["
            + "{\"role\":\"user\",\"content\":\"Q: What is AI?\\nA: Artificial Intelligence.\\n\\nQ: What is ML?\\nA: Machine Learning.\"}"
            + ",{\"role\":\"assistant\",\"content\":\"Here are the answers.\"}"
            + "]}";
        var result = await _compression.CompressAsync(body);
        Assert.False(result.Applied);
        Assert.Equal("none", result.Strategy);
    }

    [Fact]
    public async Task Compress_PlainJsonContent_StillCompresses()
    {
        var item = """{"customer_order_identifier":"A-1001","customer_delivery_address":"Street 1","customer_contact_phone":"123"}""";
        var jsonContent = "[" + string.Join(",", Enumerable.Repeat(item, 8)) + "]";
        var body = $$"""{"model":"gpt-4o","messages":[{"role":"user","content":{{System.Text.Json.JsonSerializer.Serialize(jsonContent)}}}]}""";

        var result = await _compression.CompressAsync(body);
        Assert.True(result.Applied);
    }

    private async Task<string?> WaitForMappingAndDecompress(CompressionService.CompressionResult result)
    {
        // Mapping persistence is async via channel; poll briefly.
        for (var i = 0; i < 100; i++)
        {
            var restored = await _compression.DecompressAsync(result.CompressedBody, result.Strategy, result.DecompressionKey!);
            if (restored != null) return restored;
            await Task.Delay(30);
        }
        return null;
    }
}
