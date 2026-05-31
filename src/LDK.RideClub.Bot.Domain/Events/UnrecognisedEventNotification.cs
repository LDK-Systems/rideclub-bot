namespace LDK.RideClub.Bot.Domain.Events;

/// <summary>
/// Published when the <see cref="InboundEvent"/> payload type is not recognised
/// by the event dispatcher and cannot be mapped to a known command.
/// </summary>
public sealed record UnrecognisedEventNotification(InboundEvent Event);
