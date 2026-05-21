using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Overlay;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Overlay;

public sealed class OverlayHistoryService<TPayload>(
    IServiceScopeFactory scopeFactory,
    ILogger<OverlayHistoryService<TPayload>> logger,
    OverlayHistoryOptions<TPayload> options) : IOverlayHistoryService<TPayload>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentQueue<TPayload> _cache = new();
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private bool _loaded;
    private int _effectiveCapacity = options.DefaultCapacity;

    public string HubName => options.HubName;

    public int Capacity => _effectiveCapacity;

    public async Task<IReadOnlyList<TPayload>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _cache.ToArray();
    }

    public async Task AddAsync(TPayload payload, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            _cache.Enqueue(payload);
            while (_cache.Count > _effectiveCapacity)
            {
                _cache.TryDequeue(out _);
            }

            await PersistSnapshotAsync(cancellationToken);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            while (_cache.TryDequeue(out _))
            {
            }

            _loaded = true;
            await DeleteSnapshotAsync(cancellationToken);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
            {
                return;
            }

            await LoadFromDbAsync(cancellationToken);
            _loaded = true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task LoadFromDbAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();

            var capacityRow = await context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(setting => setting.Key == options.CapacitySettingKey, cancellationToken);
            if (capacityRow is not null
                && int.TryParse(capacityRow.Value, out var configuredCapacity)
                && configuredCapacity > 0)
            {
                _effectiveCapacity = configuredCapacity;
            }

            var dataRow = await context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(setting => setting.Key == options.DataSettingKey, cancellationToken);
            if (dataRow is null || string.IsNullOrWhiteSpace(dataRow.Value))
            {
                return;
            }

            var snapshot = JsonSerializer.Deserialize<List<TPayload>>(dataRow.Value, JsonOptions);
            if (snapshot is null)
            {
                return;
            }

            foreach (var item in snapshot.TakeLast(_effectiveCapacity))
            {
                _cache.Enqueue(item);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to rehydrate overlay history for hub {HubName}; starting with empty cache.",
                options.HubName);
        }
    }

    private async Task PersistSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = _cache.ToArray();
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            var row = await context.SystemSettings
                .FirstOrDefaultAsync(setting => setting.Key == options.DataSettingKey, cancellationToken);
            if (row is null)
            {
                row = new SystemSettingEntity
                {
                    Key = options.DataSettingKey,
                    Category = "overlay.history",
                };
                context.SystemSettings.Add(row);
            }

            row.Value = json;
            row.Category = "overlay.history";
            row.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to persist overlay history snapshot for hub {HubName}.",
                options.HubName);
        }
    }

    private async Task DeleteSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            var row = await context.SystemSettings
                .FirstOrDefaultAsync(setting => setting.Key == options.DataSettingKey, cancellationToken);
            if (row is null)
            {
                return;
            }

            context.SystemSettings.Remove(row);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to clear overlay history snapshot for hub {HubName}.",
                options.HubName);
        }
    }
}
