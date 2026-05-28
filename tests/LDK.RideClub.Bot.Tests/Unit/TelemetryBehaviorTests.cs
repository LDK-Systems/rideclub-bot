// ---------------------------------------------------------------------------
// RideClub Bot — TelemetryBehavior Unit Tests (Req 3.3)
// ---------------------------------------------------------------------------

using System.Diagnostics;

using FluentAssertions;

using LDK.RideClub.Bot.Behaviors;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Responses;

using MediatR;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="TelemetryBehavior{TRequest, TResponse}"/> verifying
/// that it creates activity spans with correct attributes for success and failure.
/// </summary>
public sealed class TelemetryBehaviorTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];
    private readonly TelemetryBehavior<ProcessTextMessageCommand, MessageProcessingResult> _sut;

    public TelemetryBehaviorTests()
    {
        _sut = new TelemetryBehavior<ProcessTextMessageCommand, MessageProcessingResult>();

        // Set up an ActivityListener to capture activities from the "RideClub.Bot.Mediator" source
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "RideClub.Bot.Mediator",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
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
    public async Task Handle_Creates_Activity_Span_Named_After_Request_Type()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();

        // Act
        await _sut.Handle(command, Next(expectedResult), CancellationToken.None);

        // Assert
        _activities.Should().ContainSingle();
        _activities[0].OperationName.Should().Be(nameof(ProcessTextMessageCommand));
    }

    [Fact]
    public async Task Handle_Sets_Success_Outcome_Tag_On_Successful_Execution()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();

        // Act
        await _sut.Handle(command, Next(expectedResult), CancellationToken.None);

        // Assert
        _activities.Should().ContainSingle();
        Activity activity = _activities[0];
        activity.Tags.Should().Contain(t => t.Key == "mediatr.outcome" && t.Value == "success");
    }

    [Fact]
    public async Task Handle_Sets_Exception_Outcome_Tag_On_Failure()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();

        static Task<MessageProcessingResult> ThrowingNext(CancellationToken ct)
        {
            throw new InvalidOperationException("Handler failed");
        }

        // Act
        Func<Task> act = () => _sut.Handle(command, ThrowingNext, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _activities.Should().ContainSingle();
        Activity activity = _activities[0];
        activity.Tags.Should().Contain(t => t.Key == "mediatr.outcome" && t.Value == "exception");
        activity.Tags.Should().Contain(t => t.Key == "mediatr.exception_type" && t.Value == "InvalidOperationException");
    }

    [Fact]
    public async Task Handle_Propagates_Exception_After_Recording_Telemetry()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();
        ArgumentException expectedException = new("Bad argument");

        Task<MessageProcessingResult> ThrowingNext(CancellationToken ct)
        {
            throw expectedException;
        }

        // Act
        Func<Task> act = () => _sut.Handle(command, ThrowingNext, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .Which.Should().BeSameAs(expectedException);
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
    public async Task Handle_Records_Correct_Exception_Type_Name()
    {
        // Arrange
        ProcessTextMessageCommand command = CreateTestCommand();

        static Task<MessageProcessingResult> ThrowingNext(CancellationToken ct)
        {
            throw new TimeoutException("Timed out");
        }

        // Act
        Func<Task> act = () => _sut.Handle(command, ThrowingNext, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        _activities.Should().ContainSingle();
        Activity activity = _activities[0];
        activity.Tags.Should().Contain(t => t.Key == "mediatr.exception_type" && t.Value == "TimeoutException");
    }

    private static RequestHandlerDelegate<MessageProcessingResult> Next(MessageProcessingResult result)
    {
        return (ct) => Task.FromResult(result);
    }
}
