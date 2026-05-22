// ---------------------------------------------------------------------------
// RideClub Bot — LoggingEventProcessor (Req 4.4 — placeholder IEventProcessor)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Domain.Events;

namespace LDK.RideClub.Bot.Services;

/// <summary>
/// Placeholder event processor that logs received events.
/// Will be replaced with real processing logic once domain features are implemented.
/// </summary>
/// <param name="logger">The logger instance.</param>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed partial class LoggingEventProcessor(ILogger<LoggingEventProcessor> logger) : IEventProcessor
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public Task ProcessAsync(InboundEvent inboundEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);

        LogEventReceived(logger, inboundEvent.EventId, inboundEvent.Platform, inboundEvent.SenderId);

        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Received inbound event {EventId} from platform '{Platform}' sender '{SenderId}'")]
    private static partial void LogEventReceived(ILogger logger, string eventId, string platform, string senderId);
}
