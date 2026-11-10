using System.Diagnostics.CodeAnalysis;
using SmsManagement.Core.Contracts;

namespace SmsManagement.Core.Services;

/// <summary>
/// Outcome of <see cref="IMessageService.SendAsync"/>: either the created message (whose status is
/// <c>Sent</c> or <c>Failed</c>), or validation errors when the request was rejected before anything was persisted.
/// </summary>
public sealed class SendMessageResult
{
    private SendMessageResult(MessageResponse? message, IReadOnlyDictionary<string, string[]> validationErrors)
    {
        Message = message;
        ValidationErrors = validationErrors;
    }

    [MemberNotNullWhen(true, nameof(Message))]
    public bool IsCreated => Message is not null;

    public MessageResponse? Message { get; }

    /// <summary>Field-level validation errors keyed by JSON field name. Empty when <see cref="IsCreated"/>.</summary>
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static SendMessageResult Created(MessageResponse message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new SendMessageResult(message, new Dictionary<string, string[]>());
    }

    public static SendMessageResult Invalid(IReadOnlyDictionary<string, string[]> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (validationErrors.Count == 0)
        {
            throw new ArgumentException("At least one validation error is required.", nameof(validationErrors));
        }

        return new SendMessageResult(null, validationErrors);
    }
}
