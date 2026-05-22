using System.Text.Json.Serialization;

namespace LDK.RideClub.Bot.Adapters.WhatsApp.DTOs;

/// <summary>
/// Represents a single inbound message from the WhatsApp Business API.
/// </summary>
public sealed class WhatsAppMessage
{
    /// <summary>Gets or sets the sender's phone number.</summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    /// <summary>Gets or sets the unique message identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the Unix timestamp of the message.</summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>Gets or sets the message type (e.g., "text", "image").</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the text body, or null if the message is not a text message.</summary>
    [JsonPropertyName("text")]
    public WhatsAppTextBody? Text { get; set; }
}
