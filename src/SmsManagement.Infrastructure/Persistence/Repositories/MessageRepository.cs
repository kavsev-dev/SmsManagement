using Microsoft.EntityFrameworkCore;
using SmsManagement.Core.Entities;
using SmsManagement.Core.Repositories;

namespace SmsManagement.Infrastructure.Persistence.Repositories;

internal sealed class MessageRepository(ApplicationDbContext dbContext) : IMessageRepository
{
    public void Add(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        dbContext.Messages.Add(message);
    }

    public Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Message>> ListAsync(MessageStatus? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Messages.AsNoTracking();

        if (status is { } filter)
        {
            query = query.Where(m => m.Status == filter);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
