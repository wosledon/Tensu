using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Maps upstream provider error codes to standardized platform error codes.
/// </summary>
public static class ErrorCodeMapper
{
    /// <summary>
    /// Map an upstream HTTP status code and error type to a platform error code.
    /// </summary>
    public static string Map(int statusCode, string? upstreamErrorType = null)
    {
        if (statusCode == 400)
            return PlatformErrorCodes.UpstreamBadRequest;
        if (statusCode == 401)
            return PlatformErrorCodes.UpstreamUnauthorized;
        if (statusCode == 403)
            return PlatformErrorCodes.UpstreamForbidden;
        if (statusCode == 404)
            return PlatformErrorCodes.UpstreamNotFound;
        if (statusCode == 409)
            return PlatformErrorCodes.UpstreamConflict;
        if (statusCode == 429)
            return PlatformErrorCodes.UpstreamRateLimited;
        if (statusCode >= 500)
            return PlatformErrorCodes.UpstreamServerError;

        return PlatformErrorCodes.UnknownError;
    }

    /// <summary>
    /// Build a standardized error response for the gateway.
    /// </summary>
    public static object BuildErrorResponse(string errorCode, string message, string errorType = "proxy_error")
    {
        return new
        {
            error = new
            {
                message,
                type = errorType,
                code = errorCode
            }
        };
    }

    /// <summary>
    /// Map a RequestStatus and optional error code/message to a platform error code and response.
    /// </summary>
    public static (string Code, object Response) MapGatewayError(RequestStatus status, string? errorMessage = null, int? statusCode = null)
    {
        var message = errorMessage ?? status switch
        {
            RequestStatus.Success => "Success",
            RequestStatus.Failed => "Request failed",
            RequestStatus.Timeout => "Request timed out",
            RequestStatus.RateLimited => "Rate limit exceeded",
            _ => "Unknown error"
        };

        var code = statusCode.HasValue ? Map(statusCode.Value) : PlatformErrorCodes.UnknownError;
        return (code, BuildErrorResponse(code, message));
    }
}
