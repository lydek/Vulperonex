using Vulperonex.Domain.Events;

namespace Vulperonex.Application.EventBus;

public interface IStreamEventBus
{
    Task PublishAsync(IStreamEvent streamEvent, CancellationToken cancellationToken = default);

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IStreamEvent;

    /// <summary>
    /// Hot Rx projection of every event dispatched after subscription. Lets
    /// consumers compose pipelines (Throttle / OfType / Merge / Timeout)
    /// without rebuilding the channel-backed publish / overflow plumbing
    /// the bus already owns. Events emitted before <see cref="Subscribe"/>
    /// are not replayed.
    /// </summary>
    IObservable<IStreamEvent> Events { get; }

    Task WaitForIdleAsync(CancellationToken cancellationToken = default);
}
