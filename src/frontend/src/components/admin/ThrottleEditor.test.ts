import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import ThrottleEditor from "./ThrottleEditor.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("ThrottleEditor", () => {
  it("should emit patched throttle policy", async () => {
    const wrapper = mount(ThrottleEditor, {
      props: {
        modelValue: {
          maxConcurrent: 1,
          cooldownSeconds: 0,
          perUserCooldown: false,
          perUserCooldownSeconds: 0
        }
      },
      global: { plugins: [buildI18n()] }
    });

    await wrapper.findAll('input[type="number"]')[0].setValue("4");
    await wrapper.find('input[type="checkbox"]').setValue(true);

    expect(wrapper.emitted("update:modelValue")?.[0]?.[0]).toMatchObject({
      maxConcurrent: 4,
      cooldownSeconds: 0,
      perUserCooldown: false,
      perUserCooldownSeconds: 0
    });
    expect(wrapper.emitted("update:modelValue")?.[1]?.[0]).toMatchObject({
      maxConcurrent: 1,
      cooldownSeconds: 0,
      perUserCooldown: true,
      perUserCooldownSeconds: 0
    });
  });
});
