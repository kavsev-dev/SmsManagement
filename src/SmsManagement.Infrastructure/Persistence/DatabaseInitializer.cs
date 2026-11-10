using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmsManagement.Infrastructure.Persistence.Seeding;

namespace SmsManagement.Infrastructure.Persistence;

/// <summary>Brings the database to the current schema and seeds reference data.</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

internal sealed partial class DatabaseInitializer(
    ApplicationDbContext dbContext,
    IOptions<DatabaseOptions> options,
    TimeProvider timeProvider,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    private readonly DatabaseOptions _options = options.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_options.ApplyMigrationsOnStartup)
        {
            await WithStartupRetryAsync(ApplySchemaAsync, cancellationToken).ConfigureAwait(false);
        }

        if (_options.SeedOnStartup)
        {
            var (employees, phones) = await EmployeeSeeder
                .SeedAsync(dbContext, EmployeeSeedData.Employees, cancellationToken)
                .ConfigureAwait(false);
            LogSeeded(employees, phones);
        }
    }

    private async Task ApplySchemaAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlServer())
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            LogApplyingMigrations(pending.Count);

            // Creates the database with the model's collation (Hebrew_100_CI_AS_SC) if it does not exist yet.
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await VerifyCollationAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // The migrations target SQL Server. Other relational providers (e.g. SQLite in tests) get the schema
            // created directly from the model.
            LogEnsureCreated(dbContext.Database.ProviderName);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Text columns inherit the database collation. A database created beforehand with another collation still
    /// stores Hebrew (nvarchar), but sorts and compares it by the wrong rules, so this is reported loudly.
    /// </summary>
    private async Task VerifyCollationAsync(CancellationToken cancellationToken)
    {
        var actual = await dbContext.Database
            .SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation')) AS [Value]")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(actual, ApplicationDbContext.HebrewCollation, StringComparison.OrdinalIgnoreCase))
        {
            LogUnexpectedCollation(actual, ApplicationDbContext.HebrewCollation);
        }
    }

    private async Task WithStartupRetryAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(_options.StartupRetryDelaySeconds);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsTransientStartupFailure(ex) && attempt < _options.StartupMaxAttempts)
            {
                LogDatabaseUnavailable(ex, attempt, _options.StartupMaxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Connection-level failures while the database server is still starting up.
    private static bool IsTransientStartupFailure(Exception ex) =>
        ex is DbException or RetryLimitExceededException or TimeoutException;

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} pending database migration(s)")]
    private partial void LogApplyingMigrations(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database collation is {Actual} but {Expected} is required for correct Hebrew sorting and comparison. Recreate the database with db/scripts/000_create_database.sql, or change its collation with ALTER DATABASE ... COLLATE while it is still empty")]
    private partial void LogUnexpectedCollation(string? actual, string expected);

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating database schema from the model for provider {Provider}")]
    private partial void LogEnsureCreated(string? provider);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed data applied: {Employees} employee(s) and {Phones} phone number(s) added")]
    private partial void LogSeeded(int employees, int phones);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database not available (attempt {Attempt}/{MaxAttempts}); retrying in {DelaySeconds}s")]
    private partial void LogDatabaseUnavailable(Exception exception, int attempt, int maxAttempts, double delaySeconds);
}
