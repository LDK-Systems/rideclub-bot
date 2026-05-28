using System.Diagnostics;

using MediatR;

namespace LDK.RideClub.Bot.Behaviors;

/// <summary>
/// Pipeline behavior that logs request type and unique ID before and after handler execution,
/// including elapsed time in milliseconds.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The MediatR response type.</typeparam>
/// <param name="logger">The logger instance.</param>
internal sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestId = Guid.NewGuid().ToString("N")[..8];
        string typeName = typeof(TRequest).Name;

        LogHandling(logger, typeName, requestId);

        var sw = Stopwatch.StartNew();
#pragma warning disable CA2016 // RequestHandlerDelegate does not accept CancellationToken
        TResponse response = await next().ConfigureAwait(false);
#pragma warning restore CA2016
        sw.Stop();

        LogHandled(logger, typeName, requestId, sw.ElapsedMilliseconds);

        return response;
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Handling {RequestType} [{RequestId}]")]
    private static partial void LogHandling(ILogger logger, string requestType, string requestId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Handled {RequestType} [{RequestId}] in {ElapsedMs}ms")]
    private static partial void LogHandled(ILogger logger, string requestType, string requestId, long elapsedMs);
}
