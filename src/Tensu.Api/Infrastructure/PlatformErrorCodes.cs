namespace Tensu.Api.Infrastructure;

/// <summary>
/// Standardized error codes for the Tensu platform.
/// </summary>
public static class PlatformErrorCodes
{
    // Authentication & Authorization (40001-40099)
    public const string InvalidApiKey = "40001";
    public const string ApiKeyExpired = "40002";
    public const string IpNotWhitelisted = "40003";
    public const string ModelNotAllowed = "40004";
    public const string InvalidToken = "40005";

    // Rate Limiting & Quotas (42000-42099)
    public const string RateLimitExceeded = "42001";
    public const string TokenQuotaExceeded = "42002";
    public const string ConcurrencyLimitExceeded = "42003";

    // Request Validation (42200-42299)
    public const string MissingModelField = "42201";
    public const string InvalidModelFormat = "42202";
    public const string InvalidRequestFormat = "42203";

    // Upstream Provider Errors (50200-50299)
    public const string UpstreamBadRequest = "50201";
    public const string UpstreamUnauthorized = "50202";
    public const string UpstreamForbidden = "50203";
    public const string UpstreamNotFound = "50204";
    public const string UpstreamConflict = "50205";
    public const string UpstreamRateLimited = "50206";
    public const string UpstreamServerError = "50207";
    public const string UpstreamBadGateway = "50208";

    // Platform Errors (50000-50099)
    public const string NoAvailableProviders = "50001";
    public const string ProxyError = "50002";
    public const string CompressionError = "50003";
    public const string CacheError = "50004";

    // Fallback
    public const string UnknownError = "50000";
}
