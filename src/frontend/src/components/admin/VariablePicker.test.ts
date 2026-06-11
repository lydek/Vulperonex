import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import VariablePicker from "./VariablePicker.vue";

vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string) => key,
    te: () => false
  })
}));


describe("VariablePicker", () => {
  it("shows side filters instead of one long mixed list", async () => {
    const wrapper = mount(VariablePicker);

    await wrapper.find('[data-testid="variable-picker-toggle"]').trigger("click");

    expect(wrapper.find(".variable-picker__panel--open").exists()).toBe(true);
    expect(wrapper.find('[data-testid="variable-picker-filter-all"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="variable-picker-filter-trigger"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="variable-picker-filter-member"]').exists()).toBe(true);
    expect(wrapper.text()).toContain("Trigger Event");
    expect(wrapper.text()).toContain("Args");
    expect(wrapper.text()).toContain("Trigger User");
  });

  it("filters visible sections from the side rail", async () => {
    const wrapper = mount(VariablePicker);

    await wrapper.find('[data-testid="variable-picker-filter-member"]').trigger("click");

    expect(wrapper.text()).toContain("Member.UserId");
    expect(wrapper.text()).not.toContain("Trigger.MessageText");
    expect(wrapper.find('[data-testid="variable-picker-group-member"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="variable-picker-group-trigger"]').exists()).toBe(false);
  });

  it("toggles the side panel from the variable button", async () => {
    const wrapper = mount(VariablePicker);
    const toggle = wrapper.find('[data-testid="variable-picker-toggle"]');

    await toggle.trigger("click");
    expect(wrapper.find(".variable-picker__panel--open").exists()).toBe(true);

    await toggle.trigger("click");
    expect(wrapper.find(".variable-picker__panel--open").exists()).toBe(false);
  });

  it("does not expose low-value internal trigger fields", () => {
    const wrapper = mount(VariablePicker, {
      props: {
        expressionMode: true
      }
    });

    expect(wrapper.text()).not.toContain("Trigger.EventId");
    expect(wrapper.text()).not.toContain("Trigger.OccurredAt");
  });

  it("does not expose failure context in normal variable picking", () => {
    const wrapper = mount(VariablePicker, {
      props: {
        expressionMode: true
      }
    });

    expect(wrapper.text()).not.toContain("Failure Context");
    expect(wrapper.text()).not.toContain("Failure.StepIndex");
    expect(wrapper.text()).not.toContain("Failure.ErrorMessage");
  });

  it("filters trigger variables when an allowed list is provided", () => {
    const wrapper = mount(VariablePicker, {
      props: {
        allowedTriggerVariables: ["MessageText"]
      }
    });

    expect(wrapper.text()).toContain("{Trigger.MessageText}");
    expect(wrapper.text()).not.toContain("{Trigger.IsTest}");
  });

  it("hides trigger variables when an empty allowed list is provided", () => {
    const wrapper = mount(VariablePicker, {
      props: {
        filterKey: "target",
        allowedTriggerVariables: []
      }
    });

    expect(wrapper.text()).not.toContain("Trigger Command");
    expect(wrapper.text()).not.toContain("Trigger.Command.Target");
    expect(wrapper.text()).not.toContain("Trigger.Command.TargetLogin");
  });

  it("updates the trigger variable list when allowed variables change", async () => {
    const wrapper = mount(VariablePicker, {
      props: {
        expressionMode: true,
        allowedTriggerVariables: ["MessageText"]
      }
    });

    expect(wrapper.text()).toContain("Trigger.MessageText");
    expect(wrapper.text()).not.toContain("Trigger.EventId");

    await wrapper.setProps({ allowedTriggerVariables: ["Platform"] });

    expect(wrapper.text()).toContain("Trigger.Platform");
    expect(wrapper.text()).not.toContain("Trigger.MessageText");
  });

  it("filters variables by search text", async () => {
    const wrapper = mount(VariablePicker, {
      props: {
        expressionMode: true
      }
    });

    await wrapper.find('[data-testid="variable-picker-search"]').setValue("IsSubscriber");

    expect(wrapper.text()).toContain("Member.IsSubscriber");
    expect(wrapper.text()).not.toContain("Trigger.EventId");
  });

  it("filters target user fields to user identifiers", async () => {
    const wrapper = mount(VariablePicker, {
      props: {
        filterKey: "target",
        previousSteps: [
          { type: "parseChatCommand", outputVariable: "Command" },
          { type: "lookupTwitchUser", outputVariable: "UserLookup" },
          { type: "triggerCheckIn", outputVariable: "CheckIn" }
        ],
        actionDefinitions: [
          {
            type: "parseChatCommand",
            label: "Parse chat command",
            description: "",
            fields: [],
            outputVariables: ["CommandName", "ArgsText", "Arg1", "Target", "TargetLogin", "HasTarget"],
            create: () => ({ type: "parseChatCommand" })
          },
          {
            type: "lookupTwitchUser",
            label: "Lookup user",
            description: "",
            fields: [],
            outputVariables: ["Login", "DisplayName", "UserId", "IsFound"],
            create: () => ({ type: "lookupTwitchUser" })
          },
          {
            type: "triggerCheckIn",
            label: "Check-in",
            description: "",
            fields: [],
            outputVariables: ["CheckInCount", "TotalLoyalty", "DisplayName"],
            create: () => ({ type: "triggerCheckIn" })
          }
        ]
      }
    });

    await wrapper.find('[data-testid="variable-picker-group-member"]').trigger("click");
    expect(wrapper.text()).toContain("Member.Login");
    expect(wrapper.text()).toContain("Member.DisplayName");
    expect(wrapper.text()).toContain("Member.UserId");

    await wrapper.find('[data-testid="variable-picker-group-trigger"]').trigger("click");
    expect(wrapper.text()).toContain("Trigger.Command.Target");
    expect(wrapper.text()).not.toContain("Trigger.Command.TargetLogin");
    expect(wrapper.text()).not.toContain("Trigger.Command.ArgsText");
    expect(wrapper.text()).not.toContain("Trigger.MessageText");

    await wrapper.find('[data-testid="variable-picker-group-steps"]').trigger("click");
    expect(wrapper.text()).toContain("Step.Command.Target");
    expect(wrapper.text()).toContain("Step.Command.TargetLogin");
    expect(wrapper.text()).toContain("Step.UserLookup.Login");
    expect(wrapper.text()).toContain("Step.UserLookup.DisplayName");
    expect(wrapper.text()).toContain("Step.UserLookup.UserId");
    expect(wrapper.text()).toContain("Step.CheckIn.DisplayName");

    expect(wrapper.text()).not.toContain("Trigger.EventTypeKey");
    expect(wrapper.text()).not.toContain("Trigger.RewardId");
    expect(wrapper.text()).not.toContain("Args.UserId");
    expect(wrapper.text()).not.toContain("Args.DisplayName");
    expect(wrapper.text()).not.toContain("Member.Roles");
    expect(wrapper.text()).not.toContain("Step.Command.ArgsText");
    expect(wrapper.text()).not.toContain("Step.Command.HasTarget");
    expect(wrapper.text()).not.toContain("Step.UserLookup.IsFound");
    expect(wrapper.text()).not.toContain("Step.CheckIn.CheckInCount");
  });

  it("filters reward fields to reward/redemption identifiers", () => {
    const wrapper = mount(VariablePicker, {
      props: { filterKey: "rewardId" }
    });

    expect(wrapper.text()).toContain("Trigger.RewardId");
    expect(wrapper.text()).toContain("Trigger.RewardTitle");
    expect(wrapper.text()).toContain("Trigger.RedemptionId");
    expect(wrapper.text()).not.toContain("Trigger.MessageText");
    expect(wrapper.text()).not.toContain("Member.UserId");
  });

  it("filters plugin fields to plugin payload and workflow args", async () => {
    const wrapper = mount(VariablePicker, {
      props: {
        filterKey: "pluginId",
        expressionMode: true,
        previousSteps: [
          { type: "invokePlugin", outputVariable: "PluginResult" },
          { type: "updateCounter", outputVariable: "Counter" }
        ],
        actionDefinitions: [
          {
            type: "invokePlugin",
            label: "Invoke plugin",
            description: "",
            fields: [],
            outputVariables: ["Value"],
            create: () => ({ type: "invokePlugin" })
          },
          {
            type: "updateCounter",
            label: "Counter",
            description: "",
            fields: [],
            outputVariables: ["Value"],
            create: () => ({ type: "updateCounter" })
          }
        ]
      }
    });

    expect(wrapper.text()).toContain("Trigger.Payload.PluginId");
    expect(wrapper.text()).toContain("Trigger.Payload.ActionId");
    expect(wrapper.text()).toContain("Args.UserId");
    expect(wrapper.text()).toContain("Step.PluginResult.Value");
    expect(wrapper.text()).not.toContain("Trigger.MessageText");
    expect(wrapper.text()).not.toContain("Member.UserId");
    expect(wrapper.text()).not.toContain("Step.Counter.Value");
  });
});
