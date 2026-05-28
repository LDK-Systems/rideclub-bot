namespace LDK.RideClub.Bot.Domain.Events.Conversation;

/// <summary>
/// Published when message processing fails due to a domain error.
/// </summary>
public sealed record ProcessingFaultedEvent
{
    /// <summary>Gets the messaging platform identifier.</summary>
    public required string Platform { get; init; }

    /// <summary>Gets the platform-specific sender identifier.</summary>
    public required string SenderId { get; init; }

    /// <summary>Gets the reason the processing failed.</summary>
    public required string ErrorReason { get; init; }
}
