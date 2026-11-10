namespace SmsManagement.Core.Entities;

/// <summary>
/// An outbound SMS message. Created in <see cref="MessageStatus.Pending"/> and transitioned exactly once
/// to <see cref="MessageStatus.Sent"/> or <see cref="MessageStatus.Failed"/> after the dispatch attempt.
/// </summary>
public sealed class Message
{
    public const int ToMaxLength = 20;
    public const int BodyMaxLength = 160;
    public const int ProviderNameMaxLength = 50;
    public const int StatusMaxLength = 20;

    // Required by EF Core for materialization.
    private Message()
    {
        To = string.Empty;
        Body = string.Empty;
        ProviderName = string.Empty;
    }

    private Message(string to, string body, string providerName, DateTimeOffset createdAt)
    {
        To = to;
        Body = body;
        ProviderName = providerName;
        CreatedAt = createdAt;
        Status = MessageStatus.Pending;
    }

    /// <summary>Database identifier. Generated as a sequential GUID when the entity is first added to the context.</summary>
    public Guid Id { get; private set; }

    public string To { get; private set; }

    public string Body { get; private set; }

    public MessageStatus Status { get; private set; }

    /// <summary>Name of the provider that was selected for (and used in) the dispatch attempt.</summary>
    public string ProviderName { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Creates a new message in the <see cref="MessageStatus.Pending"/> state.</summary>
    /// <exception cref="ArgumentException">Thrown when an argument violates a persistence constraint.</exception>
    public static Message Create(string to, string body, string providerName, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        EnsureMaxLength(to, ToMaxLength, nameof(to));
        EnsureMaxLength(body, BodyMaxLength, nameof(body));
        EnsureMaxLength(providerName, ProviderNameMaxLength, nameof(providerName));

        return new Message(to, body, providerName, createdAt.ToUniversalTime());
    }

    /// <summary>Records that the provider accepted the message.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the message is no longer pending.</exception>
    public void MarkSent(DateTimeOffset sentAt)
    {
        EnsurePending();
        Status = MessageStatus.Sent;
        SentAt = sentAt.ToUniversalTime();
        ErrorMessage = null;
    }

    /// <summary>Records that the dispatch attempt failed.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the message is no longer pending.</exception>
    public void MarkFailed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        EnsurePending();
        Status = MessageStatus.Failed;
        ErrorMessage = errorMessage;
        SentAt = null;
    }

    private void EnsurePending()
    {
        if (Status != MessageStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Message '{Id}' is already in status '{Status}'; only pending messages can change status.");
        }
    }

    private static void EnsureMaxLength(string value, int maxLength, string paramName)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"Value must not exceed {maxLength} characters.", paramName);
        }
    }
}
