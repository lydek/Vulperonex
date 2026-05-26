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

function setWindowWidth(w: number): void {
  Object.defineProperty(window, "innerWidth", { value: w, configurable: true, writable: true });
}

describe("MonitorDashboardView", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("should render three-panel desktop layout at wide breakpoint", async () => {
    setWindowWidth(1440);
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
    expect(wrapper.find('[data-testid="status-chip"]').exists()).toBe(true);
    expect(wrapper.text()).toContain("HEALTHY");
  });

  it("should render eyebrow + glass header", async () => {
    setWindowWidth(1440);
    const MonitorDashboardView = await importView();

    const wrapper = mount(MonitorDashboardView, {
      global: { plugins: [buildI18n(), createPinia()] }
    });
    await flushPromises();

    expect(wrapper.find(".dashboard-header.glass").exists()).toBe(true);
    expect(wrapper.find(".dashboard-eyebrow").text()).toBe("LIVE PREVIEW");
    expect(wrapper.find(".dashboard-title").exists()).toBe(true);
    expect(wrapper.find(".header-icon").exists()).toBe(true);
  });

  it("should default sider open on wide screen and toggle collapse", async () => {
    setWindowWidth(1440);
    const MonitorDashboardView = await importView();

    const wrapper = mount(MonitorDashboardView, {
      global: { plugins: [buildI18n(), createPinia()] }
    });
    await flushPromises();

    const sider = wrapper.find('[data-testid="controls-sider"]');
    expect(sider.exists()).toBe(true);
    expect(sider.classes()).toContain("open");
    expect(sider.attributes("aria-hidden")).toBe("false");

    const toggle = wrapper.find(".sider-toggle");
    expect(toggle.attributes("aria-expanded")).toBe("true");

    await toggle.trigger("click");
    expect(sider.classes()).not.toContain("open");
    expect(sider.attributes("aria-hidden")).toBe("true");
    expect(toggle.attributes("aria-expanded")).toBe("false");
  });

  it("should open drawer on mobile and close it after simulate emit", async () => {
    setWindowWidth(768);
    const MonitorDashboardView = await importView();

    const wrapper = mount(MonitorDashboardView, {
      global: {
        plugins: [buildI18n(), createPinia()]
      }
    });
    await flushPromises();

    expect(wrapper.find(".toggle-sim-btn").exists()).toBe(true);
    expect(wrapper.find(".drawer-content").exists()).toBe(false);
    expect(wrapper.find('[data-testid="controls-sider"]').exists()).toBe(false);

    await wrapper.find(".toggle-sim-btn").trigger("click");
    expect(wrapper.find(".drawer-content").exists()).toBe(true);

    await wrapper.findAll('[data-testid="simulate-controls-stub"]')[0].trigger("click");
    await flushPromises();

    expect(wrapper.find(".drawer-content").exists()).toBe(false);
  });

  it("should reflect server health class on chip", async () => {
    setWindowWidth(1440);
    const MonitorDashboardView = await importView();

    const wrapper = mount(MonitorDashboardView, {
      global: { plugins: [buildI18n(), createPinia()] }
    });
    await flushPromises();

    const chip = wrapper.find('[data-testid="status-chip"]');
    expect(chip.classes()).toContain("healthy");
  });

  it("should expose role=status on chip and role=dialog on drawer", async () => {
    setWindowWidth(768);
    const MonitorDashboardView = await importView();

    const wrapper = mount(MonitorDashboardView, {
      global: { plugins: [buildI18n(), createPinia()] }
    });
    await flushPromises();

    expect(wrapper.find('[data-testid="status-chip"]').attributes("role")).toBe("status");

    await wrapper.find(".sider-toggle").trigger("click");
    expect(wrapper.find(".drawer-content").attributes("role")).toBe("dialog");
  });

  it("should support i18n zh-TW switch", async () => {
    setWindowWidth(1440);
    const MonitorDashboardView = await importView();
    const i18n = buildI18n();
    i18n.global.locale.value = "zh-TW";

    const wrapper = mount(MonitorDashboardView, {
      global: { plugins: [i18n, createPinia()] }
    });
    await flushPromises();

    expect(wrapper.find(".dashboard-title").text()).toBe("即時狀態中心");
    expect(wrapper.text()).toContain("HEALTHY");
  });

  it("should close drawer on Escape keydown", async () => {
    setWindowWidth(768);
    const MonitorDashboardView = await importView();

    const wrapper = mount(MonitorDashboardView, {
      attachTo: document.body,
      global: { plugins: [buildI18n(), createPinia()] }
    });
    await flushPromises();

    await wrapper.find(".sider-toggle").trigger("click");
    expect(wrapper.find(".drawer-content").exists()).toBe(true);

    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
    await flushPromises();

    expect(wrapper.find(".drawer-content").exists()).toBe(false);
    wrapper.unmount();
  });

  it("should stop health polling when tab hidden and resume on visibility regain", async () => {
    setWindowWidth(1440);
    const { getHealth } = await import("@/api/client");
    const getHealthMock = getHealth as unknown as ReturnType<typeof vi.fn>;
    getHealthMock.mockClear();

    const MonitorDashboardView = await importView();
    const wrapper = mount(MonitorDashboardView, {
      global: { plugins: [buildI18n(), createPinia()] }
    });
    await flushPromises();
    // Initial check
    expect(getHealthMock).toHaveBeenCalled();
    const initialCalls = getHealthMock.mock.calls.length;

    // Simulate tab hidden — advance 90s, polling should pause
    Object.defineProperty(document, "hidden", { value: true, configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));
    await vi.advanceTimersByTimeAsync(90_000);
    expect(getHealthMock.mock.calls.length).toBe(initialCalls);

    // Simulate tab visible — should fire immediate check
    Object.defineProperty(document, "hidden", { value: false, configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));
    await flushPromises();
    expect(getHealthMock.mock.calls.length).toBeGreaterThan(initialCalls);

    wrapper.unmount();
  });

  it("should preserve sider state when wide and not flap on resize tick", async () => {
    setWindowWidth(1440);
    const MonitorDashboardView = await importView();
    const wrapper = mount(MonitorDashboardView, {
      global: { plugins: [buildI18n(), createPinia()] }
    });
    await flushPromises();

    // Collapse sider while wide
    await wrapper.find(".sider-toggle").trigger("click");
    expect(wrapper.find('[data-testid="controls-sider"]').classes()).not.toContain("open");

    // Simulate continuous resize at same wide width — should not reset open state
    window.dispatchEvent(new Event("resize"));
    await flushPromises();
    // (rAF callback may not fire under fake timers; the key assertion is no regression)
    expect(wrapper.find('[data-testid="controls-sider"]').classes()).not.toContain("open");
  });
});
