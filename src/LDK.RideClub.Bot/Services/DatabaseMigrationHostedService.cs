// ---------------------------------------------------------------------------
// RideClub Bot — DatabaseMigrationHostedService (Req 9.4, 9.6, 9.7, 9.8)
// ---------------------------------------------------------------------------

using LDK.RideClub.Bot.Persistence;

using Microsoft.EntityFrameworkCore;

namespace LDK.RideClub.Bot.Services;

/// <summary>
/// Hosted service that applies pending EF Core migrations on startup
/// when running in the Development environment. In non-development
/// environments this service is a no-op.
/// </summary>
/// <param name="serviceProvider">The application service provider for creating scopes.</param>
/// <param name="hostEnvironment">The hosting environment information.</param>
/// <param name="logger">The logger instance.</param>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed partial class DatabaseMigrationHostedService(
    IServiceProvider serviceProvider,
    IHostEnvironment hostEnvironment,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
#pragma warning restore CA1812
{

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            LogSkippingMigration(logger, hostEnvironment.EnvironmentName);
            return;
        }

        LogApplyingMigrations(logger);

        try
        {
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                BotDbContext dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();

                // Ensure the directory for the SQLite database file exists.
                string? connectionString = dbContext.Database.GetConnectionString();
                EnsureDatabaseDirectoryExists(connectionString);

                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

                LogMigrationsApplied(logger);
            }
        }
        catch (Exception ex)
        {
            LogMigrationFailed(logger, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures the directory for the SQLite database file exists.
    /// Extracts the file path from the connection string and creates the directory if needed.
    /// </summary>
    private void EnsureDatabaseDirectoryExists(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return;
        }

        // Extract "Data Source=" value from the connection string.
        string? dataSource = ExtractDataSource(connectionString);
        if (string.IsNullOrEmpty(dataSource))
        {
            return;
        }

        // Resolve relative paths against the current directory.
        string fullPath = Path.GetFullPath(dataSource);
        string? directory = Path.GetDirectoryName(fullPath);

        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        if (!Directory.Exists(directory))
        {
            try
            {
                _ = Directory.CreateDirectory(directory);
                LogCreatedDirectory(logger, directory);
            }
            catch (Exception ex)
            {
                LogDirectoryCreationFailed(logger, directory, ex);
                throw new InvalidOperationException(
                    $"Cannot create database directory '{directory}'. " +
                    $"Ensure the path is valid and the process has write permissions.",
                    ex);
            }
        }
    }

    /// <summary>
    /// Extracts the Data Source value from a SQLite connection string.
    /// </summary>
    private static string? ExtractDataSource(string connectionString)
    {
        // Parse "Data Source=path" from the connection string.
        foreach (string part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = part.Trim();
            if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["Data Source=".Length..].Trim();
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying pending EF Core migrations in Development environment...")]
    private static partial void LogApplyingMigrations(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "EF Core migrations applied successfully.")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping automatic migration — environment is '{EnvironmentName}'.")]
    private static partial void LogSkippingMigration(ILogger logger, string environmentName);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Database migration failed during startup. The application cannot continue.")]
    private static partial void LogMigrationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created database directory: '{Directory}'.")]
    private static partial void LogCreatedDirectory(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Failed to create database directory: '{Directory}'.")]
    private static partial void LogDirectoryCreationFailed(ILogger logger, string directory, Exception exception);
}
