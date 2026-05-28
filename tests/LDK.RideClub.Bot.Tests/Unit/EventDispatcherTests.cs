// ---------------------------------------------------------------------------
// RideClub Bot — EventDispatcher Unit Tests (Req 2.1–2.5, 8.1–8.3, 8.5)
// ---------------------------------------------------------------------------

using FluentAssertions;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Domain.Responses;
using LDK.RideClub.Bot.Services;

using MassTransit;

using MediatR;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="EventDispatcher"/> verifying payload mapping,
/// MassTransit event publication, exception propagation, and bus resilience.
/// </summary>
public sealed class EventDispatcherTests
{
    private readonly IMediator _mediator;
    private readonly IBus _bus;
    private readonly FakeLogger<EventDispatcher> _logger;
    private readonly EventDispatcher _sut;

    public EventDispatcherTests()
    {
        _mediator = Substitute.For<IMediator>();
        _bus = Substitute.For<IBus>();
        _logger = new FakeLogger<EventDispatcher>();
        _sut = new EventDispatcher(_mediator, _bus, _logger);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static InboundEvent CreateTextMessageEvent(
        string senderId = "sender-1",
        string conversationId = "conv-1",
        string platform = "whatsapp",
        string text = "Hello world")
    {
        return new InboundEvent
        {
            EventId = "evt-001",
            Platform = platform,
            SenderId = senderId,
            ConversationId = conversationId,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new TextMessagePayload { Text = text }
        };
    }

    private static InboundEvent CreateCommandEvent(
        string senderId = "sender-1",
        string conversationId = "conv-1",
        string platform = "whatsapp",
        string commandName = "/start",
        IReadOnlyDictionary<string, string>? arguments = null)
    {
        return new InboundEvent
        {
            EventId = "evt-002",
            Platform = platform,
            SenderId = senderId,
            ConversationId = conversationId,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new CommandPayload
            {
                CommandName = commandName,
                Arguments = arguments ?? new Dictionary<string, string>()
            }
        };
    }

    private static InboundEvent CreateUnknownPayloadEvent(
        string senderId = "sender-1",
        string conversationId = "conv-1",
        string platform = "whatsapp")
    {
        return new InboundEvent
        {
            EventId = "evt-003",
            Platform = platform,
            SenderId = senderId,
            ConversationId = conversationId,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new UnknownTestPayload()
        };
    }

    private void SetupMediatorSuccess(MessageProcessingResult? result = null)
    {
        result ??= new MessageProcessingResult { Success = true, ReplyMessage = "OK" };
        _mediator.Send(Arg.Any<IRequest<MessageProcessingResult>>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private void SetupMediatorFailureResult()
    {
        MessageProcessingResult result = new()
        {
            Success = false,
            ErrorReason = "Domain error occurred"
        };
        _mediator.Send(Arg.Any<IRequest<MessageProcessingResult>>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    /// <summary>A payload type unknown to the EventDispatcher for testing the unrecognised path.</summary>
    private sealed record UnknownTestPayload : EventPayload;

    // -----------------------------------------------------------------------
    // Test: Text message payload maps to ProcessTextMessageCommand (Req 2.2)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_TextMessagePayload_SendsProcessTextMessageCommand()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent(
            senderId: "user-42",
            conversationId: "conv-99",
            platform: "telegram",
            text: "Hi there");
        SetupMediatorSuccess();

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ProcessTextMessageCommand>(cmd =>
                cmd.SenderId == "user-42" &&
                cmd.ConversationId == "conv-99" &&
                cmd.Platform == "telegram" &&
                cmd.MessageText == "Hi there" &&
                cmd.Timestamp == evt.Timestamp),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Test: Command payload maps to ProcessBotCommandCommand (Req 2.2)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_CommandPayload_SendsProcessBotCommandCommand()
    {
        // Arrange
        Dictionary<string, string> args = new() { ["key1"] = "value1" };
        InboundEvent evt = CreateCommandEvent(
            senderId: "user-7",
            conversationId: "conv-3",
            platform: "whatsapp",
            commandName: "/help",
            arguments: args);
        SetupMediatorSuccess();

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<ProcessBotCommandCommand>(cmd =>
                cmd.SenderId == "user-7" &&
                cmd.ConversationId == "conv-3" &&
                cmd.Platform == "whatsapp" &&
                cmd.CommandName == "/help" &&
                cmd.Arguments == args &&
                cmd.Timestamp == evt.Timestamp),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Test: Unknown payload publishes UnrecognisedEventNotification (Req 2.4)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_UnknownPayload_PublishesUnrecognisedEventNotification()
    {
        // Arrange
        InboundEvent evt = CreateUnknownPayloadEvent();

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — should publish notification, not send a command
        await _mediator.Received(1).Publish(
            Arg.Is<UnrecognisedEventNotification>(n => n.Event == evt),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive().Send(
            Arg.Any<IRequest<MessageProcessingResult>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_UnknownPayload_LogsWarning()
    {
        // Arrange
        InboundEvent evt = CreateUnknownPayloadEvent();

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — should log a warning about unrecognised payload type
        _logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("UnknownTestPayload"));
    }

    // -----------------------------------------------------------------------
    // Test: MessageReceivedEvent is published before command dispatch (Req 8.3)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_PublishesMessageReceivedEvent_BeforeCommandDispatch()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent(
            senderId: "user-1",
            conversationId: "conv-1",
            platform: "whatsapp");

        List<string> callOrder = [];

        _bus.Publish(Arg.Any<MessageReceivedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("bus_publish_received"));

        _mediator.Send(Arg.Any<IRequest<MessageProcessingResult>>(), Arg.Any<CancellationToken>())
            .Returns(new MessageProcessingResult { Success = true })
            .AndDoes(_ => callOrder.Add("mediator_send"));

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — MessageReceivedEvent published before mediator.Send
        callOrder.Should().ContainInOrder("bus_publish_received", "mediator_send");
    }

    [Fact]
    public async Task ProcessAsync_PublishesMessageReceivedEvent_WithCorrectFields()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent(
            senderId: "user-abc",
            conversationId: "conv-xyz",
            platform: "telegram");
        SetupMediatorSuccess();

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert
        await _bus.Received(1).Publish(
            Arg.Is<MessageReceivedEvent>(e =>
                e.Platform == "telegram" &&
                e.SenderId == "user-abc" &&
                e.ConversationId == "conv-xyz" &&
                e.EventId == evt.EventId &&
                e.Timestamp == evt.Timestamp),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Test: ProcessingCompletedEvent is published on success (Req 8.1)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_OnSuccess_PublishesProcessingCompletedEvent()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent(
            senderId: "user-5",
            platform: "whatsapp");
        MessageProcessingResult result = new()
        {
            Success = true,
            ReplyMessage = "All good"
        };
        SetupMediatorSuccess(result);

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert
        await _bus.Received(1).Publish(
            Arg.Is<ProcessingCompletedEvent>(e =>
                e.Platform == "whatsapp" &&
                e.SenderId == "user-5" &&
                !e.RequiresConfirmation &&
                e.ReplyMessage == "All good"),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Test: ProcessingFaultedEvent is published on domain error (Req 8.2)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_OnDomainError_PublishesProcessingFaultedEvent()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent(
            senderId: "user-9",
            platform: "telegram");
        SetupMediatorFailureResult();

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert
        await _bus.Received(1).Publish(
            Arg.Is<ProcessingFaultedEvent>(e =>
                e.Platform == "telegram" &&
                e.SenderId == "user-9" &&
                e.ErrorReason == "Domain error occurred"),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Test: Exception propagation from mediator (Req 2.5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WhenMediatorThrows_PropagatesException()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent();
        InvalidOperationException expectedException = new("No handler registered");
        _mediator.Send(Arg.Any<IRequest<MessageProcessingResult>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(expectedException);

        // Act
        Func<Task> act = () => _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — exception propagates without being caught
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task ProcessAsync_WhenMediatorThrows_DoesNotPublishOutcomeEvent()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent();
        _mediator.Send(Arg.Any<IRequest<MessageProcessingResult>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Handler failure"));

        // Act
        try
        {
            await _sut.ProcessAsync(evt, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert — no outcome event should be published since the exception propagated
        await _bus.DidNotReceive().Publish(
            Arg.Any<ProcessingCompletedEvent>(),
            Arg.Any<CancellationToken>());
        await _bus.DidNotReceive().Publish(
            Arg.Any<ProcessingFaultedEvent>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Test: Bus unavailability logs warning and continues (Req 8.5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WhenBusPublishFails_LogsWarningAndContinues()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent();
        _bus.Publish(Arg.Any<MessageReceivedEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Bus unavailable"));
        SetupMediatorSuccess();

        // Act — should not throw
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — warning logged about bus failure
        _logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("MessageReceivedEvent"));

        // Assert — mediator.Send still called (processing continues)
        await _mediator.Received(1).Send(
            Arg.Any<IRequest<MessageProcessingResult>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WhenOutcomePublishFails_LogsWarningAndCompletes()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent();
        SetupMediatorSuccess();

        // First bus.Publish (MessageReceivedEvent) succeeds
        _bus.Publish(Arg.Any<MessageReceivedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Second bus.Publish (ProcessingCompletedEvent) fails
        _bus.Publish(Arg.Any<ProcessingCompletedEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Bus unavailable for outcome"));

        // Act — should not throw
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — warning logged about outcome publish failure
        _logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("OutcomeEvent"));
    }

    // -----------------------------------------------------------------------
    // Fake Logger (same pattern as LoggingBehaviorTests)
    // -----------------------------------------------------------------------

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }

    internal sealed record LogEntry(LogLevel LogLevel, EventId EventId, string Message);
}
