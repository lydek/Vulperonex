using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Vulperonex.Application.Workflows.Conditions;

public sealed class WorkflowConditionValidator
{
    public const int MaxRegexPatternLength = 512;
    public const int MinCooldownSeconds = 1;
    public const int MaxCooldownSeconds = 86_400;
    public const string InvalidRegexPattern = "INVALID_REGEX_PATTERN";
    public const string InvalidActionConfig = "INVALID_ACTION_CONFIG";
    public const string UnknownConditionType = "UNKNOWN_CONDITION_TYPE";

    private static readonly TimeSpan RegexCompileTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly ConcurrentDictionary<string, bool> RegexValidityCache = new(StringComparer.Ordinal);

    public ConditionValidationResult Validate(WorkflowCondition condition)
    {
        return condition switch
        {
            MessageContentCondition message => ValidateMessageContent(message),
            CooldownCondition cooldown => ValidateCooldown(cooldown),
            UserRoleCondition => ConditionValidationResult.Valid,
            _ => new ConditionValidationResult(false, UnknownConditionType),
        };
    }

    private static ConditionValidationResult ValidateMessageContent(MessageContentCondition condition)
    {
        if (condition.MatchMode is not MessageContentMatchMode.FullRegex)
        {
            return ConditionValidationResult.Valid;
        }

        if (condition.Pattern.Length > MaxRegexPatternLength)
        {
            return new ConditionValidationResult(false, InvalidRegexPattern);
        }

        return IsRegexCompilable(condition.Pattern)
            ? ConditionValidationResult.Valid
            : new ConditionValidationResult(false, InvalidRegexPattern);
    }

    private static bool IsRegexCompilable(string pattern)
    {
        return RegexValidityCache.GetOrAdd(pattern, static p =>
        {
            try
            {
                _ = new Regex(p, RegexOptions.None, RegexCompileTimeout);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        });
    }

    private static ConditionValidationResult ValidateCooldown(CooldownCondition condition)
    {
        return condition.DurationSeconds is >= MinCooldownSeconds and <= MaxCooldownSeconds
            ? ConditionValidationResult.Valid
            : new ConditionValidationResult(false, InvalidActionConfig);
    }
}
