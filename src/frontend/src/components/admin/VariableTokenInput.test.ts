import { mount, type VueWrapper } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import VariableTokenInput from "./VariableTokenInput.vue";

// jsdom does not reliably persist a real Selection/Range, so emulate a collapsed
// caret sitting immediately after the first chip (the text node that follows it).
function caretAfterFirstChip(wrapper: VueWrapper): void {
  const root = wrapper.get(".token-input").element;
  const chip = root.querySelector(".token-chip")!;
  const after = chip.nextSibling!;
  vi.spyOn(window, "getSelection").mockReturnValue({
    rangeCount: 1,
    isCollapsed: true,
    getRangeAt: () => ({ startContainer: after, startOffset: 0 })
  } as unknown as Selection);
}

describe("VariableTokenInput", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("does not remove tokens on a plain click of the field or chip body", async () => {
    const wrapper = mount(VariableTokenInput, {
      props: { modelValue: "a {Trigger.A} b {Trigger.B} c" }
    });

    await wrapper.get(".token-input").trigger("click");
    await wrapper.get(".token-chip__label").trigger("click");
    await wrapper.findAll(".token-chip")[0].trigger("click");

    expect(wrapper.emitted("update:modelValue")).toBeFalsy();
    expect(wrapper.findAll(".token-chip")).toHaveLength(2);
  });

  it("renders tokens as atomic chips interleaved with text", () => {
    const wrapper = mount(VariableTokenInput, {
      props: { modelValue: "Hi {Trigger.MessageText} there {Member.DisplayName}" }
    });

    const chips = wrapper.findAll(".token-chip");
    expect(chips).toHaveLength(2);
    expect(chips[0].attributes("data-token")).toBe("{Trigger.MessageText}");
    expect(chips[0].get(".token-chip__label").text()).toBe("Trigger.MessageText");
    expect(chips[1].attributes("data-token")).toBe("{Member.DisplayName}");
    expect(wrapper.get(".token-input").text()).toContain("Hi");
    expect(wrapper.get(".token-input").text()).toContain("there");
  });

  it("inserts a token and emits the serialized string", () => {
    const wrapper = mount(VariableTokenInput, {
      props: { modelValue: "" }
    });

    (wrapper.vm as unknown as { insertToken: (t: string) => void }).insertToken("{Trigger.EventId}");

    const emitted = wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string;
    expect(emitted).toBe("{Trigger.EventId} ");
    expect(emitted.charCodeAt(emitted.length - 1)).toBe(32);
  });

  it("removes a token with Backspace when the caret follows it", async () => {
    const wrapper = mount(VariableTokenInput, {
      props: { modelValue: "keep {Trigger.EventId} end" }
    });

    caretAfterFirstChip(wrapper);
    await wrapper.get(".token-input").trigger("keydown", { key: "Backspace" });

    const emitted = wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string;
    expect(emitted).not.toContain("{Trigger.EventId}");
    expect(emitted).toContain("keep");
    expect(emitted).toContain("end");
  });

  it("restores a removed token with ctrl+z", async () => {
    const wrapper = mount(VariableTokenInput, {
      props: { modelValue: "keep {Trigger.EventId} end" }
    });

    caretAfterFirstChip(wrapper);
    await wrapper.get(".token-input").trigger("keydown", { key: "Backspace" });
    expect(wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string).not.toContain("{Trigger.EventId}");

    await wrapper.get(".token-input").trigger("keydown", { key: "z", ctrlKey: true });

    const restored = wrapper.emitted("update:modelValue")?.at(-1)?.[0] as string;
    expect(restored).toBe("keep {Trigger.EventId} end");
    expect(wrapper.findAll(".token-chip")).toHaveLength(1);
  });

  it("re-renders chips when the model value changes externally", async () => {
    const wrapper = mount(VariableTokenInput, {
      props: { modelValue: "" }
    });
    expect(wrapper.findAll(".token-chip")).toHaveLength(0);

    await wrapper.setProps({ modelValue: "{Trigger.Platform}" });

    expect(wrapper.findAll(".token-chip")).toHaveLength(1);
    expect(wrapper.get(".token-chip").attributes("data-token")).toBe("{Trigger.Platform}");
  });
});
