import { HubConnectionState } from "@microsoft/signalr";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it } from "vitest";
import { effectScope } from "vue";
import { useEventStore, type StreamEventEnvelope } from "@/stores/eventStore";
import { useStreamEvents } from "./useStreamEvents";

describe("useStreamEvents", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("should update event store when management hub payload arrives", () => {
    const connection = new FakeStreamConnection();
    const scope = effectScope();

    scope.run(() => useStreamEvents({ connection }));
    connection.emit("event", {
      type: "chat.message",
      eventId: "evt-1",
      platform: "simulation",
      occurredAt: "2026-05-21T10:00:00.000Z"
    });

    expect(useEventStore().eventsById["evt-1"].type).toBe("chat.message");
    scope.stop();
  });

  it("should expose connected state after start succeeds", async () => {
    const connection = new FakeStreamConnection();
    const scope = effectScope();
    const stream = scope.run(() => useStreamEvents({ connection }))!;

    await stream.start();

    expect(stream.state.value).toBe(HubConnectionState.Connected);
    scope.stop();
  });
});

class FakeStreamConnection {
  public state = HubConnectionState.Disconnected;
  private handlers = new Map<string, Array<(payload: StreamEventEnvelope) => void>>();
  private closeHandler: ((error?: Error) => void) | null = null;
  private reconnectingHandler: (() => void) | null = null;
  private reconnectedHandler: (() => void) | null = null;

  public on(name: string, handler: (payload: StreamEventEnvelope) => void): void {
    this.handlers.set(name, [...(this.handlers.get(name) ?? []), handler]);
  }

  public onclose(handler: (error?: Error) => void): void {
    this.closeHandler = handler;
  }

  public onreconnecting(handler: () => void): void {
    this.reconnectingHandler = handler;
  }

  public onreconnected(handler: () => void): void {
    this.reconnectedHandler = handler;
  }

  public async start(): Promise<void> {
    this.state = HubConnectionState.Connected;
    this.reconnectedHandler?.();
  }

  public async stop(): Promise<void> {
    this.state = HubConnectionState.Disconnected;
    this.closeHandler?.();
  }

  public emit(name: string, payload: StreamEventEnvelope): void {
    this.reconnectingHandler?.();
    for (const handler of this.handlers.get(name) ?? []) {
      handler(payload);
    }
  }
}
