// ---------------------------------------------------------------------------
// RideClub Bot — IAdapterOptionsRegistration
// ---------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LDK.RideClub.Bot.Abstractions.Messaging;

/// <summary>
/// Defines a contract for adapter assemblies to register their strongly-typed
/// configuration options with the host's <see cref="IServiceCollection"/>.
/// Implementations are discovered via assembly scanning at startup.
/// </summary>
public interface IAdapterOptionsRegistration
{
    /// <summary>
    /// Registers the adapter's options, validators, and configuration bindings
    /// with the service collection.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">The application configuration root.</param>
    public void RegisterOptions(IServiceCollection services, IConfiguration configuration);
}
