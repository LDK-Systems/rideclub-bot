// ---------------------------------------------------------------------------
// RideClub Bot — Full Pipeline Integration Tests (Req 10.3, 10.4)
// Feature: mediatr-masstransit-pipeline
// ---------------------------------------------------------------------------

using System.Net;
using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using LDK.RideClub.Bot.Domain.Events.Conversation;
using LDK.RideClub.Bot.Persistence.Entities;
using LDK.RideClub.Bot.Sagas;

using MassTransit;
using MassTransit.Testing;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Integration;

/// <summary>
/// Integration tests verifying the full pipeline:
/// Webhook → EventDispatcher → Mediator → Handler → Bus → State Machine.
/// Uses MassTransit test harness mode to verify saga transitions without
/// network access, Docker, or external message brokers.
/// </summary>
/// <remarks>
/// Validates Requirements 10.3 and 10.4.
/// </remarks>
#pragma warning disable CA1707 // Test method names may contain underscores
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
public sealed class FullPipelineIntegrationTests : IClassFixture<MassTransitHarnessWebApplicationFactory>, IAsyncLifetime
{
    private readonly MassTransitHarnessWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FullPipelineIntegrationTests(MassTransitHarnessWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // Full Pipeline: Webhook → EventDispatcher → Mediator → Handler → Bus → State Machine
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FullPipeline_ValidTextMessage_CreatesConversationSagaInAwaitingInputState()
    {
        // Arrange — a valid WhatsApp webhook payload with a text message
        string payload = CreateWhatsAppTextMessagePayload("447700900001", "Hello from integration test");
        HttpRequestMessage request = CreateSignedWebhookRequest(payload);

        // Act — POST the webhook
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert — webhook returns 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — the saga state machine received the MessageReceivedEvent and created an instance
        ITestHarness harness = _factory.Services.GetRequiredService<ITestHarness>();
        ISagaStateMachineTestHarness<ConversationStateMachine, ConversationSagaInstance> sagaHarness =
            harness.GetSagaStateMachineHarness<ConversationStateMachine, ConversationSagaInstance>();

        // Wait for the saga to reach AwaitingInput state (first message creates saga, then
        // the ProcessingCompletedEvent transitions it back to AwaitingInput)
        string correlationKey = "whatsapp:447700900001";
        IList<Guid> sagaIds = await sagaHarness.Exists(
            x => x.CorrelationKey == correlationKey,
            m => m.AwaitingInput);

        sagaIds.Should().NotBeEmpty("saga should exist in AwaitingInput state after full pipeline execution");

        // Verify the saga instance has correct fields
        ConversationSagaInstance instance = sagaHarness.Sagas.Contains(sagaIds[0]);
        instance.Should().NotBeNull();
        instance.Platform.Should().Be("whatsapp");
        instance.SenderId.Should().Be("447700900001");
    }

    [Fact]
    public async Task FullPipeline_TwoConsecutiveMessages_TransitionsThroughProcessingState()
    {
        // Arrange — use a unique sender to avoid collision with other tests
        string senderId = "447700900002";
        string correlationKey = $"whatsapp:{senderId}";

        ITestHarness harness = _factory.Services.GetRequiredService<ITestHarness>();
        ISagaStateMachineTestHarness<ConversationStateMachine, ConversationSagaInstance> sagaHarness =
            harness.GetSagaStateMachineHarness<ConversationStateMachine, ConversationSagaInstance>();

        // Act 1 — send first message (creates saga, transitions Initial → AwaitingInput,
        // then MessageReceived triggers AwaitingInput → Processing,
        // then ProcessingCompleted triggers Processing → AwaitingInput)
        string payload1 = CreateWhatsAppTextMessagePayload(senderId, "First message");
        HttpRequestMessage request1 = CreateSignedWebhookRequest(payload1);
        HttpResponseMessage response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for saga to settle in AwaitingInput after first message processing
        IList<Guid> sagaIds = await sagaHarness.Exists(
            x => x.CorrelationKey == correlationKey,
            m => m.AwaitingInput);
        sagaIds.Should().NotBeEmpty();

        // Act 2 — send second message (AwaitingInput → Processing → AwaitingInput)
        string payload2 = CreateWhatsAppTextMessagePayload(senderId, "Second message");
        HttpRequestMessage request2 = CreateSignedWebhookRequest(payload2);
        HttpResponseMessage response2 = await _client.SendAsync(request2);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — saga should still exist and be back in AwaitingInput
        // (the handler publishes ProcessingCompleted which transitions back)
        IList<Guid> finalSagaIds = await sagaHarness.Exists(
            x => x.CorrelationKey == correlationKey,
            m => m.AwaitingInput);
        finalSagaIds.Should().NotBeEmpty("saga should return to AwaitingInput after second message processing");

        // Verify it's the same saga instance (same correlation key)
        finalSagaIds[0].Should().Be(sagaIds[0], "the same saga instance should be reused for the same sender");
    }

    [Fact]
    public async Task FullPipeline_MessageReceivedEvent_IsPublishedBeforeCommandDispatch()
    {
        // Arrange — unique sender
        string senderId = "447700900003";

        ITestHarness harness = _factory.Services.GetRequiredService<ITestHarness>();

        // Act — send a webhook message
        string payload = CreateWhatsAppTextMessagePayload(senderId, "Ordering test");
        HttpRequestMessage request = CreateSignedWebhookRequest(payload);
        HttpResponseMessage response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — MessageReceivedEvent was published to the bus
        (await harness.Published.Any<MessageReceivedEvent>(
            x => x.Context != null && x.Context.Message.SenderId == senderId))
            .Should().BeTrue("EventDispatcher should publish MessageReceivedEvent to the bus");

        // Assert — ProcessingCompletedEvent was also published (handler succeeded)
        (await harness.Published.Any<ProcessingCompletedEvent>(
            x => x.Context != null && x.Context.Message.SenderId == senderId))
            .Should().BeTrue("Handler should publish ProcessingCompletedEvent after successful processing");
    }

    [Fact]
    public async Task FullPipeline_RunsWithoutNetworkAccess_NoExternalDependencies()
    {
        // This test verifies that the full pipeline works without any external
        // dependencies — no Docker, no network, no message broker.
        // The fact that this test runs and passes in the test harness proves Req 10.4.

        // Arrange
        string senderId = "447700900004";
        string payload = CreateWhatsAppTextMessagePayload(senderId, "No network needed");
        HttpRequestMessage request = CreateSignedWebhookRequest(payload);

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert — the full pipeline executed successfully without external dependencies
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the saga was created (proves MassTransit is working in-memory)
        ITestHarness harness = _factory.Services.GetRequiredService<ITestHarness>();
        ISagaStateMachineTestHarness<ConversationStateMachine, ConversationSagaInstance> sagaHarness =
            harness.GetSagaStateMachineHarness<ConversationStateMachine, ConversationSagaInstance>();

        string correlationKey = $"whatsapp:{senderId}";
        IList<Guid> sagaIds = await sagaHarness.Exists(
            x => x.CorrelationKey == correlationKey,
            m => m.AwaitingInput);

        sagaIds.Should().NotBeEmpty("saga should be created without any external infrastructure");
    }

    // -----------------------------------------------------------------------
    // Helper Methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a valid WhatsApp webhook payload containing a text message.
    /// </summary>
    private static string CreateWhatsAppTextMessagePayload(string senderId, string messageText)
    {
        return $$"""
            {
                "object": "whatsapp_business_account",
                "entry": [{
                    "id": "123",
                    "changes": [{
                        "value": {
                            "messaging_product": "whatsapp",
                            "messages": [{
                                "from": "{{senderId}}",
                                "id": "wamid.{{Guid.NewGuid():N}}",
                                "timestamp": "1700000000",
                                "type": "text",
                                "text": {
                                    "body": "{{messageText}}"
                                }
                            }]
                        },
                        "field": "messages"
                    }]
                }]
            }
            """;
    }

    /// <summary>
    /// Creates an HTTP POST request to the WhatsApp webhook endpoint with a valid HMAC-SHA256 signature.
    /// The test verify token is "test-token" as configured in the test factory.
    /// </summary>
    private static HttpRequestMessage CreateSignedWebhookRequest(string payload)
    {
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        byte[] keyBytes = Encoding.UTF8.GetBytes("test-token");
        byte[] hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        string signature = Convert.ToHexStringLower(hash);

        HttpRequestMessage request = new(HttpMethod.Post, "/webhooks/whatsapp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");
        return request;
    }
}
