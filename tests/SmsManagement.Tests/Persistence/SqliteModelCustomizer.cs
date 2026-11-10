using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SmsManagement.Tests.Persistence;

/// <summary>
/// Test-only model adjustments for SQLite. Applied with
/// <c>ReplaceService&lt;IModelCustomizer, SqliteModelCustomizer&gt;()</c>, so the production SQL Server model
/// is unaffected:
/// <list type="bullet">
///   <item>The SQL Server database collation (<c>Hebrew_100_CI_AS_SC</c>) does not exist in SQLite and is
///         removed. SQLite stores text as UTF-8, so Hebrew round-trips unchanged.</item>
///   <item>SQLite cannot ORDER BY <see cref="DateTimeOffset"/>; such columns are stored as <see cref="long"/> via
///         <see cref="DateTimeOffsetToBinaryConverter"/>, which preserves chronological ordering.</item>
/// </list>
/// </summary>
internal sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        modelBuilder.Model.SetCollation(null);

        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
            {
                property.SetValueConverter(dateTimeOffsetConverter);
            }
        }
    }
}
