using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmsManagement.Api.EmbeddedMock;
using SmsManagement.Infrastructure.Providers.Mock;
using SmsManagement.Infrastructure.Providers.Secondary;

namespace SmsManagement.Tests.Integration;

/// <summary>
/// Runs the real providers against the embedded mock SMS provider over real HTTP, on a free local port.
/// This checks the wire contract between the providers and the mock that `dotnet run` uses.
/// </summary>
public sealed class EmbeddedMockSmsServerTests : IAsyncLifetime
{
    private const string AccountSid = "AC123";
    private const string Token = "embedded-mock-test-token";

    private readonly EmbeddedMockSmsServer _server = CreateServer(enabled: true, url: "http://127.0.0.1:0");

    public Task InitializeAsync() => _server.StartAsync(CancellationToken.None);

    public async Task DisposeAsync()
    {
        await _server.StopAsync(CancellationToken.None);
        await _server.DisposeAsync();
    }

    [Theory]
    [InlineData("0501234567")]
    [InlineData("0509999999")]
    public async Task Mock_provider_send_succeeds(string recipient)
    {
        var result = await CreateMockProvider().SendSmsAsync(recipient, "שלום", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().StartWith("mock-");
    }

    [Fact]
    public async Task Mock_provider_send_to_number_ending_in_0000_fails_with_503()
    {
        var result = await CreateMockProvider().SendSmsAsync("0500000000", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("503");
    }

    [Fact]
    public async Task Secondary_provider_send_succeeds_with_the_expected_token()
    {
        var result = await CreateSecondaryProvider(Token).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().StartWith("SM");
    }

    [Theory]
    [InlineData("0500000000", "21211")]
    [InlineData("0509999999", "30003")]
    public async Task Secondary_provider_send_to_failure_test_numbers_fails(string recipient, string expectedCode)
    {
        var result = await CreateSecondaryProvider(Token).SendSmsAsync(recipient, "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain(expectedCode);
    }

    [Fact]
    public async Task Secondary_provider_send_with_a_wrong_token_is_rejected_with_401()
    {
        var result = await CreateSecondaryProvider("wrong-token").SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("401");
    }

    [Fact]
    public async Task Disabled_server_does_not_listen()
    {
        await using var server = CreateServer(enabled: false, url: "http://127.0.0.1:0");

        await server.StartAsync(CancellationToken.None);

        server.Address.Should().BeNull();
    }

    [Fact]
    public async Task Port_already_in_use_fails_with_a_clear_message()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var server = CreateServer(enabled: true, url: $"http://127.0.0.1:{port}");

        var start = () => server.StartAsync(CancellationToken.None);

        (await start.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*could not listen on http://127.0.0.1:{port}*EmbeddedMockSmsServer:Url*");
    }

    [Fact]
    public void Api_host_does_not_start_the_mock_outside_development()
    {
        using var factory = new SmsApiFactory();

        factory.Services.GetRequiredService<EmbeddedMockSmsServer>().Address.Should().BeNull();
    }

    private static EmbeddedMockSmsServer CreateServer(bool enabled, string url) =>
        new(
            Options.Create(new EmbeddedMockSmsServerOptions
            {
                Enabled = enabled,
                Url = url,
                SecondaryAuthToken = Token,
                LatencyMilliseconds = 0,
            }),
            NullLoggerFactory.Instance);

    private MockSmsProvider CreateMockProvider() =>
        new(new HttpClient { BaseAddress = _server.Address }, NullLogger<MockSmsProvider>.Instance);

    private SecondarySmsProvider CreateSecondaryProvider(string token)
    {
        var client = new HttpClient { BaseAddress = _server.Address };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{AccountSid}:{token}")));

        return new SecondarySmsProvider(
            client,
            Options.Create(new SecondarySmsProviderOptions { AccountSid = AccountSid, AuthToken = token }),
            NullLogger<SecondarySmsProvider>.Instance);
    }
}
