using System.Text.Json;
using Tensu.Core.Common;

namespace Tensu.Api.Infrastructure;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, code, message) = exception switch
        {
            ArgumentException => (400, 40001, exception.Message),
            UnauthorizedAccessException => (401, 40101, "Unauthorized"),
            KeyNotFoundException => (404, 40401, "Resource not found"),
            InvalidOperationException => (400, 40002, exception.Message),
            _ => (500, 50001, "An internal error occurred")
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            code,
            message,
            data = (object?)null
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
