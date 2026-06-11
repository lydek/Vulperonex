namespace Vulperonex.Web.Errors;

public static class ErrorCodeStatusMap
{
    private static readonly IReadOnlyDictionary<string, int> StatusByCode = new Dictionary<string, int>
    {
        [ErrorCodes.WorkflowRuleNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.WorkflowTimerNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.WorkflowTimerConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.TimerRuleIdRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.TimerIntervalInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ModuleNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.InvalidConfigValue] = StatusCodes.Status400BadRequest,
        [ErrorCodes.WorkflowRuleIdNotAllowed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.WorkflowRuleConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.UnknownEventTypeKey] = StatusCodes.Status400BadRequest,
        [ErrorCodes.CircularWorkflowReference] = StatusCodes.Status400BadRequest,
        [ErrorCodes.UnknownActionType] = StatusCodes.Status400BadRequest,
        [ErrorCodes.UnknownConditionType] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ActionMissingRequiredParam] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidActionConfig] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidRegexPattern] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SubWorkflowMustNotHaveTrigger] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidRuleIdMismatch] = StatusCodes.Status400BadRequest,
        [ErrorCodes.UnknownSimulateEventType] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ConfigKeySecurityNamespace] = StatusCodes.Status403Forbidden,
        [ErrorCodes.OAuthCredentialNamespace] = StatusCodes.Status403Forbidden,
        [ErrorCodes.UnknownConfigKey] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidQueryParam] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MemberNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.TwitchClientIdMissing] = StatusCodes.Status400BadRequest,
        [ErrorCodes.TwitchClientSecretMissing] = StatusCodes.Status400BadRequest,
        [ErrorCodes.TwitchOAuthStateInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.TwitchOAuthExchangeFailed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PreconditionRequired] = StatusCodes.Status428PreconditionRequired,
        [ErrorCodes.InvalidPrecondition] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MemberConcurrencyConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.ModuleDisabled] = StatusCodes.Status503ServiceUnavailable,
        [ErrorCodes.InvalidFilePath] = StatusCodes.Status400BadRequest,
        [ErrorCodes.UnsupportedFileExtension] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PresetLocked] = StatusCodes.Status409Conflict,
        [ErrorCodes.PresetNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.DraftValidationFailed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.VersionNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.InvalidModuleName] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MissingOrInvalidCsrfHeader] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MissingOriginOrRefererHeader] = StatusCodes.Status400BadRequest,
        [ErrorCodes.OriginMismatch] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidOriginHeader] = StatusCodes.Status400BadRequest,
        [ErrorCodes.RefererMismatch] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidRefererHeader] = StatusCodes.Status400BadRequest,
        [ErrorCodes.DeployFailed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidFilterKey] = StatusCodes.Status400BadRequest,
    };

    public static int GetStatusCode(string errorCode)
    {
        return StatusByCode.TryGetValue(errorCode, out var statusCode)
            ? statusCode
            : StatusCodes.Status500InternalServerError;
    }
}
