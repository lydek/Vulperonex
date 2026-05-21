import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import { computed, onScopeDispose, ref } from "vue";
import { apiBaseUrl } from "@/api/client";

export const OVERLAY_BUFFER_SIZE = 20;

export type OverlayHubName = "chat" | "alerts" | "member";

export interface OverlayHubEvent {
  eventId?: string;
  sentAt?: string;
  displayName?: string;
  eventType?: string;
  segments?: Array<{ kind: string; text: string }>;
}

export interface OverlayHubOptions {
  connection?: OverlayHubConnection;
}

export interface OverlayHubConnection {
  state: HubConnectionState;
  on(name: "event", handler: (payload: OverlayHubEvent) => void): void;
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
    events.value = [payload, ...events.value].slice(0, OVERLAY_BUFFER_SIZE);
    lastEventAt.value = Date.now();
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

  onScopeDispose(() => {
    void stop();
  });

  return {
    events: computed(() => events.value),
    error: computed(() => error.value),
    state: computed(() => state.value),
    lastEventAt: computed(() => lastEventAt.value),
    start,
    stop
  };
}
