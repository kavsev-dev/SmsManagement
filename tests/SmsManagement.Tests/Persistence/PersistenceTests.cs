using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmsManagement.Core.Entities;
using SmsManagement.Infrastructure.Persistence;
using SmsManagement.Infrastructure.Persistence.Repositories;
using SmsManagement.Infrastructure.Persistence.Seeding;

namespace SmsManagement.Tests.Persistence;

/// <summary>Seeder, repositories and initializer against a real relational engine (SQLite in-memory).</summary>
public sealed class PersistenceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 19, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task Seeder_inserts_all_employees_and_phones()
    {
        await using var context = _database.CreateContext();

        var (employees, phones) = await EmployeeSeeder.SeedAsync(context, EmployeeSeedData.Employees, CancellationToken.None);

        employees.Should().Be(EmployeeSeedData.Employees.Count);
        phones.Should().Be(EmployeeSeedData.Employees.Sum(e => e.PhoneNumbers.Count));
        (await context.Employees.CountAsync()).Should().Be(employees);
        (await context.EmployeePhones.CountAsync()).Should().Be(phones);
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        await using (var first = _database.CreateContext())
        {
            await EmployeeSeeder.SeedAsync(first, EmployeeSeedData.Employees, CancellationToken.None);
        }

        await using var second = _database.CreateContext();
        var (employees, phones) = await EmployeeSeeder.SeedAsync(second, EmployeeSeedData.Employees, CancellationToken.None);

        employees.Should().Be(0);
        phones.Should().Be(0);
    }

    [Fact]
    public async Task Seeder_adds_missing_phones_to_existing_employees()
    {
        await using (var context = _database.CreateContext())
        {
            var existing = new Employee("ישראל", "ישראלי", "דרך המכבים 68, ראשון לציון");
            existing.AddPhone("+972501234567");
            context.Employees.Add(existing);
            await context.SaveChangesAsync();
        }

        await using var seedContext = _database.CreateContext();
        var (_, phones) = await EmployeeSeeder.SeedAsync(
            seedContext,
            [new EmployeeSeed("ישראל", "ישראלי", "דרך המכבים 68, ראשון לציון", ["+972501234567", "+972521234567"])],
            CancellationToken.None);

        phones.Should().Be(1);
        (await seedContext.EmployeePhones.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Composite_natural_key_rejects_duplicate_employees()
    {
        await using var context = _database.CreateContext();
        context.Employees.Add(new Employee("שרה", "כהן", "הרצל 12, רחובות"));
        context.Employees.Add(new Employee("שרה", "כהן", "הרצל 12, רחובות"));

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Employee_lookup_by_phone_returns_every_owner_of_a_shared_number()
    {
        await SeedAsync();
        await using var context = _database.CreateContext();
        var repository = new EmployeeRepository(context);

        var shared = await repository.FindByPhoneNumberAsync(EmployeeSeedData.SharedOfficeLine, CancellationToken.None);
        var israel = await repository.FindByPhoneNumberAsync("+972501234567", CancellationToken.None);
        var unknown = await repository.FindByPhoneNumberAsync("+14155550123", CancellationToken.None);

        shared.Select(e => e.FullName).Should().BeEquivalentTo("דוד לוי", "רחל ביטון");
        israel.Should().ContainSingle().Which.Address.Should().Be("דרך המכבים 68, ראשון לציון");
        unknown.Should().BeEmpty();
    }

    [Fact]
    public async Task Employee_names_lookup_for_several_numbers_returns_names_per_known_number()
    {
        await SeedAsync();
        await using var context = _database.CreateContext();
        var repository = new EmployeeRepository(context);

        var names = await repository.FindNamesByPhoneNumbersAsync(
            [EmployeeSeedData.SharedOfficeLine, "+972501234567", "+14155550123"], CancellationToken.None);

        names.Keys.Should().BeEquivalentTo(EmployeeSeedData.SharedOfficeLine, "+972501234567");
        names[EmployeeSeedData.SharedOfficeLine].Should().Equal("רחל ביטון", "דוד לוי"); // by last name: ביטון < לוי
        names["+972501234567"].Should().Equal("ישראל ישראלי");
    }

    [Fact]
    public async Task Message_round_trips_with_generated_id_and_string_status()
    {
        var message = Message.Create("0501234567", "Hello", "Mock", Now);
        await using (var context = _database.CreateContext())
        {
            var repository = new MessageRepository(context);
            repository.Add(message);
            await repository.SaveChangesAsync(CancellationToken.None);
            message.MarkSent(Now.AddSeconds(2));
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        message.Id.Should().NotBeEmpty();
        await using var readContext = _database.CreateContext();
        var loaded = await new MessageRepository(readContext).GetByIdAsync(message.Id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(MessageStatus.Sent);
        loaded.SentAt.Should().Be(Now.AddSeconds(2));
        (await readContext.Database.SqlQueryRaw<string>("SELECT Status AS Value FROM Messages").SingleAsync()).Should().Be("Sent");
    }

    [Fact]
    public async Task ListAsync_filters_by_status_and_orders_newest_first()
    {
        var older = Message.Create("0501234567", "older", "Mock", Now);
        var newest = Message.Create("0501234567", "newest", "Mock", Now.AddMinutes(2));
        var failed = Message.Create("0501234567", "failed", "Secondary", Now.AddMinutes(1));
        older.MarkSent(Now);
        newest.MarkSent(Now.AddMinutes(2));
        failed.MarkFailed("boom");
        await using (var context = _database.CreateContext())
        {
            context.Messages.AddRange(older, newest, failed);
            await context.SaveChangesAsync();
        }

        await using var readContext = _database.CreateContext();
        var repository = new MessageRepository(readContext);

        (await repository.ListAsync(null, CancellationToken.None)).Select(m => m.Body).Should().Equal("newest", "failed", "older");
        (await repository.ListAsync(MessageStatus.Sent, CancellationToken.None)).Select(m => m.Body).Should().Equal("newest", "older");
        (await repository.ListAsync(MessageStatus.Pending, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Hebrew_text_round_trips_unchanged()
    {
        const string body = "שלום ישראל, לוח הזמנים שלך עודכן. נא לאשר הגעה 😀";
        var message = Message.Create("0501234567", body, "Mock", Now);
        await using (var context = _database.CreateContext())
        {
            context.Messages.Add(message);
            context.Employees.Add(new Employee("נועה", "שפירא", "הדקל 9, גילת"));
            await context.SaveChangesAsync();
        }

        await using var readContext = _database.CreateContext();

        (await readContext.Messages.SingleAsync()).Body.Should().Be(body);
        (await readContext.Employees.SingleAsync(e => e.LastName == "שפירא")).Address.Should().Be("הדקל 9, גילת");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_unknown_id()
    {
        await using var context = _database.CreateContext();

        (await new MessageRepository(context).GetByIdAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Initializer_creates_schema_and_seeds_on_a_blank_database()
    {
        using var blank = new SqliteTestDatabase(createSchema: false);
        await using var context = blank.CreateContext();
        var initializer = new DatabaseInitializer(
            context,
            Options.Create(new DatabaseOptions()),
            TimeProvider.System,
            NullLogger<DatabaseInitializer>.Instance);

        await initializer.InitializeAsync(CancellationToken.None);
        await initializer.InitializeAsync(CancellationToken.None); // second startup is a no-op

        (await context.Employees.CountAsync()).Should().Be(EmployeeSeedData.Employees.Count);
    }

    public void Dispose() => _database.Dispose();

    private async Task SeedAsync()
    {
        await using var context = _database.CreateContext();
        await EmployeeSeeder.SeedAsync(context, EmployeeSeedData.Employees, CancellationToken.None);
    }
}
