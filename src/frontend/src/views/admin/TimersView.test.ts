import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import TimersView from "./TimersView.vue";

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
  return mount(TimersView, {
    global: { plugins: [buildI18n()] }
  });
}

describe("TimersView", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should render timers returned by the API", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(
          JSON.stringify([
            {
              id: "timer-1",
              ruleId: "rule-1",
              intervalSeconds: 30,
              isEnabled: true,
              nextFireAt: "2026-05-23T00:00:30Z"
            }
          ]),
          { status: 200 }
        )
      )
    );

    const wrapper = mountView();
    await flushPromises();

    expect(wrapper.find('[data-testid="timers-table"]').text()).toContain("timer-1");
    expect(wrapper.text()).toContain("rule-1");
  });

  it("should post timer creation payload", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: "timer-1" }), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="timer-rule-id"]').setValue("rule-1");
    await wrapper.find('[data-testid="timer-form"]').trigger("submit");
    await flushPromises();

    expect(fetchMock.mock.calls[1][0]).toBe("/api/timers/");
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: "POST" });
    expect(JSON.parse(fetchMock.mock.calls[1][1].body as string)).toMatchObject({
      ruleId: "rule-1",
      intervalSeconds: 30,
      isEnabled: true
    });
  });

  it("should load and update a selected timer", async () => {
    const timer = {
      id: "timer-1",
      ruleId: "rule-1",
      intervalSeconds: 30,
      isEnabled: true,
      nextFireAt: "2026-05-23T00:00:30Z"
    };
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify([timer]), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(timer), { status: 200 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ ...timer, ruleId: "rule-2" }), { status: 200 })
      )
      .mockResolvedValueOnce(new Response(JSON.stringify([{ ...timer, ruleId: "rule-2" }]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="timer-show-timer-1"]').trigger("click");
    await flushPromises();
    await wrapper.find('[data-testid="timer-edit-rule-id"]').setValue("rule-2");
    await wrapper.find('[data-testid="timer-edit-form"]').trigger("submit");
    await flushPromises();

    expect(fetchMock.mock.calls[1][0]).toBe("/api/timers/timer-1");
    expect(fetchMock.mock.calls[2][0]).toBe("/api/timers/timer-1");
    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: "PUT" });
    expect(JSON.parse(fetchMock.mock.calls[2][1].body as string)).toMatchObject({
      ruleId: "rule-2",
      intervalSeconds: 30,
      isEnabled: true,
      nextFireAt: "2026-05-23T00:00:30Z"
    });
  });
});
