namespace LDK.RideClub.Bot.Domain.Commands;

/// <summary>
/// Command to process a plain text message from a user.
/// </summary>
public sealed record ProcessTextMessageCommand
{
    /// <summary>Gets the platform-specific sender identifier.</summary>
    public required string SenderId { get; init; }

    /// <summary>Gets the platform-specific conversation identifier.</summary>
    public required string ConversationId { get; init; }

    /// <summary>Gets the messaging platform identifier.</summary>
    public required string Platform { get; init; }

    /// <summary>Gets the text content of the message.</summary>
    public required string MessageText { get; init; }

    /// <summary>Gets the timestamp when the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
