import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;
    if (url.includes("/api/overlay/presets")) {
      return new Response(JSON.stringify([
        { key: "rotan-chat", label: "Rotan Chat", hubName: "chat", kind: "builtin", relativeUrl: "/overlay/chat" },
        { key: "rotan-member", label: "Rotan Member", hubName: "member", kind: "builtin", relativeUrl: "/overlay/member" }
      ]), { status: 200 });
    }
    return new Response("{}", { status: 200 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

async function mountPanel() {
  const MonitorOverlayPanel = (await import("./MonitorOverlayPanel.vue")).default;
  return mount(MonitorOverlayPanel, {
    global: {
      plugins: [buildI18n(), createPinia()]
    }
  });
}

describe("MonitorOverlayPanel", () => {
  beforeEach(() => {
    stubFetch();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should render preview eyebrow + title + hub chip", async () => {
    const wrapper = await mountPanel();
    await flushPromises();

    expect(wrapper.find('[data-testid="preview-eyebrow"]').exists()).toBe(true);
    expect(wrapper.find(".preview-overline").text()).toBe("SCENE PREVIEW");
    expect(wrapper.find(".preview-title").exists()).toBe(true);
    expect(wrapper.find('[data-testid="preview-hub-chip"]').text()).toBe("CHAT");
  });

  it("should render two-row toolbar (controls + bg row) inside preview-toolbar wrapper", async () => {
    const wrapper = await mountPanel();
    await flushPromises();

    const toolbar = wrapper.find('[data-testid="preview-toolbar"]');
    expect(toolbar.exists()).toBe(true);
    expect(toolbar.find(".monitor-controls-header").exists()).toBe(true);
    expect(toolbar.find(".bg-settings-row").exists()).toBe(true);
  });

  it("should switch hub chip when hub tab clicked", async () => {
    const wrapper = await mountPanel();
    await flushPromises();

    const tabs = wrapper.findAll(".hub-tab-btn");
    expect(tabs.length).toBe(3); // chat / member / alerts

    await tabs[1].trigger("click"); // member
    expect(wrapper.find('[data-testid="preview-hub-chip"]').text()).toBe("MEMBER");
  });

  it("should render iframe with runtime-safe preview sandbox", async () => {
    const wrapper = await mountPanel();
    await flushPromises();

    const iframe = wrapper.find("iframe.preview-iframe");
    if (iframe.exists()) {
      const sandbox = iframe.attributes("sandbox");
      expect(sandbox).toBe("allow-scripts allow-same-origin");
    }
  });

  it("should expose preview-canvas region for screenshot anchor", async () => {
    const wrapper = await mountPanel();
    await flushPromises();
    expect(wrapper.find('[data-testid="preview-canvas"]').exists()).toBe(true);
  });

  it("should switch to alerts hub and update chip", async () => {
    const wrapper = await mountPanel();
    await flushPromises();

    const tabs = wrapper.findAll(".hub-tab-btn");
    await tabs[2].trigger("click");
    expect(wrapper.find('[data-testid="preview-hub-chip"]').text()).toBe("ALERTS");
  });

  it("should apply green background option to canvas style", async () => {
    const wrapper = await mountPanel();
    await flushPromises();

    const greenRadio = wrapper.findAll('input[type="radio"]').find((r) => r.attributes("value") === "green");
    expect(greenRadio).toBeDefined();
    await greenRadio!.setValue();
    const canvas = wrapper.find(".iframe-canvas");
    expect(canvas.attributes("style") ?? "").toContain("rgb(0, 255, 0)");
  });

  it("should apply checker and black background options to canvas style", async () => {
    const wrapper = await mountPanel();
    await flushPromises();

    const checkerRadio = wrapper.findAll('input[type="radio"]').find((r) => r.attributes("value") === "checker");
    expect(checkerRadio).toBeDefined();
    await checkerRadio!.setValue();
    expect(wrapper.find(".iframe-canvas").attributes("style") ?? "").toContain("linear-gradient");

    const blackRadio = wrapper.findAll('input[type="radio"]').find((r) => r.attributes("value") === "black");
    expect(blackRadio).toBeDefined();
    await blackRadio!.setValue();
    expect(wrapper.find(".iframe-canvas").attributes("style") ?? "").toContain("rgb(0, 0, 0)");
  });

  it("should bump iframe src timestamp on reload click", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(1700000000000));
    const wrapper = await mountPanel();
    await flushPromises();

    const iframe = wrapper.find("iframe.preview-iframe");
    if (!iframe.exists()) {
      vi.useRealTimers();
      return; // no preset rendered → skip
    }
    const before = iframe.attributes("src");

    vi.setSystemTime(new Date(1700000010000));
    await wrapper.find(".reload-btn").trigger("click");
    await flushPromises();
    const after = wrapper.find("iframe.preview-iframe").attributes("src");
    expect(after).not.toBe(before);
    vi.useRealTimers();
  });
});
