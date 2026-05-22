# Requirements Document

## Introduction

This document defines the platform foundation requirements for the RideClub Bot — a multi-platform messaging bot for organising motorbike club ride outs. The focus is on establishing the application skeleton, dependency injection, webhook handling, data persistence, containerisation, and observability infrastructure. Specific bot features (ride scheduling, RSVP, etc.) are out of scope for this phase.

## Glossary

- **Platform**: The foundational application infrastructure including hosting, DI, HTTP pipeline, and deployment configuration
- **Bot_Host**: The ASP.NET Core application (LDK.RideClub.Bot) that receives and dispatches webhook events from messaging platforms
- **Webhook_Endpoint**: An HTTP endpoint exposed by the Bot_Host that receives inbound events from a messaging platform API
- **Messaging_Adapter**: A platform-specific integration component that translates between a messaging platform's API and the internal domain model
- **DI_Container**: The Autofac-extended Microsoft Generic Host dependency injection container
- **Persistence_Layer**: The Entity Framework Core data access layer backed by SQLite for local development
- **Observability_Pipeline**: The OpenTelemetry instrumentation and export pipeline that feeds traces, metrics, and logs to the Aspire Dashboard
- **Compose_Environment**: The docker-compose project that orchestrates the Bot_Host container and supporting services locally
- **Options_Model**: A strongly-typed POCO class bound to a configuration section via the .NET Options pattern
- **Domain_Model**: A strongly-typed class representing an internal domain concept, distinct from external API DTOs
- **Platform_DTO**: A strongly-typed data transfer object representing the shape of data received from or sent to an external messaging platform API
- **Monorepo**: A single Git repository containing multiple .NET projects organised under a single solution (LDK.RideClub.Bot.slnx), with shared class libraries consumable by other applications
- **Code_Style_Rules**: The .editorconfig and analyzer configuration that enforce consistent .NET coding conventions across all projects in the Monorepo
- **Serilog_Pipeline**: The structured logging pipeline provided by Serilog.AspNetCore that integrates with the Microsoft.Extensions.Logging abstraction
- **FluentValidation_Validator**: A validation class implementing AbstractValidator&lt;T&gt; from the FluentValidation library, used to validate Options_Model instances at startup

## Requirements

### Requirement 1: Application Hosting

**User Story:** As a developer, I want a .NET application built on the Microsoft Generic Host, so that I have a consistent foundation for background services, configuration, and lifetime management.

#### Acceptance Criteria

1. THE Bot_Host SHALL be an ASP.NET Core application targeting .NET 10 (net10.0) as specified in global.json (SDK 10.0.100) and Directory.Build.props
2. THE Bot_Host SHALL use the Microsoft Generic Host builder pattern for application startup
3. THE DI_Container SHALL extend the Microsoft Generic Host service provider with Autofac module registration
4. WHEN the Bot_Host starts, THE DI_Container SHALL validate the container by resolving all registered services during host startup, and SHALL throw an exception that prevents the host from accepting requests if any registration is unresolvable
5. THE Bot_Host SHALL load configuration from appsettings.json, appsettings.{EnvironmentName}.json, environment variables, and user-secrets (in development), where each subsequent source overrides values from the previous source in the listed order
6. IF the appsettings.json file is missing or contains malformed JSON at startup, THEN THE Bot_Host SHALL fail to start and log an error message identifying the configuration file issue
7. IF the Bot_Host process receives a SIGTERM or SIGINT signal, THEN THE Bot_Host SHALL initiate graceful shutdown completing within 10 seconds, allowing in-flight requests to finish before stopping the host

### Requirement 2: Monorepo and Solution Structure

**User Story:** As a developer, I want the repository structured as a monorepo with a single .NET solution containing multiple projects, so that shared types and libraries can be consumed by other applications in the same repository.

#### Acceptance Criteria

1. THE Monorepo SHALL contain a single LDK.RideClub.Bot.slnx solution file at the repository root that references all projects in the repository
2. THE Monorepo SHALL organise projects into a src/ directory for application and library projects and a tests/ directory for test projects
3. THE Platform SHALL define at least one shared class library for domain models and at least one shared class library for abstractions (interfaces and contracts), each residing in the src/ directory with an OutputType of Library and using the LDK.RideClub.Bot namespace prefix, and each referenceable via ProjectReference by other applications in the Monorepo
4. THE Monorepo SHALL use a Directory.Build.props file at the repository root to define common MSBuild properties (including TargetFramework net10.0, LangVersion latest, Nullable enable, ImplicitUsings enable, TreatWarningsAsErrors true, Deterministic true, EnableNETAnalyzers true, AnalysisLevel latest, AnalysisMode All, EnforceCodeStyleInBuild true, GenerateDocumentationFile true, and IncludeSymbols true) inherited by all projects
5. THE Monorepo SHALL use a Directory.Packages.props file at the repository root to centralise NuGet package version management with ManagePackageVersionsCentrally set to true, such that individual project files use PackageReference without a Version attribute and all version declarations exist only in Directory.Packages.props
6. WHEN a developer runs `dotnet build` from the repository root, THE Monorepo SHALL build all referenced projects and produce exit code 0 with no error-level diagnostics

### Requirement 3: Code Style and Formatting

**User Story:** As a developer, I want consistent .NET code style rules enforced across the entire solution, so that all contributors produce uniform, readable code without manual review overhead.

#### Acceptance Criteria

1. THE Monorepo SHALL include an .editorconfig file at the repository root defining C# coding conventions that apply to all projects in the solution
2. THE Code_Style_Rules SHALL enforce .NET naming conventions: PascalCase for namespaces, classes, interfaces, methods, and properties; camelCase for local variables and parameters; an "I" prefix for interface names; and an underscore-camelCase prefix for private fields (reported as suggestion severity)
3. THE Code_Style_Rules SHALL enforce formatting rules: 4-space indentation, Allman-style brace placement (csharp_new_line_before_open_brace = all), file-scoped namespaces (csharp_style_namespace_declarations = file_scoped), var usage set to false for built-in types and elsewhere but true when type is apparent, required braces for control flow statements reported as error (csharp_prefer_braces = true:error), and required accessibility modifiers reported as error (dotnet_style_require_accessibility_modifiers = always:error)
4. THE Code_Style_Rules SHALL enable Roslyn analyzers via EnforceCodeStyleInBuild set to true in Directory.Build.props, with TreatWarningsAsErrors set to true so that Style, Performance, and Reliability analyzer categories (configured as warning severity in .editorconfig) are promoted to build errors
5. WHEN a developer runs `dotnet build`, THE Code_Style_Rules SHALL report naming and style violations as MSBuild diagnostics without requiring additional tooling beyond the .NET SDK
6. IF a source file violates a naming convention rule, THEN THE Code_Style_Rules SHALL produce a build diagnostic identifying the file, line number, and violated rule

### Requirement 4: SOLID Architecture and Extensibility

**User Story:** As a developer, I want the codebase to follow SOLID principles, so that components are easy to test, extend, and maintain independently.

#### Acceptance Criteria

1. THE Platform SHALL define single-responsibility classes where each class has no more than one injected cross-cutting concern (defined as: ILogger&lt;T&gt;, telemetry/metrics interfaces, or IOptions&lt;T&gt;/IOptionsSnapshot&lt;T&gt;/IOptionsMonitor&lt;T&gt;) and no more than 5 constructor dependencies in total including the cross-cutting concern
2. THE Platform SHALL depend on abstractions (interfaces) rather than concrete implementations for logging, persistence, and messaging concerns, such that application classes reference only interface types for these dependencies
3. THE DI_Container SHALL resolve dependencies via constructor injection exclusively, with no service-locator pattern (e.g., resolving from IServiceProvider) in application code outside the composition root (defined as: Program.cs, Autofac modules, and DI registration extension methods)
4. WHEN a new Messaging_Adapter is added, THE Platform SHALL require only a new adapter class implementing the existing Messaging_Adapter interface and a DI_Container registration, with no modifications to existing adapter implementations or the HTTP pipeline
5. THE Platform SHALL segregate interfaces so that no single interface declares more than 5 methods, and no class depends on an interface containing methods that the class does not invoke
6. THE Platform SHALL ensure that all implementations of a given interface pass the same behavioural test suite, implemented as a shared abstract test fixture parameterized over each implementation, confirming Liskov substitutability
7. WHEN a developer runs `dotnet test`, THE Platform SHALL execute architectural tests that verify: constructor dependency counts do not exceed 5, no application class outside the composition root references IServiceProvider, and no interface declares more than 5 methods

### Requirement 5: Strongly-Typed Configuration

**User Story:** As a developer, I want application behaviour to be driven by strongly-typed configuration classes bound via the .NET Options pattern, so that settings are validated at startup and refactoring-safe.

#### Acceptance Criteria

1. THE Bot_Host SHALL bind each logical configuration section to a dedicated Options_Model class, using IOptions&lt;T&gt; for static configuration that does not change after startup, IOptionsSnapshot&lt;T&gt; for scoped configuration that may change between requests, and IOptionsMonitor&lt;T&gt; for singleton services that must observe runtime changes
2. WHEN the Bot_Host starts, THE Bot_Host SHALL eagerly validate all registered Options_Model instances using FluentValidation_Validator classes (implementing AbstractValidator&lt;T&gt; from FluentValidation v12.0.0) before the application begins accepting requests
3. IF an Options_Model fails FluentValidation at startup, THEN THE Bot_Host SHALL terminate the host startup process and emit an error message that identifies the Options_Model class name and the name of each property that failed validation along with the validation error message
4. IF a required configuration section is entirely missing from all configuration sources, THEN THE Bot_Host SHALL treat the missing section as a validation failure and terminate the host startup process with an error message identifying the missing section name
5. THE Platform SHALL define separate Options_Model classes for each Messaging_Adapter's credentials and endpoint configuration
6. WHEN a configuration source bound to an IOptionsMonitor&lt;T&gt; or IOptionsSnapshot&lt;T&gt; registration changes at runtime, THE Bot_Host SHALL resolve the updated values on the next injection scope or change-callback invocation without requiring a process restart
7. IF a configuration source bound to an IOptionsMonitor&lt;T&gt; or IOptionsSnapshot&lt;T&gt; registration changes at runtime and the updated values fail FluentValidation, THEN THE Bot_Host SHALL retain the last valid configuration values and log a warning via the Serilog_Pipeline that identifies the Options_Model class name and the properties that failed validation

### Requirement 6: Strongly-Typed Models and DTOs

**User Story:** As a developer, I want all data flowing through the system to be represented by strongly-typed classes, so that compile-time safety prevents data-shape errors.

#### Acceptance Criteria

1. THE Platform SHALL define Domain_Model classes for internal representations of messaging events, commands, and responses, where each Domain_Model class uses non-nullable properties for required fields and nullable properties for optional fields so that missing data is detected at compile time
2. THE Platform SHALL define Platform_DTO classes for each external messaging platform's inbound and outbound payload shapes
3. THE Messaging_Adapter SHALL map between Platform_DTO instances and Domain_Model instances such that Domain_Model classes contain no references to platform-specific SDK types, serialization attributes, or external API identifiers
4. THE Platform SHALL not pass raw JSON strings, dictionaries, or dynamic types between components for data representing messaging events, commands, or responses; raw payloads SHALL be deserialized into Platform_DTO instances at the Webhook_Endpoint boundary before being forwarded to the Messaging_Adapter
5. IF a Platform_DTO cannot be mapped to a Domain_Model due to missing required fields or an unrecognised payload structure, THEN THE Messaging_Adapter SHALL reject the input and return an error indication identifying the mapping failure reason and the name of the Platform_DTO class that failed mapping
6. IF the Webhook_Endpoint receives a request body that cannot be deserialized into the expected Platform_DTO due to malformed JSON or a type mismatch, THEN THE Webhook_Endpoint SHALL reject the request with an error indication identifying the deserialization failure reason and SHALL NOT forward the request to the Messaging_Adapter

### Requirement 7: Webhook HTTP Pipeline

**User Story:** As a developer, I want the application to expose HTTP endpoints for receiving messaging platform webhooks, so that inbound events can be processed by the appropriate adapter.

#### Acceptance Criteria

1. THE Bot_Host SHALL expose a configurable base path for Webhook_Endpoints via an Options_Model, with each registered Messaging_Adapter accessible at a dedicated sub-path segment derived from the adapter's platform identifier beneath the base path
2. WHEN an HTTP POST request is received at a Webhook_Endpoint, THE Bot_Host SHALL identify the target Messaging_Adapter by matching the request path segment to the adapter's platform identifier and route the full request to that adapter for processing
3. IF the target Messaging_Adapter's signature verification method returns false for an inbound request, THEN THE Bot_Host SHALL respond with HTTP 401 Unauthorized and SHALL NOT forward the request to the Messaging_Adapter for event processing
4. WHEN a Webhook_Endpoint receives a request that passes signature verification and matches a registered adapter, THE Bot_Host SHALL respond with HTTP 200 OK within 3 seconds of receiving the request
5. IF a Messaging_Adapter throws an unhandled exception during request processing, THEN THE Bot_Host SHALL respond with HTTP 500 Internal Server Error and log the exception via the Serilog_Pipeline and Observability_Pipeline
6. WHEN a Webhook_Endpoint receives an HTTP GET request at a path matching a registered Messaging_Adapter, THE Bot_Host SHALL delegate the request to the corresponding Messaging_Adapter's verification handler and return the adapter's response including status code and body
7. IF a Webhook_Endpoint receives a request using an HTTP method other than POST or GET, THEN THE Bot_Host SHALL respond with HTTP 405 Method Not Allowed and include an Allow header listing POST and GET
8. IF a request path does not match any registered Messaging_Adapter sub-path, THEN THE Bot_Host SHALL respond with HTTP 404 Not Found
9. IF an HTTP POST request to a Webhook_Endpoint has a body exceeding 1 MB, THEN THE Bot_Host SHALL respond with HTTP 413 Content Too Large and SHALL NOT forward the request to the Messaging_Adapter

### Requirement 8: Messaging Adapter Abstraction

**User Story:** As a developer, I want a common abstraction for messaging platform integrations, so that new platforms can be added without modifying the core pipeline.

#### Acceptance Criteria

1. THE Platform SHALL define a Messaging_Adapter interface that all platform-specific adapters implement
2. THE Messaging_Adapter interface SHALL declare a platform identifier property containing a non-empty lowercase alphanumeric string (maximum 32 characters) that uniquely identifies the messaging platform the adapter handles, a method for verifying webhook signatures that accepts the raw request body bytes and platform-provided signature header value and returns a boolean result, a method for handling platform verification challenges that accepts the inbound request and returns the platform-expected challenge response, a method for deserialising inbound events from Platform_DTO to Domain_Model, and a method for sending outbound messages that accepts a Domain_Model and returns a result indicating success or a failure containing an error reason
3. WHEN a new Messaging_Adapter is registered in the DI_Container, THE Bot_Host SHALL resolve the adapter by performing a case-insensitive match of the platform identifier from the inbound webhook URL path segment to the adapter's declared platform identifier, routing the request without code changes to the HTTP pipeline
4. THE Platform SHALL include a Messaging_Adapter implementation for at least one of: WhatsApp Business API, Telegram Bot API, or Discord Gateway
5. IF no registered Messaging_Adapter matches the platform identifier in an inbound webhook request, THEN THE Bot_Host SHALL respond with HTTP 404 Not Found
6. IF two or more registered Messaging_Adapter implementations declare the same platform identifier value, THEN THE Bot_Host SHALL fail to start and log an error message identifying the duplicate platform identifier

### Requirement 9: Data Persistence

**User Story:** As a developer, I want Entity Framework Core with SQLite configured for local development, so that I can persist and query application state without external database infrastructure.

#### Acceptance Criteria

1. THE Persistence_Layer SHALL use Entity Framework Core as the ORM
2. THE Persistence_Layer SHALL use SQLite as the database provider for local development, with the database file path configurable via a dedicated Options_Model that declares the file path property as required and is validated by a FluentValidation_Validator
3. THE Persistence_Layer SHALL be compatible with the EF Core CLI tooling such that developers can add, remove, and script migrations using `dotnet ef` commands
4. WHEN the Bot_Host starts with the hosting environment set to "Development" (as determined by IHostEnvironment.IsDevelopment()), THE Persistence_Layer SHALL automatically apply all pending migrations before the application begins accepting requests
5. THE Persistence_Layer SHALL register the DbContext in the DI_Container with a scoped lifetime
6. IF automatic migration application fails during startup in development mode, THEN THE Bot_Host SHALL terminate the host startup process with an error message identifying the failed migration name and the underlying exception message
7. WHILE the Bot_Host is running in a non-development environment, THE Persistence_Layer SHALL NOT automatically apply migrations at startup
8. IF the configured SQLite database file path refers to a directory that does not exist or a location the process cannot write to, THEN THE Persistence_Layer SHALL fail at startup with an error message identifying the invalid path and the reason for failure
9. THE Persistence_Layer SHALL define at least one DbSet property on the DbContext, backed by an entity configuration, to verify that migrations and query operations function correctly

### Requirement 10: Containerisation and Local Development

**User Story:** As a developer, I want a Docker container image and docker-compose environment, so that I can build and run the application locally in a production-like configuration.

#### Acceptance Criteria

1. THE Platform SHALL include a multi-stage Dockerfile that builds and publishes the Bot_Host as a self-contained container image targeting .NET 10, where the build stage restores and compiles the LDK.RideClub.Bot solution and the publish stage produces a runtime image containing only the published output
2. THE Compose_Environment SHALL define a service for the Bot_Host container with a health check that issues an HTTP GET to the Bot_Host health-check endpoint and marks the service as healthy when it receives an HTTP 200 response
3. THE Compose_Environment SHALL define a service for the Aspire Dashboard container, mapped to a host port such that a developer can open the Aspire Dashboard UI in a browser and receive an HTTP 200 response
4. WHEN a developer runs `docker-compose up`, THE Compose_Environment SHALL start all services and the Bot_Host health check SHALL report healthy within 30 seconds of container start
5. THE Compose_Environment SHALL support volume-mounting the SQLite database file from a host directory into the Bot_Host container so that data persists across container stop and restart cycles
6. THE Compose_Environment SHALL pass application configuration to the Bot_Host container via environment variables or mounted configuration files, including at minimum the SQLite database path and the OTLP export endpoint URL
7. THE Compose_Environment SHALL configure networking so that the Bot_Host container can reach the Aspire Dashboard OTLP endpoint using the Compose service name as the hostname
8. IF the Bot_Host container exits with a non-zero exit code, THEN THE Compose_Environment SHALL restart the container automatically up to a maximum of 3 restart attempts

### Requirement 11: Observability

**User Story:** As a developer, I want structured logging via Serilog and OpenTelemetry instrumentation exporting to the Aspire Dashboard, so that I can observe traces, metrics, and logs during local development.

#### Acceptance Criteria

1. THE Observability_Pipeline SHALL instrument the Bot_Host with OpenTelemetry for distributed tracing, metrics, and logging, including ASP.NET Core HTTP request instrumentation, HttpClient outbound call instrumentation, and Entity Framework Core query instrumentation
2. THE Observability_Pipeline SHALL export telemetry data to the Aspire Dashboard using the OTLP exporter, with the export endpoint URL configured via an Options_Model bound from application configuration, and SHALL set the OpenTelemetry service resource name to a value configured in the Options_Model so that the Bot_Host is identifiable in the Aspire Dashboard
3. THE Serilog_Pipeline SHALL be configured via Serilog.AspNetCore (v9.0.0) to replace the default Microsoft.Extensions.Logging provider, providing structured logging with enrichment from the HTTP request context
4. WHEN an HTTP request is received, THE Observability_Pipeline SHALL create a trace span that starts when the request is received and ends when the response is sent, including the HTTP method, route, and response status code as span attributes
5. WHEN a Messaging_Adapter processes an event, THE Observability_Pipeline SHALL create a child span that includes at minimum a `messaging.platform` attribute identifying the adapter (e.g., "whatsapp", "telegram", "discord") and a `messaging.operation` attribute describing the action performed
6. IF the OTLP export endpoint is unavailable due to connection failure, DNS resolution failure, or no response within 10 seconds, THEN THE Observability_Pipeline SHALL log a warning via the Serilog_Pipeline indicating the endpoint address, drop the telemetry batch, and continue operation without crashing or blocking request processing
7. THE Observability_Pipeline SHALL correlate log entries with the active trace context so that logs emitted during a request include the trace ID and span ID

### Requirement 12: Testing Infrastructure

**User Story:** As a developer, I want a test project configured with xUnit, NSubstitute, and FluentAssertions, so that I can write unit and integration tests for the platform from day one.

#### Acceptance Criteria

1. THE Platform SHALL include an xUnit test project located in the tests/ directory, referenced by the LDK.RideClub.Bot.slnx solution file, using the LDK.RideClub.Bot.Tests namespace prefix, with a ProjectReference to the Bot_Host project and using xunit 2.9.3, xunit.runner.visualstudio 3.1.4, and coverlet.collector 6.0.4 as defined in Directory.Packages.props
2. THE Platform SHALL include NSubstitute (v5.3.0) as the mocking framework and FluentAssertions (v8.8.0) as the assertion library in the test project, with versions managed centrally via Directory.Packages.props
3. THE Platform SHALL include a reusable WebApplicationFactory&lt;Program&gt;-derived fixture class that bootstraps the Bot_Host in-memory with external dependencies replaced: the SQLite provider replaced by the EF Core in-memory provider, and the OTLP exporter removed from the service collection
4. WHEN a developer runs `dotnet test` from the repository root, THE test project SHALL discover and execute all tests without requiring network access, Docker, running containers, or any external process
5. THE test project SHALL include at least one integration smoke test that uses the WebApplicationFactory fixture to start the Bot_Host and issues an HTTP GET to the health-check endpoint, asserting that the response returns HTTP 200 OK within 5 seconds

### Requirement 13: Deployment Readiness

**User Story:** As a developer, I want the container image to be deployable as either a long-running service or an AWS Lambda function, so that I have flexibility in hosting strategy.

#### Acceptance Criteria

1. THE Bot_Host SHALL read a `DEPLOYMENT_MODE` environment variable that accepts the values `kestrel` (case-insensitive) for long-running web server mode and `lambda` (case-insensitive) for AWS Lambda function handler mode
2. WHERE the Lambda deployment option is selected, THE Bot_Host SHALL use the AWS Lambda ASP.NET Core hosting package to translate API Gateway events into ASP.NET Core requests, such that the existing Webhook_Endpoints and middleware pipeline function without modification
3. THE Dockerfile SHALL produce an image that contains both the Kestrel entry point and the Lambda entry point, selectable at runtime via the `DEPLOYMENT_MODE` environment variable without rebuilding the image
4. IF the `DEPLOYMENT_MODE` environment variable is missing or set to a value other than `kestrel` or `lambda`, THEN THE Bot_Host SHALL terminate within 5 seconds of startup and emit an error message identifying the invalid value received and listing the valid options
5. WHEN the `DEPLOYMENT_MODE` environment variable is set to `kestrel`, THE Bot_Host SHALL start the Kestrel web server and begin accepting HTTP requests on the configured port within 30 seconds of process start
