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
    type: "delay",
    displayName: "Delay",
    description: "Delay workflow execution",
    parameters: [
      { key: "DelayMs", label: "Delay (ms)", type: "number", required: false, help: "Duration in milliseconds" }
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
  },
  {
    type: "triggerCheckIn",
    displayName: "Trigger Check-In",
    description: "Trigger check-in and activity tracking for a stream viewer",
    parameters: [
      { key: "UserId", label: "User ID", type: "string", required: false, help: "Default {Member.UserId}", advanced: true },
      { key: "Platform", label: "Platform", type: "string", required: false, help: "Leave empty to use trigger platform.", advanced: true }
    ]
  },
  {
    type: "lookupTwitchUser",
    displayName: "Lookup Twitch User",
    description: "Resolve a chat login or display name to one exact user.",
    parameters: [
      { key: "Target", label: "Target User", type: "string", required: true, help: "Login, @DisplayName, or a variable. No numeric ID needed." }
    ]
  },
  {
    type: "parseChatCommand",
    displayName: "Parse Chat Command",
    description: "Extract command arguments from chat text.",
    parameters: [
      { key: "Message", label: "Message", type: "string", required: false, help: "Chat message text. Empty = Trigger.MessageText." },
      { key: "CommandPrefix", label: "Command Prefix", type: "string", required: false, help: "Optional command prefix, e.g. !shoutout." }
    ]
  },
  {
    type: "shoutout",
    displayName: "Shoutout",
    description: "Send Twitch's native /shoutout for a resolved channel.",
    parameters: [
      { key: "TargetLogin", label: "Target Username", type: "string", required: true, help: "Target Twitch login name." }
    ]
  },
  {
    type: "invokeSubWorkflow",
    displayName: "Invoke Sub-Workflow",
    description: "Invoke another sub-workflow rule",
    parameters: [
      { key: "WorkflowId", label: "Workflow ID", type: "string", required: true, help: "Select target sub-workflow by name." },
      { key: "Args", label: "Arguments", type: "dictionary", required: false, help: "Arguments" }
    ]
  }
];

function stubActionMetadataFetch(status = 200): void {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const url = typeof input === "string" ? input : input.toString();
    if (url.includes("/api/metadata/actions")) {
      return new Response(
        status === 200 ? JSON.stringify(actionMetadataResponse) : "metadata unavailable",
        {
          headers: { "Content-Type": "application/json" },
          status
        }
      );
    }

    if (url.includes("/api/rules/")) {
      return new Response(JSON.stringify([
        {
          id: "rule-main",
          name: "Main Rule",
          eventTypeKey: "user.message",
          isEnabled: true,
          priority: 1,
          createdAt: "2026-06-01T00:00:00Z",
          version: 1
        },
        {
          id: "sub-1",
          name: "Daily Check-In Child",
          eventTypeKey: null,
          isEnabled: true,
          priority: 2,
          createdAt: "2026-06-01T00:00:00Z",
          version: 1
        }
      ]), {
        headers: { "Content-Type": "application/json" },
        status: 200
      });
    }

    return new Response("not found", { status: 404 });
  }));
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
      expect(wrapper.find('[data-testid="workflow-actions-add-option-randomPicker"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-option-randomPicker"]').trigger("click");
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="workflow-actions-type-0"]').text()).toContain("Random Picker");
    });

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="workflow-actions-random-picker-0"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-random-choice-0-0"]').setValue("alpha");
    await wrapper.find('[data-testid="workflow-actions-random-weight-0-0"]').setValue("2");
    await wrapper.find('[data-testid="workflow-actions-random-add-0"]').trigger("click");
    await wrapper.find('[data-testid="workflow-actions-random-choice-0-1"]').setValue("beta");
    await wrapper.find('[data-testid="workflow-actions-random-weight-0-1"]').setValue("3");
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

  it("should keep random picker weights optional when left blank", async () => {
    const wrapper = mount(WorkflowActionsEditor, {
      props: {
        modelValue: JSON.stringify([
          {
            type: "randomPicker",
            choices: ["alpha"],
            outputVariable: "Pick"
          }
        ], null, 2),
        title: "Actions",
        emptyText: "Empty"
      },
      global: {
        plugins: [buildI18n(), createPinia()]
      }
    });

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="workflow-actions-random-picker-0"]').exists()).toBe(true);
    });

    await wrapper.find('[data-testid="workflow-actions-random-add-0"]').trigger("click");
    await wrapper.find('[data-testid="workflow-actions-random-choice-0-1"]').setValue("beta");

    const emittedJson = JSON.parse(wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string);
    expect(emittedJson[0]).toEqual({
      type: "randomPicker",
      choices: ["alpha", "beta"],
      outputVariable: "Pick"
    });
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
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="workflow-actions-add-option-sendChatMessage"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-option-sendChatMessage"]').trigger("click");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe(JSON.stringify([
      { type: "sendChatMessage" }
    ], null, 2));
  });

  it("should treat trigger check-in target as implicit until advanced overrides are opened", async () => {
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
      expect(wrapper.find('[data-testid="workflow-actions-add-option-triggerCheckIn"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-option-triggerCheckIn"]').trigger("click");
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="workflow-actions-type-0"]').html()).toContain("triggerCheckIn");
    });

    expect(wrapper.get('[data-testid="workflow-actions-implicit-target-0"]').text())
      .toContain("Checks in the user who triggered this event.");
    expect(wrapper.text()).not.toContain("Default {Member.UserId}");

    const advanced = wrapper.get('[data-testid="workflow-actions-advanced-0"]');
    (advanced.element as HTMLDetailsElement).open = true;
    await advanced.trigger("toggle");
    expect(wrapper.text()).toContain("User ID");
    expect(wrapper.text()).toContain("Platform");
  });

  it("should offer sub-workflow names instead of generic variable input for workflow id", async () => {
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
      expect(wrapper.find('[data-testid="workflow-actions-add-option-invokeSubWorkflow"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-option-invokeSubWorkflow"]').trigger("click");
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="workflow-actions-type-0"]').html()).toContain("invokeSubWorkflow");
    });

    const workflowSelect = wrapper.get('[data-testid="workflow-actions-field-workflowId-0"]');
    expect(workflowSelect.element.tagName).toBe("SELECT");
    expect(workflowSelect.text()).toContain("Daily Check-In Child");
    expect(workflowSelect.text()).not.toContain("Main Rule");

    await workflowSelect.setValue("sub-1");

    const emittedJson = JSON.parse(wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string);
    expect(emittedJson[0].workflowId).toBe("sub-1");
  });

  it("should open an action menu before inserting a new step", async () => {
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

    expect(wrapper.text()).toContain("Send Chat Message");
    expect(wrapper.text()).toContain("Trigger Check-In");
    expect(wrapper.find('[data-testid="workflow-actions-add-search"]').exists()).toBe(true);
    expect(wrapper.findAll('[data-testid^="workflow-actions-card-"]').length).toBe(0);
  });

  it("should filter the action add menu by search text", async () => {
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
      expect(wrapper.find('[data-testid="workflow-actions-add-option-shoutout"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-search"]').setValue("shout");

    expect(wrapper.find('[data-testid="workflow-actions-add-option-shoutout"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="workflow-actions-add-option-sendChatMessage"]').exists()).toBe(false);
  });

  it("should use compact placeholders for lookup target fields", async () => {
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
      expect(wrapper.find('[data-testid="workflow-actions-add-option-lookupTwitchUser"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-option-lookupTwitchUser"]').trigger("click");

    const targetInput = wrapper.get('[data-testid="workflow-actions-field-target-0"]');
    expect(targetInput.attributes("data-placeholder")).toBe("login or name");
  });

  it("should filter lookup target variables to user identifiers", async () => {
    const wrapper = mount(WorkflowActionsEditor, {
      props: {
        modelValue: JSON.stringify([
          { type: "parseChatCommand", outputVariable: "Command" }
        ]),
        title: "Actions",
        emptyText: "Empty",
        eventTypeKey: "user.message"
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
      expect(wrapper.find('[data-testid="workflow-actions-add-option-lookupTwitchUser"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-option-lookupTwitchUser"]').trigger("click");
    await wrapper.find('.workflow-builder__target-field [data-testid="variable-picker-toggle"]').trigger("click");
    const targetPickerPanel = wrapper.find(".workflow-builder__target-field .variable-picker__panel");
    await targetPickerPanel.find('[data-testid="variable-picker-group-trigger"]').trigger("click");

    expect(targetPickerPanel.text()).toContain("Trigger.Command.Target");
    expect(targetPickerPanel.text()).not.toContain("Trigger.Command.TargetLogin");
    expect(targetPickerPanel.text()).not.toContain("Trigger.Command.ArgsText");

    await targetPickerPanel.find('[data-testid="variable-picker-group-member"]').trigger("click");

    expect(targetPickerPanel.text()).toContain("Member.Login");
    expect(targetPickerPanel.text()).toContain("Member.DisplayName");
    expect(targetPickerPanel.text()).toContain("Member.UserId");
    await targetPickerPanel.find('[data-testid="variable-picker-group-steps"]').trigger("click");
    expect(targetPickerPanel.text()).toContain("Step.Command.Target");
    expect(targetPickerPanel.text()).toContain("Step.Command.TargetLogin");
    expect(targetPickerPanel.text()).not.toContain("Trigger.EventTypeKey");
    expect(targetPickerPanel.text()).not.toContain("Trigger.RewardId");
    expect(targetPickerPanel.text()).not.toContain("Member.Roles");
    expect(targetPickerPanel.text()).not.toContain("Step.Command.ArgsText");
  });

  it("should hide chat command trigger target variables when event type is not chat", async () => {
    const wrapper = mount(WorkflowActionsEditor, {
      props: {
        modelValue: "[]",
        title: "Actions",
        emptyText: "Empty",
        eventTypeKey: "user.donated"
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
      expect(wrapper.find('[data-testid="workflow-actions-add-option-lookupTwitchUser"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-option-lookupTwitchUser"]').trigger("click");
    await wrapper.find('.workflow-builder__target-field [data-testid="variable-picker-toggle"]').trigger("click");
    const targetPickerPanel = wrapper.find(".workflow-builder__target-field .variable-picker__panel");

    expect(targetPickerPanel.text()).toContain("Member.Login");
    expect(targetPickerPanel.text()).not.toContain("Trigger.Command.Target");
    expect(targetPickerPanel.text()).not.toContain("Trigger.Command.TargetLogin");
  });

  it("should clamp delay milliseconds to zero in the visual editor", async () => {
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
      expect(wrapper.find('[data-testid="workflow-actions-add-option-delay"]').exists()).toBe(true);
    });
    await wrapper.find('[data-testid="workflow-actions-add-option-delay"]').trigger("click");

    const delayInput = wrapper.get('[data-testid="workflow-actions-field-delayMs-0"]');
    expect(delayInput.attributes("min")).toBe("0");
    await delayInput.setValue("-4");

    const emittedJson = JSON.parse(wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string);
    expect(emittedJson[0].delayMs).toBe(0);
  });
});
