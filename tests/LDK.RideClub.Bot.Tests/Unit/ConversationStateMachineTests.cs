// ---------------------------------------------------------------------------
// RideClub Bot — ConversationStateMachine Unit Tests (Req 6.3, 6.4, 6.5, 6.6, 10.2)
// ---------------------------------------------------------------------------

using FluentAssertions;

using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Persistence.Entities;
using LDK.RideClub.Bot.Sagas;

using MassTransit;
using MassTransit.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ConversationStateMachine"/> using the MassTransit
/// in-memory test harness to verify state transitions in isolation.
/// </summary>
public sealed class ConversationStateMachineTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<ConversationStateMachine, ConversationSagaInstance> _sagaHarness = null!;

    public async Task InitializeAsync()
    {
        ServiceCollection services = new();

        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<ConversationStateMachine, ConversationSagaInstance>()
                .InMemoryRepository();
        });

        _provider = services.BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();

        _sagaHarness = _harness.GetSagaStateMachineHarness<ConversationStateMachine, ConversationSagaInstance>();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static MessageReceivedEvent CreateMessageReceived(
        string platform = "whatsapp",
        string senderId = "user-123",
        string conversationId = "conv-456")
    {
        return new MessageReceivedEvent
        {
            Platform = platform,
            SenderId = senderId,
            ConversationId = conversationId,
            EventId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static ProcessingCompletedEvent CreateProcessingCompleted(
        string platform = "whatsapp",
        string senderId = "user-123",
        bool requiresConfirmation = false)
    {
        return new ProcessingCompletedEvent
        {
            Platform = platform,
            SenderId = senderId,
            RequiresConfirmation = requiresConfirmation,
            ReplyMessage = "Done"
        };
    }

    private static ProcessingFaultedEvent CreateProcessingFaulted(
        string platform = "whatsapp",
        string senderId = "user-123")
    {
        return new ProcessingFaultedEvent
        {
            Platform = platform,
            SenderId = senderId,
            ErrorReason = "Something went wrong"
        };
    }

    private static ConversationTimedOutEvent CreateTimedOut(
        string platform = "whatsapp",
        string senderId = "user-123")
    {
        return new ConversationTimedOutEvent
        {
            Platform = platform,
            SenderId = senderId,
            Reason = "Inactivity timeout"
        };
    }

    private static string BuildCorrelationKey(string platform = "whatsapp", string senderId = "user-123")
    {
        return $"{platform}:{senderId}";
    }

    /// <summary>
    /// Waits for the saga to reach the specified state and returns the saga ID.
    /// Uses the expression-based Exists overload which returns IList&lt;Guid&gt;.
    /// </summary>
    private async Task<Guid> WaitForState(Func<ConversationStateMachine, State> stateSelector, string correlationKey)
    {
        IList<Guid> ids = await _sagaHarness.Exists(
            x => x.CorrelationKey == correlationKey,
            stateSelector);

        ids.Should().NotBeEmpty($"saga should exist in the expected state for key '{correlationKey}'");
        return ids[0];
    }

    // -----------------------------------------------------------------------
    // Test: New message creates saga in AwaitingInput state (Req 6.3)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NewMessage_CreatesSaga_InAwaitingInputState()
    {
        // Arrange
        MessageReceivedEvent message = CreateMessageReceived();
        string key = BuildCorrelationKey();

        // Act
        await _harness.Bus.Publish(message);

        // Assert — saga should exist in AwaitingInput state
        Guid sagaId = await WaitForState(m => m.AwaitingInput, key);

        // Verify instance fields are populated correctly
        ConversationSagaInstance instance = _sagaHarness.Sagas.Contains(sagaId);
        instance.Should().NotBeNull();
        instance.Platform.Should().Be(message.Platform);
        instance.SenderId.Should().Be(message.SenderId);
        instance.ConversationId.Should().Be(message.ConversationId);
        instance.CorrelationKey.Should().Be(key);
    }

    // -----------------------------------------------------------------------
    // Test: AwaitingInput → Processing on message received (Req 6.4)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AwaitingInput_TransitionsToProcessing_OnMessageReceived()
    {
        // Arrange — create saga in AwaitingInput state
        string key = BuildCorrelationKey();
        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.AwaitingInput, key);

        // Act — send second message to transition from AwaitingInput → Processing
        await _harness.Bus.Publish(CreateMessageReceived());

        // Assert
        Guid sagaId = await WaitForState(m => m.Processing, key);
        sagaId.Should().NotBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test: Processing → AwaitingInput on completion without confirmation (Req 6.5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Processing_TransitionsToAwaitingInput_OnCompletionWithoutConfirmation()
    {
        // Arrange — get saga into Processing state
        string key = BuildCorrelationKey();
        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.AwaitingInput, key);

        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.Processing, key);

        // Act — publish completion without confirmation
        await _harness.Bus.Publish(CreateProcessingCompleted(requiresConfirmation: false));

        // Assert
        Guid sagaId = await WaitForState(m => m.AwaitingInput, key);
        sagaId.Should().NotBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test: Processing → AwaitingConfirmation on completion with confirmation (Req 6.5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Processing_TransitionsToAwaitingConfirmation_OnCompletionWithConfirmation()
    {
        // Arrange — get saga into Processing state
        string key = BuildCorrelationKey();
        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.AwaitingInput, key);

        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.Processing, key);

        // Act — publish completion with confirmation required
        await _harness.Bus.Publish(CreateProcessingCompleted(requiresConfirmation: true));

        // Assert
        Guid sagaId = await WaitForState(m => m.AwaitingConfirmation, key);
        sagaId.Should().NotBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test: Processing → Faulted on fault event (Req 6.5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Processing_TransitionsToFaulted_OnProcessingFaulted()
    {
        // Arrange — get saga into Processing state
        string key = BuildCorrelationKey();
        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.AwaitingInput, key);

        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.Processing, key);

        // Act — publish fault event
        await _harness.Bus.Publish(CreateProcessingFaulted());

        // Assert
        Guid sagaId = await WaitForState(m => m.Faulted, key);
        sagaId.Should().NotBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test: Timeout in AwaitingInput transitions to Completed (Req 6.6)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AwaitingInput_TransitionsToCompleted_OnTimeout()
    {
        // Arrange — create saga in AwaitingInput state
        string key = BuildCorrelationKey();
        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.AwaitingInput, key);

        // Act — publish timeout event
        await _harness.Bus.Publish(CreateTimedOut());

        // Assert
        Guid sagaId = await WaitForState(m => m.Completed, key);
        sagaId.Should().NotBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test: Timeout in AwaitingConfirmation transitions to Completed (Req 6.6)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AwaitingConfirmation_TransitionsToCompleted_OnTimeout()
    {
        // Arrange — get saga into AwaitingConfirmation state
        string key = BuildCorrelationKey();
        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.AwaitingInput, key);

        await _harness.Bus.Publish(CreateMessageReceived());
        await WaitForState(m => m.Processing, key);

        await _harness.Bus.Publish(CreateProcessingCompleted(requiresConfirmation: true));
        await WaitForState(m => m.AwaitingConfirmation, key);

        // Act — publish timeout event
        await _harness.Bus.Publish(CreateTimedOut());

        // Assert
        Guid sagaId = await WaitForState(m => m.Completed, key);
        sagaId.Should().NotBeEmpty();
    }
}
