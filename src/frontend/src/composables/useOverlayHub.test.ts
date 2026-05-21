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
});

class FakeHubConnection {
  private handlers = new Map<string, Array<(payload: OverlayHubEvent) => void>>();

  public on(name: string, handler: (payload: OverlayHubEvent) => void): void {
    this.handlers.set(name, [...(this.handlers.get(name) ?? []), handler]);
  }

  public onclose(): void {
  }

  public async start(): Promise<void> {
  }

  public async stop(): Promise<void> {
  }

  public emit(name: string, payload: OverlayHubEvent): void {
    for (const handler of this.handlers.get(name) ?? []) {
      handler(payload);
    }
  }
}
