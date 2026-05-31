import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import ConditionExpressionInput from "./ConditionExpressionInput.vue";

vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string) => key,
    te: () => false
  })
}));


describe("ConditionExpressionInput", () => {
  it("should build visual expressions from picked variables", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "",
        dataTestId: "condition"
      }
    });

    await wrapper.find('[data-testid="variable-picker-toggle"]').trigger("click");
    await wrapper.findAll("button").find((entry) => entry.text().includes("Member.IsSubscriber"))!.trigger("click");
    await wrapper.find('[data-testid="condition-right"]').setValue("true");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("Member.IsSubscriber == true");
  });

  it("should allow raw mode editing for complex expressions", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "",
        dataTestId: "condition"
      }
    });

    await wrapper.find('[data-testid="condition-raw-toggle"]').trigger("click");
    await wrapper.find('[data-testid="condition"]').setValue("Trigger.MessageText == 'hi' && Member.IsSubscriber == true");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("Trigger.MessageText == 'hi' && Member.IsSubscriber == true");
  });
});
