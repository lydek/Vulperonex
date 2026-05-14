using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Vulperonex.Application.Time;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Conditions;

public sealed class WorkflowConditionEvaluator(IClock clock)
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastCooldownPassByKey = new();
    private readonly WorkflowConditionValidator _validator = new();

    public bool IsMatch(WorkflowCondition condition, ConditionEvaluationContext context)
    {
        if (!_validator.Validate(condition).IsValid)
        {
            return false;
        }

        return condition switch
        {
            UserRoleCondition userRole => MatchesUserRole(userRole, context.StreamEvent.User),
            MessageContentCondition messageContent => MatchesMessageContent(messageContent, context.StreamEvent),
            CooldownCondition cooldown => MatchesCooldown(cooldown, context),
            _ => false,
        };
    }

    private static bool MatchesUserRole(UserRoleCondition condition, StreamUser? user)
    {
        if (user is null)
        {
            return false;
        }

        return condition.Mode switch
        {
            UserRoleMatchMode.HasAny => (user.Roles & condition.Roles) != StreamRole.None,
            UserRoleMatchMode.HasAll => (user.Roles & condition.Roles) == condition.Roles,
            UserRoleMatchMode.NotHave => (user.Roles & condition.Roles) == StreamRole.None,
            _ => false,
        };
    }

    private static bool MatchesMessageContent(MessageContentCondition condition, IStreamEvent streamEvent)
    {
        if (streamEvent is not UserSentMessageEvent messageEvent)
        {
            return false;
        }

        var comparison = condition.IgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return condition.MatchMode switch
        {
            MessageContentMatchMode.PrefixMatch => messageEvent.MessageText.StartsWith(condition.Pattern, comparison),
            MessageContentMatchMode.ContainsMatch => messageEvent.MessageText.Contains(condition.Pattern, comparison),
            MessageContentMatchMode.FullRegex => Regex.IsMatch(
                messageEvent.MessageText,
                condition.Pattern,
                condition.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None,
                RegexTimeout),
            _ => false,
        };
    }

    private bool MatchesCooldown(CooldownCondition condition, ConditionEvaluationContext context)
    {
        var key = BuildCooldownKey(condition, context);
        var now = clock.UtcNow;

        if (_lastCooldownPassByKey.TryGetValue(key, out var lastPass) &&
            now - lastPass < TimeSpan.FromSeconds(condition.DurationSeconds))
        {
            return false;
        }

        _lastCooldownPassByKey[key] = now;
        return true;
    }

    private static string BuildCooldownKey(CooldownCondition condition, ConditionEvaluationContext context)
    {
        var conditionKey = string.IsNullOrWhiteSpace(condition.Key)
            ? context.WorkflowRuleId
            : condition.Key;

        return condition.Scope is CooldownScope.PerUser
            ? $"{conditionKey}:{context.StreamEvent.User?.Platform}:{context.StreamEvent.User?.UserId}"
            : conditionKey;
    }
}
