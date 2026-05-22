using System.Text.Json.Serialization;

namespace LDK.RideClub.Bot.Adapters.WhatsApp.DTOs;

/// <summary>
/// Represents a change notification within a WhatsApp webhook entry.
/// </summary>
public sealed class WhatsAppChange
{
    /// <summary>Gets or sets the value object containing the change details.</summary>
    [JsonPropertyName("value")]
    public WhatsAppValue Value { get; set; } = new();

    /// <summary>Gets or sets the field that triggered the change (e.g., "messages").</summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;
}
