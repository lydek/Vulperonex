using System.Text.Json;
using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;

namespace Vulperonex.Web.Workflows;

public static class WorkflowRuleJsonMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static WorkflowRuleDto ToDto(WorkflowRule rule)
    {
        return new WorkflowRuleDto(
            rule.Id,
            rule.Name,
            rule.EventTypeKey,
            rule.Trigger ?? new WorkflowTrigger(rule.EventTypeKey),
            rule.MatchCondition,
            rule.IsSubWorkflow,
            rule.IsEnabled,
            rule.Priority,
            rule.CreatedAt,
            rule.Conditions.Select(condition => JsonSerializer.SerializeToElement(condition, JsonOptions)).ToArray(),
            rule.Actions.Select(action => JsonSerializer.SerializeToElement(action, JsonOptions)).ToArray(),
            rule.OnFailureSteps.Select(action => JsonSerializer.SerializeToElement(action, JsonOptions)).ToArray(),
            rule.ExecutionMode,
            rule.MaxParallelism,
            rule.Throttle,
            rule.TimeoutSeconds,
            rule.Version);
    }

    public static WorkflowRule ToRule(
        WorkflowRuleUpsertRequest request,
        string id,
        DateTimeOffset? createdAt = null,
        int version = 0)
    {
        return new WorkflowRule
        {
            Id = id,
            Name = request.Name,
            EventTypeKey = request.EventTypeKey,
            Trigger = NormalizeTrigger(request.EventTypeKey, request.Trigger),
            MatchCondition = request.MatchCondition,
            IsSubWorkflow = request.IsSubWorkflow,
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            Conditions = (request.Conditions ?? []).Select(DeserializeCondition).ToArray(),
            Actions = (request.Actions ?? []).Select(DeserializeAction).ToArray(),
            OnFailureSteps = (request.OnFailureSteps ?? []).Select(DeserializeAction).ToArray(),
            ExecutionMode = request.ExecutionMode,
            MaxParallelism = request.MaxParallelism,
            Throttle = request.Throttle ?? WorkflowThrottlePolicy.None,
            TimeoutSeconds = request.TimeoutSeconds,
            Version = version,
        };
    }

    private static WorkflowCondition DeserializeCondition(JsonElement element)
    {
        return element.Deserialize<WorkflowCondition>(JsonOptions)
            ?? throw new JsonException("Condition was null.");
    }

    private static WorkflowAction DeserializeAction(JsonElement element)
    {
        return element.Deserialize<WorkflowAction>(JsonOptions)
            ?? throw new JsonException("Action was null.");
    }

    private static WorkflowTrigger NormalizeTrigger(string eventTypeKey, WorkflowTrigger? trigger)
    {
        return (trigger ?? new WorkflowTrigger(eventTypeKey)) with
        {
            EventTypeKey = eventTypeKey,
        };
    }
}
