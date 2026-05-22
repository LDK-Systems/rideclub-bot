// ---------------------------------------------------------------------------
// RideClub Bot — WhatsAppAdapterOptions
// ---------------------------------------------------------------------------

namespace LDK.RideClub.Bot.Adapters.WhatsApp;

/// <summary>
/// Configuration options for the WhatsApp messaging adapter.
/// Mapped from the host's WhatsAppOptions at registration time to avoid circular project references.
/// </summary>
/// <param name="VerifyToken">The verification token used for webhook challenge and HMAC signature verification.</param>
/// <param name="AccessToken">The access token for authenticating outbound API calls.</param>
/// <param name="PhoneNumberId">The phone number ID associated with the WhatsApp Business account.</param>
public sealed record WhatsAppAdapterOptions(
    string VerifyToken,
    string AccessToken,
    string PhoneNumberId);
