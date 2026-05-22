// ---------------------------------------------------------------------------
// RideClub Bot — PersistenceServiceCollectionExtensions (Req 9.2, 9.5)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Persistence;
using LDK.RideClub.Bot.Services;

using Microsoft.EntityFrameworkCore;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Extension methods for registering EF Core persistence services
/// on the <see cref="IServiceCollection"/>.
/// </summary>
internal static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BotDbContext"/> with the SQLite provider using the
    /// connection string from the <see cref="PersistenceOptions"/> configuration section,
    /// and adds the <see cref="DatabaseMigrationHostedService"/> for automatic migration
    /// in Development environments.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBotPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration
            .GetSection(PersistenceOptions.SectionName)
            .GetValue<string>(nameof(PersistenceOptions.SqliteConnectionString))
            ?? string.Empty;

        _ = services.AddDbContext<BotDbContext>(options =>
            options.UseSqlite(connectionString));

        _ = services.AddHostedService<DatabaseMigrationHostedService>();

        return services;
    }
}
