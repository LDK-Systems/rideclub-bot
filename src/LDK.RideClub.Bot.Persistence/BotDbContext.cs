using LDK.RideClub.Bot.Persistence.Entities;

using Microsoft.EntityFrameworkCore;

namespace LDK.RideClub.Bot.Persistence;

/// <summary>
/// Entity Framework Core database context for the RideClub Bot application.
/// Provides access to persisted messaging data and applies entity configurations from this assembly.
/// </summary>
/// <param name="options">The options to configure the database context.</param>
public class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the set of message log entries.
    /// </summary>
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}
