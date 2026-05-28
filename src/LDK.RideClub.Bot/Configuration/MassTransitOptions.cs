// ---------------------------------------------------------------------------
// RideClub Bot — MassTransitOptions (Req 5.1, 5.4)
// ---------------------------------------------------------------------------

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Strongly-typed configuration for the MassTransit messaging infrastructure.
/// Bound to the "MassTransit" configuration section.
/// </summary>
#pragma warning disable CA1812 // Instantiated via options binding
internal sealed class MassTransitOptions
#pragma warning restore CA1812
{
    /// <summary>
    /// The configuration section name used for binding.
    /// </summary>
    public const string SectionName = "MassTransit";

    /// <summary>
    /// Gets or sets the transport type. Supported values: "InMemory", "RabbitMq".
    /// </summary>
    public required string TransportType { get; set; }

    /// <summary>
    /// Gets or sets the concurrency limit for message consumers.
    /// </summary>
    public int ConcurrencyLimit { get; set; } = 10;
}
