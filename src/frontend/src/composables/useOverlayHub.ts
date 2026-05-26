import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import { computed, onScopeDispose, ref } from "vue";
import { apiBaseUrl, clearOverlayHistory } from "@/api/client";

export const OVERLAY_BUFFER_SIZE = 20;

export type OverlayHubName = "chat" | "alerts" | "member";

export interface OverlayHubEvent {
  eventId?: string;
  sentAt?: string;
  timestamp?: string;
  displayName?: string;
  eventType?: string;
  segments?: Array<{ kind?: string; text?: string; type?: string; value?: string }>;
  replayed?: boolean;
  colorHex?: string | null;
  badges?: string[];
  roles?: string[];
  avatarUrl?: string | null;
  checkInCount?: number;
  memberSnapshot?: {
    displayName: string;
    avatarUrl?: string | null;
    checkInCount: number;
  } | null;
}

export interface OverlayClearedPayload {
  hubName: OverlayHubName;
}

export interface OverlayHubOptions {
  connection?: OverlayHubConnection;
}

export interface OverlayHubConnection {
  state: HubConnectionState;
  on(name: "event", handler: (payload: OverlayHubEvent) => void): void;
  on(name: "cleared", handler: (payload: OverlayClearedPayload) => void): void;
  onclose(handler: (error?: Error) => void): void;
  onreconnected?(handler: () => void): void;
  onreconnecting?(handler: () => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

export function useOverlayHub(hubName: OverlayHubName, options: OverlayHubOptions = {}) {
  const connection = options.connection ?? new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/overlay/${hubName}`)
    .withAutomaticReconnect()
    .build();
  const events = ref<OverlayHubEvent[]>([]);
  const error = ref<string | null>(null);
  const state = ref<HubConnectionState>(connection.state);
  const lastEventAt = ref<number | null>(null);

  connection.on("event", (payload: OverlayHubEvent) => {
    upsertEvent(payload);
    if (!payload.replayed) {
      lastEventAt.value = Date.now();
    }
  });

  connection.on("cleared", () => {
    events.value = [];
  });

  connection.onreconnected?.(() => {
    state.value = connection.state;
    error.value = null;
  });
  connection.onreconnecting?.(() => {
    state.value = connection.state;
  });
  connection.onclose((closeError) => {
    state.value = connection.state;
    error.value = closeError?.message ?? null;
  });

  function upsertEvent(payload: OverlayHubEvent): void {
    const eventId = payload.eventId;
    if (eventId) {
      const existingIndex = events.value.findIndex((entry) => entry.eventId === eventId);
      if (existingIndex !== -1) {
        const next = events.value.slice();
        next[existingIndex] = payload;
        events.value = next;
        return;
      }
    }
    events.value = [payload, ...events.value].slice(0, OVERLAY_BUFFER_SIZE);
  }

  async function start(): Promise<void> {
    if (connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    try {
      error.value = null;
      await connection.start();
      state.value = connection.state;
    } catch (startError) {
      error.value = startError instanceof Error ? startError.message : String(startError);
      state.value = connection.state;
    }
  }

  async function stop(): Promise<void> {
    await connection.stop();
    state.value = connection.state;
  }

  async function clear(): Promise<void> {
    await clearOverlayHistory(hubName);
  }

  onScopeDispose(() => {
    void stop();
  });

  return {
    events: computed(() => events.value),
    error: computed(() => error.value),
    state: computed(() => state.value),
    lastEventAt: computed(() => lastEventAt.value),
    connection,
    start,
    stop,
    clear
  };
}
