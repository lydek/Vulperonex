import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import RuleJsonEditor from "./RuleJsonEditor.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function mountEditor(modelValue = "") {
  return mount(RuleJsonEditor, {
    props: { modelValue },
    global: { plugins: [buildI18n()] }
  });
}

describe("RuleJsonEditor", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("should reject paste that exceeds 1 MB and surface toast", async () => {
    const wrapper = mountEditor("");
    const huge = "x".repeat(1_048_577);
    const event = new Event("paste", { bubbles: true, cancelable: true }) as Event & {
      clipboardData?: DataTransfer;
    };
    Object.defineProperty(event, "clipboardData", {
      value: { getData: () => huge }
    });

    await wrapper.find('[data-testid="rule-editor-textarea"]').element.dispatchEvent(event);
    await flushPromises();

    expect(event.defaultPrevented).toBe(true);
    expect(wrapper.find('[data-testid="rule-editor-toast"]').text())
      .toContain("exceeds the 1 MB cap");
  });

  it("should emit parsed payload after 300ms debounce on input", async () => {
    const wrapper = mountEditor("");
    const textarea = wrapper.find('[data-testid="rule-editor-textarea"]');
    await textarea.setValue("[{\"type\":\"noop\"}]");

    vi.advanceTimersByTime(299);
    expect(wrapper.emitted("parsed")).toBeUndefined();

    vi.advanceTimersByTime(1);
    expect(wrapper.emitted("parsed")?.[0]?.[0]).toEqual([{ type: "noop" }]);
  });

  it("should surface parse error when textarea content is invalid json", async () => {
    const wrapper = mountEditor("");
    await wrapper.find('[data-testid="rule-editor-textarea"]').setValue("{not-json}");
    vi.advanceTimersByTime(300);
    await flushPromises();

    expect(wrapper.find('[data-testid="rule-editor-parse-error"]').exists()).toBe(true);
    expect(wrapper.emitted("parse-error")).toBeDefined();
  });

  it("should reject file upload when extension is not .json", async () => {
    const wrapper = mountEditor("");
    const file = new File(["[]"], "rule.txt", { type: "text/plain" });
    const input = wrapper.find('[data-testid="rule-editor-file"]').element as HTMLInputElement;
    Object.defineProperty(input, "files", { value: [file] });

    await wrapper.find('[data-testid="rule-editor-file"]').trigger("change");
    await flushPromises();

    expect(wrapper.find('[data-testid="rule-editor-toast"]').text())
      .toContain("Only .json files");
  });

  it("should accept valid json file and emit update:modelValue", async () => {
    const wrapper = mountEditor("");
    const file = new File(["[1,2,3]"], "rule.json", { type: "application/json" });
    Object.defineProperty(file, "text", { value: async () => "[1,2,3]" });
    const input = wrapper.find('[data-testid="rule-editor-file"]').element as HTMLInputElement;
    Object.defineProperty(input, "files", { value: [file] });

    await wrapper.find('[data-testid="rule-editor-file"]').trigger("change");
    await flushPromises();

    expect(wrapper.emitted("update:modelValue")?.[0]?.[0]).toBe("[1,2,3]");
  });
});
