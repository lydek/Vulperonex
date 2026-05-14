using Vulperonex.Domain.Events;

namespace Vulperonex.Application.EventBus;

public interface IStreamEventBus
{
    Task PublishAsync(IStreamEvent streamEvent, CancellationToken cancellationToken = default);

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IStreamEvent;

    Task WaitForIdleAsync(CancellationToken cancellationToken = default);
}
