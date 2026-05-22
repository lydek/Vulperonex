// Mirror of src/Hosts/Vulperonex.Web/Errors/ErrorCodes.cs plus the
// frontend-only INTERNAL_ERROR sentinel for 5xx responses. Keep names in
// sync; the i18n parity test asserts each constant has a translation in
// both locales.

export const ERROR_CODES = {
  WorkflowRuleNotFound: "WORKFLOW_RULE_NOT_FOUND",
  WorkflowRuleIdNotAllowed: "WORKFLOW_RULE_ID_NOT_ALLOWED",
  WorkflowRuleConflict: "WORKFLOW_RULE_CONFLICT",
  UnknownEventTypeKey: "UNKNOWN_EVENT_TYPE_KEY",
  CircularWorkflowReference: "CIRCULAR_WORKFLOW_REFERENCE",
  UnknownActionType: "UNKNOWN_ACTION_TYPE",
  UnknownConditionType: "UNKNOWN_CONDITION_TYPE",
  ActionMissingRequiredParam: "ACTION_MISSING_REQUIRED_PARAM",
  InvalidActionConfig: "INVALID_ACTION_CONFIG",
  InvalidRegexPattern: "INVALID_REGEX_PATTERN",
  InvalidRuleIdMismatch: "INVALID_RULE_ID_MISMATCH",
  UnknownSimulateEventType: "UNKNOWN_SIMULATE_EVENT_TYPE",
  ConfigKeySecurityNamespace: "CONFIG_KEY_SECURITY_NAMESPACE",
  OAuthCredentialNamespace: "OAUTH_CREDENTIAL_NAMESPACE",
  UnknownConfigKey: "UNKNOWN_CONFIG_KEY",
  InvalidQueryParam: "INVALID_QUERY_PARAM",
  MemberNotFound: "MEMBER_NOT_FOUND",
  TwitchClientIdMissing: "TWITCH_CLIENT_ID_MISSING",
  TwitchClientSecretMissing: "TWITCH_CLIENT_SECRET_MISSING",
  TwitchOAuthStateInvalid: "TWITCH_OAUTH_STATE_INVALID",
  TwitchOAuthExchangeFailed: "TWITCH_OAUTH_EXCHANGE_FAILED",
  InternalError: "INTERNAL_ERROR",
  NetworkError: "NETWORK_ERROR"
} as const;

export type ErrorCode = (typeof ERROR_CODES)[keyof typeof ERROR_CODES];

export function errorCodeI18nKey(code: string): string {
  return `errorCode.${code}`;
}

export function resolveErrorCode(status: number, parsedCode: string | null): string {
  if (parsedCode) return parsedCode;
  if (status >= 500) return ERROR_CODES.InternalError;
  return `HTTP_${status}`;
}
