using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Persistence.Entities;

using MassTransit;

namespace LDK.RideClub.Bot.Sagas;

/// <summary>
/// MassTransit state machine that tracks conversation lifecycle from initial contact
/// through active interaction to completion. Correlates by composite key {Platform}:{SenderId}.
/// </summary>
internal sealed partial class ConversationStateMachine
    : MassTransitStateMachine<ConversationSagaInstance>
{
    private readonly ILogger<ConversationStateMachine> _logger;

    // States
    public State AwaitingInput { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State AwaitingConfirmation { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;

    // Events
    public Event<MessageReceivedEvent> MessageReceived { get; private set; } = null!;
    public Event<ProcessingCompletedEvent> ProcessingCompleted { get; private set; } = null!;
    public Event<ProcessingFaultedEvent> ProcessingFaulted { get; private set; } = null!;
    public Event<ConversationTimedOutEvent> TimedOut { get; private set; } = null!;

    public ConversationStateMachine(ILogger<ConversationStateMachine> logger)
    {
        _logger = logger;

        InstanceState(x => x.CurrentState);

        ConfigureEventCorrelation();
        ConfigureTransitions();

        // Log warning and ignore undefined transitions (Requirement 6.7)
        OnUnhandledEvent(context =>
        {
            LogUnhandledEvent(context.Saga.CurrentState, context.Saga.CorrelationKey);
            return Task.CompletedTask;
        });
    }

    private void ConfigureEventCorrelation()
    {
#pragma warning disable IDE0058 // MassTransit fluent configuration — return values are intentionally unused
        Event(() => MessageReceived, x =>
        {
            x.CorrelateBy(
                instance => instance.CorrelationKey,
                context => $"{context.Message.Platform}:{context.Message.SenderId}");
            x.SelectId(context => NewId.NextGuid());
        });

        Event(() => ProcessingCompleted, x => x.CorrelateBy(
            instance => instance.CorrelationKey,
            context => $"{context.Message.Platform}:{context.Message.SenderId}"));

        Event(() => ProcessingFaulted, x => x.CorrelateBy(
            instance => instance.CorrelationKey,
            context => $"{context.Message.Platform}:{context.Message.SenderId}"));

        Event(() => TimedOut, x => x.CorrelateBy(
            instance => instance.CorrelationKey,
            context => $"{context.Message.Platform}:{context.Message.SenderId}"));
#pragma warning restore IDE0058
    }

    private void ConfigureTransitions()
    {
        // Initial → AwaitingInput (new conversation)
        Initially(
            When(MessageReceived)
                .Then(context =>
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    context.Saga.Platform = context.Message.Platform;
                    context.Saga.SenderId = context.Message.SenderId;
                    context.Saga.ConversationId = context.Message.ConversationId;
                    context.Saga.CorrelationKey = $"{context.Message.Platform}:{context.Message.SenderId}";
                    context.Saga.CreatedAt = now;
                    context.Saga.LastModifiedAt = now;
                })
                .TransitionTo(AwaitingInput));

        // AwaitingInput → Processing on new message, or Completed on timeout
        During(AwaitingInput,
            When(MessageReceived)
                .Then(context => context.Saga.LastModifiedAt = DateTimeOffset.UtcNow)
                .TransitionTo(Processing),
            When(TimedOut)
                .Then(context => context.Saga.LastModifiedAt = DateTimeOffset.UtcNow)
                .TransitionTo(Completed));

        // Processing → AwaitingInput/AwaitingConfirmation or Faulted
        During(Processing,
            When(ProcessingCompleted)
                .Then(context => context.Saga.LastModifiedAt = DateTimeOffset.UtcNow)
                .IfElse(
                    context => context.Message.RequiresConfirmation,
                    then => then.TransitionTo(AwaitingConfirmation),
                    @else => @else.TransitionTo(AwaitingInput)),
            When(ProcessingFaulted)
                .Then(context => context.Saga.LastModifiedAt = DateTimeOffset.UtcNow)
                .TransitionTo(Faulted));

        // AwaitingConfirmation → Processing on new message, or Completed on timeout
        During(AwaitingConfirmation,
            When(MessageReceived)
                .Then(context => context.Saga.LastModifiedAt = DateTimeOffset.UtcNow)
                .TransitionTo(Processing),
            When(TimedOut)
                .Then(context => context.Saga.LastModifiedAt = DateTimeOffset.UtcNow)
                .TransitionTo(Completed));
    }

    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Warning,
        Message = "Unhandled event in state {CurrentState} for correlation key {CorrelationKey}")]
    private partial void LogUnhandledEvent(string currentState, string correlationKey);
}
