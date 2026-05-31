// ---------------------------------------------------------------------------
// RideClub Bot — ProcessBotCommandConsumer
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Domain.Responses;

using MassTransit;

namespace LDK.RideClub.Bot.Consumers;

/// <summary>
/// MassTransit consumer for <see cref="ProcessBotCommandCommand"/>.
/// Processes the bot command and publishes outcome events to the bus
/// for the conversation saga state machine.
/// </summary>
internal sealed partial class ProcessBotCommandConsumer(
    IBus bus,
    ILogger<ProcessBotCommandConsumer> logger)
    : IConsumer<ProcessBotCommandCommand>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ProcessBotCommandCommand> context)
    {
        ProcessBotCommandCommand command = context.Message;

        LogProcessingBotCommand(logger, command.CommandName, command.SenderId, command.Platform);

        // Process the bot command (placeholder — future business logic)
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
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Processing bot command {CommandName} from {SenderId} on {Platform}")]
    private static partial void LogProcessingBotCommand(ILogger logger, string commandName, string senderId, string platform);
}
