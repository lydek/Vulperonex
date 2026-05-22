import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { HubConnectionState } from "@microsoft/signalr";
import MembersView from "@/views/admin/MembersView.vue";
import EventTypeKeyDropdown from "@/components/admin/EventTypeKeyDropdown.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("WCAG AA a11y baseline (II16)", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("HubStatusChip should expose role=status and a localized aria-label per state", () => {
    const wrapper = mount(HubStatusChip, {
      props: { state: HubConnectionState.Connected, lastEventAt: null, error: null },
      global: { plugins: [buildI18n()] }
    });

    const chip = wrapper.find("[role='status']");
    expect(chip.exists()).toBe(true);
    expect(chip.attributes("aria-label")).toBeTruthy();
  });

  it("EventTypeKeyDropdown should expose an aria-label on the select element", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify([{ key: "user.message", description: "msg", isSimulatable: true }]), { status: 200 }))
    );

    const wrapper = mount(EventTypeKeyDropdown, {
      props: { modelValue: "" },
      global: { plugins: [buildI18n()] }
    });
    await flushPromises();

    const select = wrapper.find('[data-testid="event-type-select"]');
    expect(select.attributes("aria-label")).toBeTruthy();
  });

  it("MembersView rows should be keyboard activatable with role=button and tabindex=0", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(new Response(JSON.stringify([
          {
            memberId: "M-1",
            identities: [{ platform: "twitch", platformUserId: "u-1" }],
            loyalty: { totalLoyalty: 5, checkInCount: 1 }
          }
        ]), { status: 200 }))
        .mockResolvedValueOnce(new Response(JSON.stringify({
          memberId: "M-1",
          identities: [{ platform: "twitch", platformUserId: "u-1" }],
          loyalty: { totalLoyalty: 5, checkInCount: 1 }
        }), { status: 200 }))
    );

    const wrapper = mount(MembersView, {
      global: { plugins: [buildI18n(), createPinia()] }
    });
    await flushPromises();

    const row = wrapper.find('[data-testid="members-row"]');
    expect(row.attributes("role")).toBe("button");
    expect(row.attributes("tabindex")).toBe("0");
    expect(row.attributes("aria-label")).toContain("M-1");

    await row.trigger("keydown", { key: "Enter" });
    await flushPromises();
    expect(wrapper.find('[data-testid="members-detail"]').exists()).toBe(true);
  });
});
