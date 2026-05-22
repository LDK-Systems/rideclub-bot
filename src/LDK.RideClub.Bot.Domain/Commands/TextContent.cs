namespace LDK.RideClub.Bot.Domain.Commands;

/// <summary>
/// Content representing a plain text message to send.
/// </summary>
public sealed record TextContent : MessageContent
{
    /// <summary>Gets the text content of the message.</summary>
    public required string Text { get; init; }
}
