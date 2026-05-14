namespace Vulperonex.Application.Workflows.Dtos;

public sealed record WorkflowRuleSummaryDto(
    string Id,
    string Name,
    string EventTypeKey,
    bool IsEnabled,
    int Priority,
    DateTimeOffset CreatedAt);
