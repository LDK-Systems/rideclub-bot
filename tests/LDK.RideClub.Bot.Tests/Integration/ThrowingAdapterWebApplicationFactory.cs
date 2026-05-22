// ---------------------------------------------------------------------------
// RideClub Bot — ThrowingAdapterWebApplicationFactory (Property 11 support)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Responses;
using LDK.RideClub.Bot.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LDK.RideClub.Bot.Tests.Integration;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that registers a throwing adapter
/// to test that adapter exceptions result in HTTP 500 responses (Property 11).
/// </summary>
#pragma warning disable CA1515 // Public type required for test usage
public sealed class ThrowingAdapterWebApplicationFactory : WebApplicationFactory<Program>
#pragma warning restore CA1515
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>
    /// Initialises a new instance of the <see cref="ThrowingAdapterWebApplicationFactory"/> class.
    /// </summary>
    public ThrowingAdapterWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("DEPLOYMENT_MODE", "kestrel");
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.UseEnvironment(Environments.Development);

        // Provide minimal test configuration
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

            // Remove the DatabaseMigrationHostedService
            ServiceDescriptor? migrationDescriptor = services.SingleOrDefault(
                d => d.ImplementationType?.Name == "DatabaseMigrationHostedService");

            if (migrationDescriptor is not null)
            {
                _ = services.Remove(migrationDescriptor);
            }

            // Add BotDbContext with InMemory provider
            _ = services.AddDbContext<BotDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Register the throwing adapter
            _ = services.AddSingleton<IMessagingAdapter, ThrowingMessagingAdapter>();
        });
    }
}

/// <summary>
/// A test adapter that always throws an exception during event deserialization.
/// Used to verify that the global exception middleware returns HTTP 500.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class ThrowingMessagingAdapter : IMessagingAdapter
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public string PlatformId => "throwing";

    /// <inheritdoc />
    public Task<bool> VerifyWebhookAsync(HttpRequest request, CancellationToken ct = default)
    {
        // Always pass verification so we reach the deserialization step
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<IResult> HandleVerificationChallengeAsync(HttpRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(Results.Ok());
    }

    /// <inheritdoc />
    public Task<MappingResult<InboundEvent>> DeserialiseEventAsync(HttpRequest request, CancellationToken ct = default)
    {
        throw new InvalidOperationException("Simulated adapter exception for testing.");
    }

    /// <inheritdoc />
    public Task<SendResult> SendMessageAsync(OutboundMessage message, CancellationToken ct = default)
    {
        return Task.FromResult(new SendResult { Success = true });
    }
}
