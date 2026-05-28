using MassTransit;

namespace LDK.RideClub.Bot.Persistence.Entities;

/// <summary>
/// Represents the persisted state of a conversation saga instance.
/// Tracks multi-turn conversation lifecycle through state machine transitions.
/// </summary>
public sealed class ConversationSagaInstance : SagaStateMachineInstance
{
    /// <summary>
    /// Gets or sets the unique saga correlation identifier.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the composite correlation key in the format "{Platform}:{SenderId}".
    /// Used to correlate messages from the same user on the same platform to this saga instance.
    /// </summary>
    public required string CorrelationKey { get; set; }

    /// <summary>
    /// Gets or sets the current state of the conversation state machine.
    /// </summary>
    public required string CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the messaging platform identifier (e.g., "whatsapp", "telegram").
    /// </summary>
    public required string Platform { get; set; }

    /// <summary>
    /// Gets or sets the sender's identifier on the messaging platform.
    /// </summary>
    public required string SenderId { get; set; }

    /// <summary>
    /// Gets or sets the conversation or chat identifier.
    /// </summary>
    public required string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the saga instance was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the saga instance was last modified.
    /// </summary>
    public DateTimeOffset LastModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the optimistic concurrency token for preventing lost updates.
    /// </summary>
    public uint RowVersion { get; set; }
}
