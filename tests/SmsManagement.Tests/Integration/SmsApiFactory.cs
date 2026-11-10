using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmsManagement.Infrastructure.Persistence;
using SmsManagement.Infrastructure.Providers.Mock;
using SmsManagement.Infrastructure.Providers.Secondary;
using SmsManagement.Tests.Persistence;
using SmsManagement.Tests.TestDoubles;

namespace SmsManagement.Tests.Integration;

/// <summary>
/// Hosts the real API in memory. Everything runs for real (controllers, validation, service, provider strategies,
/// typed HttpClients, EF Core, startup migration/seeding, static dashboard, OpenAPI) except two boundaries:
/// SQL Server is replaced by in-memory SQLite, and the providers' network handler by <see cref="Gateway"/>.
/// </summary>
public sealed class SmsApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SmsApiFactory()
    {
        _connection.Open();
        Gateway = new StubHttpMessageHandler(SimulateProvidersAsync);
    }

    /// <summary>Records every outbound provider request.</summary>
    internal StubHttpMessageHandler Gateway { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SmsDatabase"] = "unused-replaced-by-sqlite",
            ["SmsProviders:Mock:BaseUrl"] = "http://mock-provider.test",
            ["SmsProviders:Secondary:BaseUrl"] = "http://secondary-provider.test",
            ["SmsProviders:Secondary:AuthToken"] = "integration-test-token",
        }));

        builder.ConfigureServices(services =>
        {
            // Replace the SQL Server registration (EF Core 9+ stores it as IDbContextOptionsConfiguration<T>).
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options
                .UseSqlite(_connection)
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>());

            services.AddHttpClient<MockSmsProvider>().ConfigurePrimaryHttpMessageHandler(() => Gateway);
            services.AddHttpClient<SecondarySmsProvider>().ConfigurePrimaryHttpMessageHandler(() => Gateway);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            Gateway.Dispose();
        }
    }

    /// <summary>
    /// Same failure rules as the embedded mock (Api/EmbeddedMock/MockSmsEndpoints.cs): recipients ending in 0000
    /// fail. The embedded mock itself stays off here, because the Testing environment does not enable it.
    /// </summary>
    private static async Task<HttpResponseMessage> SimulateProvidersAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
        var path = request.RequestUri!.AbsolutePath;

        if (path == "/api/sms/send")
        {
            return body.Contains("0000\"", StringComparison.Ordinal)
                ? Json(HttpStatusCode.ServiceUnavailable, """{"error":"Simulated carrier outage."}""")
                : Json(HttpStatusCode.OK, $$"""{"messageId":"mock-{{Guid.NewGuid():N}}","status":"accepted"}""");
        }

        if (path.EndsWith("/Messages.json", StringComparison.Ordinal))
        {
            return body.Contains("0000&", StringComparison.Ordinal)
                ? Json(HttpStatusCode.BadRequest, """{"code":21211,"message":"The 'To' number is not a valid phone number.","status":400}""")
                : Json(HttpStatusCode.Created, $$"""{"sid":"SM{{Guid.NewGuid():N}}","status":"queued"}""");
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
