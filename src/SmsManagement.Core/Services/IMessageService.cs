using SmsManagement.Core.Contracts;
using SmsManagement.Core.Entities;

namespace SmsManagement.Core.Services;

public interface IMessageService
{
    /// <summary>
    /// Validates the request, persists a <c>Pending</c> message, dispatches it through the selected provider,
    /// and persists the resulting <c>Sent</c>/<c>Failed</c> status.
    /// </summary>
    Task<SendMessageResult> SendAsync(SendMessageRequest request, CancellationToken cancellationToken);

    Task<MessageResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lists messages, optionally filtered by status, newest first.</summary>
    Task<IReadOnlyList<MessageResponse>> ListAsync(MessageStatus? status, CancellationToken cancellationToken);
}
