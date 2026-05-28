// ---------------------------------------------------------------------------
// RideClub Bot — WhatsAppModule (Autofac adapter module)
// ---------------------------------------------------------------------------

using Autofac;

using FluentValidation;

using LDK.RideClub.Bot.Abstractions.Messaging;

using Microsoft.Extensions.Options;

namespace LDK.RideClub.Bot.Adapters.WhatsApp;

/// <summary>
/// Autofac module that self-registers all WhatsApp adapter services.
/// Discovered at startup via assembly scanning for <see cref="Module"/> types
/// in adapter assemblies.
/// </summary>
public sealed class WhatsAppModule : Module
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

        // Register all IMessagingAdapter implementations from this assembly.
        _ = builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.IsAssignableTo<IMessagingAdapter>())
            .As<IMessagingAdapter>()
            .InstancePerLifetimeScope();

        // Register the FluentValidation validator for WhatsAppOptions.
        _ = builder.RegisterType<WhatsAppOptionsValidator>()
            .As<IValidator<WhatsAppOptions>>()
            .SingleInstance();
    }
}
