using System.Diagnostics.CodeAnalysis;

namespace SmsManagement.Core.Providers;

/// <summary>Outcome of a single provider dispatch attempt.</summary>
public sealed record SmsDeliveryResult
{
    private SmsDeliveryResult(bool isSuccess, string? providerMessageId, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ProviderMessageId = providerMessageId;
        ErrorMessage = errorMessage;
    }

    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSuccess { get; }

    /// <summary>Identifier assigned by the provider, when it returns one.</summary>
    public string? ProviderMessageId { get; }

    public string? ErrorMessage { get; }

    public static SmsDeliveryResult Success(string? providerMessageId) => new(true, providerMessageId, null);

    public static SmsDeliveryResult Failure(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new SmsDeliveryResult(false, null, errorMessage);
    }
}
