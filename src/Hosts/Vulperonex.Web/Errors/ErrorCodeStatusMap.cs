namespace Vulperonex.Web.Errors;

public static class ErrorCodeStatusMap
{
    private static readonly IReadOnlyDictionary<string, int> StatusByCode = new Dictionary<string, int>
    {
        [ErrorCodes.WorkflowRuleNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.UnknownEventTypeKey] = StatusCodes.Status400BadRequest,
        [ErrorCodes.CircularWorkflowReference] = StatusCodes.Status400BadRequest,
        [ErrorCodes.UnknownActionType] = StatusCodes.Status400BadRequest,
        [ErrorCodes.UnknownConditionType] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ActionMissingRequiredParam] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidActionConfig] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidRegexPattern] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidRuleIdMismatch] = StatusCodes.Status400BadRequest,
        [ErrorCodes.UnknownSimulateEventType] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ConfigKeySecurityNamespace] = StatusCodes.Status403Forbidden,
        [ErrorCodes.OAuthCredentialNamespace] = StatusCodes.Status403Forbidden,
        [ErrorCodes.UnknownConfigKey] = StatusCodes.Status400BadRequest,
        [ErrorCodes.InvalidQueryParam] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MemberNotFound] = StatusCodes.Status404NotFound,
    };

    public static int GetStatusCode(string errorCode)
    {
        return StatusByCode.TryGetValue(errorCode, out var statusCode)
            ? statusCode
            : StatusCodes.Status500InternalServerError;
    }
}
