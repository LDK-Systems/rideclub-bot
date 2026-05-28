// ---------------------------------------------------------------------------
// RideClub Bot — MessagingModule (Req 1.3, 4.4)
// ---------------------------------------------------------------------------

using Autofac;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Services;

namespace LDK.RideClub.Bot.Modules;

/// <summary>
/// Autofac module responsible for registering core messaging infrastructure
/// (the adapter registry). Adapter modules themselves are discovered and loaded
/// automatically via Autofac's <c>RegisterAssemblyModules</c> in the composition root.
/// </summary>
#pragma warning disable CA1812 // Instantiated in Program.cs composition root
internal sealed class MessagingModule : Autofac.Module
#pragma warning restore CA1812
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // Register AdapterRegistry as singleton — builds the lookup dictionary once at startup.
        _ = builder.RegisterType<AdapterRegistry>()
            .As<IAdapterRegistry>()
            .SingleInstance();
    }
}
