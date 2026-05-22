using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Workflows;

namespace Vulperonex.Web.Validation;

public sealed class WorkflowRuleValidator(IStreamEventTypeRegistry eventTypeRegistry)
{
    private const int MaxTemplateLength = 500;
    private const int MaxRegexLength = 512;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public string? Validate(WorkflowRuleUpsertRequest request)
    {
        if (!eventTypeRegistry.IsKnownForWorkflow(request.EventTypeKey))
        {
            return ErrorCodes.UnknownEventTypeKey;
        }

        if (request.ExecutionMode == WorkflowExecutionMode.Parallel
            && (request.MaxParallelism < 1 || request.MaxParallelism > 64))
        {
            return ErrorCodes.InvalidActionConfig;
        }

        if (request.TimeoutSeconds < 0 || request.TimeoutSeconds > 86_400)
        {
            return ErrorCodes.InvalidActionConfig;
        }

        if (HasInvalidThrottle(request.Throttle))
        {
            return ErrorCodes.InvalidActionConfig;
        }

        foreach (var condition in request.Conditions ?? [])
        {
            var error = ValidateCondition(condition);
            if (error is not null)
            {
                return error;
            }
        }

        foreach (var action in request.Actions ?? [])
        {
            var error = ValidateAction(action);
            if (error is not null)
            {
                return error;
            }

            if (IsCircularReference(request.Id, action))
            {
                return ErrorCodes.CircularWorkflowReference;
            }
        }

        return null;
    }

    private static string? ValidateCondition(JsonElement element)
    {
        var type = ReadType(element);
        if (type is null)
        {
            return ErrorCodes.UnknownConditionType;
        }

        if (type is not UserRoleCondition.ConditionType
            and not MessageContentCondition.ConditionType
            and not CooldownCondition.ConditionType)
        {
            return ErrorCodes.UnknownConditionType;
        }

        if (type == MessageContentCondition.ConditionType)
        {
            var condition = element.Deserialize<MessageContentCondition>(JsonOptions);
            if (condition is not null && condition.MatchMode == MessageContentMatchMode.FullRegex)
            {
                if (condition.Pattern.Length > MaxRegexLength)
                {
                    return ErrorCodes.InvalidRegexPattern;
                }

                try
                {
                    _ = new Regex(condition.Pattern);
                }
                catch (ArgumentException)
                {
                    return ErrorCodes.InvalidRegexPattern;
                }
            }
        }

        if (type == CooldownCondition.ConditionType)
        {
            var condition = element.Deserialize<CooldownCondition>(JsonOptions);
            if (condition is not null && (condition.DurationSeconds < 1 || condition.DurationSeconds > 86_400))
            {
                return ErrorCodes.InvalidActionConfig;
            }
        }

        return null;
    }

    private static string? ValidateAction(JsonElement element)
    {
        var type = ReadType(element);
        if (type is null)
        {
            return ErrorCodes.UnknownActionType;
        }

        if (type is not SendChatMessageAction.ActionType
            and not InvokeSubWorkflowAction.ActionType
            and not InvokePluginAction.ActionType)
        {
            return ErrorCodes.UnknownActionType;
        }

        if (type == SendChatMessageAction.ActionType)
        {
            if (!element.TryGetProperty(nameof(SendChatMessageAction.Template), out var template)
                && !element.TryGetProperty("template", out template))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }

            if (template.GetString()?.Length > MaxTemplateLength)
            {
                return ErrorCodes.InvalidActionConfig;
            }
        }

        if (type == InvokeSubWorkflowAction.ActionType)
        {
            if (!element.TryGetProperty(nameof(InvokeSubWorkflowAction.WorkflowId), out var workflowId)
                && !element.TryGetProperty("workflowId", out workflowId))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }

            if (string.IsNullOrWhiteSpace(workflowId.GetString()))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }
        }

        if (HasInvalidExecutionConfig(element))
        {
            return ErrorCodes.InvalidActionConfig;
        }

        return null;
    }

    private static bool HasInvalidExecutionConfig(JsonElement element)
    {
        return IsOutOfRange(element, "timeoutMs", 0, 60_000)
            || IsOutOfRange(element, "maxRetries", 0, 10)
            || IsOutOfRange(element, "backoffMs", 100, 30_000)
            || HasInvalidErrorBehavior(element);
    }

    private static bool IsOutOfRange(JsonElement element, string name, int min, int max)
    {
        return element.TryGetProperty(name, out var value)
            && value.TryGetInt32(out var number)
            && (number < min || number > max);
    }

    private static bool HasInvalidErrorBehavior(JsonElement element)
    {
        return element.TryGetProperty("errorBehavior", out var value)
            && value.ValueKind == JsonValueKind.String
            && !Enum.TryParse<ErrorBehavior>(value.GetString(), ignoreCase: true, out _);
    }

    private static bool HasInvalidThrottle(WorkflowThrottlePolicy? throttle)
    {
        return throttle is not null
            && (throttle.MaxConcurrent < 0
                || throttle.MaxConcurrent > 64
                || throttle.CooldownSeconds < 0
                || throttle.CooldownSeconds > 86_400
                || throttle.PerUserCooldownSeconds < 0
                || throttle.PerUserCooldownSeconds > 86_400);
    }

    private static string? ReadType(JsonElement element)
    {
        return element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
            ? type.GetString()
            : null;
    }

    private static bool IsCircularReference(string? ruleId, JsonElement element)
    {
        if (string.IsNullOrWhiteSpace(ruleId) || ReadType(element) != InvokeSubWorkflowAction.ActionType)
        {
            return false;
        }

        if (!element.TryGetProperty(nameof(InvokeSubWorkflowAction.WorkflowId), out var workflowId)
            && !element.TryGetProperty("workflowId", out workflowId))
        {
            return false;
        }

        return string.Equals(ruleId, workflowId.GetString(), StringComparison.Ordinal);
    }
}
