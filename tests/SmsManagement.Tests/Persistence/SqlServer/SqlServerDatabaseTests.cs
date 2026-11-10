using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmsManagement.Core.Entities;
using SmsManagement.Infrastructure.Persistence;
using SmsManagement.Infrastructure.Persistence.Repositories;
using SmsManagement.Infrastructure.Persistence.Seeding;

namespace SmsManagement.Tests.Persistence.SqlServer;

/// <summary>
/// Runs the real startup path (migrations + seeding) against SQL Server and verifies the Hebrew collation and
/// Hebrew round-trips, which SQLite cannot. Each test creates and drops its own database.
/// </summary>
public sealed class SqlServerDatabaseTests : IAsyncLifetime
{
    private readonly string _connectionString;

    public SqlServerDatabaseTests()
    {
        var builder = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionStringVariable) ?? "Server=unused")
        {
            InitialCatalog = $"SmsManagement_Test_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
    }

    [SqlServerFact]
    public async Task Startup_creates_the_database_with_the_hebrew_collation_inherited_by_every_text_column()
    {
        await InitializeDatabaseAsync();
        await using var context = CreateContext();

        var databaseCollation = await context.Database
            .SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation')) AS [Value]")
            .SingleAsync();

        var columnCollations = await context.Database
            .SqlQueryRaw<string>("""
                SELECT c.collation_name AS [Value]
                FROM sys.columns AS c
                INNER JOIN sys.tables AS t ON t.object_id = c.object_id
                WHERE t.name IN (N'Employees', N'EmployeePhones', N'Messages') AND c.collation_name IS NOT NULL
                """)
            .ToListAsync();

        databaseCollation.Should().Be(ApplicationDbContext.HebrewCollation);
        columnCollations.Should().HaveCount(9).And.OnlyContain(c => c == ApplicationDbContext.HebrewCollation);
    }

    [SqlServerFact]
    public async Task Startup_seeds_hebrew_employees_idempotently_and_resolves_them_by_phone()
    {
        await InitializeDatabaseAsync();
        await InitializeDatabaseAsync(); // second startup must be a no-op
        await using var context = CreateContext();

        var israel = await new EmployeeRepository(context).FindByPhoneNumberAsync("+972501234567", CancellationToken.None);
        var shared = await new EmployeeRepository(context).FindByPhoneNumberAsync(EmployeeSeedData.SharedOfficeLine, CancellationToken.None);

        (await context.Employees.CountAsync()).Should().Be(EmployeeSeedData.Employees.Count);
        israel.Should().ContainSingle().Which.Should().Match<Employee>(e =>
            e.FullName == "ישראל ישראלי" && e.Address == "דרך המכבים 68, ראשון לציון");
        shared.Select(e => e.FullName).Should().BeEquivalentTo("דוד לוי", "רחל ביטון");
    }

    [SqlServerFact]
    public async Task Hebrew_message_round_trips_and_list_is_newest_first()
    {
        await InitializeDatabaseAsync();
        var now = DateTimeOffset.UtcNow;
        const string hebrewBody = "שלום ישראל, לוח הזמנים שלך עודכן. נא לאשר הגעה 😀";

        await using (var context = CreateContext())
        {
            var repository = new MessageRepository(context);
            repository.Add(Message.Create("0501234567", "older", "Mock", now.AddMinutes(-1)));
            repository.Add(Message.Create("0501234567", hebrewBody, "Secondary", now));
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = CreateContext();
        var list = await new MessageRepository(readContext).ListAsync(null, CancellationToken.None);

        list.Select(m => m.Body).Should().Equal(hebrewBody, "older");
    }

    [SqlServerFact]
    public async Task Hebrew_natural_key_is_unique()
    {
        await InitializeDatabaseAsync();
        await using var context = CreateContext();
        context.Employees.Add(new Employee("שרה", "כהן", "הרצל 12, רחובות")); // already seeded

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionStringVariable) is null)
        {
            return;
        }

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    private ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connectionString).Options);

    private async Task InitializeDatabaseAsync()
    {
        await using var context = CreateContext();
        var initializer = new DatabaseInitializer(
            context,
            Options.Create(new DatabaseOptions { StartupMaxAttempts = 1 }),
            TimeProvider.System,
            NullLogger<DatabaseInitializer>.Instance);

        await initializer.InitializeAsync(CancellationToken.None);
    }
}
