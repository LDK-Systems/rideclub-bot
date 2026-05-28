# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```sh
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run a specific test class or method
dotnet test --filter "FullyQualifiedName~AdapterRegistryTests"

# Run the app locally (requires DEPLOYMENT_MODE)
DEPLOYMENT_MODE=kestrel dotnet run --project src/LDK.RideClub.Bot

# Run with Docker Compose (includes Aspire Dashboard for telemetry)
docker compose up

# Add an EF Core migration
dotnet ef migrations add <MigrationName> \
  --project src/LDK.RideClub.Bot.Persistence \
  --startup-project src/LDK.RideClub.Bot
```

## Architecture

A multi-platform messaging bot for WhatsApp (and future platforms). ASP.NET Core Minimal API host that receives webhook events from messaging platforms, normalises them into domain events, and processes them.

### Project layout

| Project | Role |
|---|---|
| `LDK.RideClub.Bot` | ASP.NET Core host, composition root, configuration, middleware, endpoints |
| `LDK.RideClub.Bot.Abstractions` | Interfaces: `IMessagingAdapter`, `IAdapterRegistry`, `IEventProcessor`, `MappingResult<T>` |
| `LDK.RideClub.Bot.Domain` | Platform-agnostic domain models: `InboundEvent`, `OutboundMessage`, `EventPayload` hierarchy |
| `LDK.RideClub.Bot.Adapters.WhatsApp` | WhatsApp-specific DTOs and `IMessagingAdapter` implementation |
| `LDK.RideClub.Bot.Persistence` | EF Core `BotDbContext`, `MessageLog` entity, SQLite migrations |
| `LDK.RideClub.Bot.Observability` | OpenTelemetry pipeline configuration, `MessagingActivitySource` |
| `tests/LDK.RideClub.Bot.Tests` | xUnit tests: unit, integration, property-based, architectural |

### Webhook request flow

```
POST /{basePath}/{platformId}
  → GlobalExceptionMiddleware
  → RequestBodySizeLimitMiddleware
  → WebhookEndpoints.HandlePostAsync
      → IAdapterRegistry.GetAdapter(platformId)  →  404 if not found
      → adapter.VerifyWebhookAsync()             →  401 if invalid
      → adapter.DeserialiseEventAsync()          →  400 if MappingFailure<T>
      → IEventProcessor.ProcessAsync()
      → 200 OK

GET /{basePath}/{platformId}
  → WebhookEndpoints.HandleGetAsync
      → IAdapterRegistry.GetAdapter(platformId)  →  404 if not found
      → adapter.HandleVerificationChallengeAsync()  (WhatsApp: echoes hub.challenge)
```

There is also a `/health` endpoint (no auth) used by the Docker health check.

### Adapter pattern

Each messaging platform lives in its own project (`LDK.RideClub.Bot.Adapters.{Platform}`). All `IMessagingAdapter` implementations are auto-discovered via Autofac's `RegisterAssemblyTypes` in `MessagingModule`. To add a new platform:
1. Create a new project with an `AssemblyMarker` class.
2. Implement `IMessagingAdapter` (set a unique `PlatformId` string, e.g. `"telegram"`).
3. Add the new assembly to the `RegisterAssemblyTypes` call in `MessagingModule.cs`.

WhatsApp webhook verification uses HMAC-SHA256 against the `X-Hub-Signature-256` header (not a plain token comparison). The `VerifyToken` config value is used as the HMAC key.

### MediatR command pipeline

Domain commands (`ProcessTextMessageCommand`, `ProcessBotCommandCommand`) implement `IRequest<MessageProcessingResult>` and live in `LDK.RideClub.Bot.Domain/Commands/`. All MediatR requests pass through three pipeline behaviors registered in order:

1. `ValidationBehavior<TRequest, TResponse>` — runs all `IValidator<TRequest>` registered via FluentValidation; throws `ValidationException` on failure.
2. `LoggingBehavior<TRequest, TResponse>` — logs request start/end with elapsed milliseconds.
3. `TelemetryBehavior<TRequest, TResponse>` — wraps the handler in an OpenTelemetry activity span.

`UnrecognisedEventNotification` (`INotification`) is published by the event dispatcher when no known command maps to an `InboundEvent` payload type.

### MassTransit saga

`ConversationStateMachine` (`MassTransitStateMachine<ConversationSagaInstance>`) tracks conversation lifecycle. It lives in `src/LDK.RideClub.Bot/Sagas/`. Correlation key: `"{Platform}:{SenderId}"`.

States: `Initial → AwaitingInput → Processing → AwaitingInput | AwaitingConfirmation | Faulted | Completed`.

Events: `MessageReceivedEvent`, `ProcessingCompletedEvent` (has `RequiresConfirmation` flag), `ProcessingFaultedEvent`, `ConversationTimedOutEvent`. All events live under `LDK.RideClub.Bot.Domain/Events/Conversation/`.

Saga state is persisted via `MassTransit.EntityFrameworkCore` to `ConversationSagaInstance` (see `AddConversationSagas` migration). Unhandled transitions are silently ignored (`OnUnhandledEvent(x => x.Ignore())`).

### Placeholder implementations

`LoggingEventProcessor` (the current `IEventProcessor`) only logs events — it has no real processing logic and is intended to be replaced. `WhatsAppMessagingAdapter.SendMessageAsync` is also a stub that always returns success.

### DI container

Autofac is used (not the built-in Microsoft DI container). Registrations are in three modules under `src/LDK.RideClub.Bot/Modules/`:
- `MessagingModule` — adapters, `IAdapterRegistry`, `IEventProcessor`
- `PersistenceModule` — `BotDbContext`
- `ObservabilityModule` — OpenTelemetry

`MessagingModule` maps `WhatsAppOptions` (host config) → `WhatsAppAdapterOptions` (adapter-project record) via an Autofac `Register` delegate — adapters do not take a dependency on host config types.

Architectural constraints enforced by reflection tests:
- Constructors: ≤ 5 dependencies (one `ILogger<T>` / `IOptions<T>` excluded).
- Dependencies must be injected as interfaces, not concrete types.
- No `IServiceProvider` outside the composition root (no service locator pattern).
- Interfaces declare ≤ 5 methods.
- No `JsonElement` in public method signatures outside DTOs/webhook boundaries.

### `MappingResult<T>` discriminated union

Mapping failures use `MappingSuccess<T>` / `MappingFailure<T>` instead of exceptions. Never throw from `DeserialiseEventAsync`; return a `MappingFailure<T>` with a descriptive reason string instead.

### Domain model

`EventPayload` is an abstract record. Current subtypes: `TextMessagePayload` (has `Text`) and `CommandPayload`. `OutboundMessage` uses a `MessageContent` hierarchy (current subtype: `TextContent`).

### Observability

`MessagingActivitySource` provides static helpers (`StartVerifyWebhook`, `StartDeserialiseEvent`, `StartSendMessage`, `StartHandleChallenge`) that create OpenTelemetry spans with `messaging.platform` and `messaging.operation` tags. Use these when instrumenting adapter operations. OTLP export is silently disabled at startup if no valid `Otlp:Endpoint` is configured.

### Deployment modes

`DEPLOYMENT_MODE` environment variable **must** be set to `kestrel` or `lambda` before the host builds, or the process exits with code 1. The Lambda path adds `Amazon.Lambda.AspNetCoreServer.Hosting`.

EF Core migrations run automatically at startup via `DatabaseMigrationHostedService`.

### Configuration sections

| Section | Options class | Notes |
|---|---|---|
| `Deployment` | `DeploymentOptions` | `Mode`: `Kestrel` or `Lambda` |
| `Webhooks` | `WebhookOptions` | `BasePath` (default `/webhooks`) |
| `Adapters:WhatsApp` | `WhatsAppOptions` | `VerifyToken`, `AccessToken`, `PhoneNumberId` |
| `Persistence` | `PersistenceOptions` | `SqliteConnectionString` |
| `Otlp` | `OtlpOptions` | `Endpoint`, `ServiceName` |

All options are validated with FluentValidation at startup (`ValidateOnStart()`). In Development, user secrets supplement `appsettings.json`.

### Testing

- **Unit** — NSubstitute mocks, FluentAssertions.
- **Property-based** — FsCheck.Xunit with `[Property]` attribute (minimum 100 iterations).
- **Integration** — `WebApplicationFactory<Program>` with EF Core InMemory replacing SQLite and OTLP disabled. See `BotWebApplicationFactory.cs`.
- **Architectural** — Reflection-based constraints in `ArchitecturalConstraintTests.cs`.

Package versions are centrally managed in `Directory.Packages.props`; do not include `Version=` attributes in individual `.csproj` files.
