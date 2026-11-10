using SmsManagement.Core.Entities;

namespace SmsManagement.Core.Validation;

/// <summary>
/// Validation of SMS message bodies: required (not empty or whitespace-only) and at most
/// <see cref="MaxLength"/> characters, which is the single-SMS payload limit.
/// </summary>
/// <remarks>
/// Length is measured in UTF-16 code units (<see cref="string.Length"/>), matching both the
/// <c>nvarchar(160)</c> database column and the JavaScript <c>String.length</c> used by the dashboard counter.
/// </remarks>
public static class MessageBodyValidator
{
    public const int MaxLength = Message.BodyMaxLength;

    public const string RequiredMessage = "Body is required and cannot be empty.";

    /// <summary>Returns <see langword="true"/> when the body satisfies all rules.</summary>
    public static bool IsValid(string? body) => GetError(body) is null;

    /// <summary>Returns the validation error for <paramref name="body"/>, or <see langword="null"/> when it is valid.</summary>
    public static string? GetError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return RequiredMessage;
        }

        if (body.Length > MaxLength)
        {
            return $"Body must not exceed {MaxLength} characters (received {body.Length}).";
        }

        return null;
    }
}
