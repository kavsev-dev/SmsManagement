using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SmsManagement.Infrastructure.Providers.Mock;
using SmsManagement.Tests.TestDoubles;

namespace SmsManagement.Tests.Unit.Providers;

public sealed class MockSmsProviderTests
{
    private static MockSmsProvider CreateProvider(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://mock.test/") }, NullLogger<MockSmsProvider>.Instance);

    [Fact]
    public void ProviderName_is_Mock() =>
        CreateProvider(StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}")).ProviderName.Should().Be("Mock");

    [Fact]
    public async Task Accepted_response_is_a_success_and_request_matches_the_contract()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"messageId":"mock-42","status":"accepted"}""");

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().Be("mock-42");

        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.Should().Be(new Uri("http://mock.test/api/sms/send"));
        request.ContentType.Should().Be("application/json");
        using var json = JsonDocument.Parse(request.Body!);
        json.RootElement.GetProperty("to").GetString().Should().Be("0501234567");
        json.RootElement.GetProperty("body").GetString().Should().Be("Hello");
    }

    [Fact]
    public async Task Error_status_code_is_a_failure_including_the_provider_error()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, """{"error":"carrier offline"}""");

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("503").And.Contain("carrier offline");
    }

    [Fact]
    public async Task Error_status_without_json_body_is_still_a_failure()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>") }));

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.ErrorMessage.Should().Contain("502");
    }

    [Fact]
    public async Task Non_accepted_status_is_a_failure()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"messageId":"mock-1","status":"rejected"}""");

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("rejected");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"status":"accepted"}""")] // missing messageId
    [InlineData("null")]
    public async Task Malformed_success_payload_is_a_failure(string payload)
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, payload);

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("malformed");
    }

    [Fact]
    public async Task Unreachable_server_is_a_failure_not_an_exception()
    {
        var handler = StubHttpMessageHandler.Throws(new HttpRequestException("Connection refused (mock.test:80)"));

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Mock provider is unreachable.");
    }

    [Fact]
    public async Task Timeout_is_a_failure_not_an_exception()
    {
        var handler = StubHttpMessageHandler.Throws(new TaskCanceledException("timeout", new TimeoutException()));

        var result = await CreateProvider(handler).SendSmsAsync("0501234567", "Hello", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timeout");
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = () => CreateProvider(StubHttpMessageHandler.Hangs()).SendSmsAsync("0501234567", "Hello", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
