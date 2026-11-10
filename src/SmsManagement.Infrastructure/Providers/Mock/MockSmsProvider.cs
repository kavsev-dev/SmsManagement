using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SmsManagement.Core.Providers;

namespace SmsManagement.Infrastructure.Providers.Mock;

/// <summary>
/// Default provider: sends messages to the simulated local mock SMS server
/// (<c>POST {BaseUrl}/api/sms/send</c>). The recipient is forwarded exactly as submitted.
/// </summary>
public sealed class MockSmsProvider(HttpClient httpClient, ILogger<MockSmsProvider> logger) : HttpSmsProviderBase(logger)
{
    internal const string SendPath = "api/sms/send";
    internal const string AcceptedStatus = "accepted";

    public override string ProviderName => SmsProviderNames.Mock;

    protected override async Task<SmsDeliveryResult> SendCoreAsync(string recipient, string body, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PostAsJsonAsync(SendPath, new MockSendSmsRequest(recipient, body), cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await TryReadJsonAsync<MockErrorResponse>(response, cancellationToken).ConfigureAwait(false);
            return HttpError(response.StatusCode, error?.Error);
        }

        var payload = await response.Content
            .ReadFromJsonAsync<MockSendSmsResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrWhiteSpace(payload.MessageId))
        {
            return MalformedResponse();
        }

        return string.Equals(payload.Status, AcceptedStatus, StringComparison.OrdinalIgnoreCase)
            ? SmsDeliveryResult.Success(payload.MessageId)
            : SmsDeliveryResult.Failure($"{ProviderName} provider did not accept the message (status '{payload.Status}').");
    }
}
