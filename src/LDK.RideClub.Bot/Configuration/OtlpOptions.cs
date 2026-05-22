// ---------------------------------------------------------------------------
// RideClub Bot — OtlpOptions (Req 5.1, 11.2)
// ---------------------------------------------------------------------------

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Strongly-typed configuration for the OpenTelemetry OTLP exporter.
/// Bound to the "Otlp" configuration section.
/// </summary>
#pragma warning disable CA1812 // Instantiated via options binding
internal sealed class OtlpOptions
#pragma warning restore CA1812
{
    /// <summary>
    /// The configuration section name used for binding.
    /// </summary>
    public const string SectionName = "Otlp";

    /// <summary>
    /// Gets or sets the OTLP collector endpoint URI.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the OpenTelemetry service resource name.
    /// Defaults to "rideclub-bot".
    /// </summary>
    public string ServiceName { get; set; } = "rideclub-bot";
}
