import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import WorkflowActionsEditor from "./WorkflowActionsEditor.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("WorkflowActionsEditor", () => {
  it("should add and edit actions without raw JSON", async () => {
    const wrapper = mount(WorkflowActionsEditor, {
      props: {
        modelValue: "[]",
        title: "Actions",
        emptyText: "Empty"
      },
      global: {
        plugins: [buildI18n()]
      }
    });

    await wrapper.find('[data-testid="workflow-actions-add"]').trigger("click");
    await wrapper.find('[data-testid="workflow-actions-type-0"]').setValue("randomPicker");

    const textareas = wrapper.findAll("textarea");
    await textareas[0].setValue("alpha\nbeta");
    await textareas[1].setValue("2\n3");
    await wrapper.find('[data-testid="workflow-actions-execution-0-raw-toggle"]').trigger("click");
    await wrapper.find('[data-testid="workflow-actions-execution-0"]').setValue("Trigger.IsModerator");
    await wrapper.find('input[placeholder="Result"]').setValue("Pick");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe(JSON.stringify([
      {
        type: "randomPicker",
        choices: ["alpha", "beta"],
        weights: [2, 3],
        executionCondition: "Trigger.IsModerator",
        outputVariable: "Pick"
      }
    ], null, 2));
  });
});
