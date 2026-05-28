// ---------------------------------------------------------------------------
// RideClub Bot — SagaModule (Req 5.3, 5.4, 5.5)
// ---------------------------------------------------------------------------

using Autofac;

namespace LDK.RideClub.Bot.Modules;

/// <summary>
/// Autofac module responsible for registering MassTransit saga infrastructure.
/// The core MassTransit registration (state machine, EF Core repository, transport)
/// is handled via <see cref="Configuration.MassTransitServiceCollectionExtensions.AddBotMassTransit"/>
/// on the <see cref="IServiceCollection"/>.
/// This module exists as the Autofac composition point for any future
/// saga-related registrations.
/// </summary>
#pragma warning disable CA1812 // Instantiated in Program.cs composition root
internal sealed class SagaModule : Module
#pragma warning restore CA1812
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // MassTransit and the ConversationStateMachine are registered via
        // IServiceCollection in MassTransitServiceCollectionExtensions.AddBotMassTransit().
        // Autofac automatically picks up IServiceCollection registrations via its
        // integration with the Microsoft DI container.
        //
        // This module exists as the Autofac composition point for any future
        // saga-related registrations (custom consumers, activities, etc.).
    }
}
