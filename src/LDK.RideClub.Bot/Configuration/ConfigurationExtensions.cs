// ---------------------------------------------------------------------------
// RideClub Bot — ConfigurationExtensions (Req 5.1, 5.2, 5.6, 5.7)
// ---------------------------------------------------------------------------

using FluentValidation;

using Microsoft.Extensions.Options;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Extension methods for registering strongly-typed configuration options
/// with FluentValidation validators and runtime change monitoring.
/// </summary>
internal static class ConfigurationExtensions
{
    /// <summary>
    /// Registers all Options_Model classes with their FluentValidation validators,
    /// enables ValidateOnStart, and configures IOptionsMonitor change callbacks
    /// that log warnings on invalid runtime changes.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Register FluentValidation validators
        _ = services.AddSingleton<IValidator<WebhookOptions>, WebhookOptionsValidator>();
        _ = services.AddSingleton<IValidator<WhatsAppOptions>, WhatsAppOptionsValidator>();
        _ = services.AddSingleton<IValidator<PersistenceOptions>, PersistenceOptionsValidator>();
        _ = services.AddSingleton<IValidator<OtlpOptions>, OtlpOptionsValidator>();
        _ = services.AddSingleton<IValidator<DeploymentOptions>, DeploymentOptionsValidator>();

        // Bind and validate each options section
        RegisterOptions<WebhookOptions>(services, configuration, WebhookOptions.SectionName);
        RegisterOptions<WhatsAppOptions>(services, configuration, WhatsAppOptions.SectionName);
        RegisterOptions<PersistenceOptions>(services, configuration, PersistenceOptions.SectionName);
        RegisterOptions<OtlpOptions>(services, configuration, OtlpOptions.SectionName);
        RegisterOptions<DeploymentOptions>(services, configuration, DeploymentOptions.SectionName);

        // Register IOptionsMonitor change callbacks for runtime validation
        _ = services.AddSingleton<IHostedService, OptionsChangeMonitorService>();

        return services;
    }

    private static void RegisterOptions<TOptions>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        _ = services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();

        // Register the FluentValidation-based IValidateOptions<T> adapter
        _ = services.AddSingleton<IValidateOptions<TOptions>>(sp =>
        {
            IValidator<TOptions> validator = sp.GetRequiredService<IValidator<TOptions>>();
            return new FluentValidationOptionsValidator<TOptions>(validator);
        });
    }
}
