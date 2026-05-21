import { HubConnectionBuilder } from "@microsoft/signalr";
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
  on(name: "event", handler: (payload: OverlayHubEvent) => void): void;
  onclose(handler: (error?: Error) => void): void;
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

  connection.on("event", (payload: OverlayHubEvent) => {
    events.value = [payload, ...events.value].slice(0, OVERLAY_BUFFER_SIZE);
  });

  connection.onclose((closeError) => {
    error.value = closeError?.message ?? null;
  });

  async function start(): Promise<void> {
    try {
      error.value = null;
      await connection.start();
    } catch (startError) {
      error.value = startError instanceof Error ? startError.message : String(startError);
    }
  }

  async function stop(): Promise<void> {
    await connection.stop();
  }

  onScopeDispose(() => {
    void stop();
  });

  return {
    events: computed(() => events.value),
    error: computed(() => error.value),
    start,
    stop
  };
}
