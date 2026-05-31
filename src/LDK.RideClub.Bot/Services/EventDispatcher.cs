// ---------------------------------------------------------------------------
// RideClub Bot — EventDispatcher (Req 2.1–2.6, 8.3, 8.5)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Events.Conversation;

using MassTransit;
using MassTransit.Mediator;

namespace LDK.RideClub.Bot.Services;

/// <summary>
/// Translates inbound webhook events into commands dispatched via MassTransit's
/// scoped mediator and publishes conversation events to the bus for the state machine.
/// Replaces <see cref="LoggingEventProcessor"/>.
/// </summary>
/// <param name="mediator">The MassTransit scoped mediator instance.</param>
/// <param name="bus">The MassTransit bus instance.</param>
/// <param name="logger">The logger instance.</param>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed partial class EventDispatcher(
    IScopedMediator mediator,
    IBus bus,
    ILogger<EventDispatcher> logger) : IEventProcessor
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public async Task ProcessAsync(InboundEvent inboundEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);

        // 1. Publish MessageReceivedEvent to MassTransit Bus (fire-and-forget — independent of command dispatch success)
        await PublishMessageReceivedAsync(inboundEvent, ct).ConfigureAwait(false);

        // 2. Map payload to command and send via mediator
        object? command = MapToCommand(inboundEvent);

        if (command is not null)
        {
            // Send command — mediator dispatches to consumer, exceptions propagate to caller
            await mediator.Send(command, ct).ConfigureAwait(false);
        }
        else
        {
            // Unknown payload type — publish notification via mediator and log warning
            LogUnrecognisedPayloadType(logger, inboundEvent.Payload.GetType().Name, inboundEvent.EventId);
            await mediator.Publish(new UnrecognisedEventNotification(inboundEvent), ct).ConfigureAwait(false);
        }
    }

    private static object? MapToCommand(InboundEvent evt)
    {
        return evt.Payload switch
        {
            TextMessagePayload txt => new ProcessTextMessageCommand
            {
                SenderId = evt.SenderId,
                ConversationId = evt.ConversationId,
                Platform = evt.Platform,
                MessageText = txt.Text,
                Timestamp = evt.Timestamp,
            },
            CommandPayload cmd => new ProcessBotCommandCommand
            {
                SenderId = evt.SenderId,
                ConversationId = evt.ConversationId,
                Platform = evt.Platform,
                CommandName = cmd.CommandName,
                Arguments = cmd.Arguments,
                Timestamp = evt.Timestamp,
            },
            _ => null,
        };
    }

    private async Task PublishMessageReceivedAsync(InboundEvent inboundEvent, CancellationToken ct)
    {
        try
        {
            await bus.Publish(new MessageReceivedEvent
            {
                Platform = inboundEvent.Platform,
                SenderId = inboundEvent.SenderId,
                ConversationId = inboundEvent.ConversationId,
                EventId = inboundEvent.EventId,
                Timestamp = inboundEvent.Timestamp,
            }, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Intentionally catching all exceptions — bus failures must not block processing
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogBusPublishFailed(logger, nameof(MessageReceivedEvent), ex);
        }
    }

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Warning,
        Message = "Unrecognised event payload type '{PayloadType}' for event '{EventId}'")]
    private static partial void LogUnrecognisedPayloadType(ILogger logger, string payloadType, string eventId);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Warning,
        Message = "Failed to publish '{EventType}' to MassTransit bus")]
    private static partial void LogBusPublishFailed(ILogger logger, string eventType, Exception exception);
}
