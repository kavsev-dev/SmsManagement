using System.ComponentModel.DataAnnotations;

namespace SmsManagement.Infrastructure.Persistence;

/// <summary>Startup database behaviour. Bound from the <c>Database</c> configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Name of the connection string (under <c>ConnectionStrings</c>) used for the SMS database.</summary>
    public const string ConnectionStringName = "SmsDatabase";

    /// <summary>Apply pending EF Core migrations on startup.</summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;

    /// <summary>Seed mock employees on startup (idempotent).</summary>
    public bool SeedOnStartup { get; set; } = true;

    /// <summary>
    /// How many times to retry the initial connection/migration. A SQL Server instance that is still
    /// starting (for example LocalDB on first use) can take several seconds to accept connections.
    /// </summary>
    [Range(1, 100)]
    public int StartupMaxAttempts { get; set; } = 12;

    [Range(1, 60)]
    public int StartupRetryDelaySeconds { get; set; } = 5;
}
