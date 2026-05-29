import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
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

const actionMetadataResponse = [
  {
    type: "sendChatMessage",
    displayName: "Send Chat Message",
    description: "Send a message to chat",
    parameters: [
      { key: "Template", label: "Template", type: "string", required: true, help: "Message template" }
    ]
  },
  {
    type: "randomPicker",
    displayName: "Random Picker",
    description: "Pick one value from a list",
    parameters: [
      { key: "Choices", label: "Choices", type: "array", required: true, help: "List of choices" },
      { key: "Weights", label: "Weights", type: "array", required: false, help: "Relative weights" }
    ]
  }
];

function stubActionMetadataFetch(status = 200): void {
  vi.stubGlobal("fetch", vi.fn(async () => new Response(
    status === 200 ? JSON.stringify(actionMetadataResponse) : "metadata unavailable",
    {
      headers: { "Content-Type": "application/json" },
      status
    }
  )));
}

describe("WorkflowActionsEditor", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    stubActionMetadataFetch();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("should add and edit actions without raw JSON", async () => {
    const wrapper = mount(WorkflowActionsEditor, {
      props: {
        modelValue: "[]",
        title: "Actions",
        emptyText: "Empty"
      },
      global: {
        plugins: [buildI18n(), createPinia()]
      }
    });

    await vi.waitFor(() => {
      expect(fetch).toHaveBeenCalledWith("/api/metadata/actions", expect.any(Object));
    });

    await wrapper.find('[data-testid="workflow-actions-add"]').trigger("click");
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="workflow-actions-type-0"]').text()).toContain("Random Picker");
    });
    await wrapper.find('[data-testid="workflow-actions-type-0"]').setValue("randomPicker");

    const textareas = wrapper.findAll("textarea");
    await textareas[0].setValue("alpha\nbeta");
    await textareas[1].setValue("2\n3");
    await wrapper.find('[data-testid="workflow-actions-execution-0-raw-toggle"]').trigger("click");
    await wrapper.find('[data-testid="workflow-actions-execution-0"]').setValue("Trigger.IsModerator");
    await wrapper.find('.workflow-builder__meta-output input').setValue("Pick");

    const emittedJson = JSON.parse(wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string);
    expect(emittedJson).toEqual([
      {
        type: "randomPicker",
        choices: ["alpha", "beta"],
        weights: [2, 3],
        executionCondition: "Trigger.IsModerator",
        outputVariable: "Pick"
      }
    ]);

  });

  it("should remain openable with a fallback warning when metadata fails", async () => {
    vi.unstubAllGlobals();
    stubActionMetadataFetch(500);

    const wrapper = mount(WorkflowActionsEditor, {
      props: {
        modelValue: "[]",
        title: "Actions",
        emptyText: "Empty"
      },
      global: {
        plugins: [buildI18n(), createPinia()]
      }
    });

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain("minimal fallback action list");
    });

    await wrapper.find('[data-testid="workflow-actions-add"]').trigger("click");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe(JSON.stringify([
      { type: "sendChatMessage" }
    ], null, 2));
  });
});
