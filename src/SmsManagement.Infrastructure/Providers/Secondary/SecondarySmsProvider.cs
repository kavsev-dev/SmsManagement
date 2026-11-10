using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmsManagement.Core.Providers;
using SmsManagement.Core.Validation;

namespace SmsManagement.Infrastructure.Providers.Secondary;

/// <summary>
/// Alternative provider simulating a Twilio-style REST API: form-encoded request, Basic authentication,
/// account-scoped resource path, and a message resource with a lifecycle <c>status</c>.
/// Unlike the mock provider it requires recipients in E.164 form, so national numbers are converted first.
/// </summary>
public sealed class SecondarySmsProvider(
    HttpClient httpClient,
    IOptions<SecondarySmsProviderOptions> options,
    ILogger<SecondarySmsProvider> logger) : HttpSmsProviderBase(logger)
{
    /// <summary>Statuses meaning the provider accepted the message for (or completed) delivery.</summary>
    internal static readonly IReadOnlySet<string> AcceptedStatuses =
        new HashSet<string>(["accepted", "queued", "scheduled", "sending", "sent", "delivered"], StringComparer.OrdinalIgnoreCase);

    private readonly SecondarySmsProviderOptions _options = options.Value;

    public override string ProviderName => SmsProviderNames.Secondary;

    internal string MessagesPath => $"2010-04-01/Accounts/{Uri.EscapeDataString(_options.AccountSid)}/Messages.json";

    protected override async Task<SmsDeliveryResult> SendCoreAsync(string recipient, string body, CancellationToken cancellationToken)
    {
        if (!PhoneNumberValidator.TryNormalizeToE164(recipient, out var e164))
        {
            return SmsDeliveryResult.Failure($"{ProviderName} provider requires an E.164 recipient; '{recipient}' cannot be converted.");
        }

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("To", e164),
            new KeyValuePair<string, string>("From", _options.FromNumber),
            new KeyValuePair<string, string>("Body", body),
        ]);

        using var response = await httpClient.PostAsync(MessagesPath, content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await TryReadJsonAsync<SecondaryErrorResponse>(response, cancellationToken).ConfigureAwait(false);
            var detail = error?.Message is null ? null : error.Code is null ? error.Message : $"{error.Message} (code {error.Code})";
            return HttpError(response.StatusCode, detail);
        }

        var resource = await response.Content
            .ReadFromJsonAsync<SecondaryMessageResource>(cancellationToken)
            .ConfigureAwait(false);

        if (resource is null || string.IsNullOrWhiteSpace(resource.Sid) || string.IsNullOrWhiteSpace(resource.Status))
        {
            return MalformedResponse();
        }

        if (AcceptedStatuses.Contains(resource.Status))
        {
            return SmsDeliveryResult.Success(resource.Sid);
        }

        var reason = resource.ErrorMessage is null
            ? string.Empty
            : resource.ErrorCode is null ? $": {resource.ErrorMessage}" : $": {resource.ErrorMessage} (code {resource.ErrorCode})";
        return SmsDeliveryResult.Failure($"{ProviderName} provider reported status '{resource.Status}'{reason}.");
    }
}
