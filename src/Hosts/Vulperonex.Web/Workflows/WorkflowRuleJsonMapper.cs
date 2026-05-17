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
            rule.IsEnabled,
            rule.Priority,
            rule.CreatedAt,
            rule.Conditions.Select(condition => JsonSerializer.SerializeToElement(condition, JsonOptions)).ToArray(),
            rule.Actions.Select(action => JsonSerializer.SerializeToElement(action, JsonOptions)).ToArray(),
            rule.ExecutionMode,
            rule.MaxParallelism);
    }

    public static WorkflowRule ToRule(WorkflowRuleUpsertRequest request, string id, DateTimeOffset? createdAt = null)
    {
        return new WorkflowRule
        {
            Id = id,
            Name = request.Name,
            EventTypeKey = request.EventTypeKey,
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            Conditions = (request.Conditions ?? []).Select(DeserializeCondition).ToArray(),
            Actions = (request.Actions ?? []).Select(DeserializeAction).ToArray(),
            ExecutionMode = request.ExecutionMode,
            MaxParallelism = request.MaxParallelism,
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
}
