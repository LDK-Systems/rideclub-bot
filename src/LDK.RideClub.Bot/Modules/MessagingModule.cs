// ---------------------------------------------------------------------------
// RideClub Bot — MessagingModule (Req 1.3, 4.4)
// ---------------------------------------------------------------------------

using Autofac;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Adapters.WhatsApp;
using LDK.RideClub.Bot.Configuration;
using LDK.RideClub.Bot.Services;

using Microsoft.Extensions.Options;

namespace LDK.RideClub.Bot.Modules;

/// <summary>
/// Autofac module responsible for registering messaging infrastructure:
/// adapter implementations, the adapter registry, and the event processor.
/// </summary>
#pragma warning disable CA1812 // Instantiated in Program.cs composition root
internal sealed class MessagingModule : Module
#pragma warning restore CA1812
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // Register WhatsAppAdapterOptions by mapping from the host's WhatsAppOptions.
        _ = builder.Register(ctx =>
        {
            WhatsAppOptions hostOptions = ctx.Resolve<IOptions<WhatsAppOptions>>().Value;
            return new WhatsAppAdapterOptions(
                hostOptions.VerifyToken,
                hostOptions.AccessToken,
                hostOptions.PhoneNumberId);
        })
        .As<WhatsAppAdapterOptions>()
        .SingleInstance();

        // Register all IMessagingAdapter implementations from the WhatsApp adapter assembly.
        // Additional adapter assemblies can be added here as new platforms are supported.
        _ = builder.RegisterAssemblyTypes(typeof(AssemblyMarker).Assembly)
            .Where(t => t.IsAssignableTo<IMessagingAdapter>())
            .As<IMessagingAdapter>()
            .InstancePerLifetimeScope();

        // Register AdapterRegistry as singleton — builds the lookup dictionary once at startup.
        _ = builder.RegisterType<AdapterRegistry>()
            .As<IAdapterRegistry>()
            .SingleInstance();

        // Register the placeholder event processor (scoped lifetime).
        _ = builder.RegisterType<LoggingEventProcessor>()
            .As<IEventProcessor>()
            .InstancePerLifetimeScope();
    }
}
