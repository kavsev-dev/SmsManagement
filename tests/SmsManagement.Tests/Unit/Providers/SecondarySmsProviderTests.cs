using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmsManagement.Infrastructure.Providers.Secondary;
using SmsManagement.Tests.TestDoubles;

namespace SmsManagement.Tests.Unit.Providers;

public sealed class SecondarySmsProviderTests
{
    private static SecondarySmsProvider CreateProvider(StubHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://secondary.test/") },
            Options.Create(new SecondarySmsProviderOptions
            {
                AccountSid = "AC123",
                AuthToken = "token",
                FromNumber = "+97239683000",
            }),
            NullLogger<SecondarySmsProvider>.Instance);

    [Fact]
    public void ProviderName_is_Secondary() =>
        CreateProvider(StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}")).ProviderName.Should().Be("Secondary");

    [Fact]
    public async Task Queued_resource_is_a_success_and_request_is_twilio_style_form_post()
    {
        var handler = StubHttpMessageHandler.Json(
            HttpStatusCode.Created,
            """{"sid":"SM0001","status":"queued","error_code":null,"error_message":null}""");

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello there", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().Be("SM0001");

        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.Should().Be(new Uri("http://secondary.test/2010-04-01/Accounts/AC123/Messages.json"));
        request.ContentType.Should().Be("application/x-www-form-urlencoded");

        // National numbers are converted to E.164 for this provider.
        request.Body.Should().Be("To=%2B972501234567&From=%2B97239683000&Body=Hello+there");
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("undelivered")]
    public async Task Terminal_failure_status_is_a_failure_with_provider_error_code(string status)
    {
        var handler = StubHttpMessageHandler.Json(
            HttpStatusCode.Created,
            $$"""{"sid":"SM0002","status":"{{status}}","error_code":30003,"error_message":"Unreachable destination handset"}""");

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain(status).And.Contain("30003").And.Contain("Unreachable destination handset");
    }

    [Fact]
    public async Task Api_error_is_a_failure_including_code_and_message()
    {
        var handler = StubHttpMessageHandler.Json(
            HttpStatusCode.BadRequest,
            """{"code":21211,"message":"The 'To' number is not a valid phone number.","status":400}""");

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("400").And.Contain("21211").And.Contain("not a valid phone number");
    }

    [Fact]
    public async Task Resource_without_sid_is_malformed()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.Created, """{"status":"queued"}""");

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.ErrorMessage.Should().Contain("malformed");
    }

    [Fact]
    public async Task Recipient_that_cannot_be_converted_to_e164_fails_without_calling_the_api()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.Created, """{"sid":"SM1","status":"queued"}""");

        var result = await CreateProvider(handler).SendSmsAsync("12345", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Unreachable_server_is_a_failure_not_an_exception()
    {
        var result = await CreateProvider(StubHttpMessageHandler.Throws(new HttpRequestException("refused")))
            .SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.ErrorMessage.Should().Be("Secondary provider is unreachable.");
    }
}
