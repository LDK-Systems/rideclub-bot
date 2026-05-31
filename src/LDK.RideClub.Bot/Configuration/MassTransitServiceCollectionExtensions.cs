// ---------------------------------------------------------------------------
// RideClub Bot — MassTransitServiceCollectionExtensions (Req 1.1, 1.2, 1.4, 3.4, 3.5, 3.6, 5.3, 5.4, 5.5, 9.5)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Consumers;
using LDK.RideClub.Bot.Filters;
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
    /// Registers the MassTransit in-process mediator with command consumers
    /// and pipeline filters (logging, validation, telemetry).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBotMediator(
        this IServiceCollection services)
    {
        _ = services.AddMediator(cfg =>
        {
#pragma warning disable IDE0058 // MassTransit fluent configuration — return values are intentionally unused
            // Register command consumers from Bot host assembly
            cfg.AddConsumer<ProcessTextMessageConsumer>();
            cfg.AddConsumer<ProcessBotCommandConsumer>();
            cfg.AddConsumer<UnrecognisedEventConsumer>();
#pragma warning restore IDE0058

            // Configure mediator pipeline filters (optional — when no filters
            // are registered for a request type, the mediator dispatches
            // directly to the consumer without requiring any filters to be present)
            cfg.ConfigureMediator((context, mcfg) =>
            {
                mcfg.UseSendFilter(typeof(LoggingFilter<>), context);
                mcfg.UseSendFilter(typeof(ValidationFilter<>), context);
                mcfg.UseSendFilter(typeof(TelemetryFilter<>), context);
            });
        });

        return services;
    }

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
