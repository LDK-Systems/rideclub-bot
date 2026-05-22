namespace LDK.RideClub.Bot.Abstractions.Messaging;

/// <summary>
/// Resolves the correct messaging adapter based on the platform identifier from the URL path.
/// </summary>
public interface IAdapterRegistry
{
    /// <summary>
    /// Gets the messaging adapter for the specified platform identifier.
    /// </summary>
    /// <param name="platformId">The platform identifier to look up (case-insensitive).</param>
    /// <returns>The matching adapter, or null if no adapter is registered for the platform.</returns>
    public IMessagingAdapter? GetAdapter(string platformId);

    /// <summary>
    /// Gets the collection of registered platform identifiers.
    /// </summary>
    public IEnumerable<string> RegisteredPlatforms { get; }
}
