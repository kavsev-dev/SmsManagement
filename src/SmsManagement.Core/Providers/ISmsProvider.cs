using System.Diagnostics.CodeAnalysis;

namespace SmsManagement.Core.Providers;

/// <summary>
/// Strategy contract for an SMS delivery provider.
/// Implementations must not throw for delivery failures (unreachable endpoint, rejected message, malformed response);
/// they report them through <see cref="SmsDeliveryResult.Failure"/>. Only cancellation is propagated as an exception.
/// </summary>
public interface ISmsProvider
{
    /// <summary>Unique, case-insensitive key used to select this provider, e.g. <c>Mock</c>.</summary>
    string ProviderName { get; }

    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Signature is mandated by the specification; 'To' is only a keyword in Visual Basic.")]
    Task<SmsDeliveryResult> SendSmsAsync(string to, string body, CancellationToken cancellationToken);
}
