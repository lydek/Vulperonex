import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import { storeToRefs } from "pinia";
import { computed, onScopeDispose, ref } from "vue";
import { apiBaseUrl } from "@/api/client";
import { useEventStore, type StreamEventEnvelope } from "@/stores/eventStore";

export interface StreamEventsOptions {
  connection?: StreamEventsConnection;
}

export interface StreamEventsConnection {
  state: HubConnectionState;
  on(name: "event", handler: (payload: StreamEventEnvelope) => void): void;
  onclose(handler: (error?: Error) => void): void;
  onreconnected(handler: () => void): void;
  onreconnecting(handler: () => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

export function useStreamEvents(options: StreamEventsOptions = {}) {
  const store = useEventStore();
  const { events } = storeToRefs(store);
  const connection = options.connection ?? new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/events`)
    .withAutomaticReconnect()
    .build();
  const error = ref<string | null>(null);
  const state = ref(connection.state);

  connection.on("event", (envelope: StreamEventEnvelope) => {
    store.upsertEvent(envelope);
  });

  connection.onreconnected(() => {
    state.value = connection.state;
  });
  connection.onreconnecting(() => {
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
    events,
    state: computed(() => state.value),
    error: computed(() => error.value),
    start,
    stop
  };
}
