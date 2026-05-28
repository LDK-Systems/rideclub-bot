// ---------------------------------------------------------------------------
// RideClub Bot — Program.cs (Composition Root)
// ---------------------------------------------------------------------------

using Autofac;

using LDK.RideClub.Bot.Configuration;
using LDK.RideClub.Bot.Observability;

// Step 1: Validate DEPLOYMENT_MODE before building the host (Req 13.4)
string? deploymentMode = Environment.GetEnvironmentVariable("DEPLOYMENT_MODE");

if (string.IsNullOrEmpty(deploymentMode) ||
    (!deploymentMode.Equals("kestrel", StringComparison.OrdinalIgnoreCase) &&
     !deploymentMode.Equals("lambda", StringComparison.OrdinalIgnoreCase)))
{
    await Console.Error.WriteLineAsync(
        $"DEPLOYMENT_MODE environment variable must be set to 'kestrel' or 'lambda'. Received: '{deploymentMode ?? "(null)"}'.").ConfigureAwait(false);
    Environment.Exit(1);
}

// Step 2: Create the WebApplication builder
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Step 3: Configure Autofac as the DI container (Req 1.3)
builder.Host.UseServiceProviderFactory(new Autofac.Extensions.DependencyInjection.AutofacServiceProviderFactory());

builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    // Register host-level modules explicitly.
    _ = containerBuilder.RegisterModule(new LDK.RideClub.Bot.Modules.MessagingModule());
    _ = containerBuilder.RegisterModule(new LDK.RideClub.Bot.Modules.PersistenceModule());
    _ = containerBuilder.RegisterModule(new LDK.RideClub.Bot.Modules.ObservabilityModule());
    _ = containerBuilder.RegisterModule(new LDK.RideClub.Bot.Modules.SagaModule());
    _ = containerBuilder.RegisterModule(new LDK.RideClub.Bot.Modules.MediatorModule());

    // Discover and load adapter Autofac modules from assemblies matching the naming convention.
    // This is Autofac's native assembly-scanning mechanism — any assembly in the output directory
    // named "LDK.RideClub.Bot.Adapters.*.dll" that contains a Module subclass will be loaded
    // automatically, requiring no explicit registration per adapter.
    _ = containerBuilder.RegisterAssemblyModules(LDK.RideClub.Bot.Configuration.AdapterAssemblyDiscovery.Assemblies);
});

// Step 4: Add AWS Lambda hosting when mode is "lambda" (Req 13.2)
if (deploymentMode.Equals("lambda", StringComparison.OrdinalIgnoreCase))
{
    _ = builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
}

// Step 5: Configuration sources (Req 1.5)
// WebApplication.CreateBuilder already loads appsettings.json (required),
// appsettings.{env}.json (optional), and environment variables.
// Add user-secrets in Development.
if (builder.Environment.IsDevelopment())
{
    _ = builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// Step 6: Configure graceful shutdown timeout of 10 seconds (Req 1.7)
builder.Host.ConfigureHostOptions(options => options.ShutdownTimeout = TimeSpan.FromSeconds(10));

// Step 6b: Register strongly-typed configuration options with FluentValidation (Req 5.1–5.7)
builder.Services.AddApplicationOptions(builder.Configuration);

// Step 6c: Register EF Core persistence with SQLite provider (Req 9.2, 9.5)
builder.Services.AddBotPersistence(builder.Configuration);

// Step 6c2: Register MassTransit with saga state machine and in-memory transport (Req 5.3, 5.4, 5.5)
builder.Services.AddBotMassTransit(builder.Configuration);

// Step 6d: Register OpenTelemetry observability pipeline (Req 11.1, 11.2)
builder.Services.AddBotObservability(builder.Configuration);

// Step 6e: Configure Serilog as the logging provider (Req 11.3, 11.7)
builder.Host.UseBotSerilog();

// Step 7: Add health checks (Req 10.2, 12.5)
_ = builder.Services.AddHealthChecks();

// Build the application
WebApplication app = builder.Build();

// Step 8: Wire middleware pipeline
app.UseMiddleware<LDK.RideClub.Bot.Middleware.GlobalExceptionMiddleware>();
app.UseMiddleware<LDK.RideClub.Bot.Middleware.RequestBodySizeLimitMiddleware>();

// Map health check endpoint
app.MapHealthChecks("/health");

LDK.RideClub.Bot.Endpoints.WebhookEndpoints.Map(app);

// Step 9: Run the application (Req 13.5)
await app.RunAsync().ConfigureAwait(false);

// ---------------------------------------------------------------------------
// Make Program accessible for WebApplicationFactory<Program> in tests (Req 12.3)
// ---------------------------------------------------------------------------
#pragma warning disable CA1050, CA1515 // Declare types in namespaces; Types can be made internal
/// <summary>
/// Partial class declaration to allow WebApplicationFactory to reference the entry point.
/// </summary>
public partial class Program
{
}
#pragma warning restore CA1050, CA1515
