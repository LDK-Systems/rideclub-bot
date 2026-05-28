// ---------------------------------------------------------------------------
// RideClub Bot — ValidationBehavior Unit Tests (Req 3.2)
// ---------------------------------------------------------------------------

using FluentAssertions;

using FluentValidation;
using FluentValidation.Results;

using LDK.RideClub.Bot.Behaviors;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Responses;

using MediatR;

using NSubstitute;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ValidationBehavior{TRequest, TResponse}"/> verifying
/// that it throws on validation failure and passes through on success.
/// </summary>
public sealed class ValidationBehaviorTests
{
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
    public async Task Handle_No_Validators_Passes_Through_To_Next()
    {
        // Arrange
        IEnumerable<IValidator<ProcessTextMessageCommand>> validators = [];
        ValidationBehavior<ProcessTextMessageCommand, MessageProcessingResult> sut = new(validators);
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();

        // Act
        MessageProcessingResult result = await sut.Handle(
            command,
            Next(expectedResult),
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task Handle_Valid_Request_Passes_Through_To_Next()
    {
        // Arrange
        IValidator<ProcessTextMessageCommand> validator = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        ValidationBehavior<ProcessTextMessageCommand, MessageProcessingResult> sut = new([validator]);
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = CreateSuccessResult();

        // Act
        MessageProcessingResult result = await sut.Handle(
            command,
            Next(expectedResult),
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task Handle_Invalid_Request_Throws_ValidationException()
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

        ValidationBehavior<ProcessTextMessageCommand, MessageProcessingResult> sut = new([validator]);
        ProcessTextMessageCommand command = CreateTestCommand();
        bool nextCalled = false;

        Task<MessageProcessingResult> TrackingNext(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult(CreateSuccessResult());
        }

        // Act
        Func<Task> act = () => sut.Handle(command, TrackingNext, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.PropertyName == "MessageText"));
        nextCalled.Should().BeFalse("handler should not be invoked when validation fails");
    }

    [Fact]
    public async Task Handle_Multiple_Validators_Aggregates_All_Failures()
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

        ValidationBehavior<ProcessTextMessageCommand, MessageProcessingResult> sut = new([validator1, validator2]);
        ProcessTextMessageCommand command = CreateTestCommand();

        // Act
        Func<Task> act = () => sut.Handle(command, Next(CreateSuccessResult()), CancellationToken.None);

        // Assert
        ValidationException exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors.Should().HaveCount(2);
        exception.Errors.Should().Contain(e => e.PropertyName == "SenderId");
        exception.Errors.Should().Contain(e => e.PropertyName == "Platform");
    }

    [Fact]
    public async Task Handle_Validator_Passes_Does_Not_Throw()
    {
        // Arrange
        IValidator<ProcessTextMessageCommand> validator = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        ValidationBehavior<ProcessTextMessageCommand, MessageProcessingResult> sut = new([validator]);
        ProcessTextMessageCommand command = CreateTestCommand();
        MessageProcessingResult expectedResult = new() { Success = true, ReplyMessage = "OK" };

        // Act
        Func<Task> act = () => sut.Handle(command, Next(expectedResult), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_Short_Circuits_Before_Handler_On_Failure()
    {
        // Arrange
        IValidator<ProcessTextMessageCommand> validator = Substitute.For<IValidator<ProcessTextMessageCommand>>();
        validator.ValidateAsync(
            Arg.Any<ValidationContext<ProcessTextMessageCommand>>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(
            [
                new ValidationFailure("MessageText", "Cannot be empty")
            ]));

        ValidationBehavior<ProcessTextMessageCommand, MessageProcessingResult> sut = new([validator]);
        ProcessTextMessageCommand command = CreateTestCommand();
        int nextCallCount = 0;

        Task<MessageProcessingResult> CountingNext(CancellationToken ct)
        {
            nextCallCount++;
            return Task.FromResult(CreateSuccessResult());
        }

        // Act
        try
        {
            await sut.Handle(command, CountingNext, CancellationToken.None);
        }
        catch (ValidationException)
        {
            // Expected
        }

        // Assert
        nextCallCount.Should().Be(0, "the handler should never be called when validation fails");
    }

    private static RequestHandlerDelegate<MessageProcessingResult> Next(MessageProcessingResult result)
    {
        return (ct) => Task.FromResult(result);
    }
}
