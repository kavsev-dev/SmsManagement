using SmsManagement.Core.Validation;

namespace SmsManagement.Tests.Unit.Validation;

public sealed class PhoneNumberValidatorTests
{
    [Theory]
    [InlineData("+972501234567")]   // Israeli mobile, E.164
    [InlineData("0501234567")]      // Israeli mobile, national
    [InlineData("050-123-4567")]    // separators are ignored
    [InlineData(" 050 123 4567 ")]
    [InlineData("+972-50-123-4567")]
    [InlineData("0771234567")]      // Israeli VoIP (07X)
    [InlineData("039683400")]       // Israeli landline, national (9 digits)
    [InlineData("+97239683400")]    // Israeli landline, E.164
    [InlineData("+14155550123")]    // US
    [InlineData("+447911123456")]   // UK
    [InlineData("+1234567")]        // shortest E.164 accepted: 7 digits
    [InlineData("+123456789012345")] // longest E.164: 15 digits
    public void IsValid_returns_true_for_supported_formats(string phoneNumber) =>
        PhoneNumberValidator.IsValid(phoneNumber).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("501234567")]         // missing trunk prefix 0
    [InlineData("05012345678")]       // mobile with 11 digits
    [InlineData("050123456")]         // mobile with 9 digits
    [InlineData("0101234567")]        // 01X is not an Israeli subscriber prefix
    [InlineData("0601234567")]        // 06X is not an Israeli subscriber prefix
    [InlineData("+9720501234567")]    // trunk 0 must be dropped after the country code
    [InlineData("+97250123456")]      // Israeli mobile too short
    [InlineData("972501234567")]      // international without '+'
    [InlineData("+0123456789")]       // E.164 country codes never start with 0
    [InlineData("+123456")]           // too short for E.164
    [InlineData("+1234567890123456")] // 16 digits: too long for E.164
    [InlineData("05O1234567")]        // letter O instead of zero
    [InlineData("(050)1234567")]      // parentheses are not accepted separators
    [InlineData("+972501234567;DROP")]
    [InlineData("050123456705012345670501234567890")] // exceeds the raw length guard
    public void IsValid_returns_false_for_invalid_formats(string? phoneNumber) =>
        PhoneNumberValidator.IsValid(phoneNumber).Should().BeFalse();

    [Theory]
    [InlineData("0501234567", "+972501234567")]
    [InlineData("050-123-4567", "+972501234567")]
    [InlineData("+972501234567", "+972501234567")]
    [InlineData("039683400", "+97239683400")]
    [InlineData("+14155550123", "+14155550123")]
    public void TryNormalizeToE164_converts_to_international_form(string input, string expected)
    {
        PhoneNumberValidator.TryNormalizeToE164(input, out var e164).Should().BeTrue();
        e164.Should().Be(expected);
    }

    [Fact]
    public void TryNormalizeToE164_returns_false_and_null_for_invalid_input()
    {
        PhoneNumberValidator.TryNormalizeToE164("12345", out var e164).Should().BeFalse();
        e164.Should().BeNull();
    }

    [Fact]
    public void NormalizeToE164_throws_for_invalid_input() =>
        FluentActions.Invoking(() => PhoneNumberValidator.NormalizeToE164("not-a-number"))
            .Should().Throw<ArgumentException>();

    [Theory]
    [InlineData(" 050-123 4567 ", "0501234567")]
    [InlineData("+972 50 123 4567", "+972501234567")]
    [InlineData("0501234567", "0501234567")]
    public void Compact_removes_separators_without_changing_format(string input, string expected) =>
        PhoneNumberValidator.Compact(input).Should().Be(expected);
}
