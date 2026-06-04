using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Settings;

namespace Vulperonex.Application.Workflows.Chat;

public sealed class InMemoryChatOutbox : IChatOutbox
{
    private const int DefaultDedupTtlHours = 24;
    private readonly Lock _sync = new();
    private readonly List<ChatOutboxItem> _items = [];
    private readonly Dictionary<string, DateTimeOffset> _dedupKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;
    private readonly IServiceScopeFactory? _scopeFactory;

    public InMemoryChatOutbox(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public InMemoryChatOutbox(TimeProvider timeProvider, IServiceScopeFactory scopeFactory)
    {
        _timeProvider = timeProvider;
        _scopeFactory = scopeFactory;
    }

    public async Task<ChatOutboxEnqueueResult> EnqueueAsync(
        string platform,
        string? channel,
        string message,
        string? dedupKey = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(platform))
        {
            throw new ArgumentException("Chat outbox platform must not be empty.", nameof(platform));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Chat outbox message must not be empty.", nameof(message));
        }

        var normalizedDedupKey = NormalizeOptional(dedupKey);
        var now = _timeProvider.GetUtcNow();
        var dedupTtl = await GetDedupTtlAsync(cancellationToken).ConfigureAwait(false);
        var item = new ChatOutboxItem
        {
            Id = Guid.NewGuid(),
            Platform = platform.Trim(),
            Channel = NormalizeOptional(channel),
            Message = message,
            DedupKey = normalizedDedupKey,
            EnqueuedAt = now,
        };

        lock (_sync)
        {
            if (normalizedDedupKey is not null)
            {
                PruneExpiredDedupKeys(now, dedupTtl);
                if (_dedupKeys.ContainsKey(normalizedDedupKey))
                {
                    return new ChatOutboxEnqueueResult(item, IsDuplicate: true);
                }

                _dedupKeys[normalizedDedupKey] = now;
            }

            _items.Add(item);
        }

        return new ChatOutboxEnqueueResult(item);
    }

    public Task<IReadOnlyList<ChatOutboxItem>> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ChatOutboxItem>>(_items.ToArray());
        }
    }

    public Task<IReadOnlyList<ChatOutboxItem>> DequeuePendingAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maxItems <= 0)
        {
            return Task.FromResult<IReadOnlyList<ChatOutboxItem>>([]);
        }

        lock (_sync)
        {
            var pending = _items
                .Where(item => item.Status == ChatOutboxItemStatus.Pending)
                .OrderBy(item => item.EnqueuedAt)
                .Take(maxItems)
                .ToArray();

            foreach (var item in pending)
            {
                UpdateItem(item.Id, item with { Status = ChatOutboxItemStatus.Processing });
            }

            return Task.FromResult<IReadOnlyList<ChatOutboxItem>>(pending);
        }
    }

    public Task<ChatOutboxItem?> TryDequeuePendingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var pending = _items.FirstOrDefault(item =>
                item.Id == id && item.Status == ChatOutboxItemStatus.Pending);
            if (pending is null)
            {
                return Task.FromResult<ChatOutboxItem?>(null);
            }

            var processing = pending with { Status = ChatOutboxItemStatus.Processing };
            UpdateItem(id, processing);
            return Task.FromResult<ChatOutboxItem?>(processing);
        }
    }

    public Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            UpdateStatus(id, ChatOutboxItemStatus.Sent, errorMessage: null);
        }

        return Task.CompletedTask;
    }

    public Task MarkSkippedAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            UpdateStatus(id, ChatOutboxItemStatus.Skipped, reason);
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            UpdateStatus(id, ChatOutboxItemStatus.Failed, errorMessage);
        }

        return Task.CompletedTask;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<TimeSpan> GetDedupTtlAsync(CancellationToken cancellationToken)
    {
        if (_scopeFactory is null)
        {
            return TimeSpan.FromHours(DefaultDedupTtlHours);
        }

        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        var ttlHours = await settings
            .GetAsync(SystemSettingKey.ChatOutboxDedupTtlHours, DefaultDedupTtlHours, cancellationToken)
            .ConfigureAwait(false);
        return TimeSpan.FromHours(Math.Clamp(ttlHours, 1, 24 * 30));
    }

    private void PruneExpiredDedupKeys(DateTimeOffset now, TimeSpan ttl)
    {
        foreach (var pair in _dedupKeys.ToArray())
        {
            if (now - pair.Value >= ttl)
            {
                _dedupKeys.Remove(pair.Key);
            }
        }
    }

    private void UpdateStatus(Guid id, ChatOutboxItemStatus status, string? errorMessage)
    {
        var item = _items.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null)
        {
            return;
        }

        UpdateItem(id, item with { Status = status, ErrorMessage = errorMessage });
    }

    private void UpdateItem(Guid id, ChatOutboxItem item)
    {
        var index = _items.FindIndex(candidate => candidate.Id == id);
        if (index >= 0)
        {
            _items[index] = item;
        }
    }
}
