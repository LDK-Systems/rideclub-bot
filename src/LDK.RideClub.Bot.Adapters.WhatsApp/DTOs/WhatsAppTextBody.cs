using System.Text.Json.Serialization;

namespace LDK.RideClub.Bot.Adapters.WhatsApp.DTOs;

/// <summary>
/// Represents the text body of a WhatsApp message.
/// </summary>
public sealed class WhatsAppTextBody
{
    /// <summary>Gets or sets the text content of the message.</summary>
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}
