namespace LDK.RideClub.Bot.Adapters.WhatsApp.Configuration;

/// <summary>
/// Configuration class for WhatsApp adapter settings, including API URL, access token, and phone number ID.
/// </summary>
public class WhatsAppConfiguration
{
    /// <summary>
    /// Gets or sets the WhatsApp API base URL.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration binding requires string type.")]
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the access token for authenticating with the WhatsApp API.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the phone number ID used for sending messages.
    /// </summary>
    public string? PhoneNumberId { get; set; }
}
