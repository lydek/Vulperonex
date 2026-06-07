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
  it("shows group tabs instead of one long mixed list", () => {
    const wrapper = mount(VariablePicker);

    expect(wrapper.text()).toContain("Trigger Event");
    expect(wrapper.text()).toContain("Args");
    expect(wrapper.text()).toContain("Trigger User");
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
});
