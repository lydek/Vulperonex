import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import RekaPhaseDPoc from "./RekaPhaseDPoc.vue";

describe("RekaPhaseDPoc", () => {
  it("opens a drawer, switches tabs, and preserves form state", async () => {
    const wrapper = mount(RekaPhaseDPoc, {
      attachTo: document.body
    });

    await wrapper.find('[data-testid="reka-poc-open"]').trigger("click");

    const drawer = document.body.querySelector('[data-testid="reka-poc-drawer"]');
    expect(drawer).not.toBeNull();
    expect(drawer?.getAttribute("data-state")).toBe("open");

    const basicTab = document.body.querySelector('[data-testid="reka-poc-tab-basic"]');
    expect(basicTab?.getAttribute("data-state")).toBe("active");

    const nameInput = document.body.querySelector<HTMLInputElement>('[data-testid="reka-poc-name"]');
    expect(nameInput).not.toBeNull();
    nameInput!.value = "Updated rule";
    nameInput!.dispatchEvent(new Event("input"));
    await wrapper.vm.$nextTick();

    document.body.querySelector<HTMLButtonElement>('[data-testid="reka-poc-tab-actions"]')?.click();
    await wrapper.vm.$nextTick();
    document.body.querySelector<HTMLButtonElement>('[data-testid="reka-poc-tab-basic"]')?.click();
    await wrapper.vm.$nextTick();

    expect(document.body.querySelector<HTMLInputElement>('[data-testid="reka-poc-name"]')?.value)
      .toBe("Updated rule");
  });
});
