// ---------------------------------------------------------------------------
// RideClub Bot — LoggingFilter Unit Tests (Req 3.1)
// ---------------------------------------------------------------------------

using FluentAssertions;

using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Filters;

using MassTransit;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="LoggingFilter{T}"/> verifying
/// that it logs before/after pipeline execution with elapsed time.
/// </summary>
public sealed class LoggingFilterTests
{
    private readonly FakeLogger<LoggingFilter<ProcessTextMessageCommand>> _logger;
    private readonly LoggingFilter<ProcessTextMessageCommand> _sut;

    public LoggingFilterTests()
    {
        _logger = new FakeLogger<LoggingFilter<ProcessTextMessageCommand>>();
        _sut = new LoggingFilter<ProcessTextMessageCommand>(_logger);
    }

    private static SendContext<ProcessTextMessageCommand> CreateSendContext()
    {
        SendContext<ProcessTextMessageCommand> context = Substitute.For<SendContext<ProcessTextMessageCommand>>();
        context.Message.Returns(new ProcessTextMessageCommand
        {
            SenderId = "sender-1",
            ConversationId = "conv-1",
            Platform = "whatsapp",
            MessageText = "Hello",
            Timestamp = DateTimeOffset.UtcNow
        });
        return context;
    }

    private static IPipe<SendContext<ProcessTextMessageCommand>> CreateNextPipe(
        Action? onSend = null)
    {
        IPipe<SendContext<ProcessTextMessageCommand>> next = Substitute.For<IPipe<SendContext<ProcessTextMessageCommand>>>();
        next.Send(Arg.Any<SendContext<ProcessTextMessageCommand>>())
            .Returns(callInfo =>
            {
                onSend?.Invoke();
                return Task.CompletedTask;
            });
        return next;
    }

    [Fact]
    public async Task Send_Logs_Before_And_After_Execution()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await _sut.Send(context, next);

        // Assert
        _logger.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Send_Logs_At_Information_Level()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await _sut.Send(context, next);

        // Assert — both log calls should be at Information level
        _logger.Entries.Should().AllSatisfy(entry =>
            entry.LogLevel.Should().Be(LogLevel.Information));
    }

    [Fact]
    public async Task Send_Invokes_Next_Pipe_Exactly_Once()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await _sut.Send(context, next);

        // Assert
        await next.Received(1).Send(context);
    }

    [Fact]
    public async Task Send_Logs_Handling_Event_Before_Execution()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await _sut.Send(context, next);

        // Assert — the first log entry (EventId 2000) should be the "Handling" message
        _logger.Entries[0].EventId.Should().Be(new EventId(2000));
        _logger.Entries[0].Message.Should().Contain("Handling");
        _logger.Entries[0].Message.Should().Contain(nameof(ProcessTextMessageCommand));
    }

    [Fact]
    public async Task Send_Logs_Handled_Event_After_Execution_With_Elapsed_Time()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await _sut.Send(context, next);

        // Assert — the second log entry (EventId 2001) should be the "Handled" message with elapsed ms
        _logger.Entries[1].EventId.Should().Be(new EventId(2001));
        _logger.Entries[1].Message.Should().Contain("Handled");
        _logger.Entries[1].Message.Should().Contain(nameof(ProcessTextMessageCommand));
        _logger.Entries[1].Message.Should().MatchRegex(@"\d+ms");
    }

    [Fact]
    public async Task Send_Logs_Contain_Request_Id()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await _sut.Send(context, next);

        // Assert — both entries should contain a request ID (8-char hex string)
        _logger.Entries[0].Message.Should().MatchRegex(@"\[[0-9a-f]{8}\]");
        _logger.Entries[1].Message.Should().MatchRegex(@"\[[0-9a-f]{8}\]");
    }

    [Fact]
    public async Task Send_When_Next_Throws_Still_Propagates_Exception()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = Substitute.For<IPipe<SendContext<ProcessTextMessageCommand>>>();
        next.Send(Arg.Any<SendContext<ProcessTextMessageCommand>>())
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        // Act
        Func<Task> act = () => _sut.Send(context, next);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler failed");
    }

    [Fact]
    public void Probe_Creates_Logging_Filter_Scope()
    {
        // Arrange
        ProbeContext probeContext = Substitute.For<ProbeContext>();

        // Act
        _sut.Probe(probeContext);

        // Assert
        probeContext.Received(1).CreateFilterScope("logging");
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
