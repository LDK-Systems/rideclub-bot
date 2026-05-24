# AGENTS.md

## Project overview
- This is a .NET 10 ASP.NET Core bot for Ride Club with WhatsApp support, EF Core persistence, OpenTelemetry, and dual deployment modes (`kestrel` and `lambda`).
- The main entry point is [src/LDK.RideClub.Bot/Program.cs](src/LDK.RideClub.Bot/Program.cs).
- The runtime is organized around a composition root in `Program.cs`, Autofac modules in [src/LDK.RideClub.Bot/Modules](src/LDK.RideClub.Bot/Modules), and endpoint registration in [src/LDK.RideClub.Bot/Endpoints/WebhookEndpoints.cs](src/LDK.RideClub.Bot/Endpoints/WebhookEndpoints.cs).
- Review [README.md](README.md) for a high-level product summary and deployment notes.

## Build and run commands
- Build or test the solution with `dotnet test LDK.RideClub.Bot.slnx`.
- A full build is `dotnet build LDK.RideClub.Bot.slnx`.
- Docker-based local run uses `docker compose up --build` and is defined in [docker-compose.yml](docker-compose.yml).
- The container image is built from [Dockerfile](Dockerfile).

## Architecture and conventions
- `Program.cs` validates `DEPLOYMENT_MODE` before host startup and then wires configuration, persistence, observability, middleware, and webhook endpoints.
- `MessagingModule` registers adapter implementations and the adapter registry, and it is the place to extend support for additional platforms.
- `PersistenceModule` is intentionally small; EF Core registration and migrations are configured via the persistence service extensions.
- `ObservabilityModule` is similarly small; observability registration lives in the observability extensions.
- Webhooks are mapped under a base path from configuration and route to the selected adapter through `IAdapterRegistry`.

## Testing guidance
- Tests are in [tests/LDK.RideClub.Bot.Tests](tests/LDK.RideClub.Bot.Tests).
- The test suite uses xUnit, FluentAssertions, NSubstitute, and FsCheck.
- Integration tests use [tests/LDK.RideClub.Bot.Tests/Integration/BotWebApplicationFactory.cs](tests/LDK.RideClub.Bot.Tests/Integration/BotWebApplicationFactory.cs) to boot the app.
- Architectural constraints are enforced in [tests/LDK.RideClub.Bot.Tests/Architecture/ArchitecturalConstraintTests.cs](tests/LDK.RideClub.Bot.Tests/Architecture/ArchitecturalConstraintTests.cs).
- Keep constructor dependencies small, prefer interfaces for messaging/persistence dependencies, and avoid service-locator usage outside the composition root.

## Current verification status
- The repository currently has a failing `dotnet test LDK.RideClub.Bot.slnx` run in the working tree. Before claiming a green build, re-run the command and address any failures that remain.

## Common pitfalls
- `DEPLOYMENT_MODE` must be set to `kestrel` or `lambda`; otherwise the app exits during startup.
- The WhatsApp adapter expects configured values in the options sections, including the verification token and access token.
- Runtime option validation is enabled on startup, so invalid configuration will fail fast.
- The OpenTelemetry endpoint is configured via options and the compose file includes an Aspire dashboard service.

## Key files to inspect
- [src/LDK.RideClub.Bot/Program.cs](src/LDK.RideClub.Bot/Program.cs)
- [src/LDK.RideClub.Bot/Endpoints/WebhookEndpoints.cs](src/LDK.RideClub.Bot/Endpoints/WebhookEndpoints.cs)
- [src/LDK.RideClub.Bot/Modules/MessagingModule.cs](src/LDK.RideClub.Bot/Modules/MessagingModule.cs)
- [src/LDK.RideClub.Bot/Configuration/ConfigurationExtensions.cs](src/LDK.RideClub.Bot/Configuration/ConfigurationExtensions.cs)
- [tests/LDK.RideClub.Bot.Tests/Integration/BotWebApplicationFactory.cs](tests/LDK.RideClub.Bot.Tests/Integration/BotWebApplicationFactory.cs)
- [tests/LDK.RideClub.Bot.Tests/Architecture/ArchitecturalConstraintTests.cs](tests/LDK.RideClub.Bot.Tests/Architecture/ArchitecturalConstraintTests.cs)
