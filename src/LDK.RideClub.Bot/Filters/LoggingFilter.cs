using System.Diagnostics;

using MassTransit;

namespace LDK.RideClub.Bot.Filters;

/// <summary>
/// MassTransit send filter that logs command type and unique request ID before and after
/// pipeline execution, including elapsed time in milliseconds.
/// </summary>
/// <typeparam name="T">The message type being sent through the mediator pipeline.</typeparam>
/// <param name="logger">The logger instance.</param>
internal sealed partial class LoggingFilter<T>(
    ILogger<LoggingFilter<T>> logger)
    : IFilter<SendContext<T>>
    where T : class
{
    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        string requestId = Guid.NewGuid().ToString("N")[..8];
        string typeName = typeof(T).Name;

        LogHandling(logger, typeName, requestId);

        var sw = Stopwatch.StartNew();
        await next.Send(context).ConfigureAwait(false);
        sw.Stop();

        LogHandled(logger, typeName, requestId, sw.ElapsedMilliseconds);
    }

    public void Probe(ProbeContext context)
    {
        _ = context.CreateFilterScope("logging");
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
