// ---------------------------------------------------------------------------
// RideClub Bot — WebhookOptions (Req 5.1, 7.1)
// ---------------------------------------------------------------------------

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Strongly-typed configuration for webhook endpoint routing.
/// Bound to the "Webhooks" configuration section.
/// </summary>
#pragma warning disable CA1812 // Instantiated via options binding
internal sealed class WebhookOptions
#pragma warning restore CA1812
{
    /// <summary>
    /// The configuration section name used for binding.
    /// </summary>
    public const string SectionName = "Webhooks";

    /// <summary>
    /// Gets or sets the base path for all webhook endpoints.
    /// Must start with "/". Defaults to "/webhooks".
    /// </summary>
    public string BasePath { get; set; } = "/webhooks";
}
