// ---------------------------------------------------------------------------
// RideClub Bot — ObservabilityExtensions (Req 11.1, 11.2, 11.3, 11.6, 11.7)
// ---------------------------------------------------------------------------

using System.Globalization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;

namespace LDK.RideClub.Bot.Observability;

/// <summary>
/// Extension methods for configuring OpenTelemetry and Serilog observability
/// on the Bot_Host application.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Custom <see cref="System.Diagnostics.ActivitySource"/> name used by messaging
    /// adapter instrumentation. Register this source with the tracing builder to
    /// capture adapter spans.
    /// </summary>
    public const string MessagingActivitySourceName = "LDK.RideClub.Bot.Messaging";

    /// <summary>
    /// Adds OpenTelemetry tracing and metrics to the service collection, configured
    /// with ASP.NET Core, HttpClient, and Entity Framework Core instrumentation.
    /// Telemetry is exported via OTLP to the endpoint specified in the "Otlp"
    /// configuration section.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBotObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection otlpSection = configuration.GetSection("Otlp");
        string endpoint = otlpSection["Endpoint"] ?? string.Empty;
        string serviceName = otlpSection["ServiceName"] ?? "rideclub-bot";

        bool hasValidEndpoint = Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? otlpUri)
            && (otlpUri.Scheme == Uri.UriSchemeHttp || otlpUri.Scheme == Uri.UriSchemeHttps);

        OpenTelemetry.OpenTelemetryBuilder otelBuilder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));

        _ = otelBuilder.WithTracing(tracing =>
        {
            _ = tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource(MessagingActivitySourceName);

            if (hasValidEndpoint)
            {
                _ = tracing.AddOtlpExporter(options => options.Endpoint = otlpUri!);
            }
        });

        _ = otelBuilder.WithMetrics(metrics =>
        {
            _ = metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();

            if (hasValidEndpoint)
            {
                _ = metrics.AddOtlpExporter(options => options.Endpoint = otlpUri!);
            }
        });

        if (!hasValidEndpoint && !string.IsNullOrEmpty(endpoint))
        {
            LogOtlpWarning(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "OTLP endpoint '{0}' is not a valid URI. Telemetry export is disabled.",
                    endpoint));
        }
        else if (!hasValidEndpoint)
        {
            LogOtlpWarning("OTLP endpoint is not configured. Telemetry export is disabled.");
        }

        return services;
    }

    /// <summary>
    /// Configures Serilog as the logging provider for the host, replacing the
    /// default Microsoft.Extensions.Logging implementation. Serilog reads its
    /// configuration from the host configuration, enriches log entries from the
    /// HTTP request context, and writes structured output to the console.
    /// Log entries are automatically correlated with the active OpenTelemetry
    /// trace context (trace ID and span ID).
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder UseBotSerilog(this IHostBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        _ = hostBuilder.UseSerilog((context, loggerConfiguration) =>
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

        return hostBuilder;
    }

    /// <summary>
    /// Writes an observability warning to the console during startup.
    /// </summary>
    /// <param name="message">The warning message to write.</param>
    private static void LogOtlpWarning(string message)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[Observability] {0}", message));
    }
}
