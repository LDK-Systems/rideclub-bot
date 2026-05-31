// ---------------------------------------------------------------------------
// RideClub Bot — ValidationFilter Unit Tests (Req 3.2)
// ---------------------------------------------------------------------------

using FluentAssertions;

using FluentValidation;
using FluentValidation.Results;

using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Filters;

using MassTransit;

using NSubstitute;

using Xunit;

using ValidationResult = FluentValidation.Results.ValidationResult;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ValidationFilter{T}"/> verifying
/// that it throws on validation failure and passes through on success.
/// </summary>
public sealed class ValidationFilterTests
{
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
    public async Task Send_No_Validators_Passes_Through_To_Next()
    {
        // Arrange
        IEnumerable<IValidator<ProcessTextMessageCommand>> validators = [];
        ValidationFilter<ProcessTextMessageCommand> sut = new(validators);
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await sut.Send(context, next);

        // Assert
        await next.Received(1).Send(context);
    }

    [Fact]
    public async Task Send_Valid_Request_Passes_Through_To_Next()
    {
        // Arrange
        IValidator<ProcessTextMessageCommand> validator = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        ValidationFilter<ProcessTextMessageCommand> sut = new([validator]);
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        await sut.Send(context, next);

        // Assert
        await next.Received(1).Send(context);
    }

    [Fact]
    public async Task Send_Invalid_Request_Throws_ValidationException()
    {
        // Arrange
        List<ValidationFailure> failures =
        [
            new("MessageText", "MessageText must not be empty")
        ];

        IValidator<ProcessTextMessageCommand> validator = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        ValidationFilter<ProcessTextMessageCommand> sut = new([validator]);
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        Func<Task> act = () => sut.Send(context, next);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.PropertyName == "MessageText"));
    }

    [Fact]
    public async Task Send_Invalid_Request_Does_Not_Invoke_Next_Pipe()
    {
        // Arrange
        List<ValidationFailure> failures =
        [
            new("MessageText", "Cannot be empty")
        ];

        IValidator<ProcessTextMessageCommand> validator = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        ValidationFilter<ProcessTextMessageCommand> sut = new([validator]);
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        try
        {
            await sut.Send(context, next);
        }
        catch (ValidationException)
        {
            // Expected
        }

        // Assert — next pipe should never be called when validation fails
        await next.DidNotReceive().Send(Arg.Any<SendContext<ProcessTextMessageCommand>>());
    }

    [Fact]
    public async Task Send_Multiple_Validators_Aggregates_All_Failures()
    {
        // Arrange
        IValidator<ProcessTextMessageCommand> validator1 = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator1.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(
            [
                new ValidationFailure("SenderId", "SenderId is required")
            ]));

        IValidator<ProcessTextMessageCommand> validator2 = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator2.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(
            [
                new ValidationFailure("Platform", "Platform is required")
            ]));

        ValidationFilter<ProcessTextMessageCommand> sut = new([validator1, validator2]);
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        Func<Task> act = () => sut.Send(context, next);

        // Assert
        ValidationException exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors.Should().HaveCount(2);
        exception.Errors.Should().Contain(e => e.PropertyName == "SenderId");
        exception.Errors.Should().Contain(e => e.PropertyName == "Platform");
    }

    [Fact]
    public async Task Send_Validator_Passes_Does_Not_Throw()
    {
        // Arrange
        IValidator<ProcessTextMessageCommand> validator = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        ValidationFilter<ProcessTextMessageCommand> sut = new([validator]);
        SendContext<ProcessTextMessageCommand> context = CreateSendContext();
        IPipe<SendContext<ProcessTextMessageCommand>> next = CreateNextPipe();

        // Act
        Func<Task> act = () => sut.Send(context, next);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Probe_Creates_Validation_Filter_Scope()
    {
        // Arrange
        IEnumerable<IValidator<ProcessTextMessageCommand>> validators = [];
        ValidationFilter<ProcessTextMessageCommand> sut = new(validators);
        ProbeContext probeContext = Substitute.For<ProbeContext>();

        // Act
        sut.Probe(probeContext);

        // Assert
        probeContext.Received(1).CreateFilterScope("validation");
    }
}
