namespace LDK.RideClub.Bot.Domain.Events.Conversation;

/// <summary>
/// Published when message processing completes successfully.
/// </summary>
public sealed record ProcessingCompletedEvent
{
    /// <summary>Gets the messaging platform identifier.</summary>
    public required string Platform { get; init; }

    /// <summary>Gets the platform-specific sender identifier.</summary>
    public required string SenderId { get; init; }

    /// <summary>Gets whether the result requires user confirmation before proceeding.</summary>
    public required bool RequiresConfirmation { get; init; }

    /// <summary>Gets the optional reply message to send back to the user.</summary>
    public string? ReplyMessage { get; init; }
}
