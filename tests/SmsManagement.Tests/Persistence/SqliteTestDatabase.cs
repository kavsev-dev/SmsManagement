using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmsManagement.Infrastructure.Persistence;

namespace SmsManagement.Tests.Persistence;

/// <summary>
/// An in-memory SQLite database whose lifetime is bound to an open connection. Provides a real relational
/// engine (constraints, unique indexes, SQL translation) without requiring SQL Server.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase(bool createSchema = true)
    {
        _connection.Open();
        if (createSchema)
        {
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }
    }

    public DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
            .Options;

    /// <summary>Creates a fresh context (empty change tracker) over the same database.</summary>
    public ApplicationDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}
