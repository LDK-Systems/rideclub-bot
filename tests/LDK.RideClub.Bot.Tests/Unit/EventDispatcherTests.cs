// ---------------------------------------------------------------------------
// RideClub Bot — EventDispatcher Unit Tests (Req 2.1–2.5, 8.3, 8.5)
// ---------------------------------------------------------------------------

using FluentAssertions;

using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Services;

using MassTransit;
using MassTransit.Mediator;

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
    private readonly IScopedMediator _mediator;
    private readonly IBus _bus;
    private readonly FakeLogger<EventDispatcher> _logger;
    private readonly EventDispatcher _sut;

    public EventDispatcherTests()
    {
        _mediator = Substitute.For<IScopedMediator>();
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

        object? capturedCommand = null;
        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedCommand = ci.Arg<object>());

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
        capturedCommand.Should().BeOfType<ProcessTextMessageCommand>()
            .Which.Should().BeEquivalentTo(new
            {
                SenderId = "user-42",
                ConversationId = "conv-99",
                Platform = "telegram",
                MessageText = "Hi there",
                evt.Timestamp,
            });
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

        object? capturedCommand = null;
        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedCommand = ci.Arg<object>());

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert
        ProcessBotCommandCommand cmd = capturedCommand.Should().BeOfType<ProcessBotCommandCommand>().Subject;
        cmd.SenderId.Should().Be("user-7");
        cmd.ConversationId.Should().Be("conv-3");
        cmd.Platform.Should().Be("whatsapp");
        cmd.CommandName.Should().Be("/help");
        cmd.Arguments.Should().BeSameAs(args);
        cmd.Timestamp.Should().Be(evt.Timestamp);
    }

    // -----------------------------------------------------------------------
    // Test: Unknown payload publishes UnrecognisedEventNotification (Req 2.4)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_UnknownPayload_PublishesUnrecognisedEventNotification()
    {
        // Arrange
        InboundEvent evt = CreateUnknownPayloadEvent();

        UnrecognisedEventNotification? capturedNotification = null;
        _mediator.Publish(Arg.Any<UnrecognisedEventNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedNotification = ci.Arg<UnrecognisedEventNotification>());

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — should publish notification, not send a command
        await _mediator.Received(1).Publish(
            Arg.Any<UnrecognisedEventNotification>(), Arg.Any<CancellationToken>());
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Event.Should().BeSameAs(evt);

        await _mediator.DidNotReceive().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
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

        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
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
    // Test: Exception propagation from mediator (Req 2.5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WhenMediatorThrows_PropagatesException()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent();
        InvalidOperationException expectedException = new("No handler registered");
        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(expectedException);

        // Act
        Func<Task> act = () => _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — exception propagates without being caught or wrapped
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task ProcessAsync_WhenMediatorThrowsTimeoutException_PropagatesExactType()
    {
        // Arrange
        InboundEvent evt = CreateCommandEvent();
        TimeoutException expectedException = new("Consumer timed out");
        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(expectedException);

        // Act
        Func<Task> act = () => _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — exact exception type propagates (not wrapped in AggregateException etc.)
        (await act.Should().ThrowAsync<TimeoutException>())
            .Which.Should().BeSameAs(expectedException);
    }

    // -----------------------------------------------------------------------
    // Test: Bus failure resilience — log warning, continue (Req 8.5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WhenBusPublishFails_LogsWarningAndContinues()
    {
        // Arrange
        InboundEvent evt = CreateTextMessageEvent();
        _bus.Publish(Arg.Any<MessageReceivedEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Bus unavailable"));

        // Act — should not throw
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — warning logged about bus failure
        _logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("MessageReceivedEvent"));

        // Assert — mediator.Send still called (processing continues despite bus failure)
        await _mediator.Received(1).Send(
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WhenBusPublishFails_StillDispatchesCommand()
    {
        // Arrange
        InboundEvent evt = CreateCommandEvent(commandName: "/status");
        _bus.Publish(Arg.Any<MessageReceivedEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Transport not started"));

        object? capturedCommand = null;
        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedCommand = ci.Arg<object>());

        // Act
        await _sut.ProcessAsync(evt, CancellationToken.None);

        // Assert — command dispatch proceeds despite bus failure
        await _mediator.Received(1).Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
        capturedCommand.Should().BeOfType<ProcessBotCommandCommand>()
            .Which.CommandName.Should().Be("/status");
    }

    // -----------------------------------------------------------------------
    // Fake Logger (same pattern as other test files)
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
