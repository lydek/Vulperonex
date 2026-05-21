import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import SimulateView from "./SimulateView.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function mountView() {
  return mount(SimulateView, {
    global: {
      plugins: [buildI18n(), createPinia()]
    }
  });
}

describe("SimulateView", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should display ack with eventId and platformUserId when api accepts simulate", async () => {
    const ack = {
      accepted: true,
      eventTypeKey: "user.sent_message",
      eventId: "evt-abc",
      platform: "simulation",
      platformUserId: "sim-007",
      displayName: "Sim User",
      occurredAt: "2026-05-21T00:00:00Z"
    };
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify(ack), { status: 202 }))
    );

    const wrapper = mountView();
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    const ackCard = wrapper.find('[data-testid="simulate-ack"]');
    expect(ackCard.exists()).toBe(true);
    expect(wrapper.find('[data-testid="ack-accepted"]').text()).toBe("true");
    expect(wrapper.find('[data-testid="ack-event-id"]').text()).toBe("evt-abc");
    expect(wrapper.find('[data-testid="ack-platform-user-id"]').text()).toBe("sim-007");
    expect(wrapper.find('[data-testid="simulate-error"]').exists()).toBe(false);
  });

  it("should render error code badge when api returns 400 with envelope", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(
        JSON.stringify({ error: "UNKNOWN_SIMULATE_EVENT_TYPE" }),
        { status: 400 }
      ))
    );

    const wrapper = mountView();
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    const errorCard = wrapper.find('[data-testid="simulate-error"]');
    expect(errorCard.exists()).toBe(true);
    expect(errorCard.text()).toContain("UNKNOWN_SIMULATE_EVENT_TYPE");
    expect(wrapper.find('[data-testid="simulate-ack"]').exists()).toBe(false);
  });

  it("should target chosen alias endpoint when alias dropdown is changed to follow", async () => {
    const fetchMock = vi.fn(async () =>
      new Response(
        JSON.stringify({
          accepted: true,
          eventTypeKey: "user.followed",
          eventId: "evt-2",
          platform: "simulation",
          platformUserId: null,
          displayName: null,
          occurredAt: "2026-05-21T00:00:00Z"
        }),
        { status: 202 }
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await wrapper.find("select").setValue("follow");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/simulate/follow",
      expect.objectContaining({ method: "POST" })
    );
  });
});
