// ---------------------------------------------------------------------------
// RideClub Bot — WhatsAppOptionsRegistration
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Abstractions.Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LDK.RideClub.Bot.Adapters.WhatsApp;

/// <summary>
/// Registers WhatsApp adapter options with the host's service collection.
/// Discovered via assembly scanning for <see cref="IAdapterOptionsRegistration"/> implementations.
/// </summary>
public sealed class WhatsAppOptionsRegistration : IAdapterOptionsRegistration
{
    /// <inheritdoc />
    public void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _ = services.AddOptions<WhatsAppOptions>()
            .Bind(configuration.GetSection(WhatsAppOptions.SectionName))
            .ValidateOnStart();

        // Register the FluentValidation-based IValidateOptions<T> adapter
        _ = services.AddSingleton<IValidateOptions<WhatsAppOptions>>(sp =>
        {
            FluentValidation.IValidator<WhatsAppOptions> validator = sp.GetRequiredService<FluentValidation.IValidator<WhatsAppOptions>>();
            return new Abstractions.Messaging.FluentValidationOptionsValidator<WhatsAppOptions>(validator);
        });
    }
}
