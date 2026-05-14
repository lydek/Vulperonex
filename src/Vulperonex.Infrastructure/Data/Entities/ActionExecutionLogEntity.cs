namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class ActionExecutionLogEntity
{
    public string DedupKey { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
