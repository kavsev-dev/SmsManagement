using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmsManagement.Core.Providers;
using SmsManagement.Core.Repositories;
using SmsManagement.Infrastructure.Persistence;
using SmsManagement.Infrastructure.Persistence.Repositories;
using SmsManagement.Infrastructure.Providers;
using SmsManagement.Infrastructure.Providers.Mock;
using SmsManagement.Infrastructure.Providers.Secondary;

namespace SmsManagement.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence (EF Core / SQL Server), repositories, the database initializer,
    /// and the SMS provider strategies with their factory.
    /// </summary>
    public static IServiceCollection AddSmsManagementInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);

        AddPersistence(services, configuration);
        AddSmsProviders(services, configuration);

        return services;
    }

    /// <summary>Applies migrations and seed data. Call once at application startup, before serving requests.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{DatabaseOptions.ConnectionStringName}' is not configured " +
                    $"(set ConnectionStrings__{DatabaseOptions.ConnectionStringName}).");
            }

            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name);
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            });
        });

        services.TryAddScoped<IMessageRepository, MessageRepository>();
        services.TryAddScoped<IEmployeeRepository, EmployeeRepository>();
        services.TryAddScoped<IDatabaseInitializer, DatabaseInitializer>();
    }

    private static void AddSmsProviders(IServiceCollection services, IConfiguration configuration)
    {
        AddProviderOptions<MockSmsProviderOptions>(services, configuration, MockSmsProviderOptions.SectionName);
        AddProviderOptions<SecondarySmsProviderOptions>(services, configuration, SecondarySmsProviderOptions.SectionName);

        services.AddHttpClient<MockSmsProvider>((sp, client) =>
            ConfigureClient(client, sp.GetRequiredService<IOptions<MockSmsProviderOptions>>().Value));

        services.AddHttpClient<SecondarySmsProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SecondarySmsProviderOptions>>().Value;
            ConfigureClient(client, options);
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AccountSid}:{options.AuthToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        });

        // Each strategy is exposed as ISmsProvider; the factory receives all of them. Registration order is the
        // order reported by ISmsProviderFactory.SupportedProviders.
        services.AddTransient<ISmsProvider>(sp => sp.GetRequiredService<MockSmsProvider>());
        services.AddTransient<ISmsProvider>(sp => sp.GetRequiredService<SecondarySmsProvider>());

        services.TryAddScoped<ISmsProviderFactory, SmsProviderFactory>();
    }

    private static void AddProviderOptions<TOptions>(IServiceCollection services, IConfiguration configuration, string sectionName)
        where TOptions : HttpSmsProviderOptions =>
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

    private static void ConfigureClient(HttpClient client, HttpSmsProviderOptions options)
    {
        client.BaseAddress = options.GetBaseAddress();
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
