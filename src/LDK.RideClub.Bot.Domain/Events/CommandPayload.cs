namespace LDK.RideClub.Bot.Domain.Events;

/// <summary>
/// Payload representing a parsed command from a user.
/// </summary>
public sealed record CommandPayload : EventPayload
{
    /// <summary>Gets the name of the command.</summary>
    public required string CommandName { get; init; }

    /// <summary>Gets the command arguments as key-value pairs.</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; init; }
        = new Dictionary<string, string>();
}
