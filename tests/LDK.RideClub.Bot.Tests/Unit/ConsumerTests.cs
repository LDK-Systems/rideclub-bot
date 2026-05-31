// ---------------------------------------------------------------------------
// RideClub Bot — Consumer Unit Tests (Req 8.1, 8.2, 2.4)
// ---------------------------------------------------------------------------

using FluentAssertions;

using LDK.RideClub.Bot.Consumers;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Events.Conversation;

using MassTransit;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for MassTransit consumers verifying outcome event publication
/// and logging behaviour.
/// </summary>
public sealed class ConsumerTests
{
    // -----------------------------------------------------------------------
    // ProcessTextMessageConsumer Tests (Req 8.1)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessTextMessageConsumer_Publishes_ProcessingCompletedEvent_On_Success()
    {
        // Arrange
        IBus bus = Substitute.For<IBus>();
        FakeLogger<ProcessTextMessageConsumer> logger = new();
        ProcessTextMessageConsumer sut = new(bus, logger);

        ConsumeContext<ProcessTextMessageCommand> context = Substitute.For<ConsumeContext<ProcessTextMessageCommand>>();
        context.Message.Returns(new ProcessTextMessageCommand
        {
            SenderId = "user-123",
            ConversationId = "conv-456",
            Platform = "whatsapp",
            MessageText = "Hello bot",
            Timestamp = DateTimeOffset.UtcNow
        });
        context.CancellationToken.Returns(CancellationToken.None);

        // Act
        await sut.Consume(context);

        // Assert
        await bus.Received(1).Publish(
            Arg.Is<ProcessingCompletedEvent>(e =>
                e.Platform == "whatsapp" &&
                e.SenderId == "user-123" &&
                !e.RequiresConfirmation),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTextMessageConsumer_Does_Not_Publish_ProcessingFaultedEvent_On_Success()
    {
        // Arrange
        IBus bus = Substitute.For<IBus>();
        FakeLogger<ProcessTextMessageConsumer> logger = new();
        ProcessTextMessageConsumer sut = new(bus, logger);

        ConsumeContext<ProcessTextMessageCommand> context = Substitute.For<ConsumeContext<ProcessTextMessageCommand>>();
        context.Message.Returns(new ProcessTextMessageCommand
        {
            SenderId = "user-123",
            ConversationId = "conv-456",
            Platform = "whatsapp",
            MessageText = "Hello bot",
            Timestamp = DateTimeOffset.UtcNow
        });
        context.CancellationToken.Returns(CancellationToken.None);

        // Act
        await sut.Consume(context);

        // Assert
        await bus.DidNotReceive().Publish(
            Arg.Any<ProcessingFaultedEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTextMessageConsumer_Publishes_Event_With_Correct_Platform_And_SenderId()
    {
        // Arrange
        IBus bus = Substitute.For<IBus>();
        FakeLogger<ProcessTextMessageConsumer> logger = new();
        ProcessTextMessageConsumer sut = new(bus, logger);

        ConsumeContext<ProcessTextMessageCommand> context = Substitute.For<ConsumeContext<ProcessTextMessageCommand>>();
        context.Message.Returns(new ProcessTextMessageCommand
        {
            SenderId = "specific-sender",
            ConversationId = "specific-conv",
            Platform = "telegram",
            MessageText = "Test message",
            Timestamp = DateTimeOffset.UtcNow
        });
        context.CancellationToken.Returns(CancellationToken.None);

        // Act
        await sut.Consume(context);

        // Assert
        await bus.Received(1).Publish(
            Arg.Is<ProcessingCompletedEvent>(e =>
                e.Platform == "telegram" &&
                e.SenderId == "specific-sender"),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // ProcessBotCommandConsumer Tests (Req 8.1, 8.2)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessBotCommandConsumer_Publishes_ProcessingCompletedEvent_On_Success()
    {
        // Arrange
        IBus bus = Substitute.For<IBus>();
        FakeLogger<ProcessBotCommandConsumer> logger = new();
        ProcessBotCommandConsumer sut = new(bus, logger);

        ConsumeContext<ProcessBotCommandCommand> context = Substitute.For<ConsumeContext<ProcessBotCommandCommand>>();
        context.Message.Returns(new ProcessBotCommandCommand
        {
            SenderId = "user-789",
            ConversationId = "conv-012",
            Platform = "whatsapp",
            CommandName = "/help",
            Timestamp = DateTimeOffset.UtcNow
        });
        context.CancellationToken.Returns(CancellationToken.None);

        // Act
        await sut.Consume(context);

        // Assert
        await bus.Received(1).Publish(
            Arg.Is<ProcessingCompletedEvent>(e =>
                e.Platform == "whatsapp" &&
                e.SenderId == "user-789" &&
                !e.RequiresConfirmation),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBotCommandConsumer_Does_Not_Publish_ProcessingFaultedEvent_On_Success()
    {
        // Arrange
        IBus bus = Substitute.For<IBus>();
        FakeLogger<ProcessBotCommandConsumer> logger = new();
        ProcessBotCommandConsumer sut = new(bus, logger);

        ConsumeContext<ProcessBotCommandCommand> context = Substitute.For<ConsumeContext<ProcessBotCommandCommand>>();
        context.Message.Returns(new ProcessBotCommandCommand
        {
            SenderId = "user-789",
            ConversationId = "conv-012",
            Platform = "whatsapp",
            CommandName = "/help",
            Timestamp = DateTimeOffset.UtcNow
        });
        context.CancellationToken.Returns(CancellationToken.None);

        // Act
        await sut.Consume(context);

        // Assert
        await bus.DidNotReceive().Publish(
            Arg.Any<ProcessingFaultedEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBotCommandConsumer_Publishes_Event_With_Correct_Platform_And_SenderId()
    {
        // Arrange
        IBus bus = Substitute.For<IBus>();
        FakeLogger<ProcessBotCommandConsumer> logger = new();
        ProcessBotCommandConsumer sut = new(bus, logger);

        ConsumeContext<ProcessBotCommandCommand> context = Substitute.For<ConsumeContext<ProcessBotCommandCommand>>();
        context.Message.Returns(new ProcessBotCommandCommand
        {
            SenderId = "bot-user",
            ConversationId = "conv-abc",
            Platform = "telegram",
            CommandName = "/start",
            Timestamp = DateTimeOffset.UtcNow
        });
        context.CancellationToken.Returns(CancellationToken.None);

        // Act
        await sut.Consume(context);

        // Assert
        await bus.Received(1).Publish(
            Arg.Is<ProcessingCompletedEvent>(e =>
                e.Platform == "telegram" &&
                e.SenderId == "bot-user"),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // UnrecognisedEventConsumer Tests (Req 2.4)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnrecognisedEventConsumer_Logs_Warning_With_Payload_Type()
    {
        // Arrange
        FakeLogger<UnrecognisedEventConsumer> logger = new();
        UnrecognisedEventConsumer sut = new(logger);

        InboundEvent inboundEvent = new()
        {
            EventId = "evt-1",
            Platform = "whatsapp",
            SenderId = "sender-1",
            ConversationId = "conv-1",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new UnknownTestPayload()
        };

        ConsumeContext<UnrecognisedEventNotification> context =
            Substitute.For<ConsumeContext<UnrecognisedEventNotification>>();
        context.Message.Returns(new UnrecognisedEventNotification(inboundEvent));

        // Act
        await sut.Consume(context);

        // Assert
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].LogLevel.Should().Be(LogLevel.Warning);
        logger.Entries[0].Message.Should().Contain(nameof(UnknownTestPayload));
    }

    [Fact]
    public async Task UnrecognisedEventConsumer_Logs_At_Warning_Level()
    {
        // Arrange
        FakeLogger<UnrecognisedEventConsumer> logger = new();
        UnrecognisedEventConsumer sut = new(logger);

        InboundEvent inboundEvent = new()
        {
            EventId = "evt-2",
            Platform = "telegram",
            SenderId = "sender-2",
            ConversationId = "conv-2",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new UnknownTestPayload()
        };

        ConsumeContext<UnrecognisedEventNotification> context =
            Substitute.For<ConsumeContext<UnrecognisedEventNotification>>();
        context.Message.Returns(new UnrecognisedEventNotification(inboundEvent));

        // Act
        await sut.Consume(context);

        // Assert
        logger.Entries.Should().AllSatisfy(entry =>
            entry.LogLevel.Should().Be(LogLevel.Warning));
    }

    [Fact]
    public async Task UnrecognisedEventConsumer_Returns_Completed_Task()
    {
        // Arrange
        FakeLogger<UnrecognisedEventConsumer> logger = new();
        UnrecognisedEventConsumer sut = new(logger);

        InboundEvent inboundEvent = new()
        {
            EventId = "evt-3",
            Platform = "whatsapp",
            SenderId = "sender-3",
            ConversationId = "conv-3",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new UnknownTestPayload()
        };

        ConsumeContext<UnrecognisedEventNotification> context =
            Substitute.For<ConsumeContext<UnrecognisedEventNotification>>();
        context.Message.Returns(new UnrecognisedEventNotification(inboundEvent));

        // Act
        Func<Task> act = () => sut.Consume(context);

        // Assert — should complete without throwing
        await act.Should().NotThrowAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>An unknown payload type for testing the unrecognised event path.</summary>
    private sealed record UnknownTestPayload : EventPayload;

    /// <summary>
    /// A simple fake logger that captures log entries for assertion.
    /// Avoids NSubstitute proxy issues with internal generic type parameters.
    /// </summary>
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
