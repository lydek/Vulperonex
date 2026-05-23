import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import ChatOverlayView from "./ChatOverlayView.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ query: {} })
}));

vi.mock("@/composables/useOverlayHub", () => ({
  useOverlayHub: () => ({
    events: ref([{ eventId: "e1", displayName: "Streamer", segments: [{ kind: "text", text: "hi" }] }]),
    start: vi.fn(async () => undefined),
    state: ref(1),
    lastEventAt: ref(null),
    error: ref(null),
    clear: vi.fn(async () => undefined)
  })
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
  return mount(ChatOverlayView, {
    global: {
      plugins: [buildI18n()],
      stubs: {
        HubStatusChip: true,
        ConfirmDialog: true
      }
    }
  });
}

describe("ChatOverlayView preset switching", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("falls back to default preset when config has no value", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(JSON.stringify({ key: "overlay.chat.preset", value: null }), { status: 200 })
      )
    );

    const wrapper = mountView();
    await flushPromises();

    expect(wrapper.find('[data-testid="chat-preset-default"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="chat-preset-compact"]').exists()).toBe(false);
  });

  it("switches to compact preset via the dropdown", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(JSON.stringify({ key: "overlay.chat.preset", value: null }), { status: 200 })
      )
    );

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="chat-overlay-preset-select"]').setValue("compact-line");
    await flushPromises();

    expect(wrapper.find('[data-testid="chat-preset-compact"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="chat-preset-default"]').exists()).toBe(false);
  });

  it("respects the preset stored in system settings", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(JSON.stringify({ key: "overlay.chat.preset", value: "compact-line" }), { status: 200 })
      )
    );

    const wrapper = mountView();
    await flushPromises();

    expect(wrapper.find('[data-testid="chat-preset-compact"]').exists()).toBe(true);
  });
});
