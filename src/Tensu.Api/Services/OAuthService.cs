using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class OAuthService
{
    private readonly TensuDbContext _db;
    private readonly OAuthProviderService _providerService;
    private readonly JwtService _jwt;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        TensuDbContext db,
        OAuthProviderService providerService,
        JwtService jwt,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<OAuthService> logger)
    {
        _db = db;
        _providerService = providerService;
        _jwt = jwt;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<object>> GetLoginProvidersAsync()
    {
        var providers = await _providerService.GetEnabledAsync();
        return providers.Select(p => new
        {
            p.Id,
            p.Name,
            p.DisplayName,
            p.Protocol
        }).ToList();
    }

    public async Task<(bool success, string? redirectUrl, string? error)> ChallengeAsync(string providerName, string redirectUriBase)
    {
        var provider = await _providerService.GetEnabledByNameAsync(providerName);
        if (provider == null)
            return (false, null, $"OAuth provider '{providerName}' is not enabled or does not exist.");

        if (provider.Protocol == "OIDC" && string.IsNullOrEmpty(provider.AuthorizationEndpoint))
        {
            if (string.IsNullOrEmpty(provider.Issuer))
                return (false, null, "OIDC provider must configure either AuthorizationEndpoint or Issuer for discovery.");

            provider = await DiscoverEndpointsAsync(provider);
            if (string.IsNullOrEmpty(provider.AuthorizationEndpoint))
                return (false, null, "OIDC discovery did not return an authorization endpoint.");
        }

        if (string.IsNullOrEmpty(provider.AuthorizationEndpoint))
            return (false, null, "Authorization endpoint is not configured.");

        var state = GenerateState();
        var nonce = GenerateState();
        var redirectUri = $"{redirectUriBase.TrimEnd('/')}/oauth/callback";

        _cache.Set(CacheKeyForState(state), new OAuthState(providerName, nonce, redirectUri), TimeSpan.FromMinutes(10));

        var url = QueryHelpers.AddQueryString(provider.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = provider.ClientId,
            ["response_type"] = "code",
            ["scope"] = provider.Scope,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["nonce"] = nonce
        });

        return (true, url, null);
    }

    public async Task<(bool success, string? token, string? error)> CallbackAsync(
        string? code,
        string? state,
        string? error,
        string redirectUriBase)
    {
        if (!string.IsNullOrEmpty(error))
            return (false, null, $"OAuth provider error: {error}");

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return (false, null, "Missing code or state parameter.");

        if (!_cache.TryGetValue(CacheKeyForState(state), out OAuthState? oauthState) || oauthState == null)
            return (false, null, "Invalid or expired state.");

        _cache.Remove(CacheKeyForState(state));

        var provider = await _providerService.GetEnabledByNameAsync(oauthState.ProviderName);
        if (provider == null)
            return (false, null, $"OAuth provider '{oauthState.ProviderName}' no longer exists.");

        if (provider.Protocol == "OIDC" && string.IsNullOrEmpty(provider.TokenEndpoint) && !string.IsNullOrEmpty(provider.Issuer))
            provider = await DiscoverEndpointsAsync(provider);

        if (string.IsNullOrEmpty(provider.TokenEndpoint))
            return (false, null, "Token endpoint is not configured.");

        var clientSecret = _providerService.DecryptSecret(provider.ClientSecret);
        var redirectUri = oauthState.RedirectUri ?? $"{redirectUriBase.TrimEnd('/')}/oauth/callback";

        var tokenResponse = await ExchangeCodeAsync(provider.TokenEndpoint, code, redirectUri, provider.ClientId, clientSecret);
        if (!tokenResponse.success)
            return (false, null, tokenResponse.error);

        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (provider.Protocol == "OIDC" && !string.IsNullOrEmpty(tokenResponse.idToken))
        {
            var idClaims = ValidateIdToken(tokenResponse.idToken, provider, oauthState.Nonce);
            if (!string.IsNullOrEmpty(idClaims.error))
                return (false, null, idClaims.error);
            foreach (var kv in idClaims.claims)
                claims[kv.Key] = kv.Value;
        }

        if (!string.IsNullOrEmpty(provider.UserInfoEndpoint) && !string.IsNullOrEmpty(tokenResponse.accessToken))
        {
            var userInfo = await GetUserInfoAsync(provider.UserInfoEndpoint, tokenResponse.accessToken);
            foreach (var kv in userInfo)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                    claims[kv.Key] = kv.Value;
            }
        }

        var sub = claims.GetValueOrDefault("sub");
        var email = claims.GetValueOrDefault("email");
        var name = claims.GetValueOrDefault("name");
        var picture = claims.GetValueOrDefault("picture");

        if (string.IsNullOrEmpty(sub))
            return (false, null, "OIDC provider did not return a subject claim.");

        var user = await FindOrCreateUserAsync(sub, email, name, picture, provider.Name);
        var jwtToken = _jwt.GenerateToken(user.Id, user.Username, user.Role.ToString(), user.OrganizationId);
        return (true, jwtToken, null);
    }

    public async Task<User> FindOrCreateUserAsync(string externalId, string? email, string? name, string? picture, string providerName)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.ExternalId == externalId && u.AuthProvider == providerName);

        if (user == null && !string.IsNullOrEmpty(email))
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user != null)
        {
            user.ExternalId = externalId;
            user.AuthProvider = providerName;
            user.Email = email ?? user.Email;
            user.DisplayName = name ?? user.DisplayName;
            user.PictureUrl = picture ?? user.PictureUrl;
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var org = await _db.Organizations.FirstOrDefaultAsync()
                ?? await EnsureDefaultOrganizationAsync();

            var username = !string.IsNullOrEmpty(email) ? email : externalId;
            if (await _db.Users.AnyAsync(u => u.Username == username))
                username = $"{username}_{Guid.NewGuid():N}";

            user = new User
            {
                OrganizationId = org.Id,
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                Email = email,
                DisplayName = name,
                Role = UserRole.Developer,
                AuthProvider = providerName,
                ExternalId = externalId,
                PictureUrl = picture,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
        }

        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<OAuthProvider> DiscoverEndpointsAsync(OAuthProvider provider)
    {
        if (string.IsNullOrEmpty(provider.Issuer))
            return provider;

        var discoveryUrl = $"{provider.Issuer.TrimEnd('/')}/.well-known/openid-configuration";
        try
        {
            var client = _httpClientFactory.CreateClient();
            var doc = await client.GetFromJsonAsync<JsonObject>(discoveryUrl);
            if (doc != null)
            {
                provider.AuthorizationEndpoint = doc["authorization_endpoint"]?.GetValue<string>() ?? provider.AuthorizationEndpoint;
                provider.TokenEndpoint = doc["token_endpoint"]?.GetValue<string>() ?? provider.TokenEndpoint;
                provider.UserInfoEndpoint = doc["userinfo_endpoint"]?.GetValue<string>() ?? provider.UserInfoEndpoint;
                if (string.IsNullOrEmpty(provider.Issuer))
                    provider.Issuer = doc["issuer"]?.GetValue<string>() ?? provider.Issuer;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OIDC discovery failed for {Issuer}", provider.Issuer);
        }
        return provider;
    }

    private async Task<(bool success, string? accessToken, string? idToken, string? error)> ExchangeCodeAsync(
        string tokenEndpoint,
        string code,
        string redirectUri,
        string clientId,
        string clientSecret)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(parameters));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OAuth token exchange failed: {Status} {Body}", response.StatusCode, content);
            return (false, null, null, $"Token exchange failed: {response.StatusCode}");
        }

        var doc = JsonNode.Parse(content)?.AsObject();
        if (doc == null)
            return (false, null, null, "Invalid token response.");

        var accessToken = doc["access_token"]?.GetValue<string>();
        var idToken = doc["id_token"]?.GetValue<string>();

        return (true, accessToken, idToken, null);
    }

    private (Dictionary<string, string> claims, string? error) ValidateIdToken(
        string idToken,
        OAuthProvider provider,
        string? expectedNonce)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(idToken);
            var claims = token.Claims.ToDictionary(c => c.Type, c => c.Value, StringComparer.OrdinalIgnoreCase);

            var exp = token.Payload.Expiration;
            if (exp.HasValue && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= exp.Value)
                return (claims, "ID token has expired.");

            if (claims.TryGetValue("aud", out var aud) && aud != provider.ClientId)
                return (claims, "ID token audience does not match client_id.");

            if (claims.TryGetValue("iss", out var iss) && !string.IsNullOrEmpty(provider.Issuer) && iss != provider.Issuer)
                return (claims, "ID token issuer does not match configured issuer.");

            if (!string.IsNullOrEmpty(expectedNonce) && claims.GetValueOrDefault("nonce") != expectedNonce)
                return (claims, "ID token nonce mismatch.");

            return (claims, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse id_token");
            return (new Dictionary<string, string>(), "Invalid id_token.");
        }
    }

    private async Task<Dictionary<string, string>> GetUserInfoAsync(string userInfoEndpoint, string accessToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var doc = await client.GetFromJsonAsync<JsonObject>(userInfoEndpoint);
            return doc?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.GetValue<string>() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UserInfo endpoint call failed");
            return new Dictionary<string, string>();
        }
    }

    private async Task<Organization> EnsureDefaultOrganizationAsync()
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
        return org;
    }

    private static string GenerateState() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string CacheKeyForState(string state) =>
        $"oauth_state:{state}";

    private record OAuthState(string ProviderName, string Nonce, string RedirectUri);
}

internal static class DictionaryExtensions
{
    public static string? GetValueOrDefault(this Dictionary<string, string> dict, string key)
    {
        dict.TryGetValue(key, out var value);
        return value;
    }
}
