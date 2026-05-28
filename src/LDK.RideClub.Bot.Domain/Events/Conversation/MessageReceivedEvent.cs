namespace LDK.RideClub.Bot.Domain.Events.Conversation;

/// <summary>
/// Published when a new message is received from a user, triggering the conversation state machine.
/// </summary>
public sealed record MessageReceivedEvent
{
    /// <summary>Gets the messaging platform identifier.</summary>
    public required string Platform { get; init; }

    /// <summary>Gets the platform-specific sender identifier.</summary>
    public required string SenderId { get; init; }

    /// <summary>Gets the conversation identifier.</summary>
    public required string ConversationId { get; init; }

    /// <summary>Gets the unique event identifier.</summary>
    public required string EventId { get; init; }

    /// <summary>Gets the timestamp when the message was received.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
