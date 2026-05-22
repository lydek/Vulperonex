namespace Vulperonex.Application.Workflows.Chat;

public interface IChatOutbox
{
    Task<ChatOutboxEnqueueResult> EnqueueAsync(
        string platform,
        string? channel,
        string message,
        string? dedupKey = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatOutboxItem>> SnapshotAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatOutboxItem>> DequeuePendingAsync(int maxItems, CancellationToken cancellationToken = default);

    Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkSkippedAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default);
}
