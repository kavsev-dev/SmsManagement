using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace SmsManagement.Api.EmbeddedMock;

/// <summary>
/// Runs the simulated external SMS provider (<see cref="MockSmsEndpoints"/>) inside the API process, on its own
/// port (default <c>http://localhost:5000</c>). The providers still call it over real HTTP, exactly as they would
/// call a third-party API, so the whole system starts with a single <c>dotnet run</c>.
/// Does nothing unless <see cref="EmbeddedMockSmsServerOptions.Enabled"/> is set.
/// </summary>
public sealed partial class EmbeddedMockSmsServer(
    IOptions<EmbeddedMockSmsServerOptions> options,
    ILoggerFactory loggerFactory) : IHostedService, IAsyncDisposable
{
    private readonly EmbeddedMockSmsServerOptions _options = options.Value;
    private readonly ILogger _logger = loggerFactory.CreateLogger<EmbeddedMockSmsServer>();
    private WebApplication? _app;

    /// <summary>The address the mock is listening on, or <see langword="null"/> when it is not running.</summary>
    public Uri? Address { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        // An empty builder: no configuration sources, so the API's URLs and settings do not leak into the mock.
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseKestrelCore();
        builder.Services.AddRoutingCore();
        // Log through the API's logging pipeline, so the API's log levels and console output apply.
        builder.Services.Replace(ServiceDescriptor.Singleton(loggerFactory));
        builder.Services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));
        // The API owns the console and Ctrl+C; the mock is stopped by StopAsync.
        builder.Services.Replace(ServiceDescriptor.Singleton<IHostLifetime, NoopHostLifetime>());

        var app = builder.Build();
        app.Urls.Add(_options.Url);
        app.MapMockSmsProvider(_options, loggerFactory.CreateLogger(MockSmsEndpoints.LoggerCategory));

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch (IOException ex)
        {
            await app.DisposeAsync();
            throw new InvalidOperationException(
                $"The embedded mock SMS provider could not listen on {_options.Url}: {ex.Message} " +
                $"Free the port, or change {EmbeddedMockSmsServerOptions.SectionName}:Url together with " +
                "SmsProviders:Mock:BaseUrl and SmsProviders:Secondary:BaseUrl.",
                ex);
        }

        _app = app;
        Address = new Uri(app.Urls.First());
        LogListening(Address);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
            _app = null;
            Address = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Embedded mock SMS provider listening on {Address}")]
    private partial void LogListening(Uri address);

    private sealed class NoopHostLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

public static class EmbeddedMockSmsServerExtensions
{
    /// <summary>
    /// Registers <see cref="EmbeddedMockSmsServer"/> as a hosted service. It starts only when
    /// <c>EmbeddedMockSmsServer:Enabled</c> is <see langword="true"/>.
    /// </summary>
    public static IServiceCollection AddEmbeddedMockSmsServer(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<EmbeddedMockSmsServerOptions>()
            .Bind(configuration.GetSection(EmbeddedMockSmsServerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<EmbeddedMockSmsServer>();
        services.AddHostedService(sp => sp.GetRequiredService<EmbeddedMockSmsServer>());
        return services;
    }
}
