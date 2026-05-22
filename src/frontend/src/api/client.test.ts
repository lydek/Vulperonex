import { afterEach, describe, expect, it, vi } from "vitest";
import {
  ApiError,
  clearOverlayHistory,
  createRule,
  deleteRule,
  getEventTypes,
  getHealth,
  getMember,
  getMembers,
  getRule,
  getRules,
  getTwitchAuthStatus,
  postSimulate,
  setRuleEnabled,
  updateRule
} from "./client";

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

  it("should request twitch status", async () => {
    const body = { clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false };
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(body), { status: 200 })));

    await expect(getTwitchAuthStatus()).resolves.toEqual(body);
  });

  it("should list members with platform filter and parse response", async () => {
    const members = [{
      memberId: "M-1",
      identities: [{ platform: "twitch", platformUserId: "u-1" }],
      loyalty: { totalLoyalty: 10, checkInCount: 1 }
    }];
    const fetchMock = vi.fn<typeof fetch>(async () => new Response(JSON.stringify(members), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getMembers({ platform: "twitch", limit: 5, offset: 0 })).resolves.toEqual(members);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/members/?platform=twitch&limit=5&offset=0");
  });

  it("should fetch a single member by id", async () => {
    const member = {
      memberId: "M-2",
      identities: [],
      loyalty: { totalLoyalty: 0, checkInCount: 0 }
    };
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(member), { status: 200 })));

    await expect(getMember("M-2")).resolves.toEqual(member);
  });

  it("should fetch event types", async () => {
    const list = [{ key: "user.message", description: "msg", isSimulatable: true }];
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(list), { status: 200 })));

    await expect(getEventTypes()).resolves.toEqual(list);
  });

  it("should list rules", async () => {
    const rules = [{
      id: "r-1",
      name: "x",
      eventTypeKey: "user.message",
      isEnabled: true,
      priority: 1,
      createdAt: "2026-05-22T00:00:00Z",
      version: 1
    }];
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(rules), { status: 200 })));

    await expect(getRules()).resolves.toEqual(rules);
  });

  it("should fetch rule detail", async () => {
    const rule = {
      id: "r-1",
      name: "x",
      eventTypeKey: "user.message",
      isEnabled: true,
      priority: 1,
      createdAt: "2026-05-22T00:00:00Z",
      version: 1,
      conditions: [],
      actions: [],
      executionMode: "Serial",
      maxParallelism: 1
    };
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(rule), { status: 200 })));

    await expect(getRule("r-1")).resolves.toEqual(rule);
  });

  it("should toggle rule enable/disable via PUT", async () => {
    const fetchMock = vi.fn<typeof fetch>(async () => new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await setRuleEnabled("r-1", true);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/rules/r-1/enable");
    await setRuleEnabled("r-1", false);
    expect(fetchMock.mock.calls[1][0]).toBe("/api/rules/r-1/disable");
  });

  it("should throw ApiError when toggle returns 409", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify({ error: "WORKFLOW_RULE_CONFLICT" }), { status: 409 }))
    );

    await expect(setRuleEnabled("r-1", true)).rejects.toBeInstanceOf(ApiError);
  });

  it("should delete rule and throw ApiError when missing", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response("nope", { status: 404 }));
    vi.stubGlobal("fetch", fetchMock);

    await deleteRule("r-1");
    await expect(deleteRule("missing")).rejects.toBeInstanceOf(ApiError);
  });

  it("should create rule with POST body", async () => {
    const created = {
      id: "r-1",
      name: "x",
      eventTypeKey: "user.message",
      isEnabled: true,
      priority: 1,
      createdAt: "2026-05-22T00:00:00Z",
      version: 1,
      conditions: [],
      actions: [],
      executionMode: "Serial",
      maxParallelism: 1
    };
    const fetchMock = vi.fn<typeof fetch>(async () => new Response(JSON.stringify(created), { status: 201 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      createRule({
        name: "x",
        eventTypeKey: "user.message",
        isEnabled: true,
        priority: 1,
        conditions: [],
        actions: []
      })
    ).resolves.toEqual(created);
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: "POST" });
  });

  it("should update rule with PUT body and throw ApiError on 409", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: "r-1" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ error: "WORKFLOW_RULE_CONFLICT" }), { status: 409 }));
    vi.stubGlobal("fetch", fetchMock);

    await updateRule("r-1", {
      name: "x",
      eventTypeKey: "user.message",
      isEnabled: true,
      priority: 1,
      conditions: [],
      actions: []
    });
    expect(fetchMock.mock.calls[0][0]).toBe("/api/rules/r-1");
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ method: "PUT" });

    await expect(
      updateRule("r-1", {
        name: "x",
        eventTypeKey: "user.message",
        isEnabled: true,
        priority: 1,
        conditions: [],
        actions: []
      })
    ).rejects.toBeInstanceOf(ApiError);
  });

  it("should clear overlay history via DELETE for each hub", async () => {
    const fetchMock = vi.fn<typeof fetch>(async () => new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await clearOverlayHistory("chat");
    await clearOverlayHistory("alerts");
    await clearOverlayHistory("member");

    expect(fetchMock.mock.calls.map((call) => call[0])).toEqual([
      "/api/overlay/chat/messages",
      "/api/overlay/alerts/messages",
      "/api/overlay/member/messages"
    ]);
  });

  it("should throw ApiError when clearOverlayHistory receives error response", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("nope", { status: 500 })));
    await expect(clearOverlayHistory("chat")).rejects.toBeInstanceOf(ApiError);
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
