using LDK.RideClub.Bot.Domain.Responses;

using MediatR;

namespace LDK.RideClub.Bot.Domain.Commands;

/// <summary>
/// Command to process a parsed bot command from a user.
/// </summary>
public sealed record ProcessBotCommandCommand : IRequest<MessageProcessingResult>
{
    /// <summary>Gets the platform-specific sender identifier.</summary>
    public required string SenderId { get; init; }

    /// <summary>Gets the platform-specific conversation identifier.</summary>
    public required string ConversationId { get; init; }

    /// <summary>Gets the messaging platform identifier.</summary>
    public required string Platform { get; init; }

    /// <summary>Gets the name of the command.</summary>
    public required string CommandName { get; init; }

    /// <summary>Gets the command arguments as key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Gets the timestamp when the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
