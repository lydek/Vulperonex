export interface HealthResponse {
  status: string;
}

export interface TwitchAuthStatusResponse {
  clientIdConfigured: boolean;
  clientSecretConfigured: boolean;
  hasRefreshToken: boolean;
}

export type SimulateAlias = "chat" | "follow" | "sub";

export interface SimulateRequestBody {
  platformUserId?: string;
  displayName?: string;
  message?: string;
  tier?: string;
}

export interface SimulateAck {
  accepted: boolean;
  eventTypeKey: string;
  eventId: string;
  platform: string;
  platformUserId: string | null;
  displayName: string | null;
  occurredAt: string;
}

const configuredBaseUrl = import.meta.env.VITE_API_URL?.trim();

export const apiBaseUrl = configuredBaseUrl
  ? configuredBaseUrl.replace(/\/$/, "")
  : "";

export async function getHealth(signal?: AbortSignal): Promise<HealthResponse> {
  return getJson<HealthResponse>("/health", signal);
}

export async function getTwitchAuthStatus(signal?: AbortSignal): Promise<TwitchAuthStatusResponse> {
  return getJson<TwitchAuthStatusResponse>("/api/twitch/auth/status", signal);
}

export interface PlatformIdentity {
  platform: string;
  platformUserId: string;
}

export interface MemberLoyalty {
  totalLoyalty: number;
  checkInCount: number;
}

export interface MemberReadModel {
  memberId: string;
  identities: PlatformIdentity[];
  loyalty: MemberLoyalty;
}

export interface MemberListQuery {
  platform?: string;
  limit?: number;
  offset?: number;
}

export async function getMembers(
  query: MemberListQuery = {},
  signal?: AbortSignal
): Promise<MemberReadModel[]> {
  const params = new URLSearchParams();
  if (query.platform) params.set("platform", query.platform);
  if (query.limit !== undefined) params.set("limit", String(query.limit));
  if (query.offset !== undefined) params.set("offset", String(query.offset));
  const search = params.toString();
  const path = search ? `/api/members/?${search}` : "/api/members/";
  return getJson<MemberReadModel[]>(path, signal);
}

export async function getMember(
  memberId: string,
  signal?: AbortSignal
): Promise<MemberReadModel> {
  return getJson<MemberReadModel>(`/api/members/${encodeURIComponent(memberId)}`, signal);
}

export interface WorkflowRuleSummary {
  id: string;
  name: string;
  eventTypeKey: string;
  isEnabled: boolean;
  priority: number;
  createdAt: string;
  version: number;
}

export interface WorkflowTrigger {
  eventTypeKey: string;
  filter: Record<string, string>;
  matchCondition?: string | null;
}

export interface WorkflowThrottlePolicy {
  maxConcurrent: number;
  cooldownSeconds: number;
  perUserCooldown: boolean;
  perUserCooldownSeconds: number;
}

export interface WorkflowRuleDetail extends WorkflowRuleSummary {
  trigger: WorkflowTrigger;
  matchCondition?: string | null;
  isSubWorkflow: boolean;
  conditions: unknown[];
  actions: unknown[];
  onFailureSteps: unknown[];
  executionMode: string;
  maxParallelism: number;
  throttle: WorkflowThrottlePolicy;
  timeoutSeconds: number;
}

export async function getRules(signal?: AbortSignal): Promise<WorkflowRuleSummary[]> {
  return getJson<WorkflowRuleSummary[]>("/api/rules/", signal);
}

export async function getRule(id: string, signal?: AbortSignal): Promise<WorkflowRuleDetail> {
  return getJson<WorkflowRuleDetail>(`/api/rules/${encodeURIComponent(id)}`, signal);
}

export async function setRuleEnabled(
  id: string,
  isEnabled: boolean,
  signal?: AbortSignal
): Promise<void> {
  const path = isEnabled ? "enable" : "disable";
  const response = await fetch(`${apiBaseUrl}/api/rules/${encodeURIComponent(id)}/${path}`, {
    method: "PUT",
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export async function deleteRule(id: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/rules/${encodeURIComponent(id)}`, {
    method: "DELETE",
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export interface WorkflowTimerDto {
  id: string;
  ruleId: string;
  intervalSeconds: number;
  isEnabled: boolean;
  nextFireAt: string;
}

export interface WorkflowTimerUpsertRequest {
  ruleId: string;
  intervalSeconds: number;
  isEnabled: boolean;
  nextFireAt: string;
}

export async function getTimers(signal?: AbortSignal): Promise<WorkflowTimerDto[]> {
  return getJson<WorkflowTimerDto[]>("/api/timers/", signal);
}

export async function getTimer(id: string, signal?: AbortSignal): Promise<WorkflowTimerDto> {
  return getJson<WorkflowTimerDto>(`/api/timers/${encodeURIComponent(id)}`, signal);
}

export async function createTimer(
  body: WorkflowTimerUpsertRequest,
  signal?: AbortSignal
): Promise<WorkflowTimerDto> {
  const response = await fetch(`${apiBaseUrl}/api/timers/`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<WorkflowTimerDto>;
}

export async function updateTimer(
  id: string,
  body: WorkflowTimerUpsertRequest,
  signal?: AbortSignal
): Promise<WorkflowTimerDto> {
  const response = await fetch(`${apiBaseUrl}/api/timers/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<WorkflowTimerDto>;
}

export async function deleteTimer(id: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/timers/${encodeURIComponent(id)}`, {
    method: "DELETE",
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export type ChatOutboxItemStatus = "Pending" | "Processing" | "Sent" | "Skipped" | "Failed";

export interface ChatOutboxItemDto {
  id: string;
  platform: string;
  channel: string | null;
  message: string;
  dedupKey: string | null;
  enqueuedAt: string;
  status: ChatOutboxItemStatus;
  errorMessage: string | null;
}

export interface ChatOutboxQuery {
  status?: ChatOutboxItemStatus;
  platform?: string;
  limit?: number;
}

export async function getChatOutbox(
  query: ChatOutboxQuery = {},
  signal?: AbortSignal
): Promise<ChatOutboxItemDto[]> {
  const params = new URLSearchParams();
  if (query.status) params.set("status", query.status);
  if (query.platform) params.set("platform", query.platform);
  if (query.limit !== undefined) params.set("limit", String(query.limit));
  const search = params.toString();
  const path = search ? `/api/chat-outbox/?${search}` : "/api/chat-outbox/";
  return getJson<ChatOutboxItemDto[]>(path, signal);
}

export interface TwitchAuthStartResponse {
  authorizeUrl: string;
  state: string;
  callbackPort: number;
}

export async function startTwitchAuth(
  callbackPort?: number,
  signal?: AbortSignal
): Promise<TwitchAuthStartResponse> {
  const port = callbackPort ?? (typeof window !== "undefined" && window.location.port ? parseInt(window.location.port, 10) : undefined);
  const response = await fetch(`${apiBaseUrl}/api/twitch/auth/start`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ callbackPort: port }),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<TwitchAuthStartResponse>;
}

export async function resetTwitchAuth(signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/twitch/auth/token`, {
    method: "DELETE",
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export interface WorkflowRuleUpsertRequest {
  id?: string;
  name: string;
  eventTypeKey: string;
  isEnabled: boolean;
  priority: number;
  conditions: unknown[];
  actions: unknown[];
  onFailureSteps?: unknown[];
  executionMode?: string;
  maxParallelism?: number;
  throttle?: WorkflowThrottlePolicy;
  timeoutSeconds?: number;
  trigger?: WorkflowTrigger;
  matchCondition?: string | null;
  isSubWorkflow?: boolean;
}

export async function createRule(
  body: WorkflowRuleUpsertRequest,
  signal?: AbortSignal
): Promise<WorkflowRuleDetail> {
  const response = await fetch(`${apiBaseUrl}/api/rules/`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<WorkflowRuleDetail>;
}

export async function updateRule(
  id: string,
  body: WorkflowRuleUpsertRequest,
  signal?: AbortSignal
): Promise<WorkflowRuleDetail> {
  const response = await fetch(`${apiBaseUrl}/api/rules/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<WorkflowRuleDetail>;
}

export interface EventTypeMetadata {
  key: string;
  description: string;
  isSimulatable: boolean;
}

export async function getEventTypes(signal?: AbortSignal): Promise<EventTypeMetadata[]> {
  return getJson<EventTypeMetadata[]>("/api/event-types", signal);
}

export type OverlayHubName = "chat" | "alerts" | "member";

export async function clearOverlayHistory(
  hubName: OverlayHubName,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/overlay/${hubName}/messages`, {
    method: "DELETE",
    signal
  });

  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export interface ConfigValueResponse {
  key: string;
  value: string | null;
}

export async function getConfigValue(
  key: string,
  signal?: AbortSignal
): Promise<ConfigValueResponse> {
  return getJson<ConfigValueResponse>(`/api/config/${encodeURIComponent(key)}`, signal);
}

export async function setConfigValue(
  key: string,
  value: string,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/config/${encodeURIComponent(key)}`, {
    method: "PUT",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ value }),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export async function postSimulate(
  alias: SimulateAlias,
  body: SimulateRequestBody,
  signal?: AbortSignal
): Promise<SimulateAck> {
  const response = await fetch(`${apiBaseUrl}/api/simulate/${alias}`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body),
    signal
  });

  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }

  return response.json() as Promise<SimulateAck>;
}

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { Accept: "application/json" },
    signal
  });

  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }

  return response.json() as Promise<T>;
}

async function safeReadBody(response: Response): Promise<string> {
  try {
    return await response.text();
  } catch {
    return "";
  }
}

export class ApiError extends Error {
  public constructor(
    public readonly status: number,
    public readonly body: string
  ) {
    super(`API request failed with HTTP ${status}`);
  }

  public get errorCode(): string | null {
    if (!this.body) {
      return null;
    }
    try {
      const parsed = JSON.parse(this.body) as { error?: unknown };
      return typeof parsed.error === "string" ? parsed.error : null;
    } catch {
      return null;
    }
  }
}
