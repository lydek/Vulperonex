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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
