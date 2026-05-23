import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import OnFailureEditor from "./OnFailureEditor.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("OnFailureEditor", () => {
  it("should add steps via the shared action builder", async () => {
    const wrapper = mount(OnFailureEditor, {
      props: { modelValue: "[]" },
      global: { plugins: [buildI18n()] }
    });

    await wrapper.find('[data-testid="on-failure-actions-add"]').trigger("click");

    const emitted = wrapper.emitted("update:modelValue");
    expect(emitted).toBeTruthy();
    const last = emitted?.at(-1)?.[0];
    expect(typeof last).toBe("string");
    expect(JSON.parse(last as string)).toEqual([
      expect.objectContaining({ type: expect.any(String) })
    ]);
  });

  it("should expose the nested-onFailure notice", () => {
    const wrapper = mount(OnFailureEditor, {
      props: { modelValue: "[]" },
      global: { plugins: [buildI18n()] }
    });

    expect(wrapper.text()).toContain("nested");
  });
});
