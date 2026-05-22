using LDK.RideClub.Bot.Domain.Commands;
using LDK.RideClub.Bot.Domain.Events;
using LDK.RideClub.Bot.Domain.Responses;

using Microsoft.AspNetCore.Http;

namespace LDK.RideClub.Bot.Abstractions.Messaging;

/// <summary>
/// Defines the contract for a platform-specific messaging adapter.
/// Each messaging platform (WhatsApp, Telegram, Discord, etc.) implements this interface.
/// </summary>
public interface IMessagingAdapter
{
    /// <summary>
    /// Gets the unique identifier for the messaging platform (e.g., "whatsapp", "telegram", "discord").
    /// Used for routing webhook requests to the correct adapter.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Verifies the webhook signature/token from the platform.
    /// </summary>
    /// <param name="request">The inbound HTTP request to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the request signature is valid; otherwise, false.</returns>
    public Task<bool> VerifyWebhookAsync(HttpRequest request, CancellationToken ct = default);

    /// <summary>
    /// Handles platform verification challenges (e.g., GET requests with challenge tokens).
    /// </summary>
    /// <param name="request">The inbound HTTP request containing the verification challenge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The platform-expected challenge response.</returns>
    public Task<IResult> HandleVerificationChallengeAsync(HttpRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deserialises the inbound platform payload into a domain event.
    /// </summary>
    /// <param name="request">The inbound HTTP request containing the platform payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A mapping result containing the deserialised inbound event or a failure reason.</returns>
    public Task<MappingResult<InboundEvent>> DeserialiseEventAsync(HttpRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends an outbound message to the platform.
    /// </summary>
    /// <param name="message">The outbound message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure with an error reason.</returns>
    public Task<SendResult> SendMessageAsync(OutboundMessage message, CancellationToken ct = default);
}
