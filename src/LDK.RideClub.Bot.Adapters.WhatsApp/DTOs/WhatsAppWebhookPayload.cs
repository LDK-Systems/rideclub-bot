using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LDK.RideClub.Bot.Adapters.WhatsApp.DTOs;

/// <summary>
/// Represents the top-level webhook payload from the WhatsApp Business API.
/// This is a pure serialization shape with no domain logic.
/// </summary>
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO requires settable collections for System.Text.Json deserialization.")]
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO requires List<T> for System.Text.Json deserialization.")]
public sealed class WhatsAppWebhookPayload
{
    /// <summary>Gets or sets the object type (always "whatsapp_business_account").</summary>
    [JsonPropertyName("object")]
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Maps to the WhatsApp API JSON field 'object'.")]
    public string Object { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of entry objects in the webhook payload.</summary>
    [JsonPropertyName("entry")]
    public List<WhatsAppEntry> Entry { get; set; } = [];
}
