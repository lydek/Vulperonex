import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import VariableFieldInput from "./VariableFieldInput.vue";

describe("VariableFieldInput", () => {
  it("inserts the selected variable at the current cursor position", async () => {
    const wrapper = mount(VariableFieldInput, {
      props: {
        modelValue: "Hello  world"
      },
      global: {
        stubs: {
          VariablePicker: {
            template: '<button type="button" data-testid="picker" @click="$emit(\'select\', \'{user.name}\')">pick</button>'
          }
        }
      }
    });

    const input = wrapper.get("input");
    (input.element as HTMLInputElement).setSelectionRange(6, 6);

    await wrapper.get('[data-testid="picker"]').trigger("click");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("Hello {user.name} world");
  });
});
