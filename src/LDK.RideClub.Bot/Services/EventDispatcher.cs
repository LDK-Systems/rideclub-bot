// ---------------------------------------------------------------------------
// RideClub Bot — EventDispatcher (Req 2.1–2.6, 8.1–8.3, 8.5)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Domain.Responses;

using MassTransit;

using MediatR;

namespace LDK.RideClub.Bot.Services;

/// <summary>
/// Translates inbound webhook events into MediatR commands and publishes
/// MassTransit conversation events to drive the state machine.
/// Replaces <see cref="LoggingEventProcessor"/>.
/// </summary>
/// <param name="mediator">The MediatR mediator instance.</param>
/// <param name="bus">The MassTransit bus instance.</param>
/// <param name="logger">The logger instance.</param>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed partial class EventDispatcher(
    IMediator mediator,
    IBus bus,
    ILogger<EventDispatcher> logger) : IEventProcessor
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public async Task ProcessAsync(InboundEvent inboundEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);

        // 1. Publish MessageReceivedEvent to MassTransit (non-blocking on failure)
        await PublishMessageReceivedAsync(inboundEvent, ct).ConfigureAwait(false);

        // 2. Map payload to MediatR command and dispatch
        IRequest<MessageProcessingResult>? command = MapToCommand(inboundEvent);

        if (command is not null)
        {
            // 3. Send command via MediatR — exceptions propagate to caller
            MessageProcessingResult result = await mediator.Send(command, ct).ConfigureAwait(false);

            // 4. Publish outcome event to MassTransit
            await PublishOutcomeAsync(inboundEvent, result, ct).ConfigureAwait(false);
        }
        else
        {
            // Unknown payload type — publish notification and log warning
            LogUnrecognisedPayloadType(logger, inboundEvent.Payload.GetType().Name, inboundEvent.EventId);
            await mediator.Publish(new UnrecognisedEventNotification(inboundEvent), ct).ConfigureAwait(false);
        }
    }

    private static IRequest<MessageProcessingResult>? MapToCommand(InboundEvent evt)
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

    private async Task PublishOutcomeAsync(InboundEvent inboundEvent, MessageProcessingResult result, CancellationToken ct)
    {
        try
        {
            if (result.Success)
            {
                await bus.Publish(new ProcessingCompletedEvent
                {
                    Platform = inboundEvent.Platform,
                    SenderId = inboundEvent.SenderId,
                    RequiresConfirmation = false,
                    ReplyMessage = result.ReplyMessage,
                }, ct).ConfigureAwait(false);
            }
            else
            {
                await bus.Publish(new ProcessingFaultedEvent
                {
                    Platform = inboundEvent.Platform,
                    SenderId = inboundEvent.SenderId,
                    ErrorReason = result.ErrorReason ?? "Unknown error",
                }, ct).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Intentionally catching all exceptions — bus failures must not block processing
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogBusPublishFailed(logger, "OutcomeEvent", ex);
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
