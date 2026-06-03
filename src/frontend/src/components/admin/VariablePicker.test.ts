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
