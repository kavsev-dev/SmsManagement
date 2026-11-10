using Microsoft.EntityFrameworkCore;
using SmsManagement.Core.Entities;

namespace SmsManagement.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Database collation, so Hebrew text sorts and compares correctly. Every text column inherits it
    /// (no column-level overrides).
    /// <list type="bullet">
    ///   <item><c>Hebrew_100</c>: newest Hebrew collation version in SQL Server 2022.</item>
    ///   <item><c>CI</c>: case-insensitive (relevant to Latin text mixed with Hebrew).</item>
    ///   <item><c>AS</c>: accent-sensitive, so text with niqqud is distinct from text without it.</item>
    ///   <item><c>SC</c>: supplementary characters (emoji) are handled as single characters.</item>
    /// </list>
    /// Text columns are <c>nvarchar</c> (UTF-16), so Hebrew is stored losslessly; the collation governs
    /// sorting and comparison.
    /// </summary>
    public const string HebrewCollation = "Hebrew_100_CI_AS_SC";

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<EmployeePhone> EmployeePhones => Set<EmployeePhone>();

    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Database-level collation. Applied when EF Core creates the database on startup
        // (CREATE DATABASE ... COLLATE); db/scripts/000_create_database.sql does the same for manual provisioning.
        modelBuilder.UseCollation(HebrewCollation);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
