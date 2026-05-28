using System.Text.Json;
using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Application.Workflows.Dtos;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Workflows;

internal static class WorkflowRuleMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static WorkflowRule ToDomain(WorkflowRuleEntity entity)
    {
        return new WorkflowRule
        {
            Id = entity.Id,
            Name = entity.Name,
            EventTypeKey = entity.EventTypeKey,
            Trigger = DeserializeTrigger(entity.TriggerJson),
            MatchCondition = entity.MatchCondition,
            IsSubWorkflow = entity.IsSubWorkflow,
            Conditions = JsonSerializer.Deserialize<IReadOnlyList<WorkflowCondition>>(entity.ConditionsJson, JsonOptions) ?? [],
            Actions = JsonSerializer.Deserialize<IReadOnlyList<WorkflowAction>>(entity.ActionsJson, JsonOptions) ?? [],
            OnFailureSteps = JsonSerializer.Deserialize<IReadOnlyList<WorkflowAction>>(entity.OnFailureActionsJson, JsonOptions) ?? [],
            IsEnabled = entity.IsEnabled,
            Priority = entity.Priority,
            CreatedAt = entity.CreatedAt,
            ExecutionMode = Enum.TryParse<WorkflowExecutionMode>(entity.ExecutionMode, out var mode) ? mode : WorkflowExecutionMode.Serial,
            MaxParallelism = entity.MaxParallelism,
            Throttle = JsonSerializer.Deserialize<WorkflowThrottlePolicy>(entity.ThrottleJson, JsonOptions) ?? WorkflowThrottlePolicy.None,
            TimeoutSeconds = entity.TimeoutSeconds <= 0 ? 30 : entity.TimeoutSeconds,
            Version = entity.Version,
        };
    }

    public static WorkflowRuleSummaryDto ToSummary(WorkflowRuleEntity entity)
    {
        return new WorkflowRuleSummaryDto(
            entity.Id,
            entity.Name,
            entity.EventTypeKey,
            entity.IsEnabled,
            entity.Priority,
            entity.CreatedAt,
            entity.Version);
    }

    public static WorkflowRuleEntity ToEntity(WorkflowRule rule)
    {
        return new WorkflowRuleEntity
        {
            Id = rule.Id,
            Name = rule.Name,
            EventTypeKey = rule.EventTypeKey,
            TriggerJson = rule.Trigger is not null ? JsonSerializer.Serialize(rule.Trigger, JsonOptions) : null,
            MatchCondition = rule.MatchCondition,
            IsSubWorkflow = rule.IsSubWorkflow,
            ConditionsJson = JsonSerializer.Serialize(rule.Conditions, JsonOptions),
            ActionsJson = JsonSerializer.Serialize(rule.Actions, JsonOptions),
            OnFailureActionsJson = JsonSerializer.Serialize(rule.OnFailureSteps, JsonOptions),
            IsEnabled = rule.IsEnabled,
            Priority = rule.Priority,
            CreatedAt = rule.CreatedAt,
            ExecutionMode = rule.ExecutionMode.ToString(),
            MaxParallelism = rule.MaxParallelism,
            ThrottleJson = JsonSerializer.Serialize(rule.Throttle, JsonOptions),
            TimeoutSeconds = rule.TimeoutSeconds,
            Version = rule.Version,
        };
    }

    public static void CopyToEntity(WorkflowRule rule, WorkflowRuleEntity entity)
    {
        entity.Name = rule.Name;
        entity.EventTypeKey = rule.EventTypeKey;
        entity.TriggerJson = rule.Trigger is not null ? JsonSerializer.Serialize(rule.Trigger, JsonOptions) : null;
        entity.MatchCondition = rule.MatchCondition;
        entity.IsSubWorkflow = rule.IsSubWorkflow;
        entity.ConditionsJson = JsonSerializer.Serialize(rule.Conditions, JsonOptions);
        entity.ActionsJson = JsonSerializer.Serialize(rule.Actions, JsonOptions);
        entity.OnFailureActionsJson = JsonSerializer.Serialize(rule.OnFailureSteps, JsonOptions);
        entity.IsEnabled = rule.IsEnabled;
        entity.Priority = rule.Priority;
        entity.ExecutionMode = rule.ExecutionMode.ToString();
        entity.MaxParallelism = rule.MaxParallelism;
        entity.ThrottleJson = JsonSerializer.Serialize(rule.Throttle, JsonOptions);
        entity.TimeoutSeconds = rule.TimeoutSeconds;
    }

    private static WorkflowTrigger? DeserializeTrigger(string? triggerJson)
    {
        if (string.IsNullOrWhiteSpace(triggerJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<WorkflowTrigger>(triggerJson, JsonOptions);
    }
}
