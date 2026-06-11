using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Settings;

public class SystemSettingsService : ISystemSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly VulperonexDbContext context;
    private readonly SystemSettingsBroker _broker;

    public SystemSettingsService(VulperonexDbContext context, SystemSettingsBroker? broker = null)
    {
        this.context = context;
        _broker = broker ?? new SystemSettingsBroker();
    }

    public IObservable<SettingChangedEvent> Changes => _broker;

    public async Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var row = await context.SystemSettings.FindAsync([normalizedKey], cancellationToken);
        if (row is null)
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(row.Value, JsonOptions) ?? defaultValue;
        }
        catch (JsonException) when (StartsWithQuote(row.Value))
        {
            // Older HTTP writes persisted every value as a JSON string (e.g. "\"true\"").
            // Unwrap the string once and retry so typed readers keep working on legacy
            // rows. Malformed payloads still throw so callers can surface corruption.
            var unwrapped = JsonSerializer.Deserialize<string>(row.Value, JsonOptions);
            if (unwrapped is null)
            {
                return defaultValue;
            }

            return JsonSerializer.Deserialize<T>(unwrapped, JsonOptions) ?? defaultValue;
        }
    }

    private static bool StartsWithQuote(string value)
    {
        return value.AsSpan().TrimStart().StartsWith("\"", StringComparison.Ordinal);
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
        _broker.Publish(new SettingChangedEvent(normalizedKey, oldValue, row.Value));
    }

    public virtual async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var row = await context.SystemSettings.FindAsync([normalizedKey], cancellationToken);
        if (row is null)
        {
            return;
        }

        var oldValue = row.Value;
        context.SystemSettings.Remove(row);
        await context.SaveChangesAsync(cancellationToken);
        _broker.Publish(new SettingChangedEvent(normalizedKey, oldValue, null));
    }

    protected static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Setting key must not be empty.", nameof(key));
        }

        return key.Trim().ToLowerInvariant();
    }

}
