import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "./client";
import { describeApiError } from "./errors";

describe("describeApiError", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should return backend error code when envelope is present", () => {
    const error = new ApiError(400, JSON.stringify({ error: "INVALID_QUERY_PARAM" }));
    expect(describeApiError(error)).toBe("INVALID_QUERY_PARAM");
  });

  it("should log to console.error and return INTERNAL_ERROR for unparsed 5xx", () => {
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    const error = new ApiError(503, "service down");

    expect(describeApiError(error)).toBe("INTERNAL_ERROR");
    expect(consoleError).toHaveBeenCalledWith(
      "API 5xx response",
      expect.objectContaining({ status: 503, body: "service down" })
    );
  });

  it("should return HTTP_<status> for unparsed 4xx", () => {
    const error = new ApiError(404, "");
    expect(describeApiError(error)).toBe("HTTP_404");
  });

  it("should return NETWORK_ERROR for thrown non-ApiError", () => {
    expect(describeApiError(new Error("boom"))).toBe("NETWORK_ERROR");
    expect(describeApiError("string")).toBe("NETWORK_ERROR");
  });
});
