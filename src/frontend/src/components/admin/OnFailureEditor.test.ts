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
  it("should forward JSON editor updates", async () => {
    const wrapper = mount(OnFailureEditor, {
      props: { modelValue: "[]" },
      global: {
        plugins: [buildI18n()],
        stubs: {
          RuleJsonEditor: {
            props: ["modelValue"],
            emits: ["update:modelValue"],
            template:
              '<textarea data-testid="on-failure-json" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
          }
        }
      }
    });

    await wrapper.find('[data-testid="on-failure-json"]').setValue('[{"type":"noop"}]');

    expect(wrapper.emitted("update:modelValue")?.[0]).toEqual(['[{"type":"noop"}]']);
  });
});
