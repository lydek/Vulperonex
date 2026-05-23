export type JsonRecord = Record<string, unknown>;

export type FieldKind =
  | "text"
  | "textarea"
  | "number"
  | "checkbox"
  | "select"
  | "string-list"
  | "number-list"
  | "string-map"
  | "json-object";

export interface SelectOption {
  label: string;
  value: string;
}

export interface FieldDefinition {
  key: string;
  label: string;
  kind: FieldKind;
  placeholder?: string;
  options?: SelectOption[];
}

export type VariableType = "string" | "number" | "boolean" | "enum";

export interface VariableDefinition {
  path: string;
  label: string;
  type: VariableType;
  options?: string[];
  hint?: string;
}

export interface VariableGroup {
  key: string;
  label: string;
  variables: VariableDefinition[];
}

interface OperatorDefinition {
  label: string;
  value: string;
  types?: VariableType[];
}

export interface ActionDefinition {
  type: string;
  label: string;
  description: string;
  fields: FieldDefinition[];
  outputVariables?: string[];
  create(): JsonRecord;
}

export interface ConditionDefinition {
  type: string;
  label: string;
  description: string;
  fields: FieldDefinition[];
  create(): JsonRecord;
}

const roleOptions = ["Subscriber", "Moderator", "Vip", "Follower"] as const;

const userRoleOptions: SelectOption[] = [
  { label: "Has any", value: "HasAny" },
  { label: "Has all", value: "HasAll" },
  { label: "Does not have", value: "NotHave" }
];

const messageMatchOptions: SelectOption[] = [
  { label: "Contains", value: "ContainsMatch" },
  { label: "Prefix", value: "PrefixMatch" },
  { label: "Regex", value: "FullRegex" }
];

const cooldownScopeOptions: SelectOption[] = [
  { label: "Global", value: "Global" },
  { label: "Per user", value: "PerUser" }
];

const overlayTargetOptions: SelectOption[] = [
  { label: "Alerts", value: "alerts" },
  { label: "Chat", value: "chat" },
  { label: "Member", value: "member" }
];

const severityOptions: SelectOption[] = [
  { label: "Info", value: "info" },
  { label: "Success", value: "success" },
  { label: "Warning", value: "warning" },
  { label: "Error", value: "error" }
];

const operatorDefinitions: OperatorDefinition[] = [
  { label: "Equals (==)", value: "==" },
  { label: "Not equals (!=)", value: "!=" },
  { label: "Greater than (>)", value: ">", types: ["number"] },
  { label: "Less than (<)", value: "<", types: ["number"] },
  { label: "Greater or equal (>=)", value: ">=", types: ["number"] },
  { label: "Less or equal (<=)", value: "<=", types: ["number"] },
  { label: "Contains", value: "contains", types: ["string"] }
];

const triggerVariableDefinitions: VariableDefinition[] = [
  { path: "Trigger.EventId", label: "Event id", type: "string" },
  { path: "Trigger.Channel", label: "Channel", type: "string" },
  { path: "Trigger.UserId", label: "User id", type: "string" },
  { path: "Trigger.UserLogin", label: "User login", type: "string" },
  { path: "Trigger.DisplayName", label: "Display name", type: "string" },
  { path: "Trigger.MessageText", label: "Message text", type: "string" },
  { path: "Trigger.Arg0", label: "Arg0", type: "string" },
  { path: "Trigger.Arg1", label: "Arg1", type: "string" },
  { path: "Trigger.RewardId", label: "Reward id", type: "string" },
  { path: "Trigger.RedemptionId", label: "Redemption id", type: "string" },
  { path: "Trigger.TimerName", label: "Timer name", type: "string" },
  { path: "Trigger.IntervalSeconds", label: "Interval seconds", type: "number" },
  { path: "Trigger.IsTest", label: "Is test", type: "boolean", options: ["true", "false"] }
];

const memberVariableDefinitions: VariableDefinition[] = [
  { path: "Member.UserId", label: "Member user id", type: "string" },
  { path: "Member.Platform", label: "Member platform", type: "string" },
  { path: "Member.DisplayName", label: "Member display name", type: "string" },
  { path: "Member.Roles", label: "Member roles", type: "string" },
  { path: "Member.IsSubscriber", label: "Member is subscriber", type: "boolean", options: ["true", "false"] }
];

const argsVariableDefinitions: VariableDefinition[] = [
  { path: "Args.Input", label: "Arg input", type: "string", hint: "Replace Input with your sub-workflow arg key." },
  { path: "Args.UserId", label: "Arg user id", type: "string" },
  { path: "Args.DisplayName", label: "Arg display name", type: "string" }
];

const failureVariableDefinitions: VariableDefinition[] = [
  { path: "Failure.ErrorCode", label: "Failure error code", type: "string" },
  { path: "Failure.ErrorMessage", label: "Failure message", type: "string" },
  { path: "Failure.StepType", label: "Failure step type", type: "string" }
];

export const conditionDefinitions: ConditionDefinition[] = [
  {
    type: "userRole",
    label: "User role",
    description: "Match stream user roles with any/all/none logic.",
    fields: [
      { key: "mode", label: "Mode", kind: "select", options: userRoleOptions },
      { key: "roles", label: "Roles", kind: "text", placeholder: "Subscriber, Vip" }
    ],
    create: () => ({ type: "userRole", mode: "HasAny", roles: "Subscriber" })
  },
  {
    type: "messageContent",
    label: "Message content",
    description: "Match message text by contains, prefix, or regex.",
    fields: [
      { key: "matchMode", label: "Match mode", kind: "select", options: messageMatchOptions },
      { key: "pattern", label: "Pattern", kind: "text", placeholder: "!hello" },
      { key: "ignoreCase", label: "Ignore case", kind: "checkbox" }
    ],
    create: () => ({ type: "messageContent", matchMode: "ContainsMatch", pattern: "", ignoreCase: true })
  },
  {
    type: "cooldown",
    label: "Cooldown",
    description: "Block repeated triggers for a duration.",
    fields: [
      { key: "scope", label: "Scope", kind: "select", options: cooldownScopeOptions },
      { key: "durationSeconds", label: "Duration seconds", kind: "number" },
      { key: "key", label: "Key", kind: "text", placeholder: "{Member.UserId}" }
    ],
    create: () => ({ type: "cooldown", scope: "Global", durationSeconds: 60, key: "" })
  }
];

export const actionDefinitions: ActionDefinition[] = [
  {
    type: "sendChatMessage",
    label: "Send chat message",
    description: "Send a rendered template to chat via outbox.",
    fields: [
      { key: "template", label: "Template", kind: "textarea", placeholder: "Hello {Member.DisplayName}" },
      { key: "targetPlatform", label: "Target platform", kind: "text", placeholder: "twitch" },
      { key: "channel", label: "Channel", kind: "text", placeholder: "{Trigger.Channel}" },
      { key: "dedupKey", label: "Dedup key", kind: "text", placeholder: "{Trigger.EventId}" }
    ],
    create: () => ({ type: "sendChatMessage", template: "" })
  },
  {
    type: "randomPicker",
    label: "Random picker",
    description: "Pick one value from a list of choices.",
    fields: [
      { key: "choices", label: "Choices", kind: "string-list", placeholder: "alpha\nbeta\ngamma" },
      { key: "weights", label: "Weights", kind: "number-list", placeholder: "1\n1\n1" }
    ],
    outputVariables: ["Picked", "Index"],
    create: () => ({ type: "randomPicker", choices: [] })
  },
  {
    type: "delay",
    label: "Delay",
    description: "Pause workflow execution for a period.",
    fields: [{ key: "delayMs", label: "Delay (ms)", kind: "number" }],
    create: () => ({ type: "delay", delayMs: 1000 })
  },
  {
    type: "stopIf",
    label: "Stop if",
    description: "Stop current workflow when condition is true.",
    fields: [{ key: "condition", label: "Condition", kind: "text", placeholder: "Trigger.MessageText == '!stop'" }],
    create: () => ({ type: "stopIf", condition: "" })
  },
  {
    type: "updateCounter",
    label: "Update counter",
    description: "Increment or decrement a named counter.",
    fields: [
      { key: "key", label: "Counter key", kind: "text", placeholder: "checkin.total" },
      { key: "delta", label: "Delta", kind: "number" }
    ],
    outputVariables: ["Value"],
    create: () => ({ type: "updateCounter", key: "", delta: 1 })
  },
  {
    type: "invokeSubWorkflow",
    label: "Invoke sub-workflow",
    description: "Call another workflow and pass args.",
    fields: [
      { key: "workflowId", label: "Workflow id", kind: "text", placeholder: "rule-id" },
      { key: "args", label: "Args", kind: "string-map", placeholder: "Target={Step.Pick.Picked}" }
    ],
    create: () => ({ type: "invokeSubWorkflow", workflowId: "", args: {} })
  },
  {
    type: "lookupTwitchUser",
    label: "Lookup Twitch user",
    description: "Resolve Twitch user metadata by login or user id.",
    fields: [
      { key: "login", label: "Login", kind: "text", placeholder: "{Trigger.Arg0}" },
      { key: "userId", label: "User id", kind: "text", placeholder: "{Trigger.UserId}" }
    ],
    outputVariables: ["Login", "DisplayName", "UserId", "IsFound"],
    create: () => ({ type: "lookupTwitchUser", login: "" })
  },
  {
    type: "shoutout",
    label: "Shoutout",
    description: "Issue a Twitch shoutout for a target login.",
    fields: [{ key: "targetLogin", label: "Target login", kind: "text", placeholder: "{Trigger.Arg0}" }],
    create: () => ({ type: "shoutout", targetLogin: "" })
  },
  {
    type: "refundTwitchRedemption",
    label: "Refund redemption",
    description: "Cancel a redemption and refund channel points.",
    fields: [
      { key: "rewardId", label: "Reward id", kind: "text", placeholder: "{Trigger.RewardId}" },
      { key: "redemptionId", label: "Redemption id", kind: "text", placeholder: "{Trigger.RedemptionId}" }
    ],
    create: () => ({ type: "refundTwitchRedemption", rewardId: "{Trigger.RewardId}", redemptionId: "{Trigger.RedemptionId}" })
  },
  {
    type: "emitOverlayWidget",
    label: "Emit overlay widget",
    description: "Broadcast a strong-typed overlay widget payload.",
    fields: [
      { key: "widgetType", label: "Widget type", kind: "text", placeholder: "banner" },
      { key: "overlayTarget", label: "Overlay target", kind: "select", options: overlayTargetOptions },
      { key: "displayText", label: "Display text", kind: "textarea", placeholder: "Alert from {Member.DisplayName}" },
      { key: "severity", label: "Severity", kind: "select", options: severityOptions },
      { key: "durationMs", label: "Duration (ms)", kind: "number" }
    ],
    create: () => ({ type: "emitOverlayWidget", widgetType: "", overlayTarget: "alerts", displayText: "", severity: "info", durationMs: 5000 })
  },
  {
    type: "emitSystemEvent",
    label: "Emit system event",
    description: "Publish a custom event to the internal event bus.",
    fields: [
      { key: "eventTypeKey", label: "Event type key", kind: "text", placeholder: "custom.fanout" },
      { key: "payload", label: "Payload", kind: "string-map", placeholder: "message={Trigger.MessageText}" }
    ],
    create: () => ({ type: "emitSystemEvent", eventTypeKey: "", payload: {} })
  },
  {
    type: "triggerEffect",
    label: "Trigger effect",
    description: "Broadcast a strong-typed overlay effect payload.",
    fields: [
      { key: "effectId", label: "Effect id", kind: "text", placeholder: "confetti" },
      { key: "durationMs", label: "Duration (ms)", kind: "number" }
    ],
    create: () => ({ type: "triggerEffect", effectId: "" })
  },
  {
    type: "triggerCheckIn",
    label: "Trigger check-in",
    description: "Increment check-in state for a user.",
    fields: [
      { key: "userId", label: "User id", kind: "text", placeholder: "{Member.UserId}" },
      { key: "platform", label: "Platform", kind: "text", placeholder: "twitch" }
    ],
    create: () => ({ type: "triggerCheckIn", userId: "{Member.UserId}" })
  },
  {
    type: "addLotteryTickets",
    label: "Add lottery tickets",
    description: "Grant counter-backed lottery tickets to a user.",
    fields: [
      { key: "userId", label: "User id", kind: "text", placeholder: "{Member.UserId}" },
      { key: "amount", label: "Amount", kind: "number" }
    ],
    outputVariables: ["Value"],
    create: () => ({ type: "addLotteryTickets", userId: "{Member.UserId}", amount: 1 })
  },
  {
    type: "invokePlugin",
    label: "Invoke plugin action",
    description: "Call a plugin action with params and resolved args.",
    fields: [
      { key: "pluginId", label: "Plugin id", kind: "text", placeholder: "sample-plugin" },
      { key: "actionId", label: "Action id", kind: "text", placeholder: "sample-action" },
      { key: "params", label: "Params", kind: "json-object", placeholder: "{\"mode\":\"demo\"}" },
      { key: "args", label: "Args", kind: "string-map", placeholder: "user={Member.DisplayName}" }
    ],
    create: () => ({ type: "invokePlugin", pluginId: "", actionId: "", params: {}, args: {} })
  }
];

export function parseArrayModel(modelValue: string): JsonRecord[] {
  try {
    const parsed = JSON.parse(modelValue) as unknown;
    return Array.isArray(parsed) ? parsed.filter(isJsonRecord) : [];
  } catch {
    return [];
  }
}

export function stringifyArrayModel(items: JsonRecord[]): string {
  return JSON.stringify(items, null, 2);
}

export function isJsonRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function asString(value: unknown): string {
  if (typeof value === "string") {
    return value;
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  return "";
}

export function asNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

export function asBoolean(value: unknown, fallback = false): boolean {
  return typeof value === "boolean" ? value : fallback;
}

export function asStringList(value: unknown): string[] {
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === "string")
    : [];
}

export function asNumberList(value: unknown): number[] {
  return Array.isArray(value)
    ? value.filter((item): item is number => typeof item === "number" && Number.isFinite(item))
    : [];
}

export function asStringMap(value: unknown): Record<string, string> {
  if (!isJsonRecord(value)) {
    return {};
  }

  const map: Record<string, string> = {};
  for (const [key, entry] of Object.entries(value)) {
    if (typeof entry === "string") {
      map[key] = entry;
    }
  }
  return map;
}

export function asJsonObject(value: unknown): JsonRecord {
  return isJsonRecord(value) ? value : {};
}

export function toStringListText(value: unknown): string {
  return asStringList(value).join("\n");
}

export function fromStringListText(value: string): string[] {
  return value.split(/\r?\n|,/).map((item) => item.trim()).filter((item) => item.length > 0);
}

export function toNumberListText(value: unknown): string {
  return asNumberList(value).join("\n");
}

export function fromNumberListText(value: string): number[] {
  return value
    .split(/\r?\n|,/)
    .map((item) => Number(item.trim()))
    .filter((item) => Number.isFinite(item));
}

export function toStringMapText(value: unknown): string {
  return Object.entries(asStringMap(value))
    .map(([key, entry]) => `${key}=${entry}`)
    .join("\n");
}

export function fromStringMapText(value: string): Record<string, string> {
  const map: Record<string, string> = {};
  for (const line of value.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (trimmed.length === 0) {
      continue;
    }

    const separatorIndex = trimmed.indexOf("=");
    if (separatorIndex <= 0) {
      continue;
    }

    const key = trimmed.slice(0, separatorIndex).trim();
    const entry = trimmed.slice(separatorIndex + 1).trim();
    if (key.length > 0) {
      map[key] = entry;
    }
  }
  return map;
}

export function toJsonObjectText(value: unknown): string {
  return JSON.stringify(asJsonObject(value), null, 2);
}

export function fromJsonObjectText(value: string): JsonRecord {
  try {
    const parsed = JSON.parse(value) as unknown;
    return isJsonRecord(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

export function findActionDefinition(type: string): ActionDefinition | undefined {
  return actionDefinitions.find((definition) => definition.type === type);
}

export function findConditionDefinition(type: string): ConditionDefinition | undefined {
  return conditionDefinitions.find((definition) => definition.type === type);
}

export function toVariableToken(path: string, expressionMode: boolean): string {
  return expressionMode ? path : `{${path}}`;
}

export function getVariableInfo(path: string): VariableDefinition | undefined {
  const cleanPath = path.replace(/[{}]/g, "");
  const direct = [
    ...triggerVariableDefinitions,
    ...argsVariableDefinitions,
    ...memberVariableDefinitions,
    ...failureVariableDefinitions
  ].find((entry) => entry.path === cleanPath);
  if (direct) {
    return direct;
  }

  if (/^Step\.[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*$/.test(cleanPath)) {
    return {
      path: cleanPath,
      label: cleanPath.replace(/^Step\./, ""),
      type: cleanPath.endsWith(".Status") ? "enum" : "string",
      options: cleanPath.endsWith(".Status") ? ["success", "repeat", "cooldown", "error"] : undefined
    };
  }

  return undefined;
}

export function getOperatorOptions(path: string) {
  const variableType = getVariableInfo(path)?.type ?? "string";
  return operatorDefinitions.filter((definition) => !definition.types || definition.types.includes(variableType));
}

export function buildVariableGroups(previousSteps: JsonRecord[], expressionMode: boolean): VariableGroup[] {
  const stepVariables: VariableDefinition[] = [];

  for (const item of previousSteps) {
    const outputVariable = asString(item.outputVariable).trim();
    if (outputVariable.length === 0) {
      continue;
    }

    const definition = findActionDefinition(asString(item.type));
    const outputFields = definition?.outputVariables ?? ["Status", "Value"];
    for (const outputField of outputFields) {
      stepVariables.push({
        path: `Step.${outputVariable}.${outputField}`,
        label: `${outputVariable}.${outputField}`,
        type: outputField === "Status" ? "enum" : "string",
        options: outputField === "Status" ? ["success", "repeat", "cooldown", "error"] : undefined,
        hint: definition?.label
      });
    }
  }

  const buildGroup = (key: string, label: string, variables: VariableDefinition[]): VariableGroup => ({
    key,
    label,
    variables: variables.map((entry) => ({
      ...entry,
      path: toVariableToken(entry.path, expressionMode)
    }))
  });

  return [
    buildGroup("trigger", "Trigger", triggerVariableDefinitions),
    buildGroup("args", "Args", argsVariableDefinitions),
    buildGroup("steps", "Step Outputs", stepVariables),
    buildGroup("member", "Member", memberVariableDefinitions),
    buildGroup("failure", "Failure", failureVariableDefinitions)
  ].filter((group) => group.variables.length > 0);
}

export function normalizeRoles(value: unknown): string {
  if (typeof value === "string") {
    return value;
  }

  if (typeof value === "number" && Number.isFinite(value)) {
    const selected = roleOptions.filter((_, index) => (value & (1 << index)) !== 0);
    return selected.join(", ");
  }

  return "";
}

export function roleCheckboxState(value: unknown, role: string): boolean {
  return normalizeRoles(value)
    .split(",")
    .map((entry) => entry.trim())
    .includes(role);
}

export function updateRoleSelection(value: unknown, role: string, checked: boolean): string {
  const current = new Set(
    normalizeRoles(value)
      .split(",")
      .map((entry) => entry.trim())
      .filter((entry) => entry.length > 0)
  );

  if (checked) {
    current.add(role);
  } else {
    current.delete(role);
  }

  return roleOptions.filter((entry) => current.has(entry)).join(", ");
}
