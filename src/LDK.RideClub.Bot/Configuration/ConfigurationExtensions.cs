// ---------------------------------------------------------------------------
// RideClub Bot — ConfigurationExtensions (Req 5.1, 5.2, 5.6, 5.7)
// ---------------------------------------------------------------------------

using System.Reflection;

using FluentValidation;

using LDK.RideClub.Bot.Abstractions.Messaging;

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

        // Register FluentValidation validators for host-level options
        _ = services.AddSingleton<IValidator<WebhookOptions>, WebhookOptionsValidator>();
        _ = services.AddSingleton<IValidator<PersistenceOptions>, PersistenceOptionsValidator>();
        _ = services.AddSingleton<IValidator<OtlpOptions>, OtlpOptionsValidator>();
        _ = services.AddSingleton<IValidator<DeploymentOptions>, DeploymentOptionsValidator>();
        _ = services.AddSingleton<IValidator<MassTransitOptions>, MassTransitOptionsValidator>();

        // Bind and validate each host-level options section
        RegisterOptions<WebhookOptions>(services, configuration, WebhookOptions.SectionName);
        RegisterOptions<PersistenceOptions>(services, configuration, PersistenceOptions.SectionName);
        RegisterOptions<OtlpOptions>(services, configuration, OtlpOptions.SectionName);
        RegisterOptions<DeploymentOptions>(services, configuration, DeploymentOptions.SectionName);
        RegisterOptions<MassTransitOptions>(services, configuration, MassTransitOptions.SectionName);

        // Discover and invoke adapter options registrations from referenced adapter assemblies.
        RegisterAdapterOptions(services, configuration);

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

    /// <summary>
    /// Scans referenced adapter assemblies for <see cref="IAdapterOptionsRegistration"/>
    /// implementations and invokes them to register adapter-specific options.
    /// Uses the same assembly set as Autofac's <c>RegisterAssemblyModules</c> call.
    /// </summary>
    private static void RegisterAdapterOptions(IServiceCollection services, IConfiguration configuration)
    {
        foreach (Assembly assembly in AdapterAssemblyDiscovery.Assemblies)
        {
            IEnumerable<Type> registrationTypes = assembly.GetExportedTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false } &&
                            t.IsAssignableTo(typeof(IAdapterOptionsRegistration)));

            foreach (Type registrationType in registrationTypes)
            {
                var registration = (IAdapterOptionsRegistration)Activator.CreateInstance(registrationType)!;
                registration.RegisterOptions(services, configuration);
            }
        }
    }
}
