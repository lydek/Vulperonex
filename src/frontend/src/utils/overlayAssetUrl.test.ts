import { describe, expect, it } from "vitest";
import { cssUrl, sanitizeAssetUrl } from "./overlayAssetUrl";

describe("overlayAssetUrl", () => {
  it("accepts http and data image urls", () => {
    expect(sanitizeAssetUrl("https://example.com/bg.png")).toBe("https://example.com/bg.png");
    expect(sanitizeAssetUrl("data:image/png;base64,AAAA")).toBe("data:image/png;base64,AAAA");
  });

  it("rejects unsupported schemes", () => {
    expect(sanitizeAssetUrl("javascript:alert(1)")).toBe("");
    expect(sanitizeAssetUrl("file:///etc/passwd")).toBe("");
  });

  it("rejects css url metacharacters", () => {
    expect(sanitizeAssetUrl("https://example.com/a)b.png")).toBe("");
    expect(cssUrl("https://example.com/bg.png")).toBe('url("https://example.com/bg.png")');
  });
});
