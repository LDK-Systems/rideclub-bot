namespace LDK.RideClub.Bot.Domain.Commands;

/// <summary>
/// Represents a message to be sent to a user on a messaging platform.
/// </summary>
public sealed record OutboundMessage
{
    /// <summary>Gets the target messaging platform identifier.</summary>
    public required string Platform { get; init; }

    /// <summary>Gets the platform-specific recipient identifier.</summary>
    public required string RecipientId { get; init; }

    /// <summary>Gets the platform-specific conversation identifier.</summary>
    public required string ConversationId { get; init; }

    /// <summary>Gets the message content to send.</summary>
    public required MessageContent Content { get; init; }
}
