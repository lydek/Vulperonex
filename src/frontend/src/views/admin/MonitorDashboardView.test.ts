import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia } from "pinia";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";

vi.mock("@/api/client", () => ({
  getHealth: vi.fn(async () => ({ status: "Healthy" }))
}));

vi.mock("@/components/admin/SimulateControlsPanel.vue", () => ({
  default: {
    name: "SimulateControlsPanelStub",
    template: "<div data-testid='simulate-controls-stub' @click=\"$emit('simulated', { accepted: true })\">simulate</div>"
  }
}));

vi.mock("@/components/admin/MonitorOverlayPanel.vue", () => ({
  default: {
    name: "MonitorOverlayPanelStub",
    template: "<div data-testid='monitor-overlay-stub'>overlay</div>"
  }
}));

vi.mock("@/components/admin/ChatStreamPanel.vue", () => ({
  default: {
    name: "ChatStreamPanelStub",
    template: "<div data-testid='chat-stream-stub'>chat</div>"
  }
}));

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

async function importView() {
  const module = await import("./MonitorDashboardView.vue");
  return module.default;
}

describe("MonitorDashboardView", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("should render three-panel desktop layout", async () => {
    window.innerWidth = 1280;
    const MonitorDashboardView = await importView();

    const wrapper = mount(MonitorDashboardView, {
      global: {
        plugins: [buildI18n(), createPinia()]
      }
    });
    await flushPromises();

    expect(wrapper.find('[data-testid="monitor-dashboard"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="simulate-controls-stub"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="monitor-overlay-stub"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="chat-stream-stub"]').exists()).toBe(true);
    expect(wrapper.text()).toContain("HEALTHY");
  });

  it("should open drawer on mobile and close it after simulate emit", async () => {
    window.innerWidth = 768;
    const MonitorDashboardView = await importView();

    const wrapper = mount(MonitorDashboardView, {
      global: {
        plugins: [buildI18n(), createPinia()]
      }
    });
    await flushPromises();

    expect(wrapper.find(".toggle-sim-btn").exists()).toBe(true);
    expect(wrapper.find(".drawer-content").exists()).toBe(false);

    await wrapper.find(".toggle-sim-btn").trigger("click");
    expect(wrapper.find(".drawer-content").exists()).toBe(true);

    await wrapper.findAll('[data-testid="simulate-controls-stub"]')[0].trigger("click");
    await flushPromises();

    expect(wrapper.find(".drawer-content").exists()).toBe(false);
  });
});
