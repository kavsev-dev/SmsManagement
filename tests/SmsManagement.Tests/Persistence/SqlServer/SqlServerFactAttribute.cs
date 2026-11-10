namespace SmsManagement.Tests.Persistence.SqlServer;

/// <summary>
/// A fact that runs only when <see cref="ConnectionStringVariable"/> holds a SQL Server connection string
/// (the database name in it is ignored; each test uses its own throwaway database). Skipped otherwise, so the
/// default test run needs no SQL Server.
/// </summary>
/// <example>
/// <c>SMS_SQLSERVER_TEST_CONNECTION="Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True"</c>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "SMS_SQLSERVER_TEST_CONNECTION";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Set {ConnectionStringVariable} to a SQL Server connection string to run SQL Server tests.";
        }
    }
}
