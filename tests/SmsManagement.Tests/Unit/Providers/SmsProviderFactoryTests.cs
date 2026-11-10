using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmsManagement.Core.Providers;
using SmsManagement.Infrastructure;
using SmsManagement.Infrastructure.Providers;
using SmsManagement.Infrastructure.Providers.Mock;
using SmsManagement.Infrastructure.Providers.Secondary;
using SmsManagement.Tests.TestDoubles;

namespace SmsManagement.Tests.Unit.Providers;

public sealed class SmsProviderFactoryTests
{
    private static ISmsProvider Provider(string name)
    {
        var provider = Substitute.For<ISmsProvider>();
        provider.ProviderName.Returns(name);
        return provider;
    }

    [Theory]
    [InlineData("Mock", "Mock")]
    [InlineData("mock", "Mock")]
    [InlineData("MOCK", "Mock")]
    [InlineData(" Secondary ", "Secondary")]
    [InlineData("secondary", "Secondary")]
    public void GetProvider_resolves_the_strategy_by_key_case_insensitively(string key, string expected)
    {
        var mock = Provider("Mock");
        var secondary = Provider("Secondary");
        var factory = new SmsProviderFactory([mock, secondary]);

        var resolved = factory.GetProvider(key);

        resolved.Should().BeSameAs(expected == "Mock" ? mock : secondary);
    }

    [Fact]
    public void GetProvider_throws_for_an_unknown_key_and_lists_supported_providers()
    {
        var factory = new SmsProviderFactory([Provider("Mock"), Provider("Secondary")]);

        var act = () => factory.GetProvider("Twilio");

        var exception = act.Should().Throw<UnknownSmsProviderException>().Which;
        exception.ProviderName.Should().Be("Twilio");
        exception.SupportedProviders.Should().Equal("Mock", "Secondary");
    }

    [Fact]
    public void SupportedProviders_preserves_registration_order()
    {
        var factory = new SmsProviderFactory([Provider("Secondary"), Provider("Mock")]);

        factory.SupportedProviders.Should().Equal("Secondary", "Mock");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("Unknown", false)]
    [InlineData("mock", true)]
    public void IsSupported_reports_registered_keys(string? key, bool expected) =>
        new SmsProviderFactory([Provider("Mock")]).IsSupported(key).Should().Be(expected);

    [Fact]
    public void Duplicate_provider_names_are_rejected()
    {
        var act = () => new SmsProviderFactory([Provider("Mock"), Provider("MOCK")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Mock*");
    }

    [Fact]
    public void Container_registration_resolves_concrete_providers_by_key()
    {
        using var services = BuildServices().BuildServiceProvider(validateScopes: true);
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISmsProviderFactory>();

        factory.SupportedProviders.Should().Equal(SmsProviderNames.Mock, SmsProviderNames.Secondary);
        factory.GetProvider("Mock").Should().BeOfType<MockSmsProvider>();
        factory.GetProvider("Secondary").Should().BeOfType<SecondarySmsProvider>();
    }

    [Fact]
    public async Task Container_configures_secondary_client_with_base_url_and_basic_auth()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.Created, """{"sid":"SM1","status":"queued"}""");
        var collection = BuildServices();
        collection.AddHttpClient<SecondarySmsProvider>().ConfigurePrimaryHttpMessageHandler(() => handler);
        using var services = collection.BuildServiceProvider();
        using var scope = services.CreateScope();

        var provider = scope.ServiceProvider.GetRequiredService<ISmsProviderFactory>().GetProvider("Secondary");
        var result = await provider.SendSmsAsync("0501234567", "Hi", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var request = handler.Requests.Single();
        request.RequestUri.Should().Be(new Uri("http://secondary.test/base/2010-04-01/Accounts/AC123/Messages.json"));
        request.Authorization!.Scheme.Should().Be("Basic");
        request.Authorization.Parameter.Should().Be(Convert.ToBase64String("AC123:token-xyz"u8.ToArray()));
    }

    private static ServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmsProviders:Mock:BaseUrl"] = "http://mock.test",
                ["SmsProviders:Secondary:BaseUrl"] = "http://secondary.test/base",
                ["SmsProviders:Secondary:AccountSid"] = "AC123",
                ["SmsProviders:Secondary:AuthToken"] = "token-xyz",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSmsManagementInfrastructure(configuration);
        return services;
    }
}
