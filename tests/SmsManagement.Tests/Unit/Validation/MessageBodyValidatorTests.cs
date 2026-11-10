using SmsManagement.Core.Validation;

namespace SmsManagement.Tests.Unit.Validation;

public sealed class MessageBodyValidatorTests
{
    [Fact]
    public void Max_length_is_the_single_sms_limit() => MessageBodyValidator.MaxLength.Should().Be(160);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Empty_or_whitespace_body_is_invalid(string? body)
    {
        MessageBodyValidator.IsValid(body).Should().BeFalse();
        MessageBodyValidator.GetError(body).Should().Be(MessageBodyValidator.RequiredMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    [InlineData(159)]
    [InlineData(160)]
    public void Body_up_to_160_characters_is_valid(int length)
    {
        var body = new string('a', length);

        MessageBodyValidator.IsValid(body).Should().BeTrue();
        MessageBodyValidator.GetError(body).Should().BeNull();
    }

    [Theory]
    [InlineData(161)]
    [InlineData(500)]
    public void Body_longer_than_160_characters_is_invalid_and_reports_its_length(int length)
    {
        var body = new string('a', length);

        MessageBodyValidator.IsValid(body).Should().BeFalse();
        MessageBodyValidator.GetError(body).Should().Contain("160").And.Contain(length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Hebrew_text_of_160_characters_is_valid() =>
        MessageBodyValidator.IsValid(new string('ש', 160)).Should().BeTrue();

    [Fact]
    public void Length_is_measured_in_utf16_code_units_matching_the_database_column()
    {
        const string emoji = "😀"; // one glyph, two UTF-16 code units

        MessageBodyValidator.IsValid(string.Concat(Enumerable.Repeat(emoji, 80))).Should().BeTrue();  // 160 units
        MessageBodyValidator.IsValid(string.Concat(Enumerable.Repeat(emoji, 81))).Should().BeFalse(); // 162 units
    }
}
