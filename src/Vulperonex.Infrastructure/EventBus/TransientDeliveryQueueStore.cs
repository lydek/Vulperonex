using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.EventBus;

public sealed class TransientDeliveryQueueStore(VulperonexDbContext context)
{
    public async Task<TransientDeliveryQueueEntity> EnqueueAsync(
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new TransientDeliveryQueueEntity
        {
            EventType = eventType,
            PayloadJson = payloadJson,
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.TransientDeliveryQueue.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return item;
    }

    public Task<List<TransientDeliveryQueueEntity>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return context.TransientDeliveryQueue
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await context.TransientDeliveryQueue.Where(item => item.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}
