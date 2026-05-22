// ---------------------------------------------------------------------------
// RideClub Bot — DeploymentOptions (Req 5.1, 13.1)
// ---------------------------------------------------------------------------

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Strongly-typed configuration for the deployment mode selection.
/// Bound to the "Deployment" configuration section.
/// </summary>
#pragma warning disable CA1812 // Instantiated via options binding
internal sealed class DeploymentOptions
#pragma warning restore CA1812
{
    /// <summary>
    /// The configuration section name used for binding.
    /// </summary>
    public const string SectionName = "Deployment";

    /// <summary>
    /// Gets or sets the deployment mode. Must be "Kestrel" or "Lambda" (case-insensitive).
    /// </summary>
    public required string Mode { get; set; }
}
