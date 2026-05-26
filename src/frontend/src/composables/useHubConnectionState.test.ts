import { effectScope, nextTick } from "vue";
import { HubConnectionState } from "@microsoft/signalr";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  POLL_INTERVAL_MS,
  RECONNECT_BACKOFF_MS,
  useHubConnectionState,
  type HubConnectionLike
} from "./useHubConnectionState";

class FakeHubConnection implements HubConnectionLike {
  state: HubConnectionState = HubConnectionState.Disconnected;
  startMock = vi.fn(async () => {
    this.state = HubConnectionState.Connected;
  });
  private closeHandlers: Array<(err?: Error) => void> = [];
  private reconnectingHandlers: Array<(err?: Error) => void> = [];
  private reconnectedHandlers: Array<(id?: string) => void> = [];

  onclose(handler: (err?: Error) => void): void {
    this.closeHandlers.push(handler);
  }
  onreconnecting(handler: (err?: Error) => void): void {
    this.reconnectingHandlers.push(handler);
  }
  onreconnected(handler: (id?: string) => void): void {
    this.reconnectedHandlers.push(handler);
  }
  async start(): Promise<void> {
    await this.startMock();
  }
  emitClose(): void {
    this.state = HubConnectionState.Disconnected;
    this.closeHandlers.forEach((h) => h());
  }
  emitReconnecting(): void {
    this.state = HubConnectionState.Reconnecting;
    this.reconnectingHandlers.forEach((h) => h());
  }
  emitReconnected(): void {
    this.state = HubConnectionState.Connected;
    this.reconnectedHandlers.forEach((h) => h());
  }
}

describe("useHubConnectionState", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("initial state mirrors connection.state", () => {
    const connection = new FakeHubConnection();
    connection.state = HubConnectionState.Connected;
    const scope = effectScope();
    const api = scope.run(() => useHubConnectionState(connection))!;
    expect(api.state.value).toBe(HubConnectionState.Connected);
    scope.stop();
  });

  it("L1 callback: onclose flips ref to Disconnected", async () => {
    const connection = new FakeHubConnection();
    connection.state = HubConnectionState.Connected;
    const scope = effectScope();
    const api = scope.run(() => useHubConnectionState(connection))!;
    expect(api.state.value).toBe(HubConnectionState.Connected);

    connection.emitClose();
    await nextTick();
    expect(api.state.value).toBe(HubConnectionState.Disconnected);
    scope.stop();
  });

  it("L3 polling: picks up state when callbacks missed", async () => {
    const connection = new FakeHubConnection();
    connection.state = HubConnectionState.Connected;
    const scope = effectScope();
    const api = scope.run(() => useHubConnectionState(connection))!;

    // Force-attach poll loop (onMounted not triggered without component)
    // — use syncNow as proxy after silent state mutation
    connection.state = HubConnectionState.Disconnected;
    expect(api.state.value).toBe(HubConnectionState.Connected); // not yet synced

    api.syncNow();
    expect(api.state.value).toBe(HubConnectionState.Disconnected);
    scope.stop();
  });

  it("manualReconnect no-op when not Disconnected", async () => {
    const connection = new FakeHubConnection();
    connection.state = HubConnectionState.Connected;
    const scope = effectScope();
    const api = scope.run(() => useHubConnectionState(connection))!;

    await api.manualReconnect();
    expect(connection.startMock).not.toHaveBeenCalled();
    scope.stop();
  });

  it("manualReconnect re-entry guard prevents pile-up", async () => {
    const connection = new FakeHubConnection();
    connection.state = HubConnectionState.Disconnected;
    // slow start
    connection.startMock.mockImplementation(async () => {
      await new Promise((resolve) => setTimeout(resolve, 5_000));
      connection.state = HubConnectionState.Connected;
    });
    const scope = effectScope();
    const api = scope.run(() => useHubConnectionState(connection))!;

    const p1 = api.manualReconnect();
    const p2 = api.manualReconnect(); // should be no-op while p1 pending

    await vi.advanceTimersByTimeAsync(RECONNECT_BACKOFF_MS[0] + 5_100);
    await Promise.all([p1, p2]);

    expect(connection.startMock).toHaveBeenCalledTimes(1);
    scope.stop();
  });

  it("successful reconnect resets reconnectAttempt", async () => {
    const connection = new FakeHubConnection();
    connection.state = HubConnectionState.Disconnected;
    const scope = effectScope();
    const api = scope.run(() => useHubConnectionState(connection))!;

    // simulate prior failed attempts
    api.reconnectAttempt.value = 3;

    const p = api.manualReconnect();
    await vi.advanceTimersByTimeAsync(RECONNECT_BACKOFF_MS[3] + 100);
    await p;

    expect(api.state.value).toBe(HubConnectionState.Connected);
    expect(api.reconnectAttempt.value).toBe(0);
    scope.stop();
  });

  it("failed reconnect increments counter and does NOT auto-reschedule", async () => {
    const connection = new FakeHubConnection();
    connection.state = HubConnectionState.Disconnected;
    connection.startMock.mockRejectedValue(new Error("boom"));
    const scope = effectScope();
    const api = scope.run(() => useHubConnectionState(connection))!;

    const p = api.manualReconnect();
    await vi.advanceTimersByTimeAsync(RECONNECT_BACKOFF_MS[0] + 100);
    await p;

    expect(api.reconnectAttempt.value).toBe(1);
    // advance far beyond — no auto-reschedule should fire
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 2);
    expect(connection.startMock).toHaveBeenCalledTimes(1);
    scope.stop();
  });

  it("scope dispose cleans timers (no further state writes)", async () => {
    const connection = new FakeHubConnection();
    connection.state = HubConnectionState.Connected;
    const scope = effectScope();
    const api = scope.run(() => useHubConnectionState(connection))!;

    scope.stop();
    // After dispose, state mutations on the connection should not leak through poll
    connection.state = HubConnectionState.Disconnected;
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 2);

    // syncNow after dispose should be no-op
    api.syncNow();
    expect(api.state.value).toBe(HubConnectionState.Connected);
  });
});
