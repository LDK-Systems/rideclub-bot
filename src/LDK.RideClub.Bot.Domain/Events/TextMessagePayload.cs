namespace LDK.RideClub.Bot.Domain.Events;

/// <summary>
/// Payload representing a plain text message from a user.
/// </summary>
public sealed record TextMessagePayload : EventPayload
{
    /// <summary>Gets the text content of the message.</summary>
    public required string Text { get; init; }
}
