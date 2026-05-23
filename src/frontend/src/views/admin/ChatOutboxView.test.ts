import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import ChatOutboxView from "./ChatOutboxView.vue";

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
  return mount(ChatOutboxView, {
    global: { plugins: [buildI18n()] }
  });
}

const sampleItems = [
  {
    id: "11111111-1111-1111-1111-111111111111",
    platform: "simulation",
    channel: "main",
    message: "hello world",
    dedupKey: null,
    enqueuedAt: "2026-05-24T10:00:00Z",
    status: "Sent",
    errorMessage: null
  },
  {
    id: "22222222-2222-2222-2222-222222222222",
    platform: "simulation",
    channel: null,
    message: "duplicate",
    dedupKey: "key-1",
    enqueuedAt: "2026-05-24T10:00:01Z",
    status: "Skipped",
    errorMessage: "duplicate dedup key"
  },
  {
    id: "33333333-3333-3333-3333-333333333333",
    platform: "twitch",
    channel: "broadcaster",
    message: "boom",
    dedupKey: null,
    enqueuedAt: "2026-05-24T10:00:02Z",
    status: "Failed",
    errorMessage: "rate limited"
  }
];

describe("ChatOutboxView", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("renders sent / skipped / failed rows with status badges", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(JSON.stringify(sampleItems), { status: 200 })
      )
    );

    const wrapper = mountView();
    await flushPromises();

    const rows = wrapper.findAll('[data-testid^="chat-outbox-row-"]');
    expect(rows.length).toBe(3);

    expect(wrapper.find(`[data-testid="chat-outbox-status-${sampleItems[0].id}"]`).text()).toBe("Sent");
    expect(wrapper.find(`[data-testid="chat-outbox-status-${sampleItems[1].id}"]`).text()).toBe("Skipped");
    expect(wrapper.find(`[data-testid="chat-outbox-status-${sampleItems[2].id}"]`).text()).toBe("Failed");

    const summary = wrapper.find('[data-testid="chat-outbox-summary"]').text();
    expect(summary).toContain("Sent: 1");
    expect(summary).toContain("Skipped: 1");
    expect(summary).toContain("Failed: 1");
  });

  it("passes status and platform filters through to the API", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify([]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="chat-outbox-status-filter"]').setValue("Failed");
    await wrapper.find('[data-testid="chat-outbox-platform-filter"]').setValue("twitch");
    await wrapper.find('[data-testid="chat-outbox-toolbar"]').trigger("submit");
    await flushPromises();

    const calls = fetchMock.mock.calls.map((call) => String((call as unknown[])[0]));
    expect(calls.some((url) => url.includes("status=Failed") && url.includes("platform=twitch"))).toBe(true);
  });

  it("surfaces API errors", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response("oops", { status: 500 }))
    );

    const wrapper = mountView();
    await flushPromises();

    expect(wrapper.find('[data-testid="chat-outbox-error"]').exists()).toBe(true);
  });
});
