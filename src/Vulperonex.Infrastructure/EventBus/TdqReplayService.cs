using Vulperonex.Application.EventBus;

namespace Vulperonex.Infrastructure.EventBus;

public sealed class TdqReplayService(
    TransientDeliveryQueueStore queue,
    IStreamEventBus eventBus)
{
    public async Task ReplayAsync(CancellationToken cancellationToken = default)
    {
        var pendingItems = await queue.GetPendingAsync(cancellationToken);

        foreach (var item in pendingItems)
        {
            var streamEvent = StreamEventJsonSerializer.Deserialize(item.EventType, item.PayloadJson);
            await eventBus.PublishAsync(streamEvent, cancellationToken);
            await eventBus.WaitForIdleAsync(cancellationToken);
            await queue.DeleteAsync(item.Id, cancellationToken);
        }
    }
}
