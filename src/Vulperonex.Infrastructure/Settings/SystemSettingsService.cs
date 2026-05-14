using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Settings;

public class SystemSettingsService(VulperonexDbContext context) : ISystemSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SettingsObservable _changes = new();

    public IObservable<SettingChangedEvent> Changes => _changes;

    public async Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var row = await context.SystemSettings.FindAsync([normalizedKey], cancellationToken);
        if (row is null)
        {
            return defaultValue;
        }

        return JsonSerializer.Deserialize<T>(row.Value, JsonOptions) ?? defaultValue;
    }

    public virtual async Task SetAsync<T>(string key, T value, string category, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var row = await context.SystemSettings.FindAsync([normalizedKey], cancellationToken);
        var oldValue = row?.Value;
        if (row is null)
        {
            row = new SystemSettingEntity { Key = normalizedKey };
            context.SystemSettings.Add(row);
        }

        row.Value = JsonSerializer.Serialize(value, JsonOptions);
        row.Category = category.ToLowerInvariant();
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        _changes.Publish(new SettingChangedEvent(normalizedKey, oldValue, row.Value));
    }

    protected static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Setting key must not be empty.", nameof(key));
        }

        return key.Trim().ToLowerInvariant();
    }

    private sealed class SettingsObservable : IObservable<SettingChangedEvent>
    {
        private readonly List<IObserver<SettingChangedEvent>> _observers = [];

        public IDisposable Subscribe(IObserver<SettingChangedEvent> observer)
        {
            _observers.Add(observer);
            return new Subscription(_observers, observer);
        }

        public void Publish(SettingChangedEvent changedEvent)
        {
            foreach (var observer in _observers.ToArray())
            {
                try
                {
                    observer.OnNext(changedEvent);
                }
                catch
                {
                }
            }
        }

        private sealed class Subscription(
            List<IObserver<SettingChangedEvent>> observers,
            IObserver<SettingChangedEvent> observer) : IDisposable
        {
            public void Dispose() => observers.Remove(observer);
        }
    }
}
