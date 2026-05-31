# Implementation Plan: Unified MassTransit Pipeline Migration

## Overview

This plan migrates the RideClub Bot from a dual MediatR + MassTransit architecture to a unified MassTransit-only approach. MassTransit's built-in mediator (`AddMediator` / `IScopedMediator`) replaces MediatR for in-process command dispatch, while the existing MassTransit bus continues to host the conversation saga state machine. The migration converts MediatR handlers to MassTransit consumers, pipeline behaviors to send filters, and removes all MediatR package dependencies.

## Tasks

- [x] 1. Convert domain contracts to plain records (remove MediatR dependency)
  - [x] 1.1 Convert command types from `IRequest<T>` to plain records
    - Remove `MediatR.Contracts` package reference from `src/LDK.RideClub.Bot.Domain/LDK.RideClub.Bot.Domain.csproj`
    - Update `src/LDK.RideClub.Bot.Domain/Commands/ProcessTextMessageCommand.cs` — remove `using MediatR;` and `: IRequest<MessageProcessingResult>` inheritance, keep as plain `sealed record`
    - Update `src/LDK.RideClub.Bot.Domain/Commands/ProcessBotCommandCommand.cs` — same removal of MediatR interface
    - Verify `MessageProcessingResult` in `src/LDK.RideClub.Bot.Domain/Responses/` has no MediatR dependency (should already be clean)
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 1.2 Convert `UnrecognisedEventNotification` from `INotification` to plain record
    - Update `src/LDK.RideClub.Bot.Domain/Events/UnrecognisedEventNotification.cs` — remove `using MediatR;` and `: INotification` inheritance, keep as plain `sealed record`
    - _Requirements: 2.4_

- [ ] 2. Convert MediatR pipeline behaviors to MassTransit send filters
  - [x] 2.1 Create `LoggingFilter<T>` implementing `IFilter<SendContext<T>>`
    - Create `src/LDK.RideClub.Bot/Filters/LoggingFilter.cs`
    - Implement `Send(SendContext<T> context, IPipe<SendContext<T>> next)` — log command type + request ID before, log type + ID + elapsed ms after
    - Implement `Probe(ProbeContext context)` with scope name `"logging"`
    - _Requirements: 3.1, 3.4_

  - [x] 2.2 Create `ValidationFilter<T>` implementing `IFilter<SendContext<T>>`
    - Create `src/LDK.RideClub.Bot/Filters/ValidationFilter.cs`
    - Resolve `IEnumerable<IValidator<T>>`, run validation, throw `ValidationException` on failure
    - Short-circuit before reaching the consumer if validation fails
    - Implement `Probe(ProbeContext context)` with scope name `"validation"`
    - _Requirements: 3.2, 3.4_

  - [x] 2.3 Create `TelemetryFilter<T>` implementing `IFilter<SendContext<T>>`
    - Create `src/LDK.RideClub.Bot/Filters/TelemetryFilter.cs`
    - Use `ActivitySource` named `RideClub.Bot.Mediator`, start child activity named after command type
    - Record outcome (success/exception) as span attributes
    - Implement `Probe(ProbeContext context)` with scope name `"telemetry"`
    - _Requirements: 3.3, 3.4, 9.1_

  - [x] 2.4 Write unit tests for pipeline filters
    - Create `tests/LDK.RideClub.Bot.Tests/Unit/LoggingFilterTests.cs` — test logs before/after with elapsed time
    - Create `tests/LDK.RideClub.Bot.Tests/Unit/ValidationFilterTests.cs` — test throws on validation failure, passes through on success
    - Create `tests/LDK.RideClub.Bot.Tests/Unit/TelemetryFilterTests.cs` — test creates activity spans with correct attributes
    - _Requirements: 3.1, 3.2, 3.3_

- [x] 3. Convert MediatR handlers to MassTransit consumers
  - [x] 3.1 Create `ProcessTextMessageConsumer` implementing `IConsumer<ProcessTextMessageCommand>`
    - Create `src/LDK.RideClub.Bot/Consumers/ProcessTextMessageConsumer.cs`
    - Inject `IBus` for publishing outcome events to the saga state machine
    - Process the command and publish `ProcessingCompletedEvent` or `ProcessingFaultedEvent` to the bus
    - _Requirements: 2.2, 8.1, 8.2_

  - [x] 3.2 Create `ProcessBotCommandConsumer` implementing `IConsumer<ProcessBotCommandCommand>`
    - Create `src/LDK.RideClub.Bot/Consumers/ProcessBotCommandConsumer.cs`
    - Inject `IBus` for publishing outcome events to the saga state machine
    - Process the command and publish `ProcessingCompletedEvent` or `ProcessingFaultedEvent` to the bus
    - _Requirements: 2.2, 8.1, 8.2_

  - [x] 3.3 Create `UnrecognisedEventConsumer` implementing `IConsumer<UnrecognisedEventNotification>`
    - Create `src/LDK.RideClub.Bot/Consumers/UnrecognisedEventConsumer.cs`
    - Log warning with payload type name, no response needed
    - _Requirements: 2.4_

  - [x] 3.4 Write unit tests for consumers
    - Test `ProcessTextMessageConsumer` publishes `ProcessingCompletedEvent` on success
    - Test `ProcessBotCommandConsumer` publishes `ProcessingFaultedEvent` on failure
    - Test `UnrecognisedEventConsumer` logs warning
    - _Requirements: 8.1, 8.2, 2.4_

- [x] 4. Update EventDispatcher to use `IScopedMediator`
  - [x] 4.1 Rewrite `EventDispatcher` to use MassTransit's `IScopedMediator` instead of MediatR's `IMediator`
    - Update `src/LDK.RideClub.Bot/Services/EventDispatcher.cs`
    - Replace `IMediator mediator` constructor parameter with `IScopedMediator mediator`
    - Replace `mediator.Send(command, ct)` with `mediator.Send(command, ct)` (MassTransit API — fire-and-forget, no response)
    - Replace `mediator.Publish(notification, ct)` with `mediator.Publish(notification, ct)` (MassTransit publish)
    - Remove `using MediatR;` — add `using MassTransit;` and `using MassTransit.Mediator;`
    - Update `MapToCommand` return type from `IRequest<MessageProcessingResult>?` to `object?` (plain records, no marker interface)
    - Remove response handling from `Send` — consumers now publish outcome events directly to the bus
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 8.3, 8.5_

  - [x] 4.2 Update unit tests for EventDispatcher
    - Update `tests/LDK.RideClub.Bot.Tests/Unit/EventDispatcherTests.cs` — mock `IScopedMediator` instead of MediatR `IMediator`
    - Verify `Send` is called for known payload types
    - Verify `Publish` is called for unrecognised payload types
    - Verify `MessageReceivedEvent` published before command dispatch
    - Verify exception propagation from mediator
    - Verify bus failure resilience (log warning, continue)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 8.3, 8.5_

- [ ] 5. Checkpoint - Verify consumers and filters compile
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Replace MediatorModule + SagaModule with unified MassTransitModule
  - [x] 6.1 Create `MassTransitModule` Autofac module
    - Create `src/LDK.RideClub.Bot/Modules/MassTransitModule.cs`
    - Register `EventDispatcher` as `IEventProcessor` (scoped lifetime)
    - This module replaces both `MediatorModule` and `SagaModule`
    - _Requirements: 1.3, 1.5, 2.6, 5.1_

  - [x] 6.2 Create `AddBotMediator` extension method for MassTransit mediator registration
    - Create or update `src/LDK.RideClub.Bot/Configuration/MassTransitServiceCollectionExtensions.cs`
    - Add `AddBotMediator(this IServiceCollection services)` method
    - Register `AddMediator` with consumer discovery: `ProcessTextMessageConsumer`, `ProcessBotCommandConsumer`, `UnrecognisedEventConsumer`
    - Configure mediator pipeline filters via `ConfigureMediator`: `UseSendFilter(typeof(LoggingFilter<>))`, `UseSendFilter(typeof(ValidationFilter<>))`, `UseSendFilter(typeof(TelemetryFilter<>))`
    - _Requirements: 1.1, 1.2, 1.4, 3.4, 3.5, 3.6_

  - [x] 6.3 Update `AddBotMassTransit` to remove any MediatR-related configuration
    - Update `src/LDK.RideClub.Bot/Configuration/MassTransitServiceCollectionExtensions.cs`
    - Ensure `AddBotMassTransit` only handles bus + saga (state machine, EF Core repository, in-memory transport)
    - Verify no MediatR references remain
    - _Requirements: 5.2, 5.3, 5.4, 5.5_

  - [-] 6.4 Update `Program.cs` to use new module structure
    - Remove `containerBuilder.RegisterModule(new MediatorModule())` line
    - Remove `containerBuilder.RegisterModule(new SagaModule())` line
    - Add `containerBuilder.RegisterModule(new MassTransitModule())` line
    - Add `builder.Services.AddBotMediator()` call alongside existing `AddBotMassTransit`
    - _Requirements: 1.3, 1.5, 5.1_

- [ ] 7. Remove MediatR packages and old files
  - [-] 7.1 Remove MediatR package references from all projects
    - Remove `MediatR`, `MediatR.Contracts`, `MediatR.Extensions.Autofac.DependencyInjection` from `Directory.Packages.props`
    - Remove `MediatR.Contracts` from `src/LDK.RideClub.Bot.Domain/LDK.RideClub.Bot.Domain.csproj`
    - Remove `MediatR` and `MediatR.Extensions.Autofac.DependencyInjection` from `src/LDK.RideClub.Bot/LDK.RideClub.Bot.csproj`
    - _Requirements: 1.1_

  - [-] 7.2 Delete old MediatR-specific files
    - Delete `src/LDK.RideClub.Bot/Behaviors/LoggingBehavior.cs`
    - Delete `src/LDK.RideClub.Bot/Behaviors/ValidationBehavior.cs`
    - Delete `src/LDK.RideClub.Bot/Behaviors/TelemetryBehavior.cs`
    - Delete `src/LDK.RideClub.Bot/Handlers/ProcessTextMessageCommandHandler.cs`
    - Delete `src/LDK.RideClub.Bot/Handlers/ProcessBotCommandCommandHandler.cs`
    - Delete `src/LDK.RideClub.Bot/Modules/MediatorModule.cs`
    - Delete `src/LDK.RideClub.Bot/Modules/SagaModule.cs`
    - _Requirements: 1.1, 2.6_

- [ ] 8. Checkpoint - Verify solution compiles without MediatR
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Configure Serilog for MassTransit error logging and observability
  - [ ] 9.1 Configure Serilog to permit MassTransit error logs regardless of startup outcome
    - Update Serilog configuration (in `UseBotSerilog` or appsettings) to set minimum level override for `MassTransit.*` source contexts to `Error`
    - Ensure MassTransit transport initialisation failures, saga persistence errors, and bus-level exceptions are always visible
    - Verify Serilog is configured early in the host builder pipeline (before MassTransit bus start)
    - _Requirements: 5.7, 6.8_

  - [ ] 9.2 Implement trace ID inclusion policy in observability enricher
    - Create or update a Serilog enricher that conditionally adds `TraceId` and `SpanId` properties
    - Warning and Error levels: always include trace identifiers
    - Informational level and below during successful operations: omit trace identifiers
    - Informational level during failed operations: include trace identifiers
    - _Requirements: 9.4_

  - [ ] 9.3 Register `RideClub.Bot.Mediator` ActivitySource in OpenTelemetry pipeline
    - Update `src/LDK.RideClub.Bot.Observability/ObservabilityExtensions.cs` to add the `RideClub.Bot.Mediator` ActivitySource
    - Ensure mediator dispatch spans and saga transition spans are correlated under the webhook request parent trace
    - _Requirements: 9.1, 9.2, 9.3, 9.5_

- [ ] 10. Update ConversationStateMachine for broadened undefined transition handling
  - [ ] 10.1 Update `ConversationStateMachine` for undefined transition logging
    - Update `src/LDK.RideClub.Bot/Sagas/ConversationStateMachine.cs`
    - Ensure undefined transitions for ANY event (not limited to conversation events) log a warning with event type, source, current state, and correlation key
    - Verify state machine remains in current state on undefined transitions
    - Ensure warnings are logged via Serilog even during startup (buffered until Serilog is initialised)
    - _Requirements: 6.7, 6.8_

  - [ ] 10.2 Write unit tests for undefined transition handling
    - Test that undefined transitions leave state unchanged
    - Test that warning is logged with correct event type, source, state, and correlation key
    - Test transitions from all states with unexpected events
    - _Requirements: 6.7, 6.8_

- [ ] 11. Update test infrastructure for MassTransit mediator
  - [ ] 11.1 Update existing unit tests to remove MediatR mocks
    - Update `tests/LDK.RideClub.Bot.Tests/Unit/LoggingBehaviorTests.cs` → rename/rewrite as `LoggingFilterTests.cs` testing `IFilter<SendContext<T>>`
    - Update `tests/LDK.RideClub.Bot.Tests/Unit/ValidationBehaviorTests.cs` → rename/rewrite as `ValidationFilterTests.cs`
    - Update `tests/LDK.RideClub.Bot.Tests/Unit/TelemetryBehaviorTests.cs` → rename/rewrite as `TelemetryFilterTests.cs`
    - Delete old behavior test files after creating filter test files
    - _Requirements: 10.1_

  - [ ] 11.2 Update integration test factories for MassTransit mediator
    - Update `tests/LDK.RideClub.Bot.Tests/Integration/MassTransitHarnessWebApplicationFactory.cs` to include mediator registration
    - Update `tests/LDK.RideClub.Bot.Tests/Integration/FullPipelineIntegrationTests.cs` to verify consumer execution instead of MediatR handler execution
    - Ensure tests run without external message broker or Docker
    - _Requirements: 10.2, 10.3, 10.4_

  - [ ] 11.3 Update property-based tests for MassTransit mediator
    - Update `tests/LDK.RideClub.Bot.Tests/Properties/EventDispatcherPropertyTests.cs` to mock `IScopedMediator` instead of MediatR `IMediator`
    - Verify dispatch exclusivity property still holds with MassTransit mediator
    - _Requirements: 10.5_

  - [ ] 11.4 Write property-based tests for new correctness properties
    - **Property 1: Dispatch Exclusivity** — verify exactly one Send or one Publish per InboundEvent
    - **Property 2: Command Field Mapping Preservation** — verify field values preserved from InboundEvent to command
    - **Property 3: Exception Transparency** — verify exceptions propagate without wrapping
    - **Property 12: Outcome Event Publication** — verify consumers publish exactly one outcome event
    - **Property 13: Event Ordering** — verify MessageReceivedEvent published before Send
    - **Property 14: Bus Failure Resilience** — verify bus failures are caught and logged
    - **Validates: Requirements 2.2, 2.3, 2.4, 2.5, 8.1, 8.2, 8.3, 8.5, 10.5**

  - [ ] 11.5 Write property-based tests for saga state machine properties
    - **Property 5: Saga Correlation Identity** — same Platform+SenderId correlates to same instance
    - **Property 6: New Conversation Creation** — new correlation key creates instance in AwaitingInput
    - **Property 7: AwaitingInput to Processing Transition** — MessageReceived transitions to Processing
    - **Property 8: Conditional Completion Routing** — RequiresConfirmation routes correctly
    - **Property 9: Timeout Terminates Conversation** — timeout transitions to Completed
    - **Property 10: Undefined Transitions Are Ignored and Logged** — state unchanged, warning logged
    - **Validates: Requirements 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8**

- [ ] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- The design uses C# throughout; all implementations target .NET 10
- MassTransit test harness (`InMemoryTestHarness`) enables isolated saga testing without a real transport
- FsCheck is already available in the test project for property-based testing
- The existing `ConversationStateMachine` is already MassTransit-native and stays as-is (only undefined transition handling is broadened)
- The `EventDispatcher` changes from using MediatR's `IMediator` to MassTransit's `IScopedMediator` — the interface is similar but consumers replace handlers
- Consumers publish outcome events directly to `IBus` rather than returning responses through the mediator
- Old MediatR files (behaviors, handlers, modules) are deleted after their replacements are verified working

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "3.1", "3.2", "3.3"] },
    { "id": 2, "tasks": ["2.4", "3.4", "4.1"] },
    { "id": 3, "tasks": ["4.2", "6.1", "6.2", "6.3"] },
    { "id": 4, "tasks": ["6.4", "7.1", "7.2"] },
    { "id": 5, "tasks": ["9.1", "9.2", "9.3", "10.1"] },
    { "id": 6, "tasks": ["10.2", "11.1", "11.2", "11.3"] },
    { "id": 7, "tasks": ["11.4", "11.5"] }
  ]
}
```
