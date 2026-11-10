using SmsManagement.Core.Providers;

namespace SmsManagement.Core.Contracts;

/// <summary>Request body of <c>POST /messages</c>.</summary>
/// <param name="To">Recipient in E.164 (<c>+972501234567</c>) or Israeli (<c>0501234567</c>) format.</param>
/// <param name="Body">Message text, 1–160 characters.</param>
/// <param name="Provider">
/// Optional provider key: <c>Mock</c> or <c>Secondary</c> (case-insensitive). When omitted, <c>Mock</c> is used.
/// </param>
/// <remarks>
/// Properties are nullable on purpose: missing fields are reported by <c>SendMessageRequestValidator</c>
/// as field-level validation errors instead of failing model binding.
/// </remarks>
public sealed record SendMessageRequest(string? To, string? Body, string? Provider = null)
{
    /// <summary>Provider used when the request does not name one.</summary>
    public const string DefaultProvider = SmsProviderNames.Mock;
}
