// ---------------------------------------------------------------------------
// RideClub Bot — TelemetryFilter Unit Tests (Req 3.3)
// ---------------------------------------------------------------------------

using System.Diagnostics;

using FluentAssertions;

using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Filters;

using MassTransit;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="TelemetryFilter{T}"/> verifying
/// that it creates activity spans with correct attributes for success and failure.
/// </summary>
public sealed class TelemetryFilterTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];
    private readonly TelemetryFilter<ProcessTextMessageCommand> _sut;

    public TelemetryFilterTests()
    {
        _sut = new TelemetryFilter<ProcessTextMessageCommand>();

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

    private static IPipe<SendContext<ProcessTextMessageCommand>> CreateNextPipe()
    {
        IPipe<SendContext<ProcessTextMessageCommand>> next = Substitute.For<IPipe<SendContext<ProcessTextMessageCommand>>>();
        next.Send(Arg.Any<SendContext<ProcessTextMessageCommand>>())
            .Returns(Task.CompletedTask);
        return next;
    }

    [Fact]
    public async Task Send_Creates_Activity_Span_Named_After_Message_Type()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await _sut.Send(context, next);

        // Assert
        _activities.Should().ContainSingle();
        _activities[0].OperationName.Should().Be(nameof(ProcessTextMessageCommand));
    }

    [Fact]
    public async Task Send_Sets_Success_Outcome_Tag_On_Successful_Execution()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await _sut.Send(context, next);

        // Assert
        _activities.Should().ContainSingle();
        Activity activity = _activities[0];
        activity.Tags.Should().Contain(t => t.Key == "mediator.outcome" && t.Value == "success");
    }

    [Fact]
    public async Task Send_Sets_Exception_Outcome_Tag_On_Failure()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = Substitute.For<IPipe<SendContext<ProcessTextMessageCommand>>>();
        next.Send(Arg.Any<SendContext<ProcessTextMessageCommand>>())
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        // Act
        Func<Task> act = () => _sut.Send(context, next);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _activities.Should().ContainSingle();
        Activity activity = _activities[0];
        activity.Tags.Should().Contain(t => t.Key == "mediator.outcome" && t.Value == "exception");
        activity.Tags.Should().Contain(t => t.Key == "mediator.exception_type" && t.Value == "InvalidOperationException");
    }

    [Fact]
    public async Task Send_Propagates_Exception_After_Recording_Telemetry()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        ArgumentException expectedException = new("Bad argument");
        IPipe<SendContext<ProcessTextMessageCommand>> next = Substitute.For<IPipe<SendContext<ProcessTextMessageCommand>>>();
        next.Send(Arg.Any<SendContext<ProcessTextMessageCommand>>())
            .ThrowsAsync(expectedException);

        // Act
        Func<Task> act = () => _sut.Send(context, next);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .Which.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task Send_Invokes_Next_Pipe()
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
    public async Task Send_Records_Correct_Exception_Type_Name()
    {
        // Arrange
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = Substitute.For<IPipe<SendContext<ProcessTextMessageCommand>>>();
        next.Send(Arg.Any<SendContext<ProcessTextMessageCommand>>())
            .ThrowsAsync(new TimeoutException("Timed out"));

        // Act
        Func<Task> act = () => _sut.Send(context, next);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        _activities.Should().ContainSingle();
        Activity activity = _activities[0];
        activity.Tags.Should().Contain(t => t.Key == "mediator.exception_type" && t.Value == "TimeoutException");
    }

    [Fact]
    public void Probe_Creates_Telemetry_Filter_Scope()
    {
        // Arrange
        ProbeContext probeContext = Substitute.For<ProbeContext>();

        // Act
        _sut.Probe(probeContext);

        // Assert
        probeContext.Received(1).CreateFilterScope("telemetry");
    }
}
