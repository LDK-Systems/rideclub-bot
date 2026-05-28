// ---------------------------------------------------------------------------
// RideClub Bot — WhatsAppMessagingAdapter Unit Tests (Req 6.5, 6.6)
// ---------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Adapters.WhatsApp;
using LDK.RideClub.Bot.Domain.Events;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;
namespace LDK.RideClub.Bot.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="WhatsAppMessagingAdapter"/> verifying signature verification,
/// verification challenge handling, and DTO deserialization.
/// </summary>
public sealed class WhatsAppAdapterTests
{
    private const string VerifyToken = "test-verify-token";
    private const string AccessToken = "test-access-token";
    private const string PhoneNumberId = "123456789";

    private readonly WhatsAppMessagingAdapter _adapter = new(
        new WhatsAppAdapterOptions(VerifyToken, AccessToken, PhoneNumberId));

    // -------------------------------------------------------------------------
    // VerifyWebhookAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyWebhookAsync_Returns_True_For_Valid_Signature()
    {
        // Arrange
        string body = """{"object":"whatsapp_business_account"}""";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        byte[] hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(VerifyToken), bodyBytes);
        string signature = $"sha256={Convert.ToHexStringLower(hash)}";

        HttpRequest request = CreatePostRequest(body);
        request.Headers.Append("X-Hub-Signature-256", signature);

        // Act
        bool result = await _adapter.VerifyWebhookAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyWebhookAsync_Returns_False_For_Missing_Header()
    {
        // Arrange
        HttpRequest request = CreatePostRequest("""{"object":"whatsapp_business_account"}""");

        // Act
        bool result = await _adapter.VerifyWebhookAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyWebhookAsync_Returns_False_For_Invalid_Signature()
    {
        // Arrange
        HttpRequest request = CreatePostRequest("""{"object":"whatsapp_business_account"}""");
        request.Headers.Append("X-Hub-Signature-256", "sha256=0000000000000000000000000000000000000000000000000000000000000000");

        // Act
        bool result = await _adapter.VerifyWebhookAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // HandleVerificationChallengeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleVerificationChallengeAsync_Returns_Challenge_On_Valid_Request()
    {
        // Arrange
        HttpContext context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(
            $"?hub.mode=subscribe&hub.verify_token={VerifyToken}&hub.challenge=challenge_token_123");

        // Act
        IResult result = await _adapter.HandleVerificationChallengeAsync(context.Request);

        // Assert — write the result to a response and verify the body
        var responseContext = new DefaultHttpContext();
        responseContext.Response.Body = new MemoryStream();
        responseContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        await result.ExecuteAsync(responseContext);

        responseContext.Response.Body.Position = 0;
        string responseBody = await new StreamReader(responseContext.Response.Body).ReadToEndAsync();

        responseBody.Should().Be("challenge_token_123");
        responseContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task HandleVerificationChallengeAsync_Returns_Unauthorized_On_Invalid_Token()
    {
        // Arrange
        HttpContext context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(
            "?hub.mode=subscribe&hub.verify_token=wrong-token&hub.challenge=challenge_token_123");

        // Act
        IResult result = await _adapter.HandleVerificationChallengeAsync(context.Request);

        // Assert
        var responseContext = new DefaultHttpContext();
        responseContext.Response.Body = new MemoryStream();
        responseContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        await result.ExecuteAsync(responseContext);

        responseContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // -------------------------------------------------------------------------
    // DeserialiseEventAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeserialiseEventAsync_Returns_Success_For_Valid_Payload()
    {
        // Arrange
        string payload = """
            {
                "object": "whatsapp_business_account",
                "entry": [{
                    "id": "WHATSAPP_BUSINESS_ACCOUNT_ID",
                    "changes": [{
                        "value": {
                            "messaging_product": "whatsapp",
                            "messages": [{
                                "from": "447700900000",
                                "id": "wamid.abc123",
                                "timestamp": "1700000000",
                                "type": "text",
                                "text": { "body": "Hello, world!" }
                            }]
                        },
                        "field": "messages"
                    }]
                }]
            }
            """;

        HttpRequest request = CreatePostRequest(payload);

        // Act
        MappingResult<InboundEvent> result = await _adapter.DeserialiseEventAsync(request);

        // Assert
        result.Should().BeOfType<MappingSuccess<InboundEvent>>();
        var success = (MappingSuccess<InboundEvent>)result;
        success.Value.EventId.Should().Be("wamid.abc123");
        success.Value.Platform.Should().Be("whatsapp");
        success.Value.SenderId.Should().Be("447700900000");
        success.Value.Payload.Should().BeOfType<TextMessagePayload>()
            .Which.Text.Should().Be("Hello, world!");
    }

    [Fact]
    public async Task DeserialiseEventAsync_Returns_Failure_For_Empty_Messages()
    {
        // Arrange
        string payload = """
            {
                "object": "whatsapp_business_account",
                "entry": [{
                    "id": "WHATSAPP_BUSINESS_ACCOUNT_ID",
                    "changes": [{
                        "value": {
                            "messaging_product": "whatsapp",
                            "messages": null
                        },
                        "field": "messages"
                    }]
                }]
            }
            """;

        HttpRequest request = CreatePostRequest(payload);

        // Act
        MappingResult<InboundEvent> result = await _adapter.DeserialiseEventAsync(request);

        // Assert
        result.Should().BeOfType<MappingFailure<InboundEvent>>()
            .Which.Reason.Should().Contain("No messages");
    }

    [Fact]
    public async Task DeserialiseEventAsync_Returns_Failure_For_Malformed_Json()
    {
        // Arrange
        HttpRequest request = CreatePostRequest("{ not valid json at all }}}");

        // Act
        MappingResult<InboundEvent> result = await _adapter.DeserialiseEventAsync(request);

        // Assert
        result.Should().BeOfType<MappingFailure<InboundEvent>>()
            .Which.Reason.Should().Contain("Failed to deserialize");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static HttpRequest CreatePostRequest(string body)
    {
        var context = new DefaultHttpContext();
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        context.Request.ContentType = "application/json";
        context.Request.Method = HttpMethods.Post;
        return context.Request;
    }
}
