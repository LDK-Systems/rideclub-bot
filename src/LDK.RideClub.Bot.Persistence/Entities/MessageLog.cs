namespace LDK.RideClub.Bot.Persistence.Entities;

/// <summary>
/// Represents a persisted record of an inbound messaging event received by the bot.
/// Used for auditing, debugging, and replay of message processing.
/// </summary>
public sealed class MessageLog
{
    /// <summary>
    /// Gets or sets the auto-generated primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the messaging platform identifier (e.g., "whatsapp", "telegram", "discord").
    /// </summary>
    public required string Platform { get; set; }

    /// <summary>
    /// Gets or sets the unique event identifier from the messaging platform.
    /// </summary>
    public required string EventId { get; set; }

    /// <summary>
    /// Gets or sets the sender's identifier on the messaging platform.
    /// </summary>
    public required string SenderId { get; set; }

    /// <summary>
    /// Gets or sets the conversation or chat identifier.
    /// </summary>
    public required string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the event was received by the bot.
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; set; }

    /// <summary>
    /// Gets or sets the type of payload (e.g., "TextMessage", "Command").
    /// </summary>
    public required string PayloadType { get; set; }

    /// <summary>
    /// Gets or sets the raw JSON payload for debugging purposes. May be null if payload storage is disabled.
    /// </summary>
    public string? RawPayload { get; set; }
}
