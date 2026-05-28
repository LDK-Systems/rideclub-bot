// ---------------------------------------------------------------------------
// RideClub Bot — ProcessTextMessageCommandHandler (Placeholder)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Responses;

using MediatR;

namespace LDK.RideClub.Bot.Handlers;

/// <summary>
/// Placeholder handler for <see cref="ProcessTextMessageCommand"/>.
/// Returns a successful result with no reply. Will be replaced with
/// actual business logic in a future task.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI (MediatR handler discovery)
internal sealed class ProcessTextMessageCommandHandler
    : IRequestHandler<ProcessTextMessageCommand, MessageProcessingResult>
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public Task<MessageProcessingResult> Handle(
        ProcessTextMessageCommand request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new MessageProcessingResult
        {
            Success = true,
            ReplyMessage = null
        });
    }
}
