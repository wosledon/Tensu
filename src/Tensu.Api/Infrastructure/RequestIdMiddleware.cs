namespace Tensu.Api.Infrastructure;

/// <summary>
/// Assigns a request id to every incoming request, exposes it via the X-Request-Id
/// response header, and adds it to the log scope so structured (JSON) logs carry it
/// end-to-end. Controllers may reuse the id from HttpContext.Items["RequestId"].
/// </summary>
public class RequestIdMiddleware
{
    public const string ItemKey = "RequestId";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestIdMiddleware> _logger;

    public RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 64)
            requestId = Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = requestId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Request-Id"))
                context.Response.Headers["X-Request-Id"] = requestId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
        {
            await _next(context);
        }
    }
}
