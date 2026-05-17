namespace Vulperonex.Web.Errors;

public static class ErrorCodes
{
    public const string WorkflowRuleNotFound = "WORKFLOW_RULE_NOT_FOUND";
    public const string WorkflowRuleIdNotAllowed = "WORKFLOW_RULE_ID_NOT_ALLOWED";
    public const string WorkflowRuleConflict = "WORKFLOW_RULE_CONFLICT";
    public const string UnknownEventTypeKey = "UNKNOWN_EVENT_TYPE_KEY";
    public const string CircularWorkflowReference = "CIRCULAR_WORKFLOW_REFERENCE";
    public const string UnknownActionType = "UNKNOWN_ACTION_TYPE";
    public const string UnknownConditionType = "UNKNOWN_CONDITION_TYPE";
    public const string ActionMissingRequiredParam = "ACTION_MISSING_REQUIRED_PARAM";
    public const string InvalidActionConfig = "INVALID_ACTION_CONFIG";
    public const string InvalidRegexPattern = "INVALID_REGEX_PATTERN";
    public const string InvalidRuleIdMismatch = "INVALID_RULE_ID_MISMATCH";
    public const string UnknownSimulateEventType = "UNKNOWN_SIMULATE_EVENT_TYPE";
    public const string ConfigKeySecurityNamespace = "CONFIG_KEY_SECURITY_NAMESPACE";
    public const string OAuthCredentialNamespace = "OAUTH_CREDENTIAL_NAMESPACE";
    public const string UnknownConfigKey = "UNKNOWN_CONFIG_KEY";
    public const string InvalidQueryParam = "INVALID_QUERY_PARAM";
    public const string MemberNotFound = "MEMBER_NOT_FOUND";
}
