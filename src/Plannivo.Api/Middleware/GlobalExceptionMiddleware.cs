using System.Net;
using System.Text.Json;
using Plannivo.Contracts.Common;
using Microsoft.AspNetCore.Mvc;

namespace Plannivo.Api.Middleware;

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
            // Sanitize user-controlled values before logging to prevent log-injection.
            _logger.LogError(ex, "Unhandled exception occurred for request {Method} {Path}",
                SanitizeForLog(context.Request.Method),
                SanitizeForLog(context.Request.Path.ToString()));

            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Strips CR/LF characters from user-supplied strings before they reach the log,
    /// preventing log-injection (CWE-117 / OWASP log forging).
    /// </summary>
    private static string SanitizeForLog(string input) =>
        input.Replace("\r", "\\r", StringComparison.Ordinal)
             .Replace("\n", "\\n", StringComparison.Ordinal);

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, exception.Message),
            InvalidOperationException => (HttpStatusCode.Conflict, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse.Fail(message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
