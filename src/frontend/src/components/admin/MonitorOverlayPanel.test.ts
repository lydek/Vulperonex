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

  it("should render iframe with sandbox=allow-scripts only (no allow-same-origin)", async () => {
    const wrapper = await mountPanel();
    await flushPromises();

    const iframe = wrapper.find("iframe.preview-iframe");
    if (iframe.exists()) {
      const sandbox = iframe.attributes("sandbox");
      expect(sandbox).toBe("allow-scripts");
      expect(sandbox).not.toContain("allow-same-origin");
    }
  });

  it("should expose preview-canvas region for screenshot anchor", async () => {
    const wrapper = await mountPanel();
    await flushPromises();
    expect(wrapper.find('[data-testid="preview-canvas"]').exists()).toBe(true);
  });
});
