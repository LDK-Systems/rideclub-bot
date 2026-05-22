// ---------------------------------------------------------------------------
// RideClub Bot — PersistenceOptions (Req 5.1, 9.2)
// ---------------------------------------------------------------------------

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Strongly-typed configuration for the data persistence layer.
/// Bound to the "Persistence" configuration section.
/// </summary>
#pragma warning disable CA1812 // Instantiated via options binding
internal sealed class PersistenceOptions
#pragma warning restore CA1812
{
    /// <summary>
    /// The configuration section name used for binding.
    /// </summary>
    public const string SectionName = "Persistence";

    /// <summary>
    /// Gets or sets the SQLite connection string for the application database.
    /// </summary>
    public required string SqliteConnectionString { get; set; }
}
