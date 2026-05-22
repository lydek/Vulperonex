import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import ConfirmDialog from "./ConfirmDialog.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function mountDialog(open: boolean) {
  return mount(ConfirmDialog, {
    props: {
      open,
      title: "Delete?",
      message: "Permanent.",
      confirmLabel: "Confirm",
      cancelLabel: "Cancel"
    },
    attachTo: document.body,
    global: { plugins: [buildI18n()] }
  });
}

describe("ConfirmDialog (a11y)", () => {
  it("should expose role=dialog, aria-modal, and aria-labelledby when open", () => {
    const wrapper = mountDialog(true);
    const dialog = wrapper.find("[role='dialog']");

    expect(dialog.exists()).toBe(true);
    expect(dialog.attributes("aria-modal")).toBe("true");
    expect(dialog.attributes("aria-labelledby")).toBeDefined();
    expect(wrapper.find("h2").attributes("id")).toBe(dialog.attributes("aria-labelledby"));
  });

  it("should not render dialog content when open=false", () => {
    const wrapper = mountDialog(false);
    expect(wrapper.find("[role='dialog']").exists()).toBe(false);
  });

  it("should emit cancel on Escape keydown", async () => {
    const wrapper = mountDialog(true);
    await wrapper.find("[role='dialog']").trigger("keydown", { key: "Escape" });
    expect(wrapper.emitted("cancel")).toBeDefined();
  });

  it("should emit confirm when Confirm button is clicked", async () => {
    const wrapper = mountDialog(true);
    await wrapper.find(".danger-button").trigger("click");
    expect(wrapper.emitted("confirm")).toBeDefined();
  });
});
