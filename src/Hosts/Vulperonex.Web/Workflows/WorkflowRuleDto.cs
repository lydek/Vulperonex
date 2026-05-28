using System.Text.Json;
using Vulperonex.Application.Workflows;

namespace Vulperonex.Web.Workflows;

public sealed record WorkflowRuleDto(
    string Id,
    string Name,
    string? EventTypeKey,
    WorkflowTrigger? Trigger,
    string? MatchCondition,
    bool IsSubWorkflow,
    bool IsEnabled,
    int Priority,
    DateTimeOffset CreatedAt,
    IReadOnlyList<JsonElement> Conditions,
    IReadOnlyList<JsonElement> Actions,
    IReadOnlyList<JsonElement> OnFailureSteps,
    WorkflowExecutionMode ExecutionMode,
    int MaxParallelism,
    WorkflowThrottlePolicy Throttle,
    int TimeoutSeconds,
    int Version);

public sealed record WorkflowRuleUpsertRequest(
    string? Id,
    string Name,
    string? EventTypeKey,
    bool IsEnabled,
    int Priority,
    IReadOnlyList<JsonElement>? Conditions,
    IReadOnlyList<JsonElement>? Actions,
    IReadOnlyList<JsonElement>? OnFailureSteps = null,
    WorkflowExecutionMode ExecutionMode = WorkflowExecutionMode.Serial,
    int MaxParallelism = 1,
    WorkflowThrottlePolicy? Throttle = null,
    int TimeoutSeconds = 30,
    WorkflowTrigger? Trigger = null,
    string? MatchCondition = null,
    bool IsSubWorkflow = false);
