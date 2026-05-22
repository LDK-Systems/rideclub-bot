// ---------------------------------------------------------------------------
// RideClub Bot — Webhook Pipeline Integration Tests (Properties 9-13)
// Feature: rideclub-bot-platform, Properties 9-13
// ---------------------------------------------------------------------------

using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using Xunit;

namespace LDK.RideClub.Bot.Tests.Integration;

/// <summary>
/// Integration tests for the webhook HTTP pipeline using the real HTTP pipeline
/// via <see cref="BotWebApplicationFactory"/>.
/// Validates Properties 9-13 from the design document.
/// </summary>
#pragma warning disable CA1707 // Test method names may contain underscores
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
public sealed class WebhookPipelineTests(BotWebApplicationFactory factory) : IClassFixture<BotWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // -----------------------------------------------------------------------
    // Property 9: Adapter Resolution by Platform Identifier
    // Feature: rideclub-bot-platform, Property 9: Adapter Resolution by Platform Identifier
    // **Validates: Requirements 7.2, 8.3**
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Property9_PostToWhatsappEndpoint_RoutesToAdapter_DoesNotReturn404()
    {
        // Arrange — POST to /webhooks/whatsapp with a valid HMAC-SHA256 signature
        string payload = """{"object":"whatsapp_business_account","entry":[{"id":"123","changes":[{"value":{"messaging_product":"whatsapp","messages":[{"from":"447700900000","id":"wamid.abc","timestamp":"1700000000","type":"text","text":{"body":"Hello"}}]},"field":"messages"}]}]}""";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        string signature = ComputeHmacSha256Signature(payloadBytes);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert — should reach the adapter (200 OK for valid payload), NOT 404
        _ = response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "a registered platform should be resolved by the adapter registry");
        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Property9_PostToWhatsappEndpoint_WithValidSignatureButBadPayload_Returns400()
    {
        // Arrange — valid signature but payload that can't be mapped to a domain event
        string payload = """{"object":"whatsapp_business_account","entry":[]}""";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        string signature = ComputeHmacSha256Signature(payloadBytes);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert — should reach the adapter (400 for bad payload), NOT 404
        _ = response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "a registered platform should be resolved by the adapter registry");
        _ = response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------
    // Property 10: Invalid Verification Token Returns 401
    // Feature: rideclub-bot-platform, Property 10: Invalid Verification Token Returns 401
    // **Validates: Requirements 7.3**
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Property10_PostWithoutSignatureHeader_Returns401()
    {
        // Arrange — POST without X-Hub-Signature-256 header
        string payload = """{"object":"whatsapp_business_account","entry":[]}""";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Property10_PostWithInvalidSignature_Returns401()
    {
        // Arrange — POST with an invalid HMAC signature
        string payload = """{"object":"whatsapp_business_account","entry":[]}""";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", "sha256=0000000000000000000000000000000000000000000000000000000000000000");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Property10_PostWithMalformedSignatureHeader_Returns401()
    {
        // Arrange — POST with a malformed signature (missing sha256= prefix)
        string payload = """{"object":"whatsapp_business_account","entry":[]}""";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", "invalid-format");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // Property 11: Adapter Exception Returns 500
    // Feature: rideclub-bot-platform, Property 11: Adapter Exception Returns 500
    // **Validates: Requirements 7.5**
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Property11_AdapterExceptionReturns500_ViaThrowingFactory()
    {
        // Arrange — use a custom factory with a throwing adapter
        ThrowingAdapterWebApplicationFactory throwingFactory = new();
        await using ConfiguredAsyncDisposable __ = throwingFactory.ConfigureAwait(false);
        using HttpClient client = throwingFactory.CreateClient();

        string payload = """{"test":"data"}""";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        string signature = ComputeHmacSha256Signature(payloadBytes);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/throwing");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // -----------------------------------------------------------------------
    // Property 12: Unsupported HTTP Method Returns 405
    // Feature: rideclub-bot-platform, Property 12: Unsupported HTTP Method Returns 405
    // **Validates: Requirements 7.7**
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task Property12_UnsupportedHttpMethod_Returns405(string method)
    {
        // Arrange
        using var request = new HttpRequestMessage(new HttpMethod(method), "/webhooks/whatsapp");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        // The Allow header may be on the response headers or content headers
        IEnumerable<string> allowValues = response.Content.Headers.TryGetValues("Allow", out IEnumerable<string>? contentAllow)
            ? contentAllow
            : response.Headers.TryGetValues("Allow", out IEnumerable<string>? responseAllow)
                ? responseAllow
                : [];

        string allowHeader = string.Join(", ", allowValues);
        _ = allowHeader.Should().Contain("GET");
        _ = allowHeader.Should().Contain("POST");
    }

    // -----------------------------------------------------------------------
    // Property 13: Unmatched Platform Returns 404
    // Feature: rideclub-bot-platform, Property 13: Unmatched Platform Returns 404
    // **Validates: Requirements 7.8, 8.5**
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Property13_PostToUnknownPlatform_Returns404()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/unknown-platform");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Property13_GetToNonexistentPlatform_Returns404()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/webhooks/nonexistent");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Property13_PostToRandomPlatformId_Returns404()
    {
        // Arrange — a platform ID that is definitely not registered
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/does-not-exist-12345");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        _ = response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // Helper Methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Computes an HMAC-SHA256 signature for the given payload using the test verify token.
    /// The test verify token is "test-token" as configured in BotWebApplicationFactory.
    /// </summary>
    private static string ComputeHmacSha256Signature(byte[] payload)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes("test-token");
        byte[] hash = HMACSHA256.HashData(keyBytes, payload);
        return Convert.ToHexStringLower(hash);
    }
}
