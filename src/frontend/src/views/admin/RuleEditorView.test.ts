import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import RuleEditorView from "./RuleEditorView.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {} }),
  useRouter: () => ({ push: vi.fn() })
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
  return mount(RuleEditorView, {
    global: {
      plugins: [buildI18n()],
      stubs: {
        TriggerEditor: {
          props: ["eventTypeKey", "filter", "matchCondition"],
          template:
            '<div data-testid="trigger-editor">{{ eventTypeKey }} {{ JSON.stringify(filter) }} {{ matchCondition }}</div>'
        },
        ThrottleEditor: {
          props: ["modelValue"],
          template: '<div data-testid="throttle-editor">{{ JSON.stringify(modelValue) }}</div>'
        },
        RuleJsonEditor: {
          props: ["modelValue"],
          emits: ["update:modelValue"],
          template:
            '<textarea data-testid="rule-json-editor" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
        },
        StepConditionInput: true,
        OnFailureEditor: {
          props: ["modelValue"],
          emits: ["update:modelValue"],
          template:
            '<textarea data-testid="on-failure-editor" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
        }
      }
    }
  });
}

describe("RuleEditorView", () => {
  it("should import a full rule JSON file into the editor form", async () => {
    const wrapper = mountView();
    const fileText = JSON.stringify({
          name: "Imported rule",
          eventTypeKey: "user.message",
          isEnabled: true,
          priority: 42,
          trigger: {
            eventTypeKey: "user.message",
            filter: { platform: "twitch" },
            matchCondition: "Trigger.MessageText == '!go'"
          },
          conditions: [],
          actions: [{ type: "sendChatMessage", template: "hello" }],
          onFailureSteps: [{ type: "sendChatMessage", template: "failed" }],
          throttle: {
            maxConcurrent: 1,
            cooldownSeconds: 10,
            perUserCooldown: true,
            perUserCooldownSeconds: 10
          },
          timeoutSeconds: 20
        });
    const file = new File(
      [fileText],
      "rule.json",
      { type: "application/json" }
    );
    Object.defineProperty(file, "text", { value: async () => fileText });
    const input = wrapper.find('[data-testid="rule-import-file"]').element as HTMLInputElement;
    Object.defineProperty(input, "files", { value: [file] });

    await wrapper.find('[data-testid="rule-import-file"]').trigger("change");
    await flushPromises();

    expect((wrapper.find('[data-testid="rule-editor-name"]').element as HTMLInputElement).value)
      .toBe("Imported rule");
    expect(wrapper.find('[data-testid="trigger-editor"]').text()).toContain("user.message");
    expect(wrapper.find('[data-testid="trigger-editor"]').text()).toContain("\"platform\":\"twitch\"");
    expect(wrapper.find('[data-testid="trigger-editor"]').text()).toContain("Trigger.MessageText == '!go'");
    expect((wrapper.find('[data-testid="rule-editor-priority"]').element as HTMLInputElement).value)
      .toBe("42");
    expect((wrapper.find('[data-testid="rule-editor-timeout"]').element as HTMLInputElement).value)
      .toBe("20");
    expect(wrapper.find('[data-testid="throttle-editor"]').text()).toContain("\"cooldownSeconds\":10");
    expect(wrapper.findAll('[data-testid="rule-json-editor"]')[1].element).toHaveProperty(
      "value",
      JSON.stringify([{ type: "sendChatMessage", template: "hello" }], null, 2)
    );
    expect(wrapper.find('[data-testid="on-failure-editor"]').element).toHaveProperty(
      "value",
      JSON.stringify([{ type: "sendChatMessage", template: "failed" }], null, 2)
    );
  });
});
