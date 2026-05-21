import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, getHealth } from "./client";

describe("api client", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should request health from relative base when no VITE_API_URL is configured", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ status: "ok" }))));

    await expect(getHealth()).resolves.toEqual({ status: "ok" });
    expect(fetch).toHaveBeenCalledWith("/health", expect.objectContaining({
      headers: { Accept: "application/json" }
    }));
  });

  it("should throw ApiError when backend returns non-success status", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("bad", { status: 500 })));

    await expect(getHealth()).rejects.toBeInstanceOf(ApiError);
  });

  it("should preserve ApiError status and response body", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("bad", { status: 503 })));

    try {
      await getHealth();
      throw new Error("expected request to fail");
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).status).toBe(503);
      expect((error as ApiError).body).toBe("bad");
    }
  });
});
