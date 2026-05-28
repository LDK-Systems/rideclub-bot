// ---------------------------------------------------------------------------
// RideClub Bot — MassTransitServiceCollectionExtensions (Req 5.3, 5.4, 5.5, 9.5)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Persistence;
using LDK.RideClub.Bot.Persistence.Entities;
using LDK.RideClub.Bot.Sagas;

using MassTransit;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Extension methods for registering MassTransit services
/// on the <see cref="IServiceCollection"/>.
/// </summary>
internal static class MassTransitServiceCollectionExtensions
{
    /// <summary>
    /// Registers MassTransit with the conversation saga state machine,
    /// EF Core repository using <see cref="BotDbContext"/>, and in-memory transport.
    /// OpenTelemetry instrumentation is built-in with MassTransit 9.x.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBotMassTransit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _ = services.AddMassTransit(cfg =>
        {
#pragma warning disable IDE0058 // MassTransit fluent configuration — return values are intentionally unused
            cfg.AddSagaStateMachine<ConversationStateMachine, ConversationSagaInstance>();

            cfg.SetEntityFrameworkSagaRepositoryProvider(r =>
            {
                r.ExistingDbContext<BotDbContext>();
            });

            cfg.UsingInMemory((context, inMemoryCfg) =>
            {
                inMemoryCfg.ConfigureEndpoints(context);
            });
#pragma warning restore IDE0058
        });

        return services;
    }
}
