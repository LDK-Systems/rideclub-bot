// ---------------------------------------------------------------------------
// RideClub Bot — LoggingBehavior Unit Tests (Req 3.1)
// ---------------------------------------------------------------------------

using FluentAssertions;

using LDK.RideClub.Bot.Behaviors;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Responses;

using MediatR;

using Microsoft.Extensions.Logging;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="LoggingBehavior{TRequest, TResponse}"/> verifying
/// that it logs before/after handler execution with elapsed time.
/// </summary>
public sealed class LoggingBehaviorTests
{
    private readonly FakeLogger<LoggingBehavior<ProcessTextMessageCommand, MessageProcessingResult>> _logger;
    private readonly LoggingBehavior<ProcessTextMessageCommand, MessageProcessingResult> _sut;

    public LoggingBehaviorTests()
    {
        _logger = new FakeLogger<LoggingBehavior<ProcessTextMessageCommand, MessageProcessingResult>>();
        _sut = new LoggingBehavior<ProcessTextMessageCommand, MessageProcessingResult>(_logger);
    }

    private static ProcessTextMessageCommand CreateTestCommand()
    {
        return new ProcessTextMessageCommand
        {
            SenderId = "sender-1",
            ConversationId = "conv-1",
            Platform = "whatsapp",
            MessageText = "Hello",
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static MessageProcessingResult CreateSuccessResult()
    {
        return new MessageProcessingResult { Success = true };
    }

    [Fact]
    public async Task Handle_Logs_Before_And_After_Execution()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();

        // Act
        MessageProcessingResult result = await _sut.Handle(
            command,
            Next(expectedResult),
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
        _logger.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Logs_At_Information_Level()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();

        // Act
        await _sut.Handle(command, Next(expectedResult), CancellationToken.None);

        // Assert — both log calls should be at Information level
        _logger.Entries.Should().AllSatisfy(entry =>
            entry.LogLevel.Should().Be(LogLevel.Information));
    }

    [Fact]
    public async Task Handle_Returns_Response_From_Next_Delegate()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = new() { Success = true, ReplyMessage = "Done" };

        // Act
        MessageProcessingResult result = await _sut.Handle(
            command,
            Next(expectedResult),
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task Handle_Invokes_Next_Delegate_Exactly_Once()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();
        int callCount = 0;

        Task<MessageProcessingResult> CountingNext(CancellationToken ct)
        {
            callCount++;
            return Task.FromResult(expectedResult);
        }

        // Act
        await _sut.Handle(command, CountingNext, CancellationToken.None);

        // Assert
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Logs_Handling_Event_Before_Execution()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();

        // Act
        await _sut.Handle(command, Next(expectedResult), CancellationToken.None);

        // Assert — the first log entry (EventId 2000) should be the "Handling" message
        _logger.Entries[0].EventId.Should().Be(new EventId(2000));
        _logger.Entries[0].Message.Should().Contain("Handling");
        _logger.Entries[0].Message.Should().Contain(nameof(ProcessTextMessageCommand));
    }

    [Fact]
    public async Task Handle_Logs_Handled_Event_After_Execution_With_Elapsed_Time()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();

        // Act
        await _sut.Handle(command, Next(expectedResult), CancellationToken.None);

        // Assert — the second log entry (EventId 2001) should be the "Handled" message with elapsed ms
        _logger.Entries[1].EventId.Should().Be(new EventId(2001));
        _logger.Entries[1].Message.Should().Contain("Handled");
        _logger.Entries[1].Message.Should().Contain(nameof(ProcessTextMessageCommand));
        _logger.Entries[1].Message.Should().MatchRegex(@"\d+ms");
    }

    private static RequestHandlerDelegate<MessageProcessingResult> Next(MessageProcessingResult result)
    {
        return (ct) => Task.FromResult(result);
    }

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
