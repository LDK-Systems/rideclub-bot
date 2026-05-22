using LDK.RideClub.Bot.Abstractions.Messaging;

namespace LDK.RideClub.Bot.Services;

/// <summary>
/// Resolves the correct messaging adapter based on the platform identifier.
/// Builds a case-insensitive lookup dictionary from all registered adapters at construction time.
/// </summary>
/// <remarks>
/// This class is instantiated by the DI container (Autofac) and accessed through the
/// <see cref="IAdapterRegistry"/> interface.
/// </remarks>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class AdapterRegistry : IAdapterRegistry
#pragma warning restore CA1812
{
    private readonly Dictionary<string, IMessagingAdapter> _adapters;

    /// <summary>
    /// Initialises a new instance of the <see cref="AdapterRegistry"/> class.
    /// </summary>
    /// <param name="adapters">All registered messaging adapters from the DI container.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two or more adapters declare the same platform identifier (case-insensitive).
    /// </exception>
    public AdapterRegistry(IEnumerable<IMessagingAdapter> adapters)
    {
        _adapters = new Dictionary<string, IMessagingAdapter>(StringComparer.OrdinalIgnoreCase);

        foreach (IMessagingAdapter adapter in adapters)
        {
            if (!_adapters.TryAdd(adapter.PlatformId, adapter))
            {
                throw new InvalidOperationException(
                    $"Duplicate platform identifier '{adapter.PlatformId}' detected. " +
                    $"Each messaging adapter must declare a unique PlatformId.");
            }
        }
    }

    /// <inheritdoc />
    public IMessagingAdapter? GetAdapter(string platformId)
    {
        return _adapters.GetValueOrDefault(platformId);
    }

    /// <inheritdoc />
    public IEnumerable<string> RegisteredPlatforms => _adapters.Keys;
}
