import { HubConnectionState } from "@microsoft/signalr";
import { describe, expect, it } from "vitest";
import { effectScope } from "vue";
import { useOverlayHub, type OverlayHubEvent } from "./useOverlayHub";

describe("useOverlayHub", () => {
  it("should prepend overlay events when hub payload arrives", async () => {
    const connection = new FakeHubConnection();
    const scope = effectScope();

    const hub = scope.run(() => useOverlayHub("chat", { connection }))!;
    connection.emit("event", { eventId: "evt-1", displayName: "First" });
    connection.emit("event", { eventId: "evt-2", displayName: "Second" });

    expect(hub.events.value.map((event) => event.eventId)).toEqual(["evt-2", "evt-1"]);
    scope.stop();
  });

  it("should mark state connected and record lastEventAt when start succeeds and event arrives", async () => {
    const connection = new FakeHubConnection();
    const scope = effectScope();

    const hub = scope.run(() => useOverlayHub("alerts", { connection }))!;
    await hub.start();
    connection.emit("event", { eventId: "evt-1", displayName: "First" });

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
  private handlers = new Map<string, Array<(payload: OverlayHubEvent) => void>>();
  private closeHandlers: Array<(error?: Error) => void> = [];
  private readonly failStart: boolean;

  public constructor(options: { failStart?: boolean } = {}) {
    this.failStart = options.failStart ?? false;
  }

  public on(name: string, handler: (payload: OverlayHubEvent) => void): void {
    this.handlers.set(name, [...(this.handlers.get(name) ?? []), handler]);
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

  public emit(name: string, payload: OverlayHubEvent): void {
    for (const handler of this.handlers.get(name) ?? []) {
      handler(payload);
    }
  }
}
