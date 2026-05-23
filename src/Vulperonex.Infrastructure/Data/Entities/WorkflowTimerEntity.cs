namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class WorkflowTimerEntity
{
    public string Id { get; set; } = string.Empty;

    public string RuleId { get; set; } = string.Empty;

    public int IntervalSeconds { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset NextFireAt { get; set; }
}
