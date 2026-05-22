using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LDK.RideClub.Bot.Adapters.WhatsApp.DTOs;

/// <summary>
/// Represents the value object within a WhatsApp change notification.
/// Contains the messaging product identifier and optional messages.
/// </summary>
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO requires settable collections for System.Text.Json deserialization.")]
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO requires List<T> for System.Text.Json deserialization.")]
public sealed class WhatsAppValue
{
    /// <summary>Gets or sets the messaging product (e.g., "whatsapp").</summary>
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of messages, or null if no messages are present.</summary>
    [JsonPropertyName("messages")]
    public List<WhatsAppMessage>? Messages { get; set; }
}
