using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Tests;

public class OAuthProviderServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly EncryptionService _encryption;
    private readonly OAuthProviderService _providerService;
    private readonly OAuthService _oauthService;

    public OAuthProviderServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = "TestKey32BytesForAES256Encryption!!",
                ["Encryption:IV"] = "TestIV16Bytes!!",
                ["Jwt:Key"] = "TestJwtSecretKey32CharsForSigning!!",
                ["Jwt:Issuer"] = "Tensu",
                ["Jwt:Audience"] = "Tensu",
                ["Jwt:ExpiresInMinutes"] = "60"
            })
            .Build();

        _db = new TensuDbContext(new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Database.EnsureCreated();

        _encryption = new EncryptionService(config);
        _providerService = new OAuthProviderService(_db, _encryption);

        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

        var jwt = new JwtService(config);
        var cacheMock = new Mock<IMemoryCache>();
        var loggerMock = new Mock<ILogger<OAuthService>>();

        _oauthService = new OAuthService(
            _db,
            _providerService,
            jwt,
            httpFactoryMock.Object,
            cacheMock.Object,
            config,
            loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static SaveOAuthProviderRequest CreateRequest(string name = "Okta", string secret = "secret-123") => new(
        Name: name,
        DisplayName: "Okta OIDC",
        Protocol: "OIDC",
        ClientId: "client-id",
        ClientSecret: secret,
        AuthorizationEndpoint: "https://example.com/authorize",
        TokenEndpoint: "https://example.com/token",
        UserInfoEndpoint: "https://example.com/userinfo",
        Issuer: "https://example.com",
        Scope: "openid profile email",
        IsEnabled: true);

    [Fact]
    public async Task CreateAsync_EncryptsClientSecret()
    {
        var request = CreateRequest(secret: "my-plain-secret");

        var dto = await _providerService.CreateAsync(request);

        Assert.Equal("***", dto.ClientSecret);
        var entity = await _db.OAuthProviders.FindAsync(dto.Id);
        Assert.NotNull(entity);
        Assert.NotEqual("my-plain-secret", entity!.ClientSecret);
        Assert.Equal("my-plain-secret", _providerService.DecryptSecret(entity.ClientSecret));
    }

    [Fact]
    public async Task GetListAsync_MasksClientSecret()
    {
        await _providerService.CreateAsync(CreateRequest(secret: "secret"));

        var result = await _providerService.GetListAsync(new Core.Common.PagedRequest());

        Assert.Single(result.Items);
        Assert.Equal("***", result.Items[0].ClientSecret);
    }

    [Fact]
    public async Task UpdateAsync_WithoutSecret_PreservesExistingSecret()
    {
        var created = await _providerService.CreateAsync(CreateRequest(secret: "original-secret"));
        var updated = await _providerService.UpdateAsync(created.Id, CreateRequest(secret: "") with { DisplayName = "Updated" });

        Assert.NotNull(updated);
        var entity = await _db.OAuthProviders.FindAsync(created.Id);
        Assert.NotNull(entity);
        Assert.Equal("original-secret", _providerService.DecryptSecret(entity!.ClientSecret));
    }

    [Fact]
    public async Task UpdateAsync_WithSecret_EncryptsNewSecret()
    {
        var created = await _providerService.CreateAsync(CreateRequest(secret: "old-secret"));
        await _providerService.UpdateAsync(created.Id, CreateRequest(secret: "new-secret"));

        var entity = await _db.OAuthProviders.FindAsync(created.Id);
        Assert.Equal("new-secret", _providerService.DecryptSecret(entity!.ClientSecret));
    }

    [Fact]
    public async Task FindOrCreateUserAsync_CreatesNewUser_WhenExternalIdNotFound()
    {
        await EnsureDefaultOrganizationAsync();

        var user = await _oauthService.FindOrCreateUserAsync(
            "external-sub-1",
            "oauth@example.com",
            "OAuth User",
            "https://example.com/pic.png",
            "Okta");

        Assert.Equal("Okta", user.AuthProvider);
        Assert.Equal("external-sub-1", user.ExternalId);
        Assert.Equal("oauth@example.com", user.Email);
        Assert.Equal("OAuth User", user.DisplayName);
        Assert.Equal("https://example.com/pic.png", user.PictureUrl);
        Assert.Equal(UserRole.Developer, user.Role);
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public async Task FindOrCreateUserAsync_UpdatesExistingUser_WhenExternalIdMatches()
    {
        await EnsureDefaultOrganizationAsync();
        var first = await _oauthService.FindOrCreateUserAsync("external-sub-2", "old@example.com", "Old", null, "Okta");

        var updated = await _oauthService.FindOrCreateUserAsync(
            "external-sub-2",
            "new@example.com",
            "New Name",
            "https://example.com/new.png",
            "Okta");

        Assert.Equal(first.Id, updated.Id);
        Assert.Equal("new@example.com", updated.Email);
        Assert.Equal("New Name", updated.DisplayName);
        Assert.Equal("https://example.com/new.png", updated.PictureUrl);
        Assert.True(updated.LastLoginAt >= first.LastLoginAt);
    }

    private async Task EnsureDefaultOrganizationAsync()
    {
        if (!_db.Organizations.Any())
        {
            var org = new Organization
            {
                Name = "Default",
                Path = "/",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Organizations.Add(org);
            await _db.SaveChangesAsync();
            org.Path = $"/{org.Id}/";
            await _db.SaveChangesAsync();
        }
    }
}
