namespace SmsManagement.Core.Providers;

/// <summary>Thrown when an SMS provider is requested by a name that is not registered.</summary>
public sealed class UnknownSmsProviderException : Exception
{
    public UnknownSmsProviderException()
        : base("The requested SMS provider is not registered.")
    {
        SupportedProviders = [];
    }

    public UnknownSmsProviderException(string message)
        : base(message)
    {
        SupportedProviders = [];
    }

    public UnknownSmsProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
        SupportedProviders = [];
    }

    public UnknownSmsProviderException(string providerName, IReadOnlyList<string> supportedProviders)
        : base($"SMS provider '{providerName}' is not registered. Supported providers: {string.Join(", ", supportedProviders)}.")
    {
        ProviderName = providerName;
        SupportedProviders = supportedProviders;
    }

    public string? ProviderName { get; }

    public IReadOnlyList<string> SupportedProviders { get; }
}
