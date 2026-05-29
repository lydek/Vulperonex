import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import WorkflowConditionsEditor from "./WorkflowConditionsEditor.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("WorkflowConditionsEditor", () => {
  it("should add and edit cooldown conditions without raw JSON", async () => {
    const wrapper = mount(WorkflowConditionsEditor, {
      props: {
        modelValue: "[]",
        title: "Conditions",
        emptyText: "Empty"
      },
      global: {
        plugins: [buildI18n()]
      }
    });

    await wrapper.find('[data-testid="workflow-conditions-add"]').trigger("click");
    await wrapper.find('[data-testid="workflow-conditions-type-0"]').setValue("cooldown");

    const inputs = wrapper.findAll("input");
    await inputs[0].setValue("90");
    await inputs[1].setValue("{Trigger.UserId}");

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toBe(JSON.stringify([
      {
        type: "cooldown",
        scope: "Global",
        durationSeconds: 90,
        key: "{Trigger.UserId}"
      }
    ], null, 2));
  });

  it("should pin the userRole condition to the first position", () => {
    const wrapper = mount(WorkflowConditionsEditor, {
      props: {
        modelValue: JSON.stringify([
          { type: "cooldown", scope: "Global", durationSeconds: 30 },
          { type: "userRole", mode: "HasAny", roles: "Subscriber" }
        ], null, 2),
        title: "Conditions",
        emptyText: "Empty"
      },
      global: { plugins: [buildI18n()] }
    });

    const firstType = wrapper.get('[data-testid="workflow-conditions-type-0"]');
    expect((firstType.element as HTMLSelectElement).value).toBe("userRole");
  });

  it("should show a migration chip and open the suggestion dialog for legacy NCalc role expressions", async () => {
    const wrapper = mount(WorkflowConditionsEditor, {
      props: {
        modelValue: "[]",
        title: "Conditions",
        emptyText: "Empty",
        matchCondition: "Member.IsModerator == true"
      },
      global: { plugins: [buildI18n()] }
    });

    const chip = wrapper.find('[data-testid="workflow-conditions-migration-chip"]');
    expect(chip.exists()).toBe(true);

    expect(wrapper.find('[data-testid="workflow-conditions-migration-dialog"]').exists()).toBe(false);
    await chip.trigger("click");

    const dialog = wrapper.find('[data-testid="workflow-conditions-migration-dialog"]');
    expect(dialog.exists()).toBe(true);
    expect(dialog.text()).toContain("Member.IsModerator");
    // No automatic mutation: the model is never emitted from opening the dialog.
    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
  });

  it("should not show the migration chip when no legacy role expressions exist", () => {
    const wrapper = mount(WorkflowConditionsEditor, {
      props: {
        modelValue: JSON.stringify([{ type: "userRole", mode: "HasAny", roles: "Subscriber" }], null, 2),
        title: "Conditions",
        emptyText: "Empty"
      },
      global: { plugins: [buildI18n()] }
    });

    expect(wrapper.find('[data-testid="workflow-conditions-migration-chip"]').exists()).toBe(false);
  });

  it("should toggle user roles with checkboxes", async () => {
    const wrapper = mount(WorkflowConditionsEditor, {
      props: {
        modelValue: JSON.stringify([{ type: "userRole", mode: "HasAny", roles: "Subscriber" }], null, 2),
        title: "Conditions",
        emptyText: "Empty"
      },
      global: {
        plugins: [buildI18n()]
      }
    });

    const vipCheckbox = wrapper.findAll('input[type="checkbox"]').find((entry) => entry.element.nextElementSibling?.textContent === "Vip");
    expect(vipCheckbox).toBeDefined();

    await vipCheckbox!.setValue(true);

    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0]).toContain("\"roles\": \"Subscriber, Vip\"");
  });
});
