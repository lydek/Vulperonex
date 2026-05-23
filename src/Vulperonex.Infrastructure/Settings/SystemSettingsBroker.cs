using Vulperonex.Application.Settings;

namespace Vulperonex.Infrastructure.Settings;

/// <summary>
/// Singleton publisher for <see cref="SettingChangedEvent"/>. SystemSettingsService
/// is scoped (it depends on the scoped DbContext) so the previous per-instance
/// observable only delivered events to subscribers that shared the writer's
/// scope. Promoting publication to a host-lifetime broker lets background
/// workers and the Serilog level switch react to settings written from any
/// scope (CLI, Web UI, integration tests, hosted services).
/// </summary>
public sealed class SystemSettingsBroker : IObservable<SettingChangedEvent>
{
    private readonly List<IObserver<SettingChangedEvent>> _observers = [];
    private readonly Lock _lock = new();

    public IDisposable Subscribe(IObserver<SettingChangedEvent> observer)
    {
        lock (_lock)
        {
            _observers.Add(observer);
        }
        return new Subscription(this, observer);
    }

    public void Publish(SettingChangedEvent changedEvent)
    {
        IObserver<SettingChangedEvent>[] snapshot;
        lock (_lock)
        {
            snapshot = _observers.ToArray();
        }

        foreach (var observer in snapshot)
        {
            try
            {
                observer.OnNext(changedEvent);
            }
            catch
            {
                // Subscriber failures must not break the publish loop.
            }
        }
    }

    private void Unsubscribe(IObserver<SettingChangedEvent> observer)
    {
        lock (_lock)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class Subscription(SystemSettingsBroker broker, IObserver<SettingChangedEvent> observer) : IDisposable
    {
        public void Dispose() => broker.Unsubscribe(observer);
    }
}
