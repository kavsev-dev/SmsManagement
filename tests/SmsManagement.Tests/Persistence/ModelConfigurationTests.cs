using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SmsManagement.Core.Entities;
using SmsManagement.Infrastructure.Persistence;

namespace SmsManagement.Tests.Persistence;

/// <summary>Verifies the SQL Server model against the specification. No database connection is opened.</summary>
public sealed class ModelConfigurationTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=model-only;Database=SmsManagement;Integrated Security=True")
            .Options);

    private IModel Model => _context.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void Migrations_are_in_sync_with_the_model() =>
        _context.Database.HasPendingModelChanges().Should().BeFalse(
            "every model change must be captured in a migration under /db/migrations");

    [Fact]
    public void Employee_has_a_unique_composite_natural_key_index()
    {
        var index = Model.FindEntityType(typeof(Employee))!.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_Employees_FirstName_LastName_Address");

        index.IsUnique.Should().BeTrue();
        index.Properties.Select(p => p.Name).Should().Equal("FirstName", "LastName", "Address");
    }

    [Fact]
    public void Phone_numbers_are_indexed_but_not_globally_unique()
    {
        var indexes = Model.FindEntityType(typeof(EmployeePhone))!.GetIndexes().ToList();

        indexes.Single(i => i.Properties.Count == 1 && i.Properties[0].Name == "PhoneNumber").IsUnique.Should().BeFalse();
        indexes.Single(i => i.Properties.Count == 2).IsUnique.Should().BeTrue("an employee cannot list the same number twice");
    }

    [Theory]
    [InlineData(typeof(Employee), "FirstName", "nvarchar(100)", false)]
    [InlineData(typeof(Employee), "LastName", "nvarchar(100)", false)]
    [InlineData(typeof(Employee), "Address", "nvarchar(255)", false)]
    [InlineData(typeof(EmployeePhone), "PhoneNumber", "nvarchar(20)", false)]
    [InlineData(typeof(Message), "Id", "uniqueidentifier", false)]
    [InlineData(typeof(Message), "To", "nvarchar(20)", false)]
    [InlineData(typeof(Message), "Body", "nvarchar(160)", false)]
    [InlineData(typeof(Message), "Status", "nvarchar(20)", false)]
    [InlineData(typeof(Message), "ProviderName", "nvarchar(50)", false)]
    [InlineData(typeof(Message), "ErrorMessage", "nvarchar(max)", true)]
    [InlineData(typeof(Message), "CreatedAt", "datetimeoffset", false)]
    [InlineData(typeof(Message), "SentAt", "datetimeoffset", true)]
    public void Columns_match_the_specified_schema(Type entity, string property, string columnType, bool nullable)
    {
        var column = Model.FindEntityType(entity)!.FindProperty(property)!;

        column.GetColumnType().Should().Be(columnType);
        column.IsNullable.Should().Be(nullable);
    }

    [Fact]
    public void Database_uses_the_hebrew_collation() =>
        Model.GetCollation().Should().Be(ApplicationDbContext.HebrewCollation);

    [Fact]
    public void Text_columns_are_unicode_and_inherit_the_database_collation()
    {
        var textColumns = Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(string) || p.GetColumnType()!.StartsWith("nvarchar", StringComparison.Ordinal))
            .ToList();

        textColumns.Should().HaveCount(9);
        textColumns.Should().OnlyContain(p => p.GetColumnType()!.StartsWith("nvarchar", StringComparison.Ordinal),
            "Hebrew must be stored as Unicode");
        textColumns.Should().OnlyContain(p => p.GetCollation() == null,
            "the collation is defined once, at database level");
    }

    [Fact]
    public void Message_status_is_stored_as_its_name()
    {
        var status = Model.FindEntityType(typeof(Message))!.FindProperty(nameof(Message.Status))!;

        status.GetTypeMapping().Converter!.ConvertToProvider(MessageStatus.Sent).Should().Be("Sent");
    }

    public void Dispose() => _context.Dispose();
}
