using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Application.Workflows.Metadata;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Workflows;

namespace Vulperonex.Web.Validation;

public sealed class WorkflowRuleValidator(
    IStreamEventTypeRegistry eventTypeRegistry,
    ITriggerMetadataProvider triggerMetadataProvider)
{
    private const int MaxTemplateLength = 500;
    private const int MaxRegexLength = 512;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public string? Validate(WorkflowRuleUpsertRequest request)
    {
        if (request.IsSubWorkflow)
        {
            if (!string.IsNullOrWhiteSpace(request.EventTypeKey) || request.Trigger is not null)
            {
                return ErrorCodes.SubWorkflowMustNotHaveTrigger;
            }
        }
        else
        {
            // Null / whitespace EventTypeKey: short-circuit before the registry
            // lookup. ConcurrentDictionary.TryGetValue throws ArgumentNullException
            // on a null key, which would surface as a 500 from the endpoint
            // instead of the intended 400 + UnknownEventTypeKey contract.
            if (string.IsNullOrWhiteSpace(request.EventTypeKey))
            {
                return ErrorCodes.UnknownEventTypeKey;
            }

            if (!eventTypeRegistry.IsKnownForWorkflow(request.EventTypeKey))
            {
                return ErrorCodes.UnknownEventTypeKey;
            }

            if (request.Trigger?.Filter is not null)
            {
                var allowedFields = triggerMetadataProvider.GetFilterFieldsFor(request.EventTypeKey)
                    .Select(f => f.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var key in request.Trigger.Filter.Keys)
                {
                    if (!allowedFields.Contains(key))
                    {
                        return ErrorCodes.InvalidFilterKey;
                    }
                }
            }
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

        foreach (var action in request.OnFailureSteps ?? [])
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
            if (type is not DelayAction.ActionType
                and not StopIfAction.ActionType
                and not RandomPickerAction.ActionType
                and not UpdateCounterAction.ActionType
                and not TriggerCheckInAction.ActionType
                and not AddLotteryTicketsAction.ActionType
                and not EmitSystemEventAction.ActionType
                and not TriggerEffectAction.ActionType
                and not EmitOverlayWidgetAction.ActionType
                and not LookupPlatformUserAction.ActionType
                and not ShoutoutAction.ActionType
                and not RefundRewardRedemptionAction.ActionType)
            {
                return ErrorCodes.UnknownActionType;
            }
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

        if (type == DelayAction.ActionType)
        {
            var delayAction = element.Deserialize<DelayAction>(JsonOptions);
            if (delayAction is null || delayAction.DelayMs < 100 || delayAction.DelayMs > 30_000)
            {
                return ErrorCodes.InvalidActionConfig;
            }
        }

        if (type == StopIfAction.ActionType)
        {
            var stopIfAction = element.Deserialize<StopIfAction>(JsonOptions);
            if (stopIfAction is null || string.IsNullOrWhiteSpace(stopIfAction.Condition))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }
        }

        if (type == RandomPickerAction.ActionType)
        {
            var randomPickerAction = element.Deserialize<RandomPickerAction>(JsonOptions);
            if (randomPickerAction is null || randomPickerAction.Choices.Count is 0)
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }

            if (randomPickerAction.Weights is not null
                && (randomPickerAction.Weights.Count != randomPickerAction.Choices.Count
                    || randomPickerAction.Weights.Any(weight => weight < 0)))
            {
                return ErrorCodes.InvalidActionConfig;
            }
        }

        if (type == UpdateCounterAction.ActionType)
        {
            var updateCounterAction = element.Deserialize<UpdateCounterAction>(JsonOptions);
            if (updateCounterAction is null || string.IsNullOrWhiteSpace(updateCounterAction.Key))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }
        }

        if (type == TriggerCheckInAction.ActionType)
        {
            var triggerCheckInAction = element.Deserialize<TriggerCheckInAction>(JsonOptions);
            if (triggerCheckInAction is null || string.IsNullOrWhiteSpace(triggerCheckInAction.UserId))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }
        }

        if (type == AddLotteryTicketsAction.ActionType)
        {
            var addLotteryTicketsAction = element.Deserialize<AddLotteryTicketsAction>(JsonOptions);
            if (addLotteryTicketsAction is null || string.IsNullOrWhiteSpace(addLotteryTicketsAction.UserId))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }

            if (addLotteryTicketsAction.Amount < 1)
            {
                return ErrorCodes.InvalidActionConfig;
            }
        }

        if (type == EmitSystemEventAction.ActionType)
        {
            var emitSystemEventAction = element.Deserialize<EmitSystemEventAction>(JsonOptions);
            if (emitSystemEventAction is null || string.IsNullOrWhiteSpace(emitSystemEventAction.EventTypeKey))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }
        }

        if (type == TriggerEffectAction.ActionType)
        {
            var triggerEffectAction = element.Deserialize<TriggerEffectAction>(JsonOptions);
            if (triggerEffectAction is null || string.IsNullOrWhiteSpace(triggerEffectAction.EffectId))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }

            if (triggerEffectAction.DurationMs is < 100 or > 600_000)
            {
                return ErrorCodes.InvalidActionConfig;
            }
        }

        if (type == EmitOverlayWidgetAction.ActionType)
        {
            var emitOverlayWidgetAction = element.Deserialize<EmitOverlayWidgetAction>(JsonOptions);
            if (emitOverlayWidgetAction is null
                || string.IsNullOrWhiteSpace(emitOverlayWidgetAction.WidgetType)
                || string.IsNullOrWhiteSpace(emitOverlayWidgetAction.DisplayText))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }

            if (!IsAllowedWidgetSeverity(emitOverlayWidgetAction.Severity)
                || emitOverlayWidgetAction.DurationMs is < 100 or > 600_000)
            {
                return ErrorCodes.InvalidActionConfig;
            }
        }

        if (type == LookupPlatformUserAction.ActionType)
        {
            var lookupAction = element.Deserialize<LookupPlatformUserAction>(JsonOptions);
            if (lookupAction is null
                || (string.IsNullOrWhiteSpace(lookupAction.Target)
                    && string.IsNullOrWhiteSpace(lookupAction.Login)
                    && string.IsNullOrWhiteSpace(lookupAction.UserId)))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }
        }

        if (type == ShoutoutAction.ActionType)
        {
            var shoutoutAction = element.Deserialize<ShoutoutAction>(JsonOptions);
            if (shoutoutAction is null || string.IsNullOrWhiteSpace(shoutoutAction.TargetLogin))
            {
                return ErrorCodes.ActionMissingRequiredParam;
            }
        }

        if (type == RefundRewardRedemptionAction.ActionType)
        {
            var refundAction = element.Deserialize<RefundRewardRedemptionAction>(JsonOptions);
            if (refundAction is null
                || string.IsNullOrWhiteSpace(refundAction.RewardId)
                || string.IsNullOrWhiteSpace(refundAction.RedemptionId))
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

    private static bool IsAllowedWidgetSeverity(string severity)
    {
        return severity.Equals("info", StringComparison.OrdinalIgnoreCase)
            || severity.Equals("success", StringComparison.OrdinalIgnoreCase)
            || severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
            || severity.Equals("error", StringComparison.OrdinalIgnoreCase);
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
