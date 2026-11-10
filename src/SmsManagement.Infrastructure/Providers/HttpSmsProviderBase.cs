using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmsManagement.Core.Providers;

namespace SmsManagement.Infrastructure.Providers;

/// <summary>
/// Template for HTTP-based providers. Converts transport-level faults (unreachable host, timeout, malformed
/// payload) into <see cref="SmsDeliveryResult.Failure"/> results; only caller cancellation propagates.
/// Error messages returned to API clients are deliberately generic; technical details go to the log.
/// </summary>
public abstract partial class HttpSmsProviderBase(ILogger logger) : ISmsProvider
{
    private const int MaxErrorDetailLength = 300;

    public abstract string ProviderName { get; }

    public async Task<SmsDeliveryResult> SendSmsAsync(string to, string body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        try
        {
            return await SendCoreAsync(to, body, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            LogTimeout(logger, ex, ProviderName);
            return SmsDeliveryResult.Failure($"{ProviderName} provider did not respond within the configured timeout.");
        }
        catch (HttpRequestException ex)
        {
            LogUnreachable(logger, ex, ProviderName);
            return SmsDeliveryResult.Failure($"{ProviderName} provider is unreachable.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            LogMalformedResponse(logger, ex, ProviderName);
            return MalformedResponse();
        }
    }

    /// <summary>Performs the provider-specific request. May throw transport exceptions; they are handled by the caller.</summary>
    protected abstract Task<SmsDeliveryResult> SendCoreAsync(string recipient, string body, CancellationToken cancellationToken);

    protected SmsDeliveryResult MalformedResponse() =>
        SmsDeliveryResult.Failure($"{ProviderName} provider returned a malformed response.");

    protected SmsDeliveryResult HttpError(HttpStatusCode statusCode, string? detail)
    {
        var message = $"{ProviderName} provider rejected the request with HTTP {(int)statusCode} ({statusCode})";
        return SmsDeliveryResult.Failure(string.IsNullOrWhiteSpace(detail) ? $"{message}." : $"{message}: {Truncate(detail)}");
    }

    /// <summary>Reads a JSON error payload if there is one; returns <see langword="default"/> for empty or non-JSON bodies.</summary>
    protected static async Task<T?> TryReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return default;
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxErrorDetailLength ? value : string.Concat(value.AsSpan(0, MaxErrorDetailLength), "…");

    [LoggerMessage(Level = LogLevel.Warning, Message = "SMS provider {Provider} timed out")]
    private static partial void LogTimeout(ILogger logger, Exception exception, string provider);

    [LoggerMessage(Level = LogLevel.Error, Message = "SMS provider {Provider} is unreachable")]
    private static partial void LogUnreachable(ILogger logger, Exception exception, string provider);

    [LoggerMessage(Level = LogLevel.Error, Message = "SMS provider {Provider} returned a malformed response")]
    private static partial void LogMalformedResponse(ILogger logger, Exception exception, string provider);
}
