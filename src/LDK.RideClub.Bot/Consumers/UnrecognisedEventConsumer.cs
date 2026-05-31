// ---------------------------------------------------------------------------
// RideClub Bot — UnrecognisedEventConsumer
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Domain.Events;

using MassTransit;

namespace LDK.RideClub.Bot.Consumers;

/// <summary>
/// Consumes <see cref="UnrecognisedEventNotification"/> messages published when
/// the event dispatcher cannot map an inbound event payload to a known command.
/// Logs a warning identifying the unrecognised payload type.
/// </summary>
internal sealed partial class UnrecognisedEventConsumer(
    ILogger<UnrecognisedEventConsumer> logger)
    : IConsumer<UnrecognisedEventNotification>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<UnrecognisedEventNotification> context)
    {
        LogUnrecognisedEvent(logger, context.Message.Event.Payload.GetType().Name);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Unrecognised event: {PayloadType}")]
    private static partial void LogUnrecognisedEvent(ILogger logger, string payloadType);
}
