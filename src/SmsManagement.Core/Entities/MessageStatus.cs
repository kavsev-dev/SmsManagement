namespace SmsManagement.Core.Entities;

/// <summary>Lifecycle state of an SMS message. Persisted as its string name.</summary>
public enum MessageStatus
{
    /// <summary>The message was accepted and persisted but has not yet been confirmed by a provider.</summary>
    Pending = 0,

    /// <summary>The selected provider accepted the message for delivery.</summary>
    Sent = 1,

    /// <summary>The dispatch attempt failed; see <see cref="Message.ErrorMessage"/>.</summary>
    Failed = 2,
}
