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
  advanced?: boolean;
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

const roleOptions = ["Broadcaster", "Subscriber", "Moderator", "Vip", "Follower"] as const;

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
  { path: "Trigger.EventTypeKey", label: "Event type key", type: "string" },
  { path: "Trigger.Platform", label: "Event platform", type: "string" },
  { path: "Trigger.OccurredAt", label: "Occurred at", type: "string" },
  { path: "Trigger.MessageText", label: "Message text", type: "string" },
  { path: "Trigger.RewardId", label: "Reward id", type: "string" },
  { path: "Trigger.RewardTitle", label: "Reward title", type: "string" },
  { path: "Trigger.RedemptionId", label: "Redemption id", type: "string" },
  { path: "Trigger.TotalBitsGiven", label: "Total bits given", type: "number" },
  { path: "Trigger.Tier", label: "Subscription tier", type: "string" },
  { path: "Trigger.GiftCount", label: "Gift count", type: "number" },
  { path: "Trigger.ViewerCount", label: "Viewer count", type: "number" },
  { path: "Trigger.Depth", label: "Workflow depth", type: "number" },
  { path: "Trigger.Payload.TimerId", label: "Timer id", type: "string" },
  { path: "Trigger.Payload.RuleId", label: "Timer rule id", type: "string" },
  { path: "Trigger.Payload.IntervalSeconds", label: "Timer interval seconds", type: "number" }
];

const memberVariableDefinitions: VariableDefinition[] = [
  { path: "Member.UserId", label: "Trigger user id", type: "string" },
  { path: "Member.Platform", label: "Trigger user platform", type: "string" },
  { path: "Member.DisplayName", label: "Trigger user display name", type: "string" },
  { path: "Member.Roles", label: "Trigger user roles", type: "string" },
  { path: "Member.IsSubscriber", label: "Trigger user is subscriber", type: "boolean", options: ["true", "false"] },
  { path: "Member.IsModerator", label: "Trigger user is moderator", type: "boolean", options: ["true", "false"] },
  { path: "Member.IsVip", label: "Trigger user is VIP", type: "boolean", options: ["true", "false"] },
  { path: "Member.IsFollower", label: "Trigger user is follower", type: "boolean", options: ["true", "false"] },
  { path: "Member.IsBroadcaster", label: "Trigger user is broadcaster", type: "boolean", options: ["true", "false"] }
];

const argsVariableDefinitions: VariableDefinition[] = [
  { path: "Args.Input", label: "Arg input", type: "string", hint: "Replace Input with your sub-workflow arg key." },
  { path: "Args.UserId", label: "Arg user id", type: "string" },
  { path: "Args.DisplayName", label: "Arg display name", type: "string" }
];

const failureVariableDefinitions: VariableDefinition[] = [
  { path: "Failure.StepIndex", label: "Failure step index", type: "number" },
  { path: "Failure.ErrorMessage", label: "Failure message", type: "string" }
];

const defaultStepStatusOptions = ["success", "repeat", "error"];

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

export const fallbackActionDefinitions: ActionDefinition[] = [
  {
    type: "sendChatMessage",
    label: "Send chat message",
    description: "Fallback action metadata used while backend metadata is unavailable.",
    fields: [
      { key: "template", label: "Template", kind: "textarea", placeholder: "Hello {Member.DisplayName}" }
    ],
    create: () => ({ type: "sendChatMessage" })
  },
  {
    type: "triggerCheckIn",
    label: "Trigger Check-In",
    description: "Fallback check-in action metadata.",
    fields: [
      { key: "userId", label: "User ID", kind: "text", placeholder: "{Member.UserId}", advanced: true },
      { key: "platform", label: "Platform", kind: "text", placeholder: "{Trigger.Platform}", advanced: true }
    ],
    outputVariables: ["CheckInCount", "TotalLoyalty", "DisplayName", "RoundIndex", "StampSlotInRound"],
    create: () => ({ type: "triggerCheckIn", userId: "{Member.UserId}" })
  },
  {
    type: "randomPicker",
    label: "Random Picker",
    description: "Fallback random picker action metadata.",
    fields: [
      { key: "choices", label: "Choices", kind: "string-list" }
    ],
    outputVariables: ["Picked", "Index"],
    create: () => ({ type: "randomPicker", choices: [] })
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

export function findActionDefinition(type: string, definitions: ActionDefinition[] = fallbackActionDefinitions): ActionDefinition | undefined {
  return definitions.find((definition) => definition.type === type);
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
      options: cleanPath.endsWith(".Status") ? defaultStepStatusOptions : undefined
    };
  }

  return undefined;
}

export function getOperatorOptions(path: string) {
  const variableType = getVariableInfo(path)?.type ?? "string";
  return operatorDefinitions.filter((definition) => !definition.types || definition.types.includes(variableType));
}

export function buildVariableGroups(
  previousSteps: JsonRecord[],
  expressionMode: boolean,
  actionDefinitions: ActionDefinition[] = fallbackActionDefinitions
): VariableGroup[] {
  const stepVariables: VariableDefinition[] = [];

  for (const item of previousSteps) {
    const outputVariable = asString(item.outputVariable).trim();
    if (outputVariable.length === 0) {
      continue;
    }

    const definition = findActionDefinition(asString(item.type), actionDefinitions);
    const outputFields = Array.from(new Set(["Status", ...(definition?.outputVariables ?? ["Value"])]));
    for (const outputField of outputFields) {
      stepVariables.push({
        path: `Step.${outputVariable}.${outputField}`,
        label: `${outputVariable}.${outputField}`,
        type: inferStepVariableType(asString(item.type), outputField),
        options: outputField === "Status" ? defaultStepStatusOptions : undefined,
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
    buildGroup("trigger", "Trigger Event", triggerVariableDefinitions),
    buildGroup("args", "Args", argsVariableDefinitions),
    buildGroup("steps", "Step Outputs", stepVariables),
    buildGroup("member", "Trigger User", memberVariableDefinitions),
    buildGroup("failure", "Failure", failureVariableDefinitions)
  ].filter((group) => group.variables.length > 0);
}

function inferStepVariableType(actionType: string, outputField: string): VariableType {
  if (outputField === "Status") {
    return "enum";
  }

  if (
    outputField === "CheckInCount" ||
    outputField === "TotalLoyalty" ||
    outputField === "RoundIndex" ||
    outputField === "StampSlotInRound" ||
    outputField === "Index" ||
    outputField === "Value"
  ) {
    return "number";
  }

  if (actionType === "lookupTwitchUser" && outputField === "IsFound") {
    return "boolean";
  }

  return "string";
}

export function getStepStatusOptions(path: string): string[] {
  return getVariableInfo(path)?.options ?? [];
}

export function getStepStatusModeHint(
  path: string,
  previousSteps: JsonRecord[],
  actionDefinitions: ActionDefinition[] = fallbackActionDefinitions
): string | null {
  const cleanPath = path.replace(/[{}]/g, "");
  const match = cleanPath.match(/^Step\.([A-Za-z][A-Za-z0-9_]*)\.Status$/);
  if (!match) {
    return null;
  }

  const stepName = match[1];
  const matchedStep = previousSteps.find(item => asString(item.outputVariable).trim() === stepName);
  const actionType = asString(matchedStep?.type);
  const definition = findActionDefinition(actionType, actionDefinitions);

  if (actionType === "triggerCheckIn") {
    return "Mode: Check-in status";
  }

  return definition ? `Mode: ${definition.label} status` : "Mode: Step status";
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
