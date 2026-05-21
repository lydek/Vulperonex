import { HubConnectionState } from "@microsoft/signalr";
import { afterEach, describe, expect, it, vi } from "vitest";
import { effectScope } from "vue";
import {
  useOverlayHub,
  type OverlayClearedPayload,
  type OverlayHubEvent
} from "./useOverlayHub";

describe("useOverlayHub", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should prepend overlay events when hub payload arrives", () => {
    const connection = new FakeHubConnection();
    const scope = effectScope();

    const hub = scope.run(() => useOverlayHub("chat", { connection }))!;
    connection.emitEvent({ eventId: "evt-1", displayName: "First" });
    connection.emitEvent({ eventId: "evt-2", displayName: "Second" });

    expect(hub.events.value.map((event) => event.eventId)).toEqual(["evt-2", "evt-1"]);
    scope.stop();
  });

  it("should dedupe events with same eventId when replay precedes live emit", () => {
    const connection = new FakeHubConnection();
    const scope = effectScope();

    const hub = scope.run(() => useOverlayHub("chat", { connection }))!;
    connection.emitEvent({ eventId: "evt-1", displayName: "Replayed", replayed: true });
    connection.emitEvent({ eventId: "evt-1", displayName: "Live" });
    connection.emitEvent({ eventId: "evt-2", displayName: "Other" });

    const ids = hub.events.value.map((event) => event.eventId);
    expect(ids).toEqual(["evt-2", "evt-1"]);
    expect(hub.events.value.find((event) => event.eventId === "evt-1")?.displayName).toBe("Live");
    scope.stop();
  });

  it("should reset events when cleared payload arrives", () => {
    const connection = new FakeHubConnection();
    const scope = effectScope();

    const hub = scope.run(() => useOverlayHub("alerts", { connection }))!;
    connection.emitEvent({ eventId: "evt-1", displayName: "First" });
    connection.emitCleared({ hubName: "alerts" });

    expect(hub.events.value).toHaveLength(0);
    scope.stop();
  });

  it("should not update lastEventAt when replayed event arrives", () => {
    const connection = new FakeHubConnection();
    const scope = effectScope();

    const hub = scope.run(() => useOverlayHub("alerts", { connection }))!;
    connection.emitEvent({ eventId: "evt-1", displayName: "Replayed", replayed: true });

    expect(hub.lastEventAt.value).toBeNull();
    scope.stop();
  });

  it("should call clear endpoint via fetch when clear() is invoked", async () => {
    const connection = new FakeHubConnection();
    const scope = effectScope();
    const fetchMock = vi.fn(async () => new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    const hub = scope.run(() => useOverlayHub("chat", { connection }))!;
    await hub.clear();

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/overlay/chat/messages",
      expect.objectContaining({ method: "DELETE" })
    );
    scope.stop();
  });

  it("should mark state connected and record lastEventAt when start succeeds and live event arrives", async () => {
    const connection = new FakeHubConnection();
    const scope = effectScope();

    const hub = scope.run(() => useOverlayHub("alerts", { connection }))!;
    await hub.start();
    connection.emitEvent({ eventId: "evt-1", displayName: "First" });

    expect(hub.state.value).toBe(HubConnectionState.Connected);
    expect(hub.lastEventAt.value).not.toBeNull();
    scope.stop();
  });

  it("should expose error and disconnected state when start rejects", async () => {
    const connection = new FakeHubConnection({ failStart: true });
    const scope = effectScope();

    const hub = scope.run(() => useOverlayHub("chat", { connection }))!;
    await hub.start();

    expect(hub.state.value).toBe(HubConnectionState.Disconnected);
    expect(hub.error.value).toBe("boom");
    scope.stop();
  });
});

class FakeHubConnection {
  public state: HubConnectionState = HubConnectionState.Disconnected;
  private eventHandlers: Array<(payload: OverlayHubEvent) => void> = [];
  private clearedHandlers: Array<(payload: OverlayClearedPayload) => void> = [];
  private closeHandlers: Array<(error?: Error) => void> = [];
  private readonly failStart: boolean;

  public constructor(options: { failStart?: boolean } = {}) {
    this.failStart = options.failStart ?? false;
  }

  public on(name: "event", handler: (payload: OverlayHubEvent) => void): void;
  public on(name: "cleared", handler: (payload: OverlayClearedPayload) => void): void;
  public on(name: string, handler: ((payload: OverlayHubEvent) => void) | ((payload: OverlayClearedPayload) => void)): void {
    if (name === "event") {
      this.eventHandlers.push(handler as (payload: OverlayHubEvent) => void);
    } else if (name === "cleared") {
      this.clearedHandlers.push(handler as (payload: OverlayClearedPayload) => void);
    }
  }

  public onclose(handler: (error?: Error) => void): void {
    this.closeHandlers.push(handler);
  }

  public onreconnected(): void {
  }

  public onreconnecting(): void {
  }

  public async start(): Promise<void> {
    if (this.failStart) {
      throw new Error("boom");
    }
    this.state = HubConnectionState.Connected;
  }

  public async stop(): Promise<void> {
    this.state = HubConnectionState.Disconnected;
    for (const handler of this.closeHandlers) {
      handler();
    }
  }

  public emitEvent(payload: OverlayHubEvent): void {
    for (const handler of this.eventHandlers) {
      handler(payload);
    }
  }

  public emitCleared(payload: OverlayClearedPayload): void {
    for (const handler of this.clearedHandlers) {
      handler(payload);
    }
  }
}
