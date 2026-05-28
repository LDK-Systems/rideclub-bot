namespace LDK.RideClub.Bot.Domain.Responses;

/// <summary>
/// Represents the result of processing a command through the mediator pipeline.
/// </summary>
public sealed record MessageProcessingResult
{
    /// <summary>Gets a value indicating whether processing succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the optional reply message to send back to the user.</summary>
    public string? ReplyMessage { get; init; }

    /// <summary>Gets the optional error reason when processing fails.</summary>
    public string? ErrorReason { get; init; }
}
