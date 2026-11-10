using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SmsManagement.Core.Validation;

/// <summary>
/// Validation and normalization of recipient phone numbers.
/// Accepted formats:
/// <list type="bullet">
///   <item>International E.164: <c>+</c> followed by 7–15 digits, e.g. <c>+14155550123</c>.
///         Numbers with the Israeli country code (<c>+972</c>) must also be valid Israeli national numbers.</item>
///   <item>Israeli national format: mobile/VoIP <c>05X</c>/<c>07X</c> + 7 digits (e.g. <c>0501234567</c>),
///         or landline <c>02</c>, <c>03</c>, <c>04</c>, <c>08</c>, <c>09</c> + 7 digits (e.g. <c>039683400</c>).</item>
/// </list>
/// Spaces and hyphens used as visual separators are ignored (<c>050-123-4567</c> is valid).
/// </summary>
public static partial class PhoneNumberValidator
{
    public const string IsraelCountryCode = "972";

    /// <summary>Upper bound on raw input length, to reject garbage before any pattern matching.</summary>
    public const int MaxRawLength = 32;

    public const string InvalidFormatMessage =
        "Recipient must be a valid E.164 (e.g. +972501234567) or Israeli (e.g. 0501234567) phone number.";

    /// <summary>Returns <see langword="true"/> when <paramref name="phoneNumber"/> is in an accepted format.</summary>
    public static bool IsValid([NotNullWhen(true)] string? phoneNumber) => TryNormalizeToE164(phoneNumber, out _);

    /// <summary>
    /// Removes separators (spaces, hyphens) and surrounding whitespace without changing the number's format,
    /// e.g. <c>" 050-123 4567 "</c> becomes <c>"0501234567"</c>.
    /// </summary>
    public static string Compact(string phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);
        return Separators().Replace(phoneNumber.Trim(), string.Empty);
    }

    /// <summary>Converts a valid phone number to E.164, e.g. <c>0501234567</c> becomes <c>+972501234567</c>.</summary>
    public static bool TryNormalizeToE164([NotNullWhen(true)] string? phoneNumber, [NotNullWhen(true)] out string? e164)
    {
        e164 = null;
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length > MaxRawLength)
        {
            return false;
        }

        var compact = Compact(phoneNumber);

        if (IsraeliNational().IsMatch(compact))
        {
            e164 = $"+{IsraelCountryCode}{compact[1..]}";
            return true;
        }

        if (compact.StartsWith($"+{IsraelCountryCode}", StringComparison.Ordinal))
        {
            // Israeli international numbers carry the national significant number without the trunk prefix "0".
            var significant = compact[(IsraelCountryCode.Length + 1)..];
            if (IsraeliNational().IsMatch($"0{significant}"))
            {
                e164 = compact;
                return true;
            }

            return false;
        }

        if (E164().IsMatch(compact))
        {
            e164 = compact;
            return true;
        }

        return false;
    }

    /// <summary>Converts a phone number to E.164, throwing when it is not valid.</summary>
    /// <exception cref="ArgumentException">Thrown when the number is not in an accepted format.</exception>
    public static string NormalizeToE164(string phoneNumber) =>
        TryNormalizeToE164(phoneNumber, out var e164)
            ? e164
            : throw new ArgumentException(InvalidFormatMessage, nameof(phoneNumber));

    [GeneratedRegex(@"^\+[1-9]\d{6,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164();

    // 0 + (5X|7X + 7 digits: mobile/VoIP) or (2|3|4|8|9 + 7 digits: landline area codes).
    [GeneratedRegex(@"^0(?:[57]\d{8}|[2-489]\d{7})$", RegexOptions.CultureInvariant)]
    private static partial Regex IsraeliNational();

    [GeneratedRegex(@"[\s\-]", RegexOptions.CultureInvariant)]
    private static partial Regex Separators();
}
