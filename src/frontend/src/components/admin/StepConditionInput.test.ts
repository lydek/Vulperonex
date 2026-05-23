import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import StepConditionInput from "./StepConditionInput.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("StepConditionInput", () => {
  it("should update executionCondition in the actions JSON", async () => {
    const wrapper = mount(StepConditionInput, {
      props: {
        modelValue: JSON.stringify([{ type: "sendChatMessage", template: "hi" }])
      },
      global: { plugins: [buildI18n()] }
    });

    await wrapper.findAll("input")[0].setValue("Trigger.MessageText == '!hi'");

    const emitted = wrapper.emitted("update:modelValue")?.[0]?.[0] as string;
    expect(JSON.parse(emitted)).toEqual([
      {
        type: "sendChatMessage",
        template: "hi",
        executionCondition: "Trigger.MessageText == '!hi'"
      }
    ]);
  });

  it("should remove outputVariable when cleared", async () => {
    const wrapper = mount(StepConditionInput, {
      props: {
        modelValue: JSON.stringify([{ type: "randomPicker", outputVariable: "Pick" }])
      },
      global: { plugins: [buildI18n()] }
    });

    await wrapper.findAll("input")[1].setValue("");

    const emitted = wrapper.emitted("update:modelValue")?.[0]?.[0] as string;
    expect(JSON.parse(emitted)).toEqual([
      {
        type: "randomPicker"
      }
    ]);
  });
});
