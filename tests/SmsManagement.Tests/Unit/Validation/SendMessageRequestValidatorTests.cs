using SmsManagement.Core.Contracts;
using SmsManagement.Core.Providers;
using SmsManagement.Core.Validation;
using SmsManagement.Infrastructure.Providers;

namespace SmsManagement.Tests.Unit.Validation;

public sealed class SendMessageRequestValidatorTests
{
    private readonly SendMessageRequestValidator _validator;

    public SendMessageRequestValidatorTests()
    {
        var mock = Substitute.For<ISmsProvider>();
        mock.ProviderName.Returns(SmsProviderNames.Mock);
        var secondary = Substitute.For<ISmsProvider>();
        secondary.ProviderName.Returns(SmsProviderNames.Secondary);

        _validator = new SendMessageRequestValidator(new SmsProviderFactory([mock, secondary]));
    }

    [Theory]
    [InlineData("0501234567", "Mock")]
    [InlineData("+972501234567", "Secondary")]
    [InlineData("050-123-4567", "mock")] // provider key is case-insensitive
    public void Valid_request_passes(string to, string provider)
    {
        var result = _validator.Validate(new SendMessageRequest(to, "Hello, your schedule has been updated.", provider));

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Empty_request_reports_the_required_fields_only()
    {
        var result = _validator.Validate(new SendMessageRequest(null, null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().BeEquivalentTo("to", "body");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_provider_is_valid_because_it_defaults_to_mock(string? provider)
    {
        var result = _validator.Validate(new SendMessageRequest("0501234567", "Hello", provider));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_phone_number_is_reported_on_the_to_field()
    {
        var result = _validator.Validate(new SendMessageRequest("12345", "Hi", "Mock"));

        result.Errors.Should().ContainKey("to").WhoseValue.Single().Should().Be(PhoneNumberValidator.InvalidFormatMessage);
        result.Errors.Should().NotContainKeys("body", "provider");
    }

    [Fact]
    public void Body_over_160_characters_is_reported_on_the_body_field()
    {
        var result = _validator.Validate(new SendMessageRequest("0501234567", new string('x', 161), "Mock"));

        result.Errors.Should().ContainKey("body").WhoseValue.Single().Should().Contain("160");
    }

    [Fact]
    public void Unknown_provider_is_reported_with_the_supported_list()
    {
        var result = _validator.Validate(new SendMessageRequest("0501234567", "Hi", "Twilio"));

        result.Errors.Should().ContainKey("provider")
            .WhoseValue.Single().Should().Contain("'Twilio'").And.Contain("Mock, Secondary");
    }
}
