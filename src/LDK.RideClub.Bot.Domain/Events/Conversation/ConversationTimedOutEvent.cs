namespace LDK.RideClub.Bot.Domain.Events.Conversation;

/// <summary>
/// Published when a conversation times out due to inactivity.
/// </summary>
public sealed record ConversationTimedOutEvent
{
    /// <summary>Gets the messaging platform identifier.</summary>
    public required string Platform { get; init; }

    /// <summary>Gets the platform-specific sender identifier.</summary>
    public required string SenderId { get; init; }

    /// <summary>Gets the reason the conversation timed out.</summary>
    public required string Reason { get; init; }
}
