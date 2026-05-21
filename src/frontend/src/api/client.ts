export interface HealthResponse {
  status: string;
}

export interface TwitchAuthStatusResponse {
  clientIdConfigured: boolean;
  clientSecretConfigured: boolean;
  hasRefreshToken: boolean;
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
}
