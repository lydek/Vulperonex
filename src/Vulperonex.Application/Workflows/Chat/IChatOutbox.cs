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
}
