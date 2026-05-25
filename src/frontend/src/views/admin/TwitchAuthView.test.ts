import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, storeToRefs } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";
import { createI18n } from "vue-i18n";
import { HubConnectionState } from "@microsoft/signalr";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import { useEventStore } from "@/stores/eventStore";

const stubHubState = ref(HubConnectionState.Connected);

vi.mock("@/composables/useStreamEvents", () => {
  const useStreamEvents = () => {
    const store = useEventStore();
    const { events } = storeToRefs(store);
    return {
      events,
      state: stubHubState,
      error: ref(null),
      start: async () => {},
      stop: async () => {}
    };
  };
  return { useStreamEvents };
});

const { default: TwitchAuthView } = await import("./TwitchAuthView.vue");

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function mountView() {
  return mount(TwitchAuthView, {
    global: {
      plugins: [buildI18n(), createPinia()]
    }
  });
}

function createTwitchAuthFetchMock(options?: {
  statusResponses?: Array<Record<string, unknown>>;
  startResponse?: Response;
  resetResponse?: Response;
  clientIdResponse?: Response;
}) {
  const statusResponses = [...(options?.statusResponses ?? [])];
  const startResponse = options?.startResponse;
  const resetResponse = options?.resetResponse;
  const clientIdResponse = options?.clientIdResponse
    ?? new Response(JSON.stringify({ value: "client-id" }), { status: 200 });

  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input.toString();

    if (url === "/api/twitch/auth/status") {
      const next = statusResponses.shift()
        ?? { clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false };
      return new Response(JSON.stringify(next), { status: 200 });
    }

    if (url === "/api/config/twitch.client_id") {
      return clientIdResponse;
    }

    if (url === "/api/twitch/auth/start" && startResponse) {
      return startResponse;
    }

    if (url === "/api/twitch/auth/token" && init?.method === "DELETE" && resetResponse) {
      return resetResponse;
    }

    return new Response(null, { status: 404 });
  });
}

describe("TwitchAuthView", () => {
  beforeEach(() => {
    vi.stubGlobal("open", vi.fn());
    stubHubState.value = HubConnectionState.Connected;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should show no-Twitch mode banner and disable start when client id is missing", async () => {
    vi.stubGlobal("fetch", createTwitchAuthFetchMock({
      statusResponses: [
        { clientIdConfigured: false, clientSecretConfigured: false, hasRefreshToken: false }
      ],
      clientIdResponse: new Response(JSON.stringify({ value: "" }), { status: 200 })
    }));

    const wrapper = mountView();
    await flushPromises();

    expect(wrapper.find('[data-testid="twitch-no-mode"]').exists()).toBe(true);
    const startButton = wrapper.find('[data-testid="twitch-start"]');
    expect(startButton.attributes("disabled")).toBeDefined();
  });

  it("should call start endpoint and open authorize url in new tab", async () => {
    const fetchMock = createTwitchAuthFetchMock({
      statusResponses: [
        { clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false }
      ],
      startResponse: new Response(
        JSON.stringify({ authorizeUrl: "https://id.twitch.tv/oauth2/authorize?x=1", state: "abc", callbackPort: 7979 }),
        { status: 200 }
      )
    });
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="twitch-start"]').trigger("click");
    await flushPromises();

    expect(fetchMock.mock.calls[2][0]).toBe("/api/twitch/auth/start");
    expect(window.open).toHaveBeenCalledWith(
      "https://id.twitch.tv/oauth2/authorize?x=1",
      "_blank",
      "noopener,noreferrer"
    );
    expect(wrapper.find('[data-testid="twitch-start-url"]').exists()).toBe(true);
  });

  it("should require confirm dialog before reset and refresh status afterwards", async () => {
    const fetchMock = createTwitchAuthFetchMock({
      statusResponses: [
        { clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: true },
        { clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false }
      ],
      resetResponse: new Response(null, { status: 204 })
    });
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="twitch-reset"]').trigger("click");
    await flushPromises();
    expect(wrapper.find("[role='dialog']").exists()).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(2);

    await wrapper.find(".danger-button").trigger("click");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledTimes(5);
    expect(fetchMock.mock.calls[2][0]).toBe("/api/twitch/auth/token");
    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: "DELETE" });
    expect(wrapper.find('[data-testid="twitch-no-token"]').exists()).toBe(true);
  });

  it("should reload status when platform.connection_changed envelope arrives", async () => {
    const fetchMock = createTwitchAuthFetchMock({
      statusResponses: [
        { clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false },
        { clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: true }
      ]
    });
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();
    expect(wrapper.find('[data-testid="twitch-no-token"]').exists()).toBe(true);

    const store = useEventStore();
    store.upsertEvent({
      type: "platform.connection_changed",
      eventId: "evt-conn-1",
      platform: "twitch",
      occurredAt: "2026-05-22T00:00:00Z"
    });
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledTimes(4);
    expect(wrapper.find('[data-testid="twitch-has-token"]').exists()).toBe(true);
  });

  it("should surface error code when start endpoint returns 400", async () => {
    const fetchMock = createTwitchAuthFetchMock({
      statusResponses: [
        { clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false }
      ],
      startResponse: new Response(
        JSON.stringify({ error: "TWITCH_CLIENT_ID_MISSING" }),
        { status: 400 }
      )
    });
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="twitch-start"]').trigger("click");
    await flushPromises();

    expect(wrapper.find('[data-testid="twitch-error"]').text()).toBe("TWITCH_CLIENT_ID_MISSING");
  });
});
