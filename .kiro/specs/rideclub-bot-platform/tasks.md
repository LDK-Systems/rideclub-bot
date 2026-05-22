# Implementation Plan: RideClub Bot Platform

## Overview

Transform the existing skeleton console app into a full ASP.NET Core platform with Autofac DI, webhook HTTP pipeline, EF Core persistence, OpenTelemetry observability, Docker containerisation, and dual deployment mode (Kestrel/Lambda). Implementation proceeds bottom-up: shared libraries first, then host wiring, adapters, persistence, observability, containerisation, and finally testing infrastructure.

## Tasks

- [x] 1. Set up solution structure and shared class libraries
  - [x] 1.1 Create the LDK.RideClub.Bot.Abstractions class library project
    - Create `src/LDK.RideClub.Bot.Abstractions/LDK.RideClub.Bot.Abstractions.csproj` as a class library (no OutputType, defaults to Library)
    - Add folder structure: `Messaging/` and `Persistence/`
    - Define `IMessagingAdapter` interface in `Messaging/IMessagingAdapter.cs` with PlatformId property, VerifyWebhookAsync, HandleVerificationChallengeAsync, DeserialiseEventAsync, and SendMessageAsync methods
    - Define `IAdapterRegistry` interface in `Messaging/IAdapterRegistry.cs` with GetAdapter and RegisteredPlatforms members
    - Define `IEventProcessor` interface in `Messaging/IEventProcessor.cs` with ProcessAsync method
    - Define `MappingResult<T>` discriminated union types in `Messaging/MappingResult.cs`
    - Add the project to LDK.RideClub.Bot.slnx
    - _Requirements: 2.3, 4.2, 4.5, 8.1, 8.2_

  - [x] 1.2 Create the LDK.RideClub.Bot.Domain class library project
    - Create `src/LDK.RideClub.Bot.Domain/LDK.RideClub.Bot.Domain.csproj` as a class library
    - Add folder structure: `Events/`, `Commands/`, `Responses/`
    - Define `InboundEvent`, `EventPayload`, `TextMessagePayload`, `CommandPayload` records in `Events/`
    - Define `OutboundMessage`, `MessageContent`, `TextContent` records in `Commands/`
    - Define `SendResult` record in `Responses/`
    - Add the project to LDK.RideClub.Bot.slnx
    - _Requirements: 2.3, 6.1_

  - [x] 1.3 Add NuGet package versions to Directory.Packages.props
    - Add Autofac.Extensions.DependencyInjection, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.InMemory, OpenTelemetry.Extensions.Hosting, OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http, OpenTelemetry.Instrumentation.EntityFrameworkCore, OpenTelemetry.Exporter.OpenTelemetryProtocol, Amazon.Lambda.AspNetCoreServer.Hosting, Microsoft.AspNetCore.Mvc.Testing, FsCheck.Xunit, FluentValidation.DependencyInjectionExtensions
    - _Requirements: 2.5_

- [x] 2. Implement the Bot_Host application core
  - [x] 2.1 Convert the Bot_Host project to ASP.NET Core Web SDK with Autofac
    - Change `src/LDK.RideClub.Bot/RideClub.Bot.csproj` SDK to `Microsoft.NET.Sdk.Web`, keep OutputType Exe
    - Add PackageReferences (no Version attribute): Autofac.Extensions.DependencyInjection, Serilog.AspNetCore, FluentValidation.DependencyInjectionExtensions, Microsoft.EntityFrameworkCore.Sqlite, OpenTelemetry.Extensions.Hosting, OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http, OpenTelemetry.Instrumentation.EntityFrameworkCore, OpenTelemetry.Exporter.OpenTelemetryProtocol, Amazon.Lambda.AspNetCoreServer.Hosting
    - Add ProjectReferences to LDK.RideClub.Bot.Abstractions, LDK.RideClub.Bot.Domain
    - _Requirements: 1.1, 1.3, 2.1_

  - [x] 2.2 Implement Program.cs with deployment mode selection and host builder
    - Read DEPLOYMENT_MODE environment variable; fail fast with error message if invalid (Req 13.4)
    - Configure WebApplication.CreateBuilder with Autofac service provider factory
    - Register Autofac modules (MessagingModule, PersistenceModule, ObservabilityModule)
    - Add AWS Lambda hosting when mode is "lambda"
    - Configure configuration sources: appsettings.json (required), appsettings.{env}.json (optional), environment variables, user-secrets in Development
    - Configure graceful shutdown timeout of 10 seconds
    - Wire middleware pipeline: GlobalExceptionMiddleware, health checks, webhook endpoints
    - Call app.Run()
    - _Requirements: 1.2, 1.5, 1.6, 1.7, 13.1, 13.2, 13.5_

  - [x] 2.3 Implement Options_Model classes with FluentValidation validators
    - Create `Configuration/` folder with: WebhookOptions, WhatsAppOptions, PersistenceOptions, OtlpOptions, DeploymentOptions
    - Create FluentValidation validators for each Options_Model class
    - Register options with ValidateOnStart() and ValidateDataAnnotations()
    - Implement IOptionsMonitor change callback that validates and logs warnings on invalid runtime changes
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

  - [x] 2.4 Create appsettings.json and appsettings.Development.json
    - Define configuration sections: Webhooks, Adapters:WhatsApp, Persistence, Otlp, Deployment
    - Set sensible development defaults (SQLite path, OTLP endpoint to Aspire Dashboard)
    - _Requirements: 1.5_

- [x] 3. Implement the webhook HTTP pipeline
  - [x] 3.1 Implement AdapterRegistry class
    - Create `src/LDK.RideClub.Bot/Services/AdapterRegistry.cs` implementing IAdapterRegistry
    - Accept IEnumerable<IMessagingAdapter> via constructor injection
    - Build dictionary keyed by PlatformId (case-insensitive)
    - Throw on duplicate platform identifiers during construction
    - _Requirements: 8.3, 8.5, 8.6_

  - [x] 3.2 Implement WebhookHandler and route registration
    - Create `src/LDK.RideClub.Bot/Endpoints/WebhookEndpoints.cs` with MapGroup and route handlers
    - Implement POST handler: resolve adapter → verify signature (401) → check body size (413) → deserialise → process → 200
    - Implement GET handler: delegate to adapter's HandleVerificationChallengeAsync
    - Return 404 for unmatched platform identifiers
    - Return 405 for unsupported HTTP methods (with Allow header)
    - Return 413 for bodies exceeding 1 MB
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 7.9_

  - [x] 3.3 Implement GlobalExceptionMiddleware
    - Create `src/LDK.RideClub.Bot/Middleware/GlobalExceptionMiddleware.cs`
    - Catch unhandled exceptions, log via ILogger, return 500 with JSON error body
    - _Requirements: 7.5_

  - [x] 3.4 Implement request body size limit middleware
    - Create middleware or use Kestrel's MaxRequestBodySize to enforce 1 MB limit on webhook endpoints
    - Return 413 Content Too Large when exceeded
    - _Requirements: 7.9_

- [~] 4. Checkpoint - Ensure solution builds
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement the WhatsApp messaging adapter
  - [x] 5.1 Create the LDK.RideClub.Bot.Adapters.WhatsApp project
    - Create `src/LDK.RideClub.Bot.Adapters.WhatsApp/LDK.RideClub.Bot.Adapters.WhatsApp.csproj` as a class library
    - Add ProjectReferences to Abstractions and Domain projects
    - Add folder structure: `DTOs/`
    - Add the project to LDK.RideClub.Bot.slnx
    - _Requirements: 8.4_

  - [x] 5.2 Implement WhatsApp Platform_DTO classes
    - Create DTO classes in `DTOs/`: WhatsAppWebhookPayload, WhatsAppEntry, WhatsAppChange, WhatsAppValue, WhatsAppMessage, WhatsAppTextBody
    - Use System.Text.Json attributes (JsonPropertyName) for serialization
    - _Requirements: 6.2_

  - [x] 5.3 Implement WhatsAppMessagingAdapter
    - Create `WhatsAppMessagingAdapter.cs` implementing IMessagingAdapter
    - PlatformId = "whatsapp"
    - Implement VerifyWebhookAsync using HMAC-SHA256 signature verification with WhatsAppOptions.VerifyToken
    - Implement HandleVerificationChallengeAsync for hub.challenge token exchange
    - Implement DeserialiseEventAsync mapping WhatsAppWebhookPayload → InboundEvent (return MappingFailure on invalid DTOs)
    - Implement SendMessageAsync (stub implementation returning success for now)
    - _Requirements: 6.3, 6.4, 6.5, 8.1, 8.2, 8.4_

  - [x] 5.4 Create Autofac MessagingModule
    - Create `src/LDK.RideClub.Bot/Modules/MessagingModule.cs`
    - Register all IMessagingAdapter implementations from adapter assemblies
    - Register AdapterRegistry as singleton
    - Register IEventProcessor implementation
    - _Requirements: 1.3, 4.4_

- [x] 6. Implement data persistence layer
  - [x] 6.1 Create the LDK.RideClub.Bot.Persistence project
    - Create `src/LDK.RideClub.Bot.Persistence/LDK.RideClub.Bot.Persistence.csproj` as a class library
    - Add PackageReferences: Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design
    - Add ProjectReference to Domain project
    - Add the project to LDK.RideClub.Bot.slnx
    - _Requirements: 9.1_

  - [x] 6.2 Implement BotDbContext and entity configuration
    - Create `BotDbContext.cs` with DbSet<MessageLog>
    - Create `MessageLog` entity class with Id, Platform, EventId, SenderId, ConversationId, ReceivedAt, PayloadType, RawPayload
    - Create entity configuration class with table name, indexes, and column constraints
    - _Requirements: 9.1, 9.5, 9.9_

  - [x] 6.3 Create initial EF Core migration
    - Add initial migration using EF Core tooling conventions (Migrations/ folder)
    - Ensure migration is compatible with `dotnet ef` CLI
    - _Requirements: 9.3_

  - [x] 6.4 Create Autofac PersistenceModule with startup migration logic
    - Create `src/LDK.RideClub.Bot/Modules/PersistenceModule.cs`
    - Register BotDbContext with scoped lifetime using SQLite provider
    - Validate PersistenceOptions (connection string, file path accessibility)
    - Implement auto-migration on startup in Development environment only
    - Fail startup with descriptive error if migration fails or path is invalid
    - _Requirements: 9.2, 9.4, 9.5, 9.6, 9.7, 9.8_

- [x] 7. Implement observability pipeline
  - [x] 7.1 Create the LDK.RideClub.Bot.Observability project
    - Create `src/LDK.RideClub.Bot.Observability/LDK.RideClub.Bot.Observability.csproj` as a class library
    - Add PackageReferences: OpenTelemetry.Extensions.Hosting, OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http, OpenTelemetry.Instrumentation.EntityFrameworkCore, OpenTelemetry.Exporter.OpenTelemetryProtocol, Serilog.AspNetCore
    - Add the project to LDK.RideClub.Bot.slnx
    - _Requirements: 11.1_

  - [x] 7.2 Implement ObservabilityExtensions for OpenTelemetry and Serilog
    - Create `ObservabilityExtensions.cs` with extension methods for IHostBuilder/IServiceCollection
    - Configure OpenTelemetry tracing: ASP.NET Core, HttpClient, EF Core instrumentation
    - Configure OpenTelemetry metrics: ASP.NET Core, HttpClient instrumentation
    - Configure OTLP exporter with endpoint from OtlpOptions
    - Set service resource name from configuration
    - Handle OTLP export failures gracefully (log warning, continue operation)
    - Configure Serilog to replace default logging, enrich from HTTP context, correlate with trace context
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.6, 11.7_

  - [x] 7.3 Add messaging adapter activity source and span creation
    - Create a custom ActivitySource for messaging adapter operations
    - Instrument adapter calls with child spans including `messaging.platform` and `messaging.operation` attributes
    - _Requirements: 11.5_

  - [x] 7.4 Create Autofac ObservabilityModule
    - Create `src/LDK.RideClub.Bot/Modules/ObservabilityModule.cs`
    - Wire observability extensions into the DI container
    - _Requirements: 11.1, 11.2_

- [~] 8. Checkpoint - Ensure solution builds and core pipeline works
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement containerisation and docker-compose
  - [x] 9.1 Create multi-stage Dockerfile
    - Build stage: restore and compile the full solution
    - Publish stage: produce runtime image with published output only
    - Support both Kestrel and Lambda entry points selectable via DEPLOYMENT_MODE
    - Target .NET 10 runtime image
    - _Requirements: 10.1, 13.3_

  - [x] 9.2 Create docker-compose.yml
    - Define Bot_Host service with health check (HTTP GET to /health), restart policy (max 3 attempts), environment variables for config, volume mount for SQLite database
    - Define Aspire Dashboard service with port mapping for UI access
    - Configure networking so Bot_Host can reach Aspire Dashboard OTLP endpoint via service name
    - Pass OTLP endpoint URL and SQLite path via environment variables
    - _Requirements: 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8_

  - [x] 9.3 Add health check endpoint to Bot_Host
    - Register ASP.NET Core health checks in Program.cs
    - Map /health endpoint returning 200 OK when healthy
    - _Requirements: 10.2, 12.5_

- [ ] 10. Implement testing infrastructure
  - [x] 10.1 Create the LDK.RideClub.Bot.Tests xUnit project
    - Create `tests/LDK.RideClub.Bot.Tests/LDK.RideClub.Bot.Tests.csproj`
    - Add PackageReferences: xunit, xunit.runner.visualstudio, coverlet.collector, NSubstitute, FluentAssertions, FsCheck.Xunit, Microsoft.AspNetCore.Mvc.Testing, Microsoft.EntityFrameworkCore.InMemory
    - Add ProjectReferences to Bot_Host, Abstractions, Domain, Persistence, Adapters.WhatsApp projects
    - Add the project to LDK.RideClub.Bot.slnx
    - _Requirements: 12.1, 12.2_

  - [x] 10.2 Implement WebApplicationFactory fixture
    - Create `Integration/BotWebApplicationFactory.cs` deriving from WebApplicationFactory<Program>
    - Replace SQLite with EF Core InMemory provider
    - Remove OTLP exporter from service collection
    - Set DEPLOYMENT_MODE to "kestrel" for test environment
    - Register mock adapters as needed
    - _Requirements: 12.3, 12.4_

  - [-] 10.3 Implement health check smoke test
    - Create `Integration/HealthCheckTests.cs`
    - Use BotWebApplicationFactory to start the host in-memory
    - Issue HTTP GET to /health and assert 200 OK within 5 seconds
    - _Requirements: 12.5_

  - [-] 10.4 Write property tests for architectural constraints (Properties 1-4, 7)
    - **Property 1: Constructor Dependency Limit** — scan application assemblies, verify no constructor exceeds 5 params (excluding single cross-cutting concern)
    - **Property 2: Dependencies on Abstractions** — verify logging/persistence/messaging params are interface types
    - **Property 3: No Service Locator Pattern** — verify no IServiceProvider usage outside composition root
    - **Property 4: Interface Segregation Limit** — verify no interface declares more than 5 methods
    - **Property 7: No Raw JSON in Component Interfaces** — verify no string/Dictionary/dynamic/JsonElement for domain data in public methods
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.5, 6.4**

  - [-] 10.5 Write property tests for webhook pipeline (Properties 9-13)
    - **Property 9: Adapter Resolution by Platform Identifier** — POST to /webhooks/{platformId} routes to correct adapter
    - **Property 10: Invalid Verification Token Returns 401** — invalid/missing tokens yield 401
    - **Property 11: Adapter Exception Returns 500** — adapter exceptions yield 500 with logging
    - **Property 12: Unsupported HTTP Method Returns 405** — non-POST/GET methods yield 405
    - **Property 13: Unmatched Platform Returns 404** — unknown platform IDs yield 404
    - **Validates: Requirements 7.2, 7.3, 7.5, 7.7, 7.8, 8.3, 8.5**

  - [-] 10.6 Write property tests for DTO mapping (Properties 6, 8)
    - **Property 6: DTO-to-Domain Mapping Purity** — mapped Domain_Models contain no platform-specific types
    - **Property 8: Invalid DTO Rejection** — invalid DTOs produce MappingFailure with descriptive reason
    - **Validates: Requirements 6.3, 6.5**

  - [-] 10.7 Write property tests for configuration validation (Property 5)
    - **Property 5: Options Validation Rejects Invalid Configuration** — invalid options produce error with class name and failed property names
    - **Validates: Requirements 5.2, 5.3**

  - [-] 10.8 Write property tests for deployment mode and observability (Properties 14, 15)
    - **Property 14: Adapter Span Attributes** — adapter processing creates spans with messaging.platform and messaging.operation attributes
    - **Property 15: Invalid Deployment Mode Fails Fast** — invalid DEPLOYMENT_MODE values cause startup failure with descriptive error
    - **Validates: Requirements 11.5, 13.4**

  - [-] 10.9 Write unit tests for AdapterRegistry and WhatsApp adapter
    - Test duplicate platform ID detection throws at construction
    - Test case-insensitive platform ID lookup
    - Test WhatsApp signature verification with valid/invalid tokens
    - Test WhatsApp DTO deserialization with valid/malformed payloads
    - Test WhatsApp verification challenge handling
    - _Requirements: 8.3, 8.5, 8.6, 6.5, 6.6_

- [~] 11. Final checkpoint - Ensure full solution builds and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# throughout, so all implementation uses C# with .NET 10
- FsCheck.Xunit is used for property-based testing as specified in the design
- All projects use the `LDK.RideClub.Bot` namespace prefix per repository convention

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4"] },
    { "id": 3, "tasks": ["3.1", "3.2", "3.3", "3.4", "5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3", "5.4"] },
    { "id": 5, "tasks": ["6.1"] },
    { "id": 6, "tasks": ["6.2"] },
    { "id": 7, "tasks": ["6.3", "6.4"] },
    { "id": 8, "tasks": ["7.1"] },
    { "id": 9, "tasks": ["7.2", "7.3", "7.4"] },
    { "id": 10, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 11, "tasks": ["10.1"] },
    { "id": 12, "tasks": ["10.2"] },
    { "id": 13, "tasks": ["10.3", "10.4", "10.5", "10.6", "10.7", "10.8", "10.9"] }
  ]
}
```
