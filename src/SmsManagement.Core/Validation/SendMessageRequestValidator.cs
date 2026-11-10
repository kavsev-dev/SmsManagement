using SmsManagement.Core.Contracts;
using SmsManagement.Core.Providers;

namespace SmsManagement.Core.Validation;

public interface ISendMessageRequestValidator
{
    RequestValidationResult Validate(SendMessageRequest request);
}

/// <summary>
/// Validates <see cref="SendMessageRequest"/>: recipient format, body length, and, when a provider is given,
/// that it is one of the providers registered with the <see cref="ISmsProviderFactory"/>.
/// </summary>
public sealed class SendMessageRequestValidator(ISmsProviderFactory providerFactory) : ISendMessageRequestValidator
{
    public const string ToField = "to";
    public const string BodyField = "body";
    public const string ProviderField = "provider";

    public RequestValidationResult Validate(SendMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = new RequestValidationResult.Builder();

        if (string.IsNullOrWhiteSpace(request.To))
        {
            result.Add(ToField, "Recipient phone number is required.");
        }
        else if (!PhoneNumberValidator.IsValid(request.To))
        {
            result.Add(ToField, PhoneNumberValidator.InvalidFormatMessage);
        }

        if (MessageBodyValidator.GetError(request.Body) is { } bodyError)
        {
            result.Add(BodyField, bodyError);
        }

        // Provider is optional: when it is omitted, the message goes through SendMessageRequest.DefaultProvider.
        if (!string.IsNullOrWhiteSpace(request.Provider) && !providerFactory.IsSupported(request.Provider))
        {
            result.Add(ProviderField, $"Provider '{request.Provider}' is not supported. Supported providers: {SupportedList()}.");
        }

        return result.Build();
    }

    private string SupportedList() => string.Join(", ", providerFactory.SupportedProviders);
}
