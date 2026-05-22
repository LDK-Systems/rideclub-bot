// ---------------------------------------------------------------------------
// RideClub Bot — WhatsAppMessagingAdapter (Req 6.3, 6.4, 6.5, 8.1, 8.2, 8.4)
// ---------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LDK.RideClub.Bot.Abstractions.Messaging;
using LDK.RideClub.Bot.Adapters.WhatsApp.DTOs;
using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Responses;

using Microsoft.AspNetCore.Http;

namespace LDK.RideClub.Bot.Adapters.WhatsApp;

/// <summary>
/// Messaging adapter for the WhatsApp Business API.
/// Handles webhook verification, payload deserialization, and outbound message dispatch.
/// </summary>
/// <param name="options">The WhatsApp adapter configuration options.</param>
public sealed class WhatsAppMessagingAdapter(WhatsAppAdapterOptions options) : IMessagingAdapter
{
    private readonly WhatsAppAdapterOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public string PlatformId => "whatsapp";

    /// <inheritdoc />
    /// <remarks>
    /// Verifies the inbound request using HMAC-SHA256 signature validation.
    /// The expected header format is <c>X-Hub-Signature-256: sha256=&lt;hex&gt;</c>.
    /// </remarks>
    public async Task<bool> VerifyWebhookAsync(HttpRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Headers.TryGetValue("X-Hub-Signature-256", out Microsoft.Extensions.Primitives.StringValues signatureHeader))
        {
            return false;
        }

        string? signature = signatureHeader.FirstOrDefault();
        if (string.IsNullOrEmpty(signature) || !signature.StartsWith("sha256=", StringComparison.Ordinal))
        {
            return false;
        }

        string expectedHex = signature["sha256=".Length..];

        request.EnableBuffering();
        request.Body.Position = 0;

        byte[] body;
        using (var ms = new MemoryStream())
        {
            await request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
            body = ms.ToArray();
        }

        request.Body.Position = 0;

        byte[] keyBytes = Encoding.UTF8.GetBytes(_options.VerifyToken);
        byte[] computedHash = HMACSHA256.HashData(keyBytes, body);
        string computedHex = Convert.ToHexStringLower(computedHash);

        return string.Equals(computedHex, expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Handles the WhatsApp webhook verification challenge (GET request).
    /// Returns the <c>hub.challenge</c> token when <c>hub.mode</c> is "subscribe"
    /// and <c>hub.verify_token</c> matches the configured verify token.
    /// </remarks>
    public Task<IResult> HandleVerificationChallengeAsync(HttpRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? mode = request.Query["hub.mode"].FirstOrDefault();
        string? verifyToken = request.Query["hub.verify_token"].FirstOrDefault();
        string? challenge = request.Query["hub.challenge"].FirstOrDefault();

        return string.Equals(mode, "subscribe", StringComparison.Ordinal) &&
            string.Equals(verifyToken, _options.VerifyToken, StringComparison.Ordinal) &&
            challenge is not null
            ? Task.FromResult(Results.Text(challenge))
            : Task.FromResult(Results.Unauthorized());
    }

    /// <inheritdoc />
    /// <remarks>
    /// Deserializes the WhatsApp webhook payload and maps the first message
    /// to an <see cref="InboundEvent"/>. Returns a <see cref="MappingFailure{T}"/>
    /// if the payload is invalid or contains no messages.
    /// </remarks>
    public async Task<MappingResult<InboundEvent>> DeserialiseEventAsync(
        HttpRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            request.EnableBuffering();
            request.Body.Position = 0;

            WhatsAppWebhookPayload? payload = await JsonSerializer.DeserializeAsync<WhatsAppWebhookPayload>(
                request.Body,
                cancellationToken: ct).ConfigureAwait(false);

            request.Body.Position = 0;

            if (payload is null)
            {
                return new MappingFailure<InboundEvent>("Failed to deserialize webhook payload: result was null.");
            }

            WhatsAppMessage? message = payload.Entry
                .FirstOrDefault()?.Changes
                .FirstOrDefault()?.Value.Messages
                ?.FirstOrDefault();

            if (message is null)
            {
                return new MappingFailure<InboundEvent>(
                    "No messages found in webhook payload.");
            }

            var inboundEvent = new InboundEvent
            {
                EventId = message.Id,
                Platform = PlatformId,
                SenderId = message.From,
                ConversationId = message.From,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(message.Timestamp, CultureInfo.InvariantCulture)),
                Payload = new TextMessagePayload { Text = message.Text?.Body ?? "" },
            };

            return new MappingSuccess<InboundEvent>(inboundEvent);
        }
#pragma warning disable CA1031 // Catch general exceptions — mapping failures are returned as MappingFailure
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new MappingFailure<InboundEvent>(
                $"Failed to deserialize WhatsApp webhook payload: {ex.Message}");
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Stub implementation that returns a successful result.
    /// Will be replaced with actual WhatsApp Cloud API integration in a future task.
    /// </remarks>
    public Task<SendResult> SendMessageAsync(OutboundMessage message, CancellationToken ct = default)
    {
        return Task.FromResult(new SendResult
        {
            Success = true,
            PlatformMessageId = "stub",
        });
    }
}
