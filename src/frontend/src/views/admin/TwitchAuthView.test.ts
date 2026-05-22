import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import TwitchAuthView from "./TwitchAuthView.vue";

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

describe("TwitchAuthView", () => {
  beforeEach(() => {
    vi.stubGlobal("open", vi.fn());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should show no-Twitch mode banner and disable start when client id is missing", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(
        JSON.stringify({ clientIdConfigured: false, clientSecretConfigured: false, hasRefreshToken: false }),
        { status: 200 }
      ))
    );

    const wrapper = mountView();
    await flushPromises();

    expect(wrapper.find('[data-testid="twitch-no-mode"]').exists()).toBe(true);
    const startButton = wrapper.find('[data-testid="twitch-start"]');
    expect(startButton.attributes("disabled")).toBeDefined();
  });

  it("should call start endpoint and open authorize url in new tab", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false }),
        { status: 200 }
      ))
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ authorizeUrl: "https://id.twitch.tv/oauth2/authorize?x=1", state: "abc", callbackPort: 7979 }),
        { status: 200 }
      ));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="twitch-start"]').trigger("click");
    await flushPromises();

    expect(fetchMock.mock.calls[1][0]).toBe("/api/twitch/auth/start");
    expect(window.open).toHaveBeenCalledWith(
      "https://id.twitch.tv/oauth2/authorize?x=1",
      "_blank",
      "noopener,noreferrer"
    );
    expect(wrapper.find('[data-testid="twitch-start-url"]').exists()).toBe(true);
  });

  it("should require confirm dialog before reset and refresh status afterwards", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: true }),
        { status: 200 }
      ))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false }),
        { status: 200 }
      ));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="twitch-reset"]').trigger("click");
    await flushPromises();
    expect(wrapper.find("[role='dialog']").exists()).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await wrapper.find(".danger-button").trigger("click");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[1][0]).toBe("/api/twitch/auth/token");
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: "DELETE" });
    expect(wrapper.find('[data-testid="twitch-no-token"]').exists()).toBe(true);
  });

  it("should surface error code when start endpoint returns 400", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ clientIdConfigured: true, clientSecretConfigured: true, hasRefreshToken: false }),
        { status: 200 }
      ))
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ error: "TWITCH_CLIENT_ID_MISSING" }),
        { status: 400 }
      ));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="twitch-start"]').trigger("click");
    await flushPromises();

    expect(wrapper.find('[data-testid="twitch-error"]').text()).toBe("TWITCH_CLIENT_ID_MISSING");
  });
});
