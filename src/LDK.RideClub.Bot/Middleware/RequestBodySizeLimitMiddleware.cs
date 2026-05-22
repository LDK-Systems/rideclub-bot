// ---------------------------------------------------------------------------
// RideClub Bot — RequestBodySizeLimitMiddleware (Req 7.9)
// ---------------------------------------------------------------------------

namespace LDK.RideClub.Bot.Middleware;

/// <summary>
/// Middleware that enforces a maximum request body size of 1 MB (1,048,576 bytes)
/// on incoming requests. Returns HTTP 413 Content Too Large when the limit is exceeded.
/// </summary>
/// <remarks>
/// <para>
/// If the <c>Content-Length</c> header is present and exceeds the limit, the request
/// is rejected immediately without reading the body. For chunked transfers without a
/// <c>Content-Length</c> header, the middleware enables request buffering and checks
/// the actual body size.
/// </para>
/// <para>
/// This middleware should be registered before webhook endpoints in the pipeline,
/// after <see cref="GlobalExceptionMiddleware"/>.
/// </para>
/// </remarks>
#pragma warning disable CA1812 // Instantiated via middleware pipeline
internal sealed partial class RequestBodySizeLimitMiddleware(
    RequestDelegate next,
    ILogger<RequestBodySizeLimitMiddleware> logger)
#pragma warning restore CA1812
{
    /// <summary>
    /// Maximum allowed request body size in bytes (1 MB).
    /// </summary>
    internal const int MaxBodySizeBytes = 1_048_576;

    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestBodySizeLimitMiddleware> _logger = logger;

    /// <summary>
    /// Processes the HTTP request and rejects it with 413 if the body exceeds 1 MB.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Check Content-Length header first for an early rejection
        if (context.Request.ContentLength.HasValue &&
            context.Request.ContentLength.Value > MaxBodySizeBytes)
        {
            LogContentLengthExceeded(
                _logger,
                context.Request.ContentLength.Value,
                MaxBodySizeBytes,
                context.Request.Method,
                context.Request.Path);

            await WriteContentTooLargeResponseAsync(context).ConfigureAwait(false);
            return;
        }

        // For POST requests without Content-Length (chunked transfers), buffer the body and check size
        if (!context.Request.ContentLength.HasValue &&
            context.Request.Method == HttpMethods.Post)
        {
            context.Request.EnableBuffering();

            MemoryStream buffer = new();
            await using (buffer.ConfigureAwait(false))
            {
                byte[] rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);

                try
                {
                    long totalBytesRead = 0;
                    int bytesRead;

                    while ((bytesRead = await context.Request.Body.ReadAsync(
                        rentedBuffer.AsMemory(), context.RequestAborted).ConfigureAwait(false)) > 0)
                    {
                        totalBytesRead += bytesRead;

                        if (totalBytesRead > MaxBodySizeBytes)
                        {
                            LogChunkedBodyExceeded(
                                _logger,
                                MaxBodySizeBytes,
                                context.Request.Method,
                                context.Request.Path);

                            await WriteContentTooLargeResponseAsync(context).ConfigureAwait(false);
                            return;
                        }

                        await buffer.WriteAsync(
                            rentedBuffer.AsMemory(0, bytesRead), context.RequestAborted).ConfigureAwait(false);
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer);
                }

                // Reset the body stream so downstream middleware can read it
                buffer.Position = 0;
                context.Request.Body = buffer;

                await _next(context).ConfigureAwait(false);
            }

            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static async Task WriteContentTooLargeResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            new { error = "Content Too Large" },
            context.RequestAborted).ConfigureAwait(false);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Request rejected: Content-Length {ContentLength} exceeds maximum allowed size of {MaxSize} bytes for {HttpMethod} {RequestPath}")]
    private static partial void LogContentLengthExceeded(
        ILogger logger,
        long contentLength,
        int maxSize,
        string httpMethod,
        string requestPath);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Request rejected: chunked body exceeds maximum allowed size of {MaxSize} bytes for {HttpMethod} {RequestPath}")]
    private static partial void LogChunkedBodyExceeded(
        ILogger logger,
        int maxSize,
        string httpMethod,
        string requestPath);
}
