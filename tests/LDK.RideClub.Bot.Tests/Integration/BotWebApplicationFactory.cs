// ---------------------------------------------------------------------------
// RideClub Bot — BotWebApplicationFactory (Req 12.3, 12.4)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LDK.RideClub.Bot.Tests.Integration;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that bootstraps the
/// Bot_Host in-memory with external dependencies replaced for integration testing.
/// SQLite is replaced by the EF Core InMemory provider, and the OTLP exporter
/// is effectively disabled by providing a non-routable endpoint.
/// </summary>
#pragma warning disable CA1515 // Public type required for IClassFixture<T> usage in test classes
public sealed class BotWebApplicationFactory : WebApplicationFactory<Program>
#pragma warning restore CA1515
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>
    /// Initialises a new instance of the <see cref="BotWebApplicationFactory"/> class.
    /// Sets the DEPLOYMENT_MODE environment variable required by Program.cs before
    /// the host is built.
    /// </summary>
    public BotWebApplicationFactory()
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
        });
    }
}
