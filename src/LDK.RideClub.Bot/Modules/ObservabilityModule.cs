// ---------------------------------------------------------------------------
// RideClub Bot — ObservabilityModule (Req 11.1, 11.2)
// ---------------------------------------------------------------------------

using Autofac;

namespace LDK.RideClub.Bot.Modules;

/// <summary>
/// Autofac module responsible for registering observability infrastructure.
/// OpenTelemetry and Serilog are configured via <see cref="IServiceCollection"/>
/// and <see cref="IHostBuilder"/> extension methods in
/// <see cref="Observability.ObservabilityExtensions"/> (called from Program.cs).
/// This module exists as the Autofac composition point for any future
/// observability-related registrations (custom exporters, enrichers, etc.).
/// </summary>
#pragma warning disable CA1812 // Instantiated in Program.cs composition root
internal sealed class ObservabilityModule : Module
#pragma warning restore CA1812
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // OpenTelemetry and Serilog are registered via IServiceCollection/IHostBuilder
        // extension methods in ObservabilityExtensions.AddBotObservability() and
        // ObservabilityExtensions.UseBotSerilog(). Autofac automatically picks up
        // IServiceCollection registrations via its integration with the Microsoft
        // DI container.
        //
        // This module exists as the Autofac composition point for any future
        // observability-related registrations (custom metric collectors,
        // diagnostic listeners, etc.).
    }
}
