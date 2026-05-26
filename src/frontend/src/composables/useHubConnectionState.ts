import { onScopeDispose, ref, type Ref } from "vue";
import { HubConnectionState } from "@microsoft/signalr";

/**
 * Three-layer SignalR connection state pattern.
 *
 * L1 — Passive callbacks (onclose/onreconnecting/onreconnected)
 * L2 — Manual reconnect with incremental backoff (caller-triggered only)
 * L3 — Defensive 30s polling (read-only sync, NEVER calls start())
 *
 * Hard rules enforced by this composable:
 *  - L3 interval only reads connection.state — no network, no start()
 *  - manualReconnect uses re-entry guard to prevent timer pile-up
 *  - Failed reconnect does NOT auto-schedule next attempt
 *  - All timers cleaned up in onUnmounted + onScopeDispose
 *
 * @see docs/specs/monitor-dashboard-redesign-plan.md "SignalR Connection State Pattern"
 */

export const POLL_INTERVAL_MS = 30_000;
export const RECONNECT_BACKOFF_MS = [0, 2_000, 10_000, 30_000, 60_000];

/** Minimal connection contract — compatible with @microsoft/signalr HubConnection. */
export interface HubConnectionLike {
  readonly state: HubConnectionState;
  onclose(handler: (error?: Error) => void): void;
  onreconnecting?(handler: (error?: Error) => void): void;
  onreconnected?(handler: (connectionId?: string) => void): void;
  start(): Promise<void>;
}

export interface HubConnectionStateApi {
  state: Ref<HubConnectionState>;
  lastChangedAt: Ref<number>;
  reconnectAttempt: Ref<number>;
  manualReconnect: () => Promise<void>;
  /** Test/internal hook — call to force a polling sync immediately. */
  syncNow: () => void;
}

export function useHubConnectionState(connection: HubConnectionLike): HubConnectionStateApi {
  const state = ref<HubConnectionState>(connection.state);
  const lastChangedAt = ref<number>(Date.now());
  const reconnectAttempt = ref<number>(0);
  let pollTimer: ReturnType<typeof setInterval> | null = null;
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  let visibilityListener: (() => void) | null = null;
  let disposed = false;

  function syncFromConnection(): void {
    if (disposed) return;
    const next = connection.state;
    if (next !== state.value) {
      state.value = next;
      lastChangedAt.value = Date.now();
      if (next === HubConnectionState.Connected) {
        reconnectAttempt.value = 0;
      }
    }
  }

  // L1 — passive
  connection.onclose(() => syncFromConnection());
  connection.onreconnecting?.(() => syncFromConnection());
  connection.onreconnected?.(() => syncFromConnection());

  // Start eagerly so detached effect-scope callers (tests, app-shell
  // singletons) get the same L3 polling behavior as component callers.
  if (typeof document === "undefined" || !document.hidden) {
    startPolling();
  }

  if (typeof document !== "undefined") {
    visibilityListener = onVisibilityChange;
    document.addEventListener("visibilitychange", visibilityListener);
  }

  // L2 — manual reconnect, caller-triggered only
  async function manualReconnect(): Promise<void> {
    if (disposed) return;
    if (connection.state !== HubConnectionState.Disconnected) return;
    if (reconnectTimer !== null) return; // re-entry guard

    const idx = Math.min(reconnectAttempt.value, RECONNECT_BACKOFF_MS.length - 1);
    const delay = RECONNECT_BACKOFF_MS[idx];

    return new Promise<void>((resolve) => {
      reconnectTimer = setTimeout(async () => {
        reconnectTimer = null;
        if (disposed) return resolve();
        try {
          await connection.start();
          syncFromConnection();
        } catch {
          reconnectAttempt.value += 1;
          // intentional: do NOT auto-schedule next attempt
          syncFromConnection();
        } finally {
          resolve();
        }
      }, delay);
    });
  }

  // L3 — defensive poll (read-only)
  function startPolling(): void {
    if (pollTimer !== null) return;
    pollTimer = setInterval(syncFromConnection, POLL_INTERVAL_MS);
  }

  function stopPolling(): void {
    if (pollTimer !== null) {
      clearInterval(pollTimer);
      pollTimer = null;
    }
  }

  function stopReconnect(): void {
    if (reconnectTimer !== null) {
      clearTimeout(reconnectTimer);
      reconnectTimer = null;
    }
  }

  function onVisibilityChange(): void {
    if (disposed) return;
    if (typeof document === "undefined") return;
    if (document.hidden) {
      // Tab backgrounded — pause L3 polling to skip wasted ref reads.
      // Callbacks (L1) still fire if browser delivers them, but most
      // engines throttle background timers anyway.
      stopPolling();
    } else {
      // Returning to foreground — sync immediately then resume L3 cadence.
      syncFromConnection();
      startPolling();
      if (connection.state === HubConnectionState.Disconnected) {
        void manualReconnect();
      }
    }
  }

  function detachVisibility(): void {
    if (typeof document === "undefined") return;
    if (!visibilityListener) return;
    document.removeEventListener("visibilitychange", visibilityListener);
    visibilityListener = null;
  }

  function dispose(): void {
    if (disposed) return;
    disposed = true;
    stopPolling();
    stopReconnect();
    detachVisibility();
  }

  onScopeDispose(dispose);

  return {
    state,
    lastChangedAt,
    reconnectAttempt,
    manualReconnect,
    syncNow: syncFromConnection
  };
}
