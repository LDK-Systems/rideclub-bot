// ---------------------------------------------------------------------------
// RideClub Bot — MediatorModule (Req 1.1, 1.2, 1.3, 1.4, 1.5, 2.6, 3.5)
// ---------------------------------------------------------------------------

using Autofac;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Behaviors;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Handlers;
using LDK.RideClub.Bot.Services;

using MediatR.Extensions.Autofac.DependencyInjection;
using MediatR.Extensions.Autofac.DependencyInjection.Builder;

namespace LDK.RideClub.Bot.Modules;

/// <summary>
/// Autofac module responsible for registering MediatR services, pipeline behaviors,
/// and the <see cref="EventDispatcher"/> as the <see cref="IEventProcessor"/> implementation.
/// </summary>
#pragma warning disable CA1812 // Instantiated in Program.cs composition root
internal sealed class MediatorModule : Module
#pragma warning restore CA1812
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // Register MediatR with assembly scanning of the Domain and Bot host projects
        MediatRConfiguration configuration = MediatRConfigurationBuilder
            .Create(typeof(ProcessTextMessageCommand).Assembly, typeof(ProcessTextMessageCommandHandler).Assembly)
            .WithAllOpenGenericHandlerTypesRegistered()
            .WithCustomPipelineBehaviors(new[]
            {
                typeof(LoggingBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(TelemetryBehavior<,>)
            })
            .WithRegistrationScope(RegistrationScope.Scoped)
            .Build();

        _ = builder.RegisterMediatR(configuration);

        // Replace LoggingEventProcessor with EventDispatcher
        _ = builder.RegisterType<EventDispatcher>()
            .As<IEventProcessor>()
            .InstancePerLifetimeScope();
    }
}
