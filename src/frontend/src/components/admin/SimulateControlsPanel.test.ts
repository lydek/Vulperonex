import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import SimulateControlsPanel from "./SimulateControlsPanel.vue";

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
  return mount(SimulateControlsPanel, {
    global: {
      plugins: [buildI18n(), createPinia()]
    }
  });
}

function badgesEmptyResponse(): Response {
  return new Response(JSON.stringify({ ready: false, global: [], channel: [] }), { status: 200 });
}

function routeFetch(simulateResponse: () => Response) {
  return vi.fn(async (input: RequestInfo | URL) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;
    if (url.includes("/api/twitch/badges")) return badgesEmptyResponse();
    return simulateResponse();
  });
}

describe("SimulateControlsPanel", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", routeFetch(() => new Response("{}", { status: 200 })));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("should render primary sections without test-mode controls", async () => {
    const wrapper = mountView();
    await flushPromises();

    expect(wrapper.find('[data-testid="section-test-mode"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="test-mode-toggle"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="section-event-type"]').exists()).toBe(true);
    // alias defaults to chat → full identity + event-fields visible
    expect(wrapper.find('[data-testid="section-identity"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="section-event-fields"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="section-batch"]').exists()).toBe(false);
  });

  it("should show batch section only when alias is checkin", async () => {
    const wrapper = mountView();
    await flushPromises();

    expect(wrapper.find('[data-testid="section-batch"]').exists()).toBe(false);

    const aliasSelect = wrapper.findAll("select")[0];
    await aliasSelect.setValue("checkin");
    expect(wrapper.find('[data-testid="section-batch"]').exists()).toBe(true);
    // compact identity when checkin
    expect(wrapper.find('[data-testid="section-identity-compact"]').exists()).toBe(true);
  });

  it("should render PrimeVue ProgressBar with ARIA when batch running", async () => {
    const fetchMock = routeFetch(() => new Response(JSON.stringify({
      accepted: true, eventTypeKey: "system.member.checked_in", eventId: "evt-batch-aria",
      platform: "simulation", platformUserId: "batch-user", displayName: "Batch User",
      occurredAt: "2026-05-24T00:00:00Z"
    }), { status: 202 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();
    const selects = wrapper.findAll("select");
    await selects[0].setValue("checkin");

    const numberInputs = wrapper.findAll('input[type="number"]');
    await numberInputs[1].setValue("2");
    await wrapper.find('[data-testid="batch-run-btn"]').trigger("click");

    await flushPromises();
    await vi.advanceTimersByTimeAsync(50);
    await flushPromises();

    // First iteration tick — progress visible
    const progressBlock = wrapper.find('[data-testid="batch-progress"]');
    expect(progressBlock.exists()).toBe(true);
    const bar = progressBlock.find(".monitor-progress");
    expect(bar.exists()).toBe(true);

    await vi.runAllTimersAsync();
    await flushPromises();
  });

  it("should post checkin payload to the dedicated endpoint and render simple success", async () => {
    const fetchMock = routeFetch(() => new Response(JSON.stringify({
      accepted: true,
      eventTypeKey: "system.member.checked_in",
      eventId: "evt-checkin-1",
      platform: "simulation",
      platformUserId: "sim-user",
      displayName: "Sim User",
      occurredAt: "2026-05-24T00:00:00Z"
    }), { status: 202 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    const selects = wrapper.findAll("select");

    await selects[0].setValue("checkin");
    await wrapper.find('input[placeholder="e.g. sim-user-id"]').setValue("sim-user");
    await wrapper.find('input[placeholder="e.g. Sim User"]').setValue("Sim User");
    await wrapper.find('input[type="number"]').setValue("3");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/simulate/checkin",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          platformUserId: "sim-user",
          displayName: "Sim User",
          stampCount: 3
        })
      })
    );
    expect(wrapper.find('[data-testid="simulate-ack"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="simulate-success"]').text()).toContain("Simulation sent");
    expect(wrapper.find('[data-testid="simulate-error"]').exists()).toBe(false);
  });

  it("should run batch checkin sequentially and emit latest ack", async () => {
    const fetchMock = routeFetch(() => new Response(JSON.stringify({
      accepted: true,
      eventTypeKey: "system.member.checked_in",
      eventId: "evt-batch",
      platform: "simulation",
      platformUserId: "batch-user",
      displayName: "Batch User",
      occurredAt: "2026-05-24T00:00:00Z"
    }), { status: 202 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    const selects = wrapper.findAll("select");

    await selects[0].setValue("checkin");
    await wrapper.find('input[placeholder="e.g. sim-user-id"]').setValue("batch-user");
    await wrapper.find('input[placeholder="e.g. Sim User"]').setValue("Batch User");

    const numberInputs = wrapper.findAll('input[type="number"]');
    await numberInputs[1].setValue("2");
    await wrapper.find(".batch-button").trigger("click");

    await flushPromises();
    await vi.runAllTimersAsync();
    await flushPromises();

    const simulateCalls = fetchMock.mock.calls.filter((call) => {
      const url = typeof call[0] === "string" ? call[0] : call[0] instanceof URL ? call[0].toString() : (call[0] as Request).url;
      return url.includes("/api/simulate/");
    });
    expect(simulateCalls).toHaveLength(2);
    expect(wrapper.emitted("simulated")).toHaveLength(2);
  });
});
