using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Settings;

public class SystemSettingsService(VulperonexDbContext context) : ISystemSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        if (row is null)
        {
            row = new SystemSettingEntity { Key = normalizedKey };
            context.SystemSettings.Add(row);
        }

        row.Value = JsonSerializer.Serialize(value, JsonOptions);
        row.Category = category.ToLowerInvariant();
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
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
