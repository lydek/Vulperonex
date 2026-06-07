import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import VariableFieldInput from "./VariableFieldInput.vue";

describe("VariableFieldInput", () => {
  it("inserts the selected variable as a token via the picker", async () => {
    const wrapper = mount(VariableFieldInput, {
      props: {
        modelValue: "Hello world"
      },
      global: {
        stubs: {
          VariablePicker: {
            template: '<button type="button" data-testid="picker" @click="$emit(\'select\', \'{user.name}\')">pick</button>'
          }
        }
      }
    });

    await wrapper.get('[data-testid="picker"]').trigger("click");

    const emitted = wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string | undefined;
    expect(emitted).toContain("{user.name}");
    expect(emitted).toContain("Hello world");
  });
});
