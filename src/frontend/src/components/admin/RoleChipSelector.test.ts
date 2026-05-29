import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import RoleChipSelector from "./RoleChipSelector.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("RoleChipSelector", () => {
  it("selects and clears role chips", async () => {
    const wrapper = mount(RoleChipSelector, {
      props: {
        modelValue: []
      },
      global: {
        plugins: [buildI18n()]
      }
    });

    expect(wrapper.get('[data-testid="role-chip-Everyone"]').attributes("aria-pressed")).toBe("true");

    await wrapper.get('[data-testid="role-chip-Moderator"]').trigger("click");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toEqual(["Moderator"]);

    await wrapper.setProps({ modelValue: ["Moderator"] });
    await wrapper.get('[data-testid="role-chip-Everyone"]').trigger("click");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toEqual([]);
  });
});
