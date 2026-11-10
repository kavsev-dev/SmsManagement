using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmsManagement.Infrastructure.Persistence;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling (migrations add / script). Commands that need a live database
/// read the connection string from the <c>SMS_DB_CONNECTION</c> environment variable.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string FallbackConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=SmsManagement;Integrated Security=True;TrustServerCertificate=True";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SMS_DB_CONNECTION") ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name))
            .Options;

        return new ApplicationDbContext(options);
    }
}
