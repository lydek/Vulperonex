namespace Vulperonex.Web.Errors;

public static class ErrorCodes
{
    public const string WorkflowRuleNotFound = "WORKFLOW_RULE_NOT_FOUND";
    public const string WorkflowTimerNotFound = "WORKFLOW_TIMER_NOT_FOUND";
    public const string WorkflowRuleIdNotAllowed = "WORKFLOW_RULE_ID_NOT_ALLOWED";
    public const string WorkflowRuleConflict = "WORKFLOW_RULE_CONFLICT";
    public const string UnknownEventTypeKey = "UNKNOWN_EVENT_TYPE_KEY";
    public const string CircularWorkflowReference = "CIRCULAR_WORKFLOW_REFERENCE";
    public const string UnknownActionType = "UNKNOWN_ACTION_TYPE";
    public const string UnknownConditionType = "UNKNOWN_CONDITION_TYPE";
    public const string ActionMissingRequiredParam = "ACTION_MISSING_REQUIRED_PARAM";
    public const string InvalidActionConfig = "INVALID_ACTION_CONFIG";
    public const string InvalidRegexPattern = "INVALID_REGEX_PATTERN";
    public const string SubWorkflowMustNotHaveTrigger = "SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER";
    public const string InvalidRuleIdMismatch = "INVALID_RULE_ID_MISMATCH";
    public const string UnknownSimulateEventType = "UNKNOWN_SIMULATE_EVENT_TYPE";
    public const string ConfigKeySecurityNamespace = "CONFIG_KEY_SECURITY_NAMESPACE";
    public const string OAuthCredentialNamespace = "OAUTH_CREDENTIAL_NAMESPACE";
    public const string UnknownConfigKey = "UNKNOWN_CONFIG_KEY";
    public const string InvalidQueryParam = "INVALID_QUERY_PARAM";
    public const string MemberNotFound = "MEMBER_NOT_FOUND";
    public const string TwitchClientIdMissing = "TWITCH_CLIENT_ID_MISSING";
    public const string TwitchClientSecretMissing = "TWITCH_CLIENT_SECRET_MISSING";
    public const string TwitchOAuthStateInvalid = "TWITCH_OAUTH_STATE_INVALID";
    public const string TwitchOAuthExchangeFailed = "TWITCH_OAUTH_EXCHANGE_FAILED";
    public const string PreconditionRequired = "PRECONDITION_REQUIRED";
    public const string InvalidPrecondition = "INVALID_PRECONDITION";
    public const string MemberConcurrencyConflict = "MEMBER_CONCURRENCY_CONFLICT";
    public const string ModuleDisabled = "MODULE_DISABLED";
    public const string PresetLocked = "PRESET_LOCKED";
    public const string PresetNotFound = "PRESET_NOT_FOUND";
    public const string DraftValidationFailed = "DRAFT_VALIDATION_FAILED";
    public const string VersionNotFound = "VERSION_NOT_FOUND";
    public const string InvalidModuleName = "INVALID_MODULE_NAME";
    public const string MissingOrInvalidCsrfHeader = "MISSING_OR_INVALID_CSRF_HEADER";
    public const string MissingOriginOrRefererHeader = "MISSING_ORIGIN_OR_REFERER_HEADER";
    public const string OriginMismatch = "ORIGIN_MISMATCH";
    public const string InvalidOriginHeader = "INVALID_ORIGIN_HEADER";
    public const string RefererMismatch = "REFERER_MISMATCH";
    public const string InvalidRefererHeader = "INVALID_REFERER_HEADER";
    public const string InvalidFilePath = "INVALID_FILE_PATH";
    public const string UnsupportedFileExtension = "UNSUPPORTED_FILE_EXTENSION";
    public const string DeployFailed = "DEPLOY_FAILED";
}
