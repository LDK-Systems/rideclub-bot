// ---------------------------------------------------------------------------
// RideClub Bot — MassTransitModule (Req 1.3, 1.5, 2.6, 5.1)
// ---------------------------------------------------------------------------

using Autofac;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Services;

namespace LDK.RideClub.Bot.Modules;

/// <summary>
/// Unified Autofac module for MassTransit infrastructure. Replaces both
/// <c>MediatorModule</c> and <c>SagaModule</c>. Registers the
/// <see cref="EventDispatcher"/> as the <see cref="IEventProcessor"/>
/// implementation with scoped lifetime.
/// </summary>
/// <remarks>
/// MassTransit mediator and bus registrations are handled via
/// <see cref="Configuration.MassTransitServiceCollectionExtensions"/> on the
/// <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>.
/// Autofac automatically picks up those registrations via its integration
/// with the Microsoft DI container.
/// </remarks>
#pragma warning disable CA1812 // Instantiated in Program.cs composition root
internal sealed class MassTransitModule : Module
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
