using SmsManagement.Core.Entities;

namespace SmsManagement.Core.Repositories;

public interface IMessageRepository
{
    /// <summary>Starts tracking a new message; it is inserted on the next <see cref="SaveChangesAsync"/>.</summary>
    void Add(Message message);

    /// <summary>Returns a read-only (untracked) message, or <see langword="null"/> when it does not exist.</summary>
    Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns read-only messages, optionally filtered by status, newest first (by <c>CreatedAt</c>).</summary>
    Task<IReadOnlyList<Message>> ListAsync(MessageStatus? status, CancellationToken cancellationToken);

    /// <summary>Persists all pending changes to tracked messages.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
