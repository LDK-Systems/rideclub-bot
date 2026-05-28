# Implementation Plan: MediatR & MassTransit Pipeline

## Overview

This plan integrates MediatR for in-process command/event dispatching and MassTransit for durable conversation state management into the RideClub Bot platform. Tasks are ordered to establish contracts first, then infrastructure registration, then the integration layer, and finally testing and observability wiring.

## Tasks

- [x] 1. Define domain contracts (Commands, Events, Responses)
  - [x] 1.1 Add MediatR package reference to the Domain project and create command types
    - Add `MediatR.Contracts` package reference to `src/LDK.RideClub.Bot.Domain/LDK.RideClub.Bot.Domain.csproj`
    - Create `src/LDK.RideClub.Bot.Domain/Commands/ProcessTextMessageCommand.cs` implementing `IRequest<MessageProcessingResult>` with SenderId, ConversationId, Platform, MessageText, Timestamp properties
    - Create `src/LDK.RideClub.Bot.Domain/Commands/ProcessBotCommandCommand.cs` implementing `IRequest<MessageProcessingResult>` with SenderId, ConversationId, Platform, CommandName, Arguments, Timestamp properties
    - Create `src/LDK.RideClub.Bot.Domain/Responses/MessageProcessingResult.cs` record with Success, ReplyMessage, ErrorReason properties
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 1.2 Create MassTransit conversation event types in the Domain project
    - Create `src/LDK.RideClub.Bot.Domain/Events/Conversation/MessageReceivedEvent.cs` with Platform, SenderId, ConversationId, EventId, Timestamp
    - Create `src/LDK.RideClub.Bot.Domain/Events/Conversation/ProcessingCompletedEvent.cs` with Platform, SenderId, RequiresConfirmation, ReplyMessage
    - Create `src/LDK.RideClub.Bot.Domain/Events/Conversation/ProcessingFaultedEvent.cs` with Platform, SenderId, ErrorReason
    - Create `src/LDK.RideClub.Bot.Domain/Events/Conversation/ConversationTimedOutEvent.cs` with Platform, SenderId, Reason
    - _Requirements: 8.4, 6.1_

  - [x] 1.3 Create the UnrecognisedEventNotification type
    - Create `src/LDK.RideClub.Bot.Domain/Events/UnrecognisedEventNotification.cs` as `INotification` wrapping `InboundEvent`
    - _Requirements: 2.4_

- [x] 2. Implement MediatR pipeline behaviors
  - [x] 2.1 Create LoggingBehavior
    - Create `src/LDK.RideClub.Bot/Behaviors/LoggingBehavior.cs` implementing `IPipelineBehavior<TRequest, TResponse>`
    - Log request type name and unique request ID before execution via Serilog
    - Log request type name, request ID, and elapsed milliseconds after execution
    - _Requirements: 3.1, 3.4_

  - [x] 2.2 Create ValidationBehavior
    - Create `src/LDK.RideClub.Bot/Behaviors/ValidationBehavior.cs` implementing `IPipelineBehavior<TRequest, TResponse>`
    - Resolve all `IValidator<TRequest>` instances, run validation, throw `ValidationException` on failure
    - Short-circuit before reaching the handler if validation fails
    - _Requirements: 3.2, 3.4_

  - [x] 2.3 Create TelemetryBehavior
    - Create `src/LDK.RideClub.Bot/Behaviors/TelemetryBehavior.cs` implementing `IPipelineBehavior<TRequest, TResponse>`
    - Create an `ActivitySource` named `RideClub.Bot.Mediator`
    - Start a child activity span named after the request type, record outcome (success/exception) as span attributes
    - _Requirements: 3.3, 3.4, 9.1_

  - [x] 2.4 Write unit tests for pipeline behaviors
    - Test LoggingBehavior logs before/after with elapsed time
    - Test ValidationBehavior throws on validation failure and passes through on success
    - Test TelemetryBehavior creates activity spans with correct attributes
    - _Requirements: 3.1, 3.2, 3.3_

- [x] 3. Implement the EventDispatcher
  - [x] 3.1 Create EventDispatcher implementing IEventProcessor
    - Create `src/LDK.RideClub.Bot/Services/EventDispatcher.cs` implementing `IEventProcessor`
    - Inject `IMediator`, `IBus`, and `ILogger<EventDispatcher>`
    - Publish `MessageReceivedEvent` to MassTransit before dispatching the command
    - Map `TextMessagePayload` → `ProcessTextMessageCommand`, `CommandPayload` → `ProcessBotCommandCommand`
    - For unknown payload types, publish `UnrecognisedEventNotification` via MediatR and log a warning
    - Publish `ProcessingCompletedEvent` or `ProcessingFaultedEvent` after handler execution
    - Propagate exceptions from mediator dispatch without swallowing
    - If MassTransit Bus publish fails, log warning and continue without blocking
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 8.1, 8.2, 8.3, 8.5_

  - [-] 3.2 Write unit tests for EventDispatcher
    - Test text message payload maps to ProcessTextMessageCommand
    - Test command payload maps to ProcessBotCommandCommand
    - Test unknown payload publishes UnrecognisedEventNotification
    - Test MessageReceivedEvent is published before command dispatch
    - Test ProcessingCompletedEvent is published on success
    - Test ProcessingFaultedEvent is published on domain error
    - Test exception propagation from mediator
    - Test Bus unavailability logs warning and continues
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 8.1, 8.2, 8.3, 8.5_

- [x] 4. Checkpoint - Verify core mediator pipeline compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement MassTransit saga infrastructure
  - [x] 5.1 Create ConversationSagaInstance entity and EF Core configuration
    - Create `src/LDK.RideClub.Bot.Persistence/Entities/ConversationSagaInstance.cs` implementing `SagaStateMachineInstance` with CorrelationId, CorrelationKey, CurrentState, Platform, SenderId, ConversationId, CreatedAt, LastModifiedAt, RowVersion
    - Create `src/LDK.RideClub.Bot.Persistence/Configuration/ConversationSagaInstanceConfiguration.cs` implementing `IEntityTypeConfiguration<ConversationSagaInstance>`
    - Configure table name `ConversationSagas`, unique index on CorrelationKey, RowVersion as concurrency token, max lengths on string columns
    - Register the configuration in `BotDbContext`
    - _Requirements: 7.1, 7.2, 7.6_

  - [x] 5.2 Add EF Core migration for the ConversationSagas table
    - Add MassTransit.EntityFrameworkCore package reference to the Persistence project
    - Generate an EF Core migration that creates the `ConversationSagas` table
    - Ensure migration is compatible with existing migration history
    - _Requirements: 7.4_

  - [x] 5.3 Implement ConversationStateMachine
    - Create `src/LDK.RideClub.Bot/Sagas/ConversationStateMachine.cs` inheriting `MassTransitStateMachine<ConversationSagaInstance>`
    - Define states: AwaitingInput, Processing, AwaitingConfirmation, Completed, Faulted
    - Define events: MessageReceived, ProcessingCompleted, ProcessingFaulted, TimedOut
    - Correlate by composite key `{Platform}:{SenderId}`
    - Implement transitions: Initial→AwaitingInput, AwaitingInput→Processing, Processing→AwaitingInput/AwaitingConfirmation, Processing→Faulted, AwaitingInput/AwaitingConfirmation→Completed on timeout
    - Log warning for undefined transitions
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

  - [x] 5.4 Write unit tests for ConversationStateMachine using MassTransit test harness
    - Test new message creates saga in AwaitingInput state
    - Test AwaitingInput → Processing on message received
    - Test Processing → AwaitingInput on completion without confirmation
    - Test Processing → AwaitingConfirmation on completion with confirmation
    - Test Processing → Faulted on fault event
    - Test timeout transitions to Completed
    - _Requirements: 6.3, 6.4, 6.5, 6.6, 10.2_

- [x] 6. Register MediatR and MassTransit in the DI container
  - [x] 6.1 Create MediatorModule and register in Program.cs
    - Add `MediatR` and `MediatR.Extensions.Autofac.DependencyInjection` package references to the Bot host project csproj
    - Create `src/LDK.RideClub.Bot/Modules/MediatorModule.cs` as an Autofac `Module`
    - Register MediatR with assembly scanning of the Domain project
    - Register open-generic pipeline behaviors in order: LoggingBehavior, ValidationBehavior, TelemetryBehavior
    - Register `EventDispatcher` as `IEventProcessor` (replacing `LoggingEventProcessor`)
    - Register the module in `Program.cs` alongside existing modules
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.6, 3.5_

  - [x] 6.2 Create MassTransit service registration and SagaModule
    - Add `MassTransit`, `MassTransit.EntityFrameworkCore` package references to the Bot host project csproj and `Directory.Packages.props`
    - Create `src/LDK.RideClub.Bot/Configuration/MassTransitOptions.cs` with TransportType and ConcurrencyLimit properties
    - Create `src/LDK.RideClub.Bot/Configuration/MassTransitOptionsValidator.cs` using FluentValidation
    - Create `src/LDK.RideClub.Bot/Configuration/MassTransitServiceCollectionExtensions.cs` with `AddBotMassTransit` extension method
    - Configure in-memory transport, saga state machine with EF Core repository, and OpenTelemetry instrumentation
    - Create `src/LDK.RideClub.Bot/Modules/SagaModule.cs` as an Autofac `Module`
    - Register the module in `Program.cs` and call `AddBotMassTransit` in the service configuration
    - Add `MassTransit` section to `appsettings.json` and `appsettings.Development.json`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 9.5_

- [x] 7. Checkpoint - Verify full pipeline compiles and starts
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Observability wiring
  - [x] 8.1 Register MediatR ActivitySource in the OpenTelemetry pipeline
    - Update `src/LDK.RideClub.Bot.Observability/ObservabilityExtensions.cs` to add the `RideClub.Bot.Mediator` ActivitySource to the tracing configuration
    - Ensure mediator dispatch spans and saga transition spans are correlated under the webhook request parent trace
    - Verify structured log entries include trace/span identifiers
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [ ] 9. Integration testing
  - [ ] 9.1 Write integration test for the full pipeline
    - Configure `BotWebApplicationFactory` to include MassTransit test harness mode
    - Test webhook → EventDispatcher → Mediator → Handler → Bus → State Machine flow
    - Verify state machine transitions occur correctly end-to-end
    - Ensure tests run without network access or Docker
    - _Requirements: 10.3, 10.4_

  - [ ] 9.2 Write FsCheck property-based test for EventDispatcher mapping
    - Generate arbitrary valid `InboundEvent` instances with various payload types
    - Verify: for all valid InboundEvent instances, EventDispatcher produces exactly one Command send or one Notification publish through the Mediator
    - _Requirements: 10.5_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- The design uses C# throughout; all implementations target .NET 10
- MassTransit test harness (`InMemoryTestHarness`) enables isolated saga testing without a real transport
- FsCheck is already available in the test project for property-based testing
- The existing `LoggingEventProcessor` will be replaced by `EventDispatcher` — no breaking changes to `WebhookEndpoints`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "5.1"] },
    { "id": 2, "tasks": ["2.4", "5.2", "5.3"] },
    { "id": 3, "tasks": ["3.1", "5.4"] },
    { "id": 4, "tasks": ["3.2", "6.1", "6.2"] },
    { "id": 5, "tasks": ["8.1"] },
    { "id": 6, "tasks": ["9.1", "9.2"] }
  ]
}
```
