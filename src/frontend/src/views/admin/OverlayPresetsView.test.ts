import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import OverlayPresetsView from "./OverlayPresetsView.vue";

vi.mock("@/components/admin/OverlayEditorModal.vue", () => ({
  default: {
    name: "OverlayEditorModal",
    template: "<div />"
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

function mountView() {
  return mount(OverlayPresetsView, {
    global: {
      plugins: [buildI18n()],
      stubs: {
        OverlayEditorModal: true
      }
    }
  });
}

type OverlayLanInfoFixture = {
  enabled: boolean;
  bindAddress: string;
  overlayPort: number;
  accessKey: string | null;
  suggestedHosts: string[];
};

function stubOverlayFetch(lanInfo: OverlayLanInfoFixture | (() => OverlayLanInfoFixture) = {
  enabled: true,
  bindAddress: "0.0.0.0",
  overlayPort: 5001,
  accessKey: "obs-key",
  suggestedHosts: ["192.168.1.20"]
}): void {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const url = typeof input === "string" ? input : input.toString();

    if (url === "/api/overlay/presets") {
      return new Response(JSON.stringify([
        { hubName: "chat", key: "vulperonex-default", label: "Vulperonex default", kind: "builtin", relativeUrl: "/overlay/chat.html" },
        { hubName: "member", key: "rotan-checkin", label: "Rotan checkin", kind: "builtin", relativeUrl: "/overlay/member-card.html" },
        { hubName: "alerts", key: "vulperonex-alerts", label: "Vulperonex alerts", kind: "builtin", relativeUrl: "/overlay/alerts" }
      ]), { status: 200 });
    }

    if (url === "/api/overlay/custom-presets") {
      return new Response(JSON.stringify([]), { status: 200 });
    }

    if (url === "/api/overlay/lan-info") {
      const response = typeof lanInfo === "function" ? lanInfo() : lanInfo;
      return new Response(JSON.stringify(response), { status: 200 });
    }

    if (url.startsWith("/api/config/")) {
      return new Response(JSON.stringify({ value: null }), { status: 200 });
    }

    return new Response("not found", { status: 404 });
  }));
}

describe("OverlayPresetsView", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("copies the latest LAN OBS URL without rendering the IP in the page", async () => {
    const lanResponses: OverlayLanInfoFixture[] = [
      {
        enabled: true,
        bindAddress: "0.0.0.0",
        overlayPort: 5001,
        accessKey: "obs-key",
        suggestedHosts: ["192.168.1.20"]
      },
      {
        enabled: true,
        bindAddress: "0.0.0.0",
        overlayPort: 5001,
        accessKey: "obs-key",
        suggestedHosts: ["192.168.1.45"]
      }
    ];
    stubOverlayFetch(() => lanResponses.shift() ?? lanResponses[0]);
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText }
    });

    const wrapper = mountView();
    await flushPromises();

    const chatRow = wrapper.get('[data-testid="overlay-obs-url-chat"]').text();
    const memberRow = wrapper.get('[data-testid="overlay-obs-url-member"]').text();
    expect(chatRow).toContain("Chat Overlay");
    expect(chatRow).not.toContain("192.168.1.");
    expect(memberRow).toContain("Member Overlay");
    expect(memberRow).not.toContain("192.168.1.");

    await wrapper.get('[data-testid="overlay-obs-copy-lan-chat"]').trigger("click");
    await flushPromises();
    expect(writeText).toHaveBeenCalledWith("http://192.168.1.45:5001/overlay/chat?k=obs-key");
  });

  it("copies a local OBS URL without LAN access", async () => {
    stubOverlayFetch();
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText }
    });

    const wrapper = mountView();
    await flushPromises();

    await wrapper.get('[data-testid="overlay-obs-copy-local-member"]').trigger("click");
    await flushPromises();
    expect(writeText).toHaveBeenCalledWith("http://localhost:3000/overlay/member");
  });

  it("keeps OBS URLs visible when LAN overlay access is disabled", async () => {
    stubOverlayFetch({
      enabled: false,
      bindAddress: "",
      overlayPort: 0,
      accessKey: null,
      suggestedHosts: []
    });

    const wrapper = mountView();
    await flushPromises();

    const chatRow = wrapper.get('[data-testid="overlay-obs-url-chat"]').text();
    const memberRow = wrapper.get('[data-testid="overlay-obs-url-member"]').text();
    expect(chatRow).toContain("Chat Overlay");
    expect(memberRow).toContain("Member Overlay");
    expect(wrapper.get('[data-testid="overlay-obs-copy-lan-chat"]').attributes("disabled")).toBeDefined();
  });
});
