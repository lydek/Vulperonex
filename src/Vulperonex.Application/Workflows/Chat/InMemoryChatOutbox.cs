namespace Vulperonex.Application.Workflows.Chat;

public sealed class InMemoryChatOutbox(TimeProvider timeProvider) : IChatOutbox
{
    private readonly Lock _sync = new();
    private readonly List<ChatOutboxItem> _items = [];

    public Task<ChatOutboxEnqueueResult> EnqueueAsync(
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

        var item = new ChatOutboxItem
        {
            Id = Guid.NewGuid(),
            Platform = platform.Trim(),
            Channel = NormalizeOptional(channel),
            Message = message,
            DedupKey = NormalizeOptional(dedupKey),
            EnqueuedAt = timeProvider.GetUtcNow(),
        };

        lock (_sync)
        {
            _items.Add(item);
        }

        return Task.FromResult(new ChatOutboxEnqueueResult(item));
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
