using SmsManagement.Core.Providers;

namespace SmsManagement.Infrastructure.Providers;

/// <summary>
/// Strategy registry: resolves the <see cref="ISmsProvider"/> selected by name (case-insensitive) from all
/// providers registered in DI. Adding a provider only requires registering another <see cref="ISmsProvider"/>.
/// </summary>
public sealed class SmsProviderFactory : ISmsProviderFactory
{
    private readonly Dictionary<string, ISmsProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <exception cref="InvalidOperationException">Thrown when two providers share a name, or a name is empty.</exception>
    public SmsProviderFactory(IEnumerable<ISmsProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var names = new List<string>();

        foreach (var provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider.ProviderName))
            {
                throw new InvalidOperationException($"SMS provider '{provider.GetType().Name}' has an empty ProviderName.");
            }

            if (!_providers.TryAdd(provider.ProviderName, provider))
            {
                throw new InvalidOperationException($"More than one SMS provider is registered with the name '{provider.ProviderName}'.");
            }

            names.Add(provider.ProviderName);
        }

        SupportedProviders = names.AsReadOnly();
    }

    public IReadOnlyList<string> SupportedProviders { get; }

    public ISmsProvider GetProvider(string providerName)
    {
        ArgumentNullException.ThrowIfNull(providerName);

        return _providers.TryGetValue(providerName.Trim(), out var provider)
            ? provider
            : throw new UnknownSmsProviderException(providerName, SupportedProviders);
    }

    public bool IsSupported(string? providerName) =>
        !string.IsNullOrWhiteSpace(providerName) && _providers.ContainsKey(providerName.Trim());
}
