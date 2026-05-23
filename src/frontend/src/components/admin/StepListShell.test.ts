import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import { defineComponent, h, ref } from "vue";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import StepListShell from "./StepListShell.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

const Harness = defineComponent({
  setup() {
    const items = ref<{ id: number }[]>([{ id: 1 }, { id: 2 }, { id: 3 }]);
    const json = ref("[]");
    function add(): void {
      items.value = [...items.value, { id: items.value.length + 1 }];
    }
    function remove(i: number): void {
      items.value = items.value.filter((_, idx) => idx !== i);
    }
    function move(i: number, dir: -1 | 1): void {
      const next = items.value.slice();
      const [cur] = next.splice(i, 1);
      next.splice(i + dir, 0, cur);
      items.value = next;
    }
    return { items, json, add, remove, move };
  },
  render() {
    return h(
      StepListShell,
      {
        items: this.items,
        title: "Steps",
        emptyText: "No steps",
        prefix: "harness",
        modelValue: this.json,
        onAdd: this.add,
        onRemove: this.remove,
        onMove: this.move,
        "onUpdate:modelValue": (v: string) => { this.json = v; }
      },
      {
        identity: ({ item, index }: { item: { id: number }; index: number }) =>
          h("span", { "data-testid": `harness-id-${index}` }, String(item.id)),
        body: ({ item, index }: { item: { id: number }; index: number }) =>
          h("span", { "data-testid": `harness-body-${index}` }, `body-${item.id}`)
      }
    );
  }
});

describe("StepListShell", () => {
  it("adds, removes, and moves items", async () => {
    const wrapper = mount(Harness, { global: { plugins: [buildI18n()] } });

    expect(wrapper.findAll('[data-testid^="harness-id-"]').length).toBe(3);

    await wrapper.find('[data-testid="harness-add"]').trigger("click");
    expect(wrapper.findAll('[data-testid^="harness-id-"]').length).toBe(4);

    await wrapper.find('[data-testid="harness-remove-0"]').trigger("click");
    expect(wrapper.findAll('[data-testid^="harness-id-"]').length).toBe(3);

    await wrapper.find('[data-testid="harness-down-0"]').trigger("click");
    const firstId = wrapper.find('[data-testid="harness-id-0"]').text();
    expect(firstId).toBe("3");
  });

  it("toggles collapse to hide body slot", async () => {
    const wrapper = mount(Harness, { global: { plugins: [buildI18n()] } });

    const bodyParent = (): HTMLElement | null =>
      wrapper.find('[data-testid="harness-body-0"]').element.parentElement;
    expect(bodyParent()?.style.display).toBe("");
    expect(wrapper.find('[data-testid="harness-toggle-0"]').attributes("aria-expanded")).toBe("true");

    await wrapper.find('[data-testid="harness-toggle-0"]').trigger("click");
    expect(bodyParent()?.style.display).toBe("none");
    expect(wrapper.find('[data-testid="harness-toggle-0"]').attributes("aria-expanded")).toBe("false");

    await wrapper.find('[data-testid="harness-toggle-0"]').trigger("click");
    expect(bodyParent()?.style.display).toBe("");
  });

  it("disables up at first and down at last", () => {
    const wrapper = mount(Harness, { global: { plugins: [buildI18n()] } });

    const firstUp = wrapper.find('[data-testid="harness-up-0"]').element as HTMLButtonElement;
    const lastDown = wrapper.find('[data-testid="harness-down-2"]').element as HTMLButtonElement;
    expect(firstUp.disabled).toBe(true);
    expect(lastDown.disabled).toBe(true);
  });
});
