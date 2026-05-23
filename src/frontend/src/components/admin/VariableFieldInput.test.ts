import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import VariableFieldInput from "./VariableFieldInput.vue";

describe("VariableFieldInput", () => {
  it("should insert placeholder variables into the current field", async () => {
    const wrapper = mount(VariableFieldInput, {
      props: {
        modelValue: "",
        placeholder: "Template"
      }
    });

    await wrapper.find('[data-testid="variable-picker-toggle"]').trigger("click");
    await wrapper.findAll("button").find((entry) => entry.text().includes("{Trigger.DisplayName}"))!.trigger("click");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("{Trigger.DisplayName}");
  });
});
