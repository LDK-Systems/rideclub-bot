using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LDK.RideClub.Bot.Adapters.WhatsApp.DTOs;

/// <summary>
/// Represents a single entry in the WhatsApp webhook payload.
/// Each entry corresponds to a WhatsApp Business Account.
/// </summary>
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO requires settable collections for System.Text.Json deserialization.")]
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO requires List<T> for System.Text.Json deserialization.")]
public sealed class WhatsAppEntry
{
    /// <summary>Gets or sets the WhatsApp Business Account ID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of changes within this entry.</summary>
    [JsonPropertyName("changes")]
    public List<WhatsAppChange> Changes { get; set; } = [];
}
