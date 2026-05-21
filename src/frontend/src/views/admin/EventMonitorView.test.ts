import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { computed, ref } from "vue";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import { useEventStore } from "@/stores/eventStore";

vi.mock("@/composables/useStreamEvents", () => {
  const useStreamEvents = () => {
    const store = useEventStore();
    return {
      events: computed(() => store.events),
      state: ref(HubConnectionState.Connected),
      error: ref(null),
      start: async () => {},
      stop: async () => {}
    };
  };
  return { useStreamEvents };
});

const { default: EventMonitorView } = await import("./EventMonitorView.vue");

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function mountView(pinia: Pinia) {
  return mount(EventMonitorView, {
    global: {
      plugins: [buildI18n(), pinia]
    }
  });
}

describe("EventMonitorView", () => {
  let pinia: Pinia;

  beforeEach(() => {
    pinia = createPinia();
    setActivePinia(pinia);
  });

  afterEach(() => {
    setActivePinia(undefined as unknown as Pinia);
  });

  it("should render empty placeholder when event store has no events", async () => {
    const wrapper = mountView(pinia);
    await flushPromises();

    expect(wrapper.find('[data-testid="monitor-empty"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="monitor-table"]').exists()).toBe(false);
  });

  it("should render envelope rows sorted by occurredAt desc when store has events", async () => {
    const wrapper = mountView(pinia);
    const store = useEventStore(pinia);
    store.upsertEvent({ type: "user.message", eventId: "evt-1", platform: "twitch", occurredAt: "2026-05-22T00:00:00Z" });
    store.upsertEvent({ type: "user.followed", eventId: "evt-2", platform: "simulation", occurredAt: "2026-05-22T00:01:00Z" });
    await flushPromises();

    const rows = wrapper.findAll('[data-testid="monitor-row"]');
    expect(rows).toHaveLength(2);
    expect(rows[0].text()).toContain("evt-2");
    expect(rows[1].text()).toContain("evt-1");
  });

  it("should dedupe by eventId when upsert is called twice with same id", async () => {
    const wrapper = mountView(pinia);
    const store = useEventStore(pinia);
    store.upsertEvent({ type: "user.message", eventId: "evt-1", platform: "simulation", occurredAt: "2026-05-22T00:00:00Z" });
    store.upsertEvent({ type: "user.message", eventId: "evt-1", platform: "simulation", occurredAt: "2026-05-22T00:00:01Z" });
    await flushPromises();

    expect(wrapper.findAll('[data-testid="monitor-row"]')).toHaveLength(1);
  });
});
