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

export interface WorkflowRuleDetail extends WorkflowRuleSummary {
  conditions: unknown[];
  actions: unknown[];
  executionMode: string;
  maxParallelism: number;
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
