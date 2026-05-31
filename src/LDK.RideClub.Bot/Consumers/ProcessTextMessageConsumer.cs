// ---------------------------------------------------------------------------
// RideClub Bot — ProcessTextMessageConsumer
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Domain.Responses;

using MassTransit;

namespace LDK.RideClub.Bot.Consumers;

/// <summary>
/// Consumes <see cref="ProcessTextMessageCommand"/> and publishes outcome events
/// (<see cref="ProcessingCompletedEvent"/> or <see cref="ProcessingFaultedEvent"/>)
/// to the MassTransit bus for the conversation saga state machine.
/// </summary>
internal sealed partial class ProcessTextMessageConsumer(
    IBus bus,
    ILogger<ProcessTextMessageConsumer> logger)
    : IConsumer<ProcessTextMessageCommand>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ProcessTextMessageCommand> context)
    {
        ProcessTextMessageCommand command = context.Message;

        LogProcessingTextMessage(logger, command.SenderId, command.Platform);

        // Process the text message (placeholder — future business logic)
        MessageProcessingResult result = new() { Success = true };

        // Publish outcome event to bus for saga state machine
        if (result.Success)
        {
            await bus.Publish(new ProcessingCompletedEvent
            {
                Platform = command.Platform,
                SenderId = command.SenderId,
                RequiresConfirmation = false,
                ReplyMessage = result.ReplyMessage,
            }, context.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            await bus.Publish(new ProcessingFaultedEvent
            {
                Platform = command.Platform,
                SenderId = command.SenderId,
                ErrorReason = result.ErrorReason ?? "Unknown error",
            }, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Processing text message from {SenderId} on {Platform}")]
    private static partial void LogProcessingTextMessage(ILogger logger, string senderId, string platform);
}
