namespace SmsManagement.Core.Providers;

/// <summary>Resolves the <see cref="ISmsProvider"/> strategy selected by the caller.</summary>
public interface ISmsProviderFactory
{
    /// <summary>Names of all registered providers, in registration order.</summary>
    IReadOnlyList<string> SupportedProviders { get; }

    /// <summary>Returns the provider registered under <paramref name="providerName"/> (case-insensitive).</summary>
    /// <exception cref="UnknownSmsProviderException">Thrown when no provider is registered under that name.</exception>
    ISmsProvider GetProvider(string providerName);

    /// <summary>Returns <see langword="true"/> when a provider is registered under <paramref name="providerName"/>.</summary>
    bool IsSupported(string? providerName);
}
