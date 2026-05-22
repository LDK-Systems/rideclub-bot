// ---------------------------------------------------------------------------
// RideClub Bot — PersistenceOptionsValidator (Req 5.2, 9.2)
// ---------------------------------------------------------------------------

using FluentValidation;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// FluentValidation validator for <see cref="PersistenceOptions"/>.
/// Ensures the SQLite connection string is provided.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class PersistenceOptionsValidator : AbstractValidator<PersistenceOptions>
#pragma warning restore CA1812
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersistenceOptionsValidator"/> class.
    /// </summary>
    public PersistenceOptionsValidator()
    {
        _ = RuleFor(x => x.SqliteConnectionString).NotEmpty();
    }
}
