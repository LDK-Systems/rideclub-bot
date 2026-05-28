// ---------------------------------------------------------------------------
// RideClub Bot — WhatsAppOptions (Req 5.1, 5.5)
// ---------------------------------------------------------------------------

namespace LDK.RideClub.Bot.Adapters.WhatsApp;

/// <summary>
/// Strongly-typed configuration for the WhatsApp Business API adapter.
/// Bound to the "Adapters:WhatsApp" configuration section.
/// </summary>
public sealed class WhatsAppOptions
{
    /// <summary>
    /// The configuration section name used for binding.
    /// </summary>
    public const string SectionName = "Adapters:WhatsApp";

    /// <summary>
    /// Gets or sets the verification token used for webhook challenge verification.
    /// </summary>
    public required string VerifyToken { get; set; }

    /// <summary>
    /// Gets or sets the access token for authenticating outbound API calls.
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the phone number ID associated with the WhatsApp Business account.
    /// </summary>
    public required string PhoneNumberId { get; set; }
}
