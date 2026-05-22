using LDK.RideClub.Bot.Domain.Events;

namespace LDK.RideClub.Bot.Abstractions.Messaging;

/// <summary>
/// Processes domain events after they have been deserialised from platform-specific payloads.
/// </summary>
public interface IEventProcessor
{
    /// <summary>
    /// Processes an inbound event from a messaging platform.
    /// </summary>
    /// <param name="inboundEvent">The deserialised inbound event to process.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task ProcessAsync(InboundEvent inboundEvent, CancellationToken ct = default);
}
