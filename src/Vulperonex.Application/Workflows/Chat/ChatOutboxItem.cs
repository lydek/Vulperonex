namespace Vulperonex.Application.Workflows.Chat;

public sealed record ChatOutboxItem
{
    public required Guid Id { get; init; }
    public required string Platform { get; init; }
    public string? Channel { get; init; }
    public required string Message { get; init; }
    public string? DedupKey { get; init; }
    public required DateTimeOffset EnqueuedAt { get; init; }
    public ChatOutboxItemStatus Status { get; init; } = ChatOutboxItemStatus.Pending;
    public string? ErrorMessage { get; init; }
}

public enum ChatOutboxItemStatus
{
    Pending,
    Processing,
    Sent,
    Skipped,
    Failed,
}

public sealed record ChatOutboxEnqueueResult(ChatOutboxItem Item);
