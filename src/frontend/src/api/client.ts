export interface HealthResponse {
  status: string;
}

export interface TwitchAuthStatusResponse {
  clientIdConfigured: boolean;
  clientSecretConfigured: boolean;
  hasRefreshToken: boolean;
}

export type SimulateAlias = "chat" | "follow" | "sub" | "giftsub" | "raid" | "bits" | "redeem" | "checkin";

export interface SimulateRequestBody {
  platformUserId?: string;
  displayName?: string;
  roles?: string[];
  message?: string;
  tier?: string;
  recipientDisplayName?: string;
  bits?: number;
  rewardId?: string;
  userInput?: string;
  stampCount?: number;
  badges?: string[];
  colorHex?: string;
}

export interface PlatformBadgeDescriptor {
  key: string;
  setId: string;
  version: string;
  imageUrl1x: string;
  title: string | null;
  description: string | null;
  isChannel: boolean;
}

export interface TwitchBadgesListResponse {
  ready: boolean;
  global: PlatformBadgeDescriptor[];
  channel: PlatformBadgeDescriptor[];
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

export async function getTwitchBadges(signal?: AbortSignal): Promise<TwitchBadgesListResponse> {
  return getJson<TwitchBadgesListResponse>("/api/twitch/badges", signal);
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
  updatedAtTicks: number;
  etag?: string;
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/rules/${encodeURIComponent(id)}/${path}`, {
    method: "PUT",
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export async function deleteRule(id: string, signal?: AbortSignal): Promise<void> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/rules/${encodeURIComponent(id)}`, {
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/timers/`, {
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/timers/${encodeURIComponent(id)}`, {
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/timers/${encodeURIComponent(id)}`, {
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/twitch/auth/start`, {
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/twitch/auth/token`, {
    method: "DELETE",
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export interface TwitchDeviceAuthorizationResponse {
  deviceCode: string;
  userCode: string;
  verificationUri: string;
  expiresIn: number;
  interval: number;
}

export async function startTwitchDeviceAuth(signal?: AbortSignal): Promise<TwitchDeviceAuthorizationResponse> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/twitch/auth/device/start`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<TwitchDeviceAuthorizationResponse>;
}

export async function completeTwitchDeviceAuth(
  deviceCode: string,
  signal?: AbortSignal
): Promise<boolean> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/twitch/auth/device/complete`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ deviceCode }),
    signal
  });
  if (response.status === 202) {
    return false;
  }
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return true;
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/rules/`, {
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/rules/${encodeURIComponent(id)}`, {
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/${hubName}/messages`, {
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

export interface OverlayCustomPresetMetadata {
  slug: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface OverlayPresetDescriptor {
  hubName: "chat" | "member" | "alerts";
  key: string;
  kind: string;
  label: string;
  relativeUrl: string;
}

export interface PluginModule {
  name: string;
  displayName: string;
  kind: string;
  enabled: boolean;
  dependencies: string[];
  dependents: string[];
}

export interface TogglePluginModuleResponse {
  module: PluginModule;
  changedModules: PluginModule[];
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
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/config/${encodeURIComponent(key)}`, {
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

export async function getOverlayCustomPresets(signal?: AbortSignal): Promise<OverlayCustomPresetMetadata[]> {
  return getJson<OverlayCustomPresetMetadata[]>("/api/overlay/custom-presets", signal);
}

export async function getPluginModules(signal?: AbortSignal): Promise<PluginModule[]> {
  return getJson<PluginModule[]>("/api/plugins-modules", signal);
}

export async function togglePluginModule(
  name: string,
  enabled: boolean,
  signal?: AbortSignal
): Promise<TogglePluginModuleResponse> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/plugins-modules/${encodeURIComponent(name)}/toggle`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ enabled }),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<TogglePluginModuleResponse>;
}

export async function getOverlayPresetCatalog(signal?: AbortSignal): Promise<OverlayPresetDescriptor[]> {
  return getJson<OverlayPresetDescriptor[]>("/api/overlay/presets", signal);
}

export async function deleteOverlayCustomPreset(slug: string, signal?: AbortSignal): Promise<void> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/custom-presets/${encodeURIComponent(slug)}`, {
    method: "DELETE",
    headers: {
      "X-Admin-Csrf": "true"
    },
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export interface OverlayFileDescriptor {
  relativePath: string;
  sizeBytes: number;
  lastModifiedAt: string;
}

export interface OverlayHistoryVersion {
  versionStamp: string;
  createdAt: string;
}

export interface OverlayValidationIssue {
  severity: string;
  code: string;
  message: string;
  filePath: string | null;
  line: number | null;
}

export async function getOverlayCustomPresetFiles(slug: string, signal?: AbortSignal): Promise<OverlayFileDescriptor[]> {
  return getJson<OverlayFileDescriptor[]>(`/api/overlay/custom-presets/${encodeURIComponent(slug)}/files`, signal);
}

export async function readOverlayCustomPresetFile(slug: string, path: string, signal?: AbortSignal): Promise<string> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/custom-presets/${encodeURIComponent(slug)}/files/${path}`, {
    headers: {
      Accept: "text/plain",
      "X-Admin-Csrf": "true"
    },
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.text();
}

export async function writeOverlayCustomPresetFile(
  slug: string,
  path: string,
  content: string,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/custom-presets/${encodeURIComponent(slug)}/files/${path}`, {
    method: "PUT",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-Admin-Csrf": "true"
    },
    body: JSON.stringify({ content }),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export async function deleteOverlayCustomPresetFile(
  slug: string,
  path: string,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/custom-presets/${encodeURIComponent(slug)}/files/${path}`, {
    method: "DELETE",
    headers: {
      "X-Admin-Csrf": "true"
    },
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export async function deployOverlayCustomPreset(slug: string, signal?: AbortSignal): Promise<void> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/custom-presets/${encodeURIComponent(slug)}/deploy`, {
    method: "POST",
    headers: {
      "X-Admin-Csrf": "true"
    },
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export async function validateOverlayCustomPreset(slug: string, signal?: AbortSignal): Promise<OverlayValidationIssue[]> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/custom-presets/${encodeURIComponent(slug)}/validate`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "X-Admin-Csrf": "true"
    },
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<OverlayValidationIssue[]>;
}

export async function getOverlayCustomPresetHistory(slug: string, signal?: AbortSignal): Promise<OverlayHistoryVersion[]> {
  return getJson<OverlayHistoryVersion[]>(`/api/overlay/custom-presets/${encodeURIComponent(slug)}/history`, signal);
}

export async function rollbackOverlayCustomPreset(
  slug: string,
  versionStamp: string,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/custom-presets/${encodeURIComponent(slug)}/rollback/${encodeURIComponent(versionStamp)}`, {
    method: "POST",
    headers: {
      "X-Admin-Csrf": "true"
    },
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export async function uploadOverlayCustomPreset(
  slug: string,
  file: File,
  signal?: AbortSignal
): Promise<OverlayCustomPresetMetadata> {
  const form = new FormData();
  form.set("slug", slug);
  form.set("file", file);
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/overlay/custom-presets`, {
    method: "POST",
    headers: {
      "X-Admin-Csrf": "true"
    },
    body: form,
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  return response.json() as Promise<OverlayCustomPresetMetadata>;
}

export async function postSimulate(
  alias: SimulateAlias,
  body: SimulateRequestBody,
  signal?: AbortSignal
): Promise<SimulateAck> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/simulate/${alias}`, {
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

export async function postSimulateCheckIn(
  body: { platformUserId?: string; displayName?: string; stampCount?: number; skipCooldown?: boolean },
  signal?: AbortSignal
): Promise<SimulateAck> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/simulate/checkin`, {
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

let sessionCsrfToken: string | null = (
  (typeof process !== "undefined" && process.env?.NODE_ENV === "test") ||
  (typeof import.meta !== "undefined" && import.meta.env?.MODE === "test")
) ? "true" : null;
let csrfPromise: Promise<string> | null = null;

async function getSessionCsrfToken(): Promise<string> {
  if (sessionCsrfToken) return sessionCsrfToken;
  if (!csrfPromise) {
    csrfPromise = window.fetch(`${apiBaseUrl}/api/overlay/csrf-token`)
      .then(async res => {
        if (!res.ok) {
          throw new Error(`Failed to fetch CSRF token: ${res.status}`);
        }
        const data = await res.json() as { token: string };
        sessionCsrfToken = data.token;
        return data.token;
      })
      .catch(err => {
        csrfPromise = null;
        throw err;
      });
  }
  return csrfPromise;
}

async function fetchWithCsrf(url: string, options: RequestInit = {}): Promise<Response> {
  const method = options.method?.toUpperCase() || "GET";
  const headers = { ...(options.headers as Record<string, string>) };
  
  const isCsrfRequest = url.endsWith("/api/overlay/csrf-token");
  if (!isCsrfRequest && (method !== "GET" || url.includes("/api/overlay/"))) {
    try {
      const token = await getSessionCsrfToken();
      headers["X-Admin-Csrf"] = token;
    } catch (err) {
      console.error("Failed to inject CSRF token", err);
    }
  }
  
  return window.fetch(url, {
    ...options,
    headers
  });
}

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" };
  const response = await fetchWithCsrf(`${apiBaseUrl}${path}`, {
    headers,
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

export interface ConfigValueResponse {
  key: string;
  value: string | null;
}

export async function getTwitchClientId(signal?: AbortSignal): Promise<string> {
  try {
    const res = await getJson<ConfigValueResponse>("/api/config/twitch.client_id", signal);
    return res.value || "";
  } catch {
    return "";
  }
}

export async function saveTwitchClientId(clientId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/config/twitch.client_id`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ value: clientId }),
    signal
  });

  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export interface MemberAuditLog {
  id: string;
  memberId: string;
  occurredAt: string;
  actorKind: string;
  actorId: string | null;
  operation: string;
  beforeJson: string | null;
  afterJson: string | null;
  reason: string;
}

export async function getMemberAuditLogs(
  memberId: string,
  limit?: number,
  offset?: number,
  signal?: AbortSignal
): Promise<MemberAuditLog[]> {
  const params = new URLSearchParams();
  if (limit !== undefined) params.set("limit", String(limit));
  if (offset !== undefined) params.set("offset", String(offset));
  const search = params.toString();
  const path = search
    ? `/api/members/${encodeURIComponent(memberId)}/audit?${search}`
    : `/api/members/${encodeURIComponent(memberId)}/audit`;
  return getJson<MemberAuditLog[]>(path, signal);
}

export async function adjustMemberLoyalty(
  memberId: string,
  etag: string,
  body: { totalLoyalty?: number; checkInCount?: number; reason: string },
  signal?: AbortSignal
): Promise<void> {
  const formattedEtag = etag.startsWith('"') ? etag : `"${etag}"`;
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/members/${encodeURIComponent(memberId)}/loyalty`, {
    method: "PATCH",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "If-Match": formattedEtag
    },
    body: JSON.stringify(body),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export async function resetMemberLoyalty(
  memberId: string,
  etag: string,
  body: { resetLoyalty: boolean; resetCheckIn: boolean; reason: string },
  signal?: AbortSignal
): Promise<void> {
  const formattedEtag = etag.startsWith('"') ? etag : `"${etag}"`;
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/members/${encodeURIComponent(memberId)}/reset`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "If-Match": formattedEtag
    },
    body: JSON.stringify(body),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}

export interface DeleteTokenResponse {
  token: string;
}

export async function generateDeleteToken(
  memberId: string,
  signal?: AbortSignal
): Promise<string> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/members/${encodeURIComponent(memberId)}/delete-token`, {
    method: "POST",
    headers: {
      Accept: "application/json"
    },
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
  const res = await response.json() as DeleteTokenResponse;
  return res.token;
}

export async function deleteMemberWithToken(
  memberId: string,
  token: string,
  reason: string,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetchWithCsrf(`${apiBaseUrl}/api/members/${encodeURIComponent(memberId)}`, {
    method: "DELETE",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ token, reason }),
    signal
  });
  if (!response.ok) {
    throw new ApiError(response.status, await safeReadBody(response));
  }
}


