using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Xunit;

namespace Tensu.Tests;

public class AuditSecurityTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly EncryptionService _encryption;

    public AuditSecurityTests()
    {
        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();

        var config = new ConfigurationBuilder()
            .Add(new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource
            {
                InitialData = new Dictionary<string, string?> { ["Encryption:Key"] = "TensuDefaultEncryptionKey32Bytes!" }
            })
            .Build();
        _encryption = new EncryptionService(config);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AuditContent_EncryptDecrypt_RoundTrips()
    {
        var original = """{"messages":[{"role":"user","content":"secret prompt"}]}""";

        var encrypted = _encryption.EncryptAuditContent(original);
        Assert.NotNull(encrypted);
        Assert.StartsWith(EncryptionService.AuditContentPrefix, encrypted);
        Assert.DoesNotContain("secret prompt", encrypted);

        var decrypted = _encryption.DecryptAuditContent(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void AuditContent_PlainText_PassesThrough()
    {
        var plain = "not encrypted";
        Assert.Equal(plain, _encryption.DecryptAuditContent(plain));
        Assert.Null(_encryption.DecryptAuditContent(null));
        Assert.Null(_encryption.EncryptAuditContent(null));
    }

    [Fact]
    public void AuditContent_WrongKey_ReturnsNull()
    {
        var encrypted = _encryption.EncryptAuditContent("secret");

        var otherConfig = new ConfigurationBuilder()
            .Add(new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource
            {
                InitialData = new Dictionary<string, string?> { ["Encryption:Key"] = "ADifferentEncryptionKey_32Bytes!!" }
            })
            .Build();
        var other = new EncryptionService(otherConfig);

        Assert.Null(other.DecryptAuditContent(encrypted));
    }

    [Fact]
    public async Task AuditService_ReadsEncryptedContent_AsPlainText()
    {
        var secret = "confidential request body";
        _db.RequestLogs.Add(new RequestLog
        {
            RequestId = "enc-test-1",
            ModelName = "gpt-4o",
            RequestContent = _encryption.EncryptAuditContent(secret),
            ResponseContent = _encryption.EncryptAuditContent("response body")
        });
        await _db.SaveChangesAsync();

        var service = new AuditService(_db, _encryption);
        var log = await service.GetByRequestIdAsync("enc-test-1");

        Assert.NotNull(log);
        Assert.Equal(secret, log!.RequestContent);
        Assert.Equal("response body", log.ResponseContent);
    }

    [Fact]
    public async Task RequestLog_CannotBeModified()
    {
        _db.RequestLogs.Add(new RequestLog { RequestId = "immutable-1", ModelName = "gpt-4o" });
        await _db.SaveChangesAsync();

        var log = await _db.RequestLogs.FirstAsync(r => r.RequestId == "immutable-1");
        log.ModelName = "tampered";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task RequestLog_CannotBeDeletedViaChangeTracker()
    {
        _db.RequestLogs.Add(new RequestLog { RequestId = "immutable-2", ModelName = "gpt-4o" });
        await _db.SaveChangesAsync();

        var log = await _db.RequestLogs.FirstAsync(r => r.RequestId == "immutable-2");
        _db.RequestLogs.Remove(log);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());
    }
}
