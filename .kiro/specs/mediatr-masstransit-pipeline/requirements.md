# Requirements Document

## Introduction

This document defines the requirements for integrating MediatR and MassTransit into the RideClub Bot platform. MediatR provides in-process mediator-based dispatch of webhook payloads to command and event handlers, replacing the current placeholder `LoggingEventProcessor`. MassTransit provides durable saga/state machine management for tracking multi-turn conversation state across user interactions. Together, these integrations establish the processing backbone for all future bot features.

## Glossary

- **Mediator**: The MediatR in-process mediator that dispatches requests (commands, queries) and notifications (events) to their registered handlers within the application process
- **Command**: A MediatR IRequest<TResponse> representing a single intent derived from an inbound event that produces a response, dispatched to exactly one handler
- **Notification**: A MediatR INotification representing a domain event that may be observed by zero or more handlers, dispatched to all registered notification handlers
- **Pipeline_Behavior**: A MediatR IPipelineBehavior<TRequest, TResponse> that wraps handler execution to provide cross-cutting concerns such as logging, validation, or telemetry
- **Command_Handler**: A MediatR IRequestHandler<TCommand, TResponse> that processes a single Command and returns a response
- **Notification_Handler**: A MediatR INotificationHandler<TNotification> that observes a Notification without returning a response
- **Event_Dispatcher**: The component responsible for translating an InboundEvent domain model into one or more MediatR Commands or Notifications and publishing them through the Mediator
- **Conversation_State_Machine**: A MassTransit state machine (SagaStateMachine<TInstance>) that defines the states, events, and transitions for a multi-turn conversation with a user
- **Saga_Instance**: A MassTransit saga state instance (SagaStateMachineInstance) that persists the current state of a single conversation, identified by a correlation identifier
- **Saga_Repository**: The MassTransit persistence mechanism (ISagaRepository<TInstance>) that stores and retrieves Saga_Instance data using Entity Framework Core
- **Bus**: The MassTransit IBus abstraction used to publish messages that trigger state machine transitions
- **Conversation_Event**: A MassTransit message class that represents a significant occurrence in a conversation (e.g., message received, response sent, timeout elapsed) and triggers a Conversation_State_Machine transition
- **Bot_Host**: The ASP.NET Core application (LDK.RideClub.Bot) that receives and dispatches webhook events from messaging platforms
- **DI_Container**: The Autofac-extended Microsoft Generic Host dependency injection container
- **Observability_Pipeline**: The OpenTelemetry instrumentation and export pipeline
- **Serilog_Pipeline**: The structured logging pipeline provided by Serilog.AspNetCore

## Requirements

### Requirement 1: MediatR Registration and Configuration

**User Story:** As a developer, I want MediatR registered in the DI container with handler auto-discovery, so that commands and notifications are dispatched to their handlers without manual wiring.

#### Acceptance Criteria

1. THE Bot_Host SHALL register MediatR services in the DI_Container using the MediatR.Extensions.Autofac.DependencyInjection package for Autofac integration
2. WHEN the Bot_Host starts, THE DI_Container SHALL scan designated assemblies and register all classes implementing IRequestHandler<TRequest, TResponse> or INotificationHandler<TNotification> as their respective handler interfaces
3. THE DI_Container SHALL register handler instances with a scoped lifetime so that each request scope resolves a fresh handler instance with its own dependencies
4. IF no handler is registered for a dispatched Command type, THEN THE Mediator SHALL throw an InvalidOperationException at dispatch time when attempting to resolve the handler, identifying the Command type that has no registered handler
5. THE Bot_Host SHALL register MediatR in a dedicated Autofac module (MediatorModule) following the existing module composition pattern used by MessagingModule, PersistenceModule, and ObservabilityModule

### Requirement 2: Event Dispatching via MediatR

**User Story:** As a developer, I want inbound webhook events translated into MediatR commands and dispatched through the mediator, so that event processing follows a consistent handler pattern.

#### Acceptance Criteria

1. THE Event_Dispatcher SHALL implement the existing IEventProcessor interface so that the webhook pipeline invokes it without modification to WebhookEndpoints
2. WHEN the Event_Dispatcher receives an InboundEvent, THE Event_Dispatcher SHALL translate the InboundEvent into a Command based on the EventPayload type (e.g., TextMessagePayload maps to a text message command, CommandPayload maps to a bot command)
3. WHEN the Event_Dispatcher has constructed a Command, THE Event_Dispatcher SHALL send the Command through the Mediator using IMediator.Send and await the response
4. IF the EventPayload type does not map to any known Command type, THEN THE Event_Dispatcher SHALL publish a Notification representing an unrecognised event and log a warning via the Serilog_Pipeline identifying the payload type, continuing processing even if either the warning log write or the Notification publish fails independently
5. IF the Mediator throws an exception during Command dispatch, THEN THE Event_Dispatcher SHALL propagate the exception to the caller without swallowing it, allowing the webhook pipeline's existing error handling to produce the appropriate HTTP response
6. THE Event_Dispatcher SHALL replace the existing LoggingEventProcessor registration in the DI_Container

### Requirement 3: MediatR Pipeline Behaviors

**User Story:** As a developer, I want cross-cutting concerns applied to all mediator requests via pipeline behaviors, so that logging, validation, and telemetry are consistent without duplicating code in each handler.

#### Acceptance Criteria

1. THE Bot_Host SHALL register a logging Pipeline_Behavior that logs the Command type name and a unique request identifier before pipeline execution and logs the Command type name, request identifier, and elapsed time after pipeline execution (including when validation prevents handler execution) via the Serilog_Pipeline
2. THE Bot_Host SHALL register a validation Pipeline_Behavior that resolves all FluentValidation IValidator<TRequest> instances for the current request type, executes validation, and throws a ValidationException containing all validation failures if any validator fails, before the request reaches the Command_Handler
3. THE Bot_Host SHALL register an OpenTelemetry Pipeline_Behavior that creates a child activity span for each mediator request, setting the span name to the Command type name and recording the outcome (success or exception) as span attributes on the Observability_Pipeline
4. WHEN multiple Pipeline_Behaviors are registered, THE Mediator SHALL execute them in the order: logging, validation, telemetry, handler — such that logging wraps the entire pipeline and telemetry measures only validation plus handler execution
5. THE DI_Container SHALL register Pipeline_Behaviors with open generic registrations so that new behaviors apply to all request types without per-type configuration
6. WHEN no Pipeline_Behaviors are registered for a request type, THE Mediator SHALL dispatch the request directly to the Command_Handler without requiring any behaviors to be present

### Requirement 4: Command and Response Contracts

**User Story:** As a developer, I want well-defined command and response types for each inbound event category, so that handlers have strongly-typed inputs and outputs.

#### Acceptance Criteria

1. THE Platform SHALL define a ProcessTextMessageCommand implementing IRequest<MessageProcessingResult> that carries the sender identifier, conversation identifier, platform identifier, message text, and event timestamp
2. THE Platform SHALL define a ProcessBotCommandCommand implementing IRequest<MessageProcessingResult> that carries the sender identifier, conversation identifier, platform identifier, command name, command arguments dictionary, and event timestamp
3. THE Platform SHALL define a MessageProcessingResult record that indicates whether processing succeeded, an optional reply message to send back to the user, and an optional error reason when processing fails
4. THE Platform SHALL define command types in the Domain project (LDK.RideClub.Bot.Domain) so that they are referenceable by handler projects without depending on the Bot_Host
5. IF a new EventPayload subtype is added to the domain, THEN a corresponding Command type SHALL be created following the same pattern, requiring only the new Command class and its handler with no modifications to the Event_Dispatcher's dispatch logic beyond adding a mapping case

### Requirement 5: MassTransit Registration and Configuration

**User Story:** As a developer, I want MassTransit configured with an in-memory transport for local development and Entity Framework Core saga persistence, so that conversation state machines are durable across process restarts.

#### Acceptance Criteria

1. THE Bot_Host SHALL register MassTransit services in the DI_Container using a dedicated Autofac module (SagaModule) following the existing module composition pattern
2. THE Bot_Host SHALL configure MassTransit with the in-memory transport for local development, selectable via an Options_Model configuration property
3. THE Bot_Host SHALL configure MassTransit saga persistence using the Entity Framework Core Saga_Repository backed by the existing SQLite database provider
4. WHEN the Bot_Host starts, THE DI_Container SHALL register all classes inheriting SagaStateMachine<TInstance> as state machine definitions, supporting both assembly scanning and explicit registration
5. THE Bot_Host SHALL define a MassTransit Options_Model (MassTransitOptions) with properties for transport type and concurrency limit, validated at startup by a FluentValidation_Validator
6. IF the MassTransit transport configuration is invalid or the transport cannot be initialised, THEN THE Bot_Host SHALL fail to start and log an error message identifying the transport type and the failure reason
7. THE Serilog_Pipeline SHALL permit error-level log entries from MassTransit components regardless of whether the Bot_Host startup succeeds or fails

### Requirement 6: Conversation State Machine

**User Story:** As a developer, I want a state machine that tracks conversation lifecycle from initial contact through active interaction to completion, so that the bot can maintain context across multiple user messages.

#### Acceptance Criteria

1. THE Conversation_State_Machine SHALL define at minimum the following states: Initial, AwaitingInput, Processing, AwaitingConfirmation, Completed, and Faulted
2. THE Conversation_State_Machine SHALL correlate Saga_Instances by a composite key of platform identifier and sender identifier, such that each unique user on each platform has exactly one active conversation state
3. WHEN a Conversation_Event indicating a new message is received and no Saga_Instance exists for the correlation key, THE Conversation_State_Machine SHALL create a new Saga_Instance in the AwaitingInput state
4. WHEN a Conversation_Event indicating a new message is received and a Saga_Instance exists in the AwaitingInput state, THE Conversation_State_Machine SHALL transition to the Processing state
5. WHEN a Conversation_Event indicating processing completion is received while in the Processing state, THE Conversation_State_Machine SHALL transition to AwaitingInput if the response requires no confirmation, or to AwaitingConfirmation if the response requires user confirmation
6. WHEN a Conversation_Event indicating a timeout is received while in the AwaitingInput or AwaitingConfirmation state, THE Conversation_State_Machine SHALL transition to the Completed state and record the timeout reason
7. IF any event triggers a transition that is not defined for the current state, THEN THE Conversation_State_Machine SHALL remain in the current state and log a warning via the Serilog_Pipeline identifying the event type, source, current state, and correlation key
8. THE Conversation_State_Machine SHALL log invalid transition warnings via the Serilog_Pipeline even during startup, buffering or queuing warnings until the logging system is available

### Requirement 7: Saga Persistence with Entity Framework Core

**User Story:** As a developer, I want saga state persisted via EF Core to the existing SQLite database, so that conversation state survives process restarts without additional infrastructure.

#### Acceptance Criteria

1. THE Saga_Repository SHALL use Entity Framework Core with the existing SQLite database provider configured in the Persistence_Layer
2. THE Saga_Repository SHALL store Saga_Instance data in a dedicated database table with columns for the correlation identifier, current state name, and timestamps for creation and last modification
3. WHEN a state transition occurs, THE Saga_Repository SHALL persist the updated Saga_Instance within the same database transaction as the state change
4. THE Platform SHALL include an EF Core migration that creates the saga state table, compatible with the existing migration history
5. IF the Saga_Repository fails to persist a state transition due to a database error, THEN THE Conversation_State_Machine SHALL transition the Saga_Instance to the Faulted state and log an error via the Serilog_Pipeline identifying the correlation key and the database exception message
6. THE Saga_Instance entity SHALL use optimistic concurrency (via a RowVersion/concurrency token column) to prevent lost updates when concurrent messages arrive for the same conversation

### Requirement 8: MediatR-to-MassTransit Integration

**User Story:** As a developer, I want MediatR command handlers to publish MassTransit conversation events after processing, so that the state machine tracks conversation progress driven by handler outcomes.

#### Acceptance Criteria

1. WHEN a Command_Handler successfully processes a command, THE Command_Handler SHALL publish a Conversation_Event via the MassTransit Bus indicating processing completion, carrying the correlation key and the processing result
2. WHEN a Command_Handler fails to process a command due to a domain error, THE Command_Handler SHALL publish a Conversation_Event via the MassTransit Bus indicating a fault, carrying the correlation key and the error reason
3. THE Event_Dispatcher SHALL publish a MessageReceivedEvent via the MassTransit Bus as a fire-and-forget operation before dispatching the Command through the Mediator, so that the state machine transitions to Processing independently of command dispatch success
4. THE Platform SHALL define Conversation_Event message types in the Domain project so that both the MediatR handlers and the MassTransit state machine reference the same contracts
5. IF the MassTransit Bus is unavailable when a Conversation_Event is published, THEN THE publishing component SHALL log a warning via the Serilog_Pipeline and continue processing without blocking the webhook response

### Requirement 9: Observability for Mediator and Saga Pipeline

**User Story:** As a developer, I want telemetry spans and structured logs for mediator dispatch and saga transitions, so that I can trace the full lifecycle of an inbound event through command handling and state changes.

#### Acceptance Criteria

1. WHEN the Event_Dispatcher translates an InboundEvent into a Command, THE Observability_Pipeline SHALL create a trace span with attributes identifying the source event identifier, the target Command type, and the messaging platform
2. WHEN a Conversation_State_Machine transition occurs, THE Observability_Pipeline SHALL create a trace span with attributes identifying the correlation key, the previous state, the new state, and the triggering Conversation_Event type
3. THE Observability_Pipeline SHALL correlate mediator dispatch spans and saga transition spans under the same parent trace initiated by the webhook request, so that the full processing chain is visible in the Aspire Dashboard
4. WHEN a Pipeline_Behavior or state machine transition logs a warning or error, THE Serilog_Pipeline SHALL include the trace identifier and span identifier in the structured log entry for correlation, omitting trace identifiers from log entries at informational level or below during successful operations
5. THE Bot_Host SHALL register MassTransit OpenTelemetry instrumentation so that bus publish and consume operations appear as spans in the Observability_Pipeline

### Requirement 10: Testing Support for Mediator and Saga

**User Story:** As a developer, I want the mediator and saga infrastructure testable in isolation, so that I can write unit and integration tests without requiring a running message transport.

#### Acceptance Criteria

1. THE Platform SHALL support testing Command_Handlers in isolation by resolving them from a test DI container with mocked dependencies, without requiring MassTransit bus infrastructure
2. THE Platform SHALL support testing the Conversation_State_Machine in isolation using the MassTransit test harness (InMemoryTestHarness) to publish events and assert state transitions
3. THE Platform SHALL support integration testing of the full pipeline (webhook → Event_Dispatcher → Mediator → Handler → Bus → State Machine) using the existing WebApplicationFactory fixture with MassTransit configured in test harness mode
4. WHEN a developer runs `dotnet test`, THE test project SHALL execute mediator and saga tests without requiring an external message broker, Docker, or any messaging transport infrastructure, while permitting network access for non-messaging operations such as package restoration
5. THE Platform SHALL include at least one property-based test using FsCheck that verifies: for all valid InboundEvent instances, the Event_Dispatcher produces exactly one Command send or one Notification publish through the Mediator
