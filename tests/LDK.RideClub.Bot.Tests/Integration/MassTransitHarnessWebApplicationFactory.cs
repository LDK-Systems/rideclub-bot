// ---------------------------------------------------------------------------
// RideClub Bot — MassTransitHarnessWebApplicationFactory (Req 10.3, 10.4)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Persistence;
using LDK.RideClub.Bot.Persistence.Entities;
using LDK.RideClub.Bot.Sagas;

using MassTransit;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LDK.RideClub.Bot.Tests.Integration;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that bootstraps the
/// Bot_Host with MassTransit configured in test harness mode.
/// This enables full pipeline integration testing (webhook → EventDispatcher →
/// Mediator → Handler → Bus → State Machine) without network access, Docker,
/// or external message brokers.
/// </summary>
#pragma warning disable CA1515 // Public type required for IClassFixture<T> usage in test classes
public sealed class MassTransitHarnessWebApplicationFactory : WebApplicationFactory<Program>
#pragma warning restore CA1515
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>
    /// Initialises a new instance of the <see cref="MassTransitHarnessWebApplicationFactory"/> class.
    /// Sets the DEPLOYMENT_MODE environment variable required by Program.cs before
    /// the host is built.
    /// </summary>
    public MassTransitHarnessWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("DEPLOYMENT_MODE", "kestrel");
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.UseEnvironment(Environments.Development);

        // Provide minimal test configuration that satisfies FluentValidation validators
        _ = builder.UseSetting("Webhooks:BasePath", "/webhooks");
        _ = builder.UseSetting("Adapters:WhatsApp:VerifyToken", "test-token");
        _ = builder.UseSetting("Adapters:WhatsApp:AccessToken", "test-access");
        _ = builder.UseSetting("Adapters:WhatsApp:PhoneNumberId", "123");
        _ = builder.UseSetting("Persistence:SqliteConnectionString", "Data Source=:memory:");
        _ = builder.UseSetting("Otlp:Endpoint", "http://localhost:4317");
        _ = builder.UseSetting("Otlp:ServiceName", "test");
        _ = builder.UseSetting("Deployment:Mode", "Kestrel");
        _ = builder.UseSetting("MassTransit:TransportType", "InMemory");
        _ = builder.UseSetting("MassTransit:ConcurrencyLimit", "10");

        _ = builder.ConfigureServices(services =>
        {
            // Remove the existing DbContextOptions<BotDbContext> registration
            ServiceDescriptor? dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<BotDbContext>));

            if (dbContextDescriptor is not null)
            {
                _ = services.Remove(dbContextDescriptor);
            }

            // Remove the DatabaseMigrationHostedService (not needed with InMemory provider)
            ServiceDescriptor? migrationDescriptor = services.SingleOrDefault(
                d => d.ImplementationType?.Name == "DatabaseMigrationHostedService");

            if (migrationDescriptor is not null)
            {
                _ = services.Remove(migrationDescriptor);
            }

            // Add BotDbContext with InMemory provider using a unique database name
            _ = services.AddDbContext<BotDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Replace MassTransit with the test harness.
            // AddMassTransitTestHarness replaces the transport with an in-memory
            // test harness that allows asserting on published/consumed messages
            // and saga state transitions without any external infrastructure.
            RemoveMassTransitRegistrations(services);

            _ = services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<ConversationStateMachine, ConversationSagaInstance>()
                    .InMemoryRepository();
            });
        });
    }

    /// <summary>
    /// Removes existing MassTransit service registrations so the test harness
    /// can replace them cleanly. This must remove all registrations added by
    /// AddBotMassTransit to avoid duplicate health check errors.
    /// </summary>
    private static void RemoveMassTransitRegistrations(IServiceCollection services)
    {
        // Remove ALL MassTransit-related registrations to avoid conflicts
        // with AddMassTransitTestHarness. We identify them by checking if the
        // service type or implementation type is from a MassTransit assembly.
        var massTransitDescriptors = services
            .Where(IsMassTransitDescriptor)
            .ToList();

        foreach (ServiceDescriptor descriptor in massTransitDescriptors)
        {
            _ = services.Remove(descriptor);
        }
    }

    private static bool IsMassTransitDescriptor(ServiceDescriptor descriptor)
    {
        return IsMassTransitType(descriptor.ServiceType) ||
               IsMassTransitType(descriptor.ImplementationType) ||
               IsMassTransitType(descriptor.ImplementationInstance?.GetType());
    }

    private static bool IsMassTransitType(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        string? assemblyName = type.Assembly.GetName().Name;
        return assemblyName?.StartsWith("MassTransit", StringComparison.Ordinal) == true;
    }
}
