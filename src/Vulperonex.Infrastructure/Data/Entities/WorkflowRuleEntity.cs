namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class WorkflowRuleEntity
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ConditionsJson { get; set; } = "{}";

    public string ActionsJson { get; set; } = "[]";

    public bool IsEnabled { get; set; } = true;
}
