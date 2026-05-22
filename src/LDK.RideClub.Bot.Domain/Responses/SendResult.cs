namespace LDK.RideClub.Bot.Domain.Responses;

/// <summary>
/// Represents the result of sending a message to a messaging platform.
/// </summary>
public sealed record SendResult
{
    /// <summary>Gets a value indicating whether the send operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message if the send operation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the platform-assigned message identifier on success.</summary>
    public string? PlatformMessageId { get; init; }
}
