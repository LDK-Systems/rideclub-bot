// ---------------------------------------------------------------------------
// RideClub Bot — MediatorModule (Legacy — scheduled for replacement by MassTransitModule)
// ---------------------------------------------------------------------------

using Autofac;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Services;

namespace LDK.RideClub.Bot.Modules;

/// <summary>
/// Legacy Autofac module that registers the <see cref="EventDispatcher"/> as the
/// <see cref="IEventProcessor"/> implementation. MediatR registration has been removed
/// as part of the MassTransit migration — MassTransit mediator now handles dispatch.
/// This module will be replaced by <c>MassTransitModule</c> in a subsequent task.
/// </summary>
#pragma warning disable CA1812 // Instantiated in Program.cs composition root
internal sealed class MediatorModule : Module
#pragma warning restore CA1812
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // Replace LoggingEventProcessor with EventDispatcher
        _ = builder.RegisterType<EventDispatcher>()
            .As<IEventProcessor>()
            .InstancePerLifetimeScope();
    }
}
