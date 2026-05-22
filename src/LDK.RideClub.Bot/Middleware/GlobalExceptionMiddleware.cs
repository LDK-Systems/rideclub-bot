// ---------------------------------------------------------------------------
// RideClub Bot — GlobalExceptionMiddleware (Req 7.5)
// ---------------------------------------------------------------------------

using System.Net.Mime;

namespace LDK.RideClub.Bot.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions from the request pipeline,
/// logs the error, and returns a generic 500 JSON response.
/// </summary>
#pragma warning disable CA1812 // Instantiated via middleware pipeline
internal sealed partial class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
#pragma warning restore CA1812
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger = logger;

    /// <summary>
    /// Invokes the middleware, wrapping the downstream pipeline in exception handling.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
#pragma warning disable CA1031 // Global exception handler must catch all exceptions
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUnhandledException(_logger, context.Request.Method, context.Request.Path, ex);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(new { error = "Internal server error" }).ConfigureAwait(false);
        }
    }
#pragma warning restore CA1031

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unhandled exception processing {HttpMethod} {RequestPath}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        string httpMethod,
        string requestPath,
        Exception exception);
}
