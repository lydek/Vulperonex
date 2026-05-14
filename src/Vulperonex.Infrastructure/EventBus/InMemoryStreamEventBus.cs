using System.Threading.Channels;
using Vulperonex.Application.EventBus;
using Vulperonex.Domain.Events;

namespace Vulperonex.Infrastructure.EventBus;

public sealed class InMemoryStreamEventBus : IStreamEventBus, IAsyncDisposable
{
    public const int DefaultCapacity = 10_000;

    private readonly Channel<IStreamEvent> _channel;
    private readonly TransientDeliveryQueueStore? _overflowStore;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly Task _dispatchTask;
    private readonly object _gate = new();
    private readonly List<Subscription> _subscriptions = [];
    private TaskCompletionSource? _idleCompletion;
    private int _pendingDispatchCount;

    public InMemoryStreamEventBus()
        : this(DefaultCapacity)
    {
    }

    public InMemoryStreamEventBus(int capacity, TransientDeliveryQueueStore? overflowStore = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _overflowStore = overflowStore;
        _channel = Channel.CreateBounded<IStreamEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        _dispatchTask = Task.Run(DispatchAsync);
    }

    public async Task PublishAsync(IStreamEvent streamEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);
        cancellationToken.ThrowIfCancellationRequested();

        MarkPendingDispatch();

        if (_channel.Writer.TryWrite(streamEvent))
        {
            return;
        }

        if (_overflowStore is not null)
        {
            await _overflowStore.EnqueueAsync(streamEvent, cancellationToken);
            MarkDispatchComplete();
            return;
        }

        try
        {
            await _channel.Writer.WriteAsync(streamEvent, cancellationToken);
        }
        catch
        {
            MarkDispatchComplete();
            throw;
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IStreamEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription(
            typeof(TEvent),
            (streamEvent, cancellationToken) => handler((TEvent)streamEvent, cancellationToken));

        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }

        return new SubscriptionHandle(this, subscription);
    }

    public Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        Task waitTask;

        lock (_gate)
        {
            waitTask = _pendingDispatchCount == 0
                ? Task.CompletedTask
                : (_idleCompletion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }

        return cancellationToken.CanBeCanceled
            ? waitTask.WaitAsync(cancellationToken)
            : waitTask;
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _disposeTokenSource.CancelAsync();

        try
        {
            await _dispatchTask;
        }
        catch (OperationCanceledException)
        {
        }

        _disposeTokenSource.Dispose();
    }

    private async Task DispatchAsync()
    {
        await foreach (var streamEvent in _channel.Reader.ReadAllAsync(_disposeTokenSource.Token))
        {
            try
            {
                foreach (var subscription in GetMatchingSubscriptions(streamEvent))
                {
                    try
                    {
                        await subscription.HandleAsync(streamEvent, _disposeTokenSource.Token);
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                MarkDispatchComplete();
            }
        }
    }

    private Subscription[] GetMatchingSubscriptions(IStreamEvent streamEvent)
    {
        lock (_gate)
        {
            return _subscriptions
                .Where(subscription => subscription.EventType.IsAssignableFrom(streamEvent.GetType()))
                .ToArray();
        }
    }

    private void MarkPendingDispatch()
    {
        lock (_gate)
        {
            _pendingDispatchCount++;
        }
    }

    private void MarkDispatchComplete()
    {
        TaskCompletionSource? idleCompletion = null;

        lock (_gate)
        {
            _pendingDispatchCount--;

            if (_pendingDispatchCount == 0)
            {
                idleCompletion = _idleCompletion;
                _idleCompletion = null;
            }
        }

        idleCompletion?.TrySetResult();
    }

    private void Unsubscribe(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed record Subscription(
        Type EventType,
        Func<IStreamEvent, CancellationToken, Task> HandleAsync);

    private sealed class SubscriptionHandle(
        InMemoryStreamEventBus owner,
        Subscription subscription) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            owner.Unsubscribe(subscription);
            _disposed = true;
        }
    }
}
