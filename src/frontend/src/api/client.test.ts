import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, getHealth, postSimulate } from "./client";

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

  it("should post simulate with json body and parse ack when api accepts", async () => {
    const ack = {
      accepted: true,
      eventTypeKey: "user.sent_message",
      eventId: "evt-1",
      platform: "simulation",
      platformUserId: "sim-user",
      displayName: "Sim User",
      occurredAt: "2026-05-21T00:00:00Z"
    };
    const fetchMock = vi.fn(async () => new Response(JSON.stringify(ack), { status: 202 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      postSimulate("chat", { displayName: "Sim User", message: "hi" })
    ).resolves.toEqual(ack);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/simulate/chat",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({ "Content-Type": "application/json" }),
        body: JSON.stringify({ displayName: "Sim User", message: "hi" })
      })
    );
  });

  it("should expose error code on ApiError when api returns 400 with envelope", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(
        JSON.stringify({ error: "UNKNOWN_SIMULATE_EVENT_TYPE" }),
        { status: 400 }
      ))
    );

    try {
      await postSimulate("chat", {});
      throw new Error("expected request to fail");
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).status).toBe(400);
      expect((error as ApiError).errorCode).toBe("UNKNOWN_SIMULATE_EVENT_TYPE");
    }
  });

  it("should return null error code when ApiError body is not json", async () => {
    const error = new ApiError(500, "boom");
    expect(error.errorCode).toBeNull();
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
