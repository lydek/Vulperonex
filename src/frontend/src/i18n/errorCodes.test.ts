import { describe, expect, it } from "vitest";
import enUS from "./en-US.json";
import zhTW from "./zh-TW.json";
import { ERROR_CODES, errorCodeI18nKey, resolveErrorCode } from "./errorCodes";

describe("error codes i18n parity", () => {
  for (const constantName of Object.keys(ERROR_CODES) as Array<keyof typeof ERROR_CODES>) {
    const code = ERROR_CODES[constantName];
    const key = errorCodeI18nKey(code);

    it(`should have a non-empty zh-TW translation for ${code}`, () => {
      const value = (zhTW as Record<string, string>)[key];
      expect(value, `missing zh-TW translation for ${key}`).toBeDefined();
      expect(value).not.toBe("");
    });

    it(`should have a non-empty en-US translation for ${code}`, () => {
      const value = (enUS as Record<string, string>)[key];
      expect(value, `missing en-US translation for ${key}`).toBeDefined();
      expect(value).not.toBe("");
    });
  }
});

describe("resolveErrorCode", () => {
  it("should prefer parsed code when present", () => {
    expect(resolveErrorCode(400, "WORKFLOW_RULE_NOT_FOUND")).toBe("WORKFLOW_RULE_NOT_FOUND");
  });

  it("should fall back to INTERNAL_ERROR for 5xx without parsed code", () => {
    expect(resolveErrorCode(500, null)).toBe(ERROR_CODES.InternalError);
    expect(resolveErrorCode(503, null)).toBe(ERROR_CODES.InternalError);
  });

  it("should fall back to HTTP_<status> for non-5xx without parsed code", () => {
    expect(resolveErrorCode(404, null)).toBe("HTTP_404");
    expect(resolveErrorCode(400, null)).toBe("HTTP_400");
  });
});
