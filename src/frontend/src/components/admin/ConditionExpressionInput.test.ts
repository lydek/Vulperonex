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
    await wrapper.find('[data-testid="variable-picker-group-member"]').trigger("click");
    await wrapper.findAll("button").find((entry) => entry.text().includes("Member.IsSubscriber"))!.trigger("click");
    await wrapper.find('[data-testid="condition-right"]').setValue("true");

    expect(wrapper.find('[data-testid="condition-left-selected"]').text()).toBe("Member.IsSubscriber");
    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("Member.IsSubscriber == true");
  });

  it("should clear a picked left variable via the clear button", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "Member.IsSubscriber == true",
        dataTestId: "condition"
      }
    });

    expect(wrapper.find('[data-testid="condition-left-selected"]').text()).toBe("Member.IsSubscriber");

    await wrapper.find('[data-testid="condition-left-clear"]').trigger("click");

    expect(wrapper.find('[data-testid="condition-left-selected"]').exists()).toBe(false);
    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("");
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

  it("should detect check-in status mode from previous step output", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "Step.Res.Status == 'repeat'",
        dataTestId: "condition",
        previousSteps: [{ type: "triggerCheckIn", outputVariable: "Res" }]
      }
    });

    expect(wrapper.find('[data-testid="condition-right"]').element.tagName).toBe("SELECT");
    expect(wrapper.find('[data-testid="condition-right"]').text()).toContain("repeat");
    expect(wrapper.text()).toContain("Mode: Check-in status");
  });

  it("should keep delay step status options to general action statuses", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "Step.t.Status == 'success'",
        dataTestId: "condition",
        previousSteps: [{ type: "delay", outputVariable: "t" }]
      }
    });

    const statusOptions = wrapper.find('[data-testid="condition-right"]').text();
    expect(statusOptions).toContain("success");
    expect(statusOptions).toContain("error");
    expect(statusOptions).not.toContain("repeat");
    expect(wrapper.find('[data-testid="condition-left-selected"]').text()).toBe("Step.t.Status");
  });

  it("should provide platform options when a platform variable is selected", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "Trigger.Platform",
        dataTestId: "condition"
      }
    });

    const valueSelect = wrapper.find('[data-testid="condition-right"]');
    expect(valueSelect.element.tagName).toBe("SELECT");
    expect(valueSelect.text()).toContain("simulation");
    expect(valueSelect.text()).toContain("twitch");
    expect(valueSelect.text()).toContain("system");
    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("Trigger.Platform == 'simulation'");
  });

  it("should keep free-text variables as plain value inputs without a value variable picker", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "Trigger.MessageText == '!checkin'",
        dataTestId: "condition"
      }
    });

    const valueInput = wrapper.find('[data-testid="condition-right"]');
    expect(valueInput.element.tagName).toBe("INPUT");
    expect(valueInput.attributes("placeholder")).toBe("condition.valueTextHint");
    expect(wrapper.findAll('[data-testid="variable-picker-toggle"]')).toHaveLength(1);
  });

  it("should clear stale enum values when switching to a free-text variable", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "Trigger.EventTypeKey == 'user.message'",
        dataTestId: "condition"
      }
    });

    expect((wrapper.find('[data-testid="condition-right"]').element as HTMLSelectElement).value).toBe("user.message");

    await wrapper.find('[data-testid="variable-picker-toggle"]').trigger("click");
    await wrapper.findAll("button").find((entry) => entry.text().includes("Trigger.MessageText"))!.trigger("click");

    const valueInput = wrapper.find('[data-testid="condition-right"]');
    expect(valueInput.element.tagName).toBe("INPUT");
    expect((valueInput.element as HTMLInputElement).value).toBe("");
    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("Trigger.MessageText");
  });

  it("should provide role options when member roles is selected", async () => {
    const wrapper = mount(ConditionExpressionInput, {
      props: {
        modelValue: "Member.Roles",
        dataTestId: "condition"
      }
    });

    const valueSelect = wrapper.find('[data-testid="condition-right"]');
    expect(valueSelect.element.tagName).toBe("SELECT");
    expect(valueSelect.text()).toContain("Broadcaster");
    expect(valueSelect.text()).toContain("Subscriber");
    expect(valueSelect.text()).toContain("Moderator");
    expect(valueSelect.text()).toContain("Vip");
    expect(valueSelect.text()).toContain("Follower");
    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe("Member.Roles == 'Broadcaster'");
  });
});
