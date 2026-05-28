import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import RuleEditorView from "./RuleEditorView.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {} }),
  useRouter: () => ({ push: vi.fn() }),
  onBeforeRouteLeave: vi.fn()
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
        WorkflowConditionsEditor: {
          props: ["modelValue"],
          emits: ["update:modelValue"],
          template:
            '<textarea data-testid="workflow-conditions-editor" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
        },
        WorkflowActionsEditor: {
          props: ["modelValue"],
          emits: ["update:modelValue"],
          template:
            '<textarea data-testid="workflow-actions-editor" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
        },
        ConfirmDialog: {
          props: ["open", "title", "message", "confirmLabel", "cancelLabel"],
          emits: ["confirm", "cancel"],
          template: '<div v-if="open" data-testid="unsaved-confirm">{{ title }} {{ message }}</div>'
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
    expect(wrapper.findAll('[data-testid="workflow-actions-editor"]')[0].element).toHaveProperty(
      "value",
      JSON.stringify([{ type: "sendChatMessage", template: "hello" }], null, 2)
    );
    expect(wrapper.findAll('[data-testid="workflow-actions-editor"]')[1].element).toHaveProperty(
      "value",
      JSON.stringify([{ type: "sendChatMessage", template: "failed" }], null, 2)
    );
  });

  it("should show unsaved confirmation before leaving edited content", async () => {
    const wrapper = mountView();

    await wrapper.find('[data-testid="rule-editor-name"]').setValue("Dirty rule");
    await wrapper.find('button.secondary-button:not([data-testid="rule-export"])').trigger("click");

    expect(wrapper.find('[data-testid="unsaved-confirm"]').exists()).toBe(true);
  });

  it("should surface unsupported top-level keys after import", async () => {
    const wrapper = mountView();
    const fileText = JSON.stringify({
      name: "Imported",
      eventTypeKey: "user.message",
      isEnabled: true,
      priority: 1,
      conditions: [],
      actions: [],
      onFailureSteps: [],
      throttle: { maxConcurrent: 0, cooldownSeconds: 0, perUserCooldown: false, perUserCooldownSeconds: 0 },
      timeoutSeconds: 30,
      legacyField: "ignored",
      experimental: { nested: true },
      trigger: { eventTypeKey: "user.message", filter: {}, futureKnob: 42 }
    });
    const file = new File([fileText], "rule.json", { type: "application/json" });
    Object.defineProperty(file, "text", { value: async () => fileText });
    const input = wrapper.find('[data-testid="rule-import-file"]').element as HTMLInputElement;
    Object.defineProperty(input, "files", { value: [file] });
    await wrapper.find('[data-testid="rule-import-file"]').trigger("change");
    await flushPromises();

    const banner = wrapper.find('[data-testid="rule-editor-unsupported"]');
    expect(banner.exists()).toBe(true);
    const text = banner.text();
    expect(text).toContain("experimental");
    expect(text).toContain("legacyField");
    expect(text).toContain("trigger.futureKnob");
  });

  it("should round-trip the form into a downloadable JSON payload", async () => {
    const wrapper = mountView();

    await wrapper.find('[data-testid="rule-editor-name"]').setValue("Exported");
    await wrapper.findAll('[data-testid="workflow-actions-editor"]')[0].setValue(
      JSON.stringify([{ type: "sendChatMessage", template: "hi" }])
    );

    const createObjectURL = vi.fn(() => "blob:rule");
    const revokeObjectURL = vi.fn();
    const anchorClick = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    const originalCreate = (globalThis.URL as { createObjectURL?: unknown }).createObjectURL;
    const originalRevoke = (globalThis.URL as { revokeObjectURL?: unknown }).revokeObjectURL;
    (globalThis.URL as unknown as { createObjectURL: typeof createObjectURL }).createObjectURL = createObjectURL;
    (globalThis.URL as unknown as { revokeObjectURL: typeof revokeObjectURL }).revokeObjectURL = revokeObjectURL;

    try {
      await wrapper.find('[data-testid="rule-export"]').trigger("click");
    } finally {
      anchorClick.mockRestore();
      (globalThis.URL as unknown as { createObjectURL: unknown }).createObjectURL = originalCreate;
      (globalThis.URL as unknown as { revokeObjectURL: unknown }).revokeObjectURL = originalRevoke;
    }

    expect(createObjectURL).toHaveBeenCalledTimes(1);
    expect(revokeObjectURL).toHaveBeenCalledTimes(1);

    const exposed = wrapper.vm as unknown as { buildExportPayload: () => string | null };
    const serialized = exposed.buildExportPayload();
    expect(serialized).not.toBeNull();
    const parsed = JSON.parse(serialized as string) as Record<string, unknown>;
    expect(parsed.name).toBe("Exported");
    expect(parsed.actions).toEqual([{ type: "sendChatMessage", template: "hi" }]);
    expect(parsed.trigger).toEqual({ filter: {} });
  });
});
