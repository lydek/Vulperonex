import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import VariablePicker from "./VariablePicker.vue";

describe("VariablePicker", () => {
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

    await wrapper.setProps({ allowedTriggerVariables: ["EventId"] });

    expect(wrapper.text()).toContain("Trigger.EventId");
    expect(wrapper.text()).not.toContain("Trigger.MessageText");
  });
});
