using System.Text.Json;
using Vulperonex.Application.Workflows;

namespace Vulperonex.Web.Workflows;

public sealed record WorkflowRuleDto(
    string Id,
    string Name,
    string EventTypeKey,
    bool IsEnabled,
    int Priority,
    DateTimeOffset CreatedAt,
    IReadOnlyList<JsonElement> Conditions,
    IReadOnlyList<JsonElement> Actions,
    WorkflowExecutionMode ExecutionMode,
    int MaxParallelism,
    int Version);

public sealed record WorkflowRuleUpsertRequest(
    string? Id,
    string Name,
    string EventTypeKey,
    bool IsEnabled,
    int Priority,
    IReadOnlyList<JsonElement>? Conditions,
    IReadOnlyList<JsonElement>? Actions,
    WorkflowExecutionMode ExecutionMode = WorkflowExecutionMode.Serial,
    int MaxParallelism = 1);
