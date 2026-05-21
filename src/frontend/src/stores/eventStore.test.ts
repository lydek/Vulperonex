import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it } from "vitest";
import { useEventStore } from "./eventStore";

describe("useEventStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("should keep newest event when duplicate event id arrives", () => {
    const store = useEventStore();

    store.upsertEvent({
      type: "chat.message",
      eventId: "evt-1",
      platform: "simulation",
      occurredAt: "2026-05-21T10:00:00.000Z"
    });
    store.upsertEvent({
      type: "chat.message",
      eventId: "evt-1",
      platform: "simulation",
      occurredAt: "2026-05-21T10:01:00.000Z"
    });

    expect(store.eventsById["evt-1"].occurredAt).toBe("2026-05-21T10:01:00.000Z");
    expect(store.events).toHaveLength(1);
  });

  it("should ignore older event when duplicate event id arrives", () => {
    const store = useEventStore();

    store.upsertEvent({
      type: "chat.message",
      eventId: "evt-1",
      platform: "simulation",
      timestamp: "2026-05-21T10:01:00.000Z"
    });
    store.upsertEvent({
      type: "chat.message",
      eventId: "evt-1",
      platform: "simulation",
      timestamp: "2026-05-21T09:59:00.000Z"
    });

    expect(store.eventsById["evt-1"].timestamp).toBe("2026-05-21T10:01:00.000Z");
  });
});
