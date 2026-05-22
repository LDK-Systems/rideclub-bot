// ---------------------------------------------------------------------------
// RideClub Bot — PersistenceModule (Req 9.2, 9.4, 9.5, 9.6, 9.7, 9.8)
// ---------------------------------------------------------------------------

using Autofac;

namespace LDK.RideClub.Bot.Modules;

/// <summary>
/// Autofac module responsible for registering persistence infrastructure.
/// The <see cref="Persistence.BotDbContext"/> is registered via
/// <see cref="Configuration.PersistenceServiceCollectionExtensions.AddBotPersistence"/>
/// on the <see cref="IServiceCollection"/> (standard EF Core pattern).
/// This module registers any additional persistence-related services that
/// benefit from Autofac's module-based composition.
/// </summary>
#pragma warning disable CA1812 // Instantiated in Program.cs composition root
internal sealed class PersistenceModule : Module
#pragma warning restore CA1812
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // BotDbContext and DatabaseMigrationHostedService are registered via
        // IServiceCollection in PersistenceServiceCollectionExtensions.AddBotPersistence().
        // Autofac automatically picks up IServiceCollection registrations via its
        // integration with the Microsoft DI container.
        //
        // This module exists as the Autofac composition point for any future
        // persistence-related registrations (repositories, unit-of-work, etc.).
    }
}
