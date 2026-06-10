import { describe, expect, it } from "vitest";
import enUS from "./en-US.json";
import zhTW from "./zh-TW.json";
import { i18n } from "./index";

describe("i18n locale parity", () => {
  it("should expose identical key set across zh-TW and en-US", () => {
    const enKeys = Object.keys(enUS).sort();
    const zhKeys = Object.keys(zhTW).sort();

    expect(zhKeys).toEqual(enKeys);
  });

  it("should not contain empty translations", () => {
    for (const [key, value] of Object.entries(enUS)) {
      expect(value, `en-US ${key}`).not.toBe("");
    }
    for (const [key, value] of Object.entries(zhTW)) {
      expect(value, `zh-TW ${key}`).not.toBe("");
    }
  });

  it("should compile every message without vue-i18n syntax errors", () => {
    for (const key of Object.keys(enUS)) {
      expect(() => i18n.global.t(key, 1, { locale: "en-US" }), `en-US ${key}`).not.toThrow();
    }
    for (const key of Object.keys(zhTW)) {
      expect(() => i18n.global.t(key, 1, { locale: "zh-TW" }), `zh-TW ${key}`).not.toThrow();
    }
  });

  it("should default to zh-TW with en-US fallback when missing keys", () => {
    expect(i18n.global.locale.value).toBe("zh-TW");
    expect(i18n.global.fallbackLocale.value).toBe("en-US");
    expect(i18n.global.t("__missing_key__")).toBe("__missing_key__");
  });
});
