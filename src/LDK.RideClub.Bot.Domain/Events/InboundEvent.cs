namespace LDK.RideClub.Bot.Domain.Events;

/// <summary>
/// Represents an inbound event from any messaging platform,
/// normalised into a platform-agnostic shape.
/// </summary>
public sealed record InboundEvent
{
    /// <summary>Gets the unique identifier for this event.</summary>
    public required string EventId { get; init; }

    /// <summary>Gets the messaging platform identifier (e.g., "whatsapp", "telegram").</summary>
    public required string Platform { get; init; }

    /// <summary>Gets the platform-specific sender identifier.</summary>
    public required string SenderId { get; init; }

    /// <summary>Gets the platform-specific conversation identifier.</summary>
    public required string ConversationId { get; init; }

    /// <summary>Gets the timestamp when the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the event payload containing the message or command data.</summary>
    public required EventPayload Payload { get; init; }
}
