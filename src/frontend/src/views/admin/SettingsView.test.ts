import { flushPromises, mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import { resetThemeForTests, THEME_STORAGE_KEY } from "@/composables/useTheme";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import SettingsView from "./SettingsView.vue";

const apiMocks = vi.hoisted(() => ({
  getConfigValue: vi.fn(),
  getPluginModules: vi.fn(),
  setConfigValue: vi.fn(),
  togglePluginModule: vi.fn()
}));

vi.mock("@/api/client", () => ({
  ApiError: class ApiError extends Error {
    constructor(
      public readonly status: number,
      public readonly body: string
    ) {
      super(`HTTP ${status}`);
    }

    get errorCode(): string | null {
      try {
        const parsed = JSON.parse(this.body) as { error?: string };
        return parsed.error ?? null;
      } catch {
        return null;
      }
    }
  },
  getConfigValue: apiMocks.getConfigValue,
  getPluginModules: apiMocks.getPluginModules,
  setConfigValue: apiMocks.setConfigValue,
  togglePluginModule: apiMocks.togglePluginModule
}));

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("SettingsView", () => {
  beforeEach(() => {
    apiMocks.getConfigValue.mockReset();
    apiMocks.getPluginModules.mockReset();
    apiMocks.setConfigValue.mockReset();
    apiMocks.togglePluginModule.mockReset();
    resetThemeForTests();
    window.localStorage.clear();
    apiMocks.getConfigValue.mockImplementation(async (key: string) => {
      const defaults: Record<string, string | null> = {
        "overlay.chat.assistant_display_name": "",
        "overlay.chat.assistant_avatar_url": "",
        "overlay.chat.checkin_display_name": "",
        "workflow.chat.output_destination": "dual",
        "checkin.reset_time_local": "05:00",
        "checkin.repeat_card_enabled": "true"
      };
      return { key, value: defaults[key] ?? null };
    });
  });

  it("renders module cards from the API", async () => {
    apiMocks.getPluginModules.mockResolvedValue([
      {
        name: "member",
        displayName: "Member Module",
        kind: "core",
        enabled: true,
        dependencies: [],
        dependents: ["checkin"]
      }
    ]);

    const wrapper = mount(SettingsView, {
      global: {
        plugins: [buildI18n()]
      }
    });

    await flushPromises();
    await wrapper.get('[data-testid="settings-tab-modules"]').trigger("click");

    expect(wrapper.get('[data-testid="module-card-member"]').text()).toContain("Member Module");
    expect(wrapper.text()).toContain("Enabled");
  });

  it("toggles a module after confirmation", async () => {
    apiMocks.getPluginModules.mockResolvedValue([
      {
        name: "member",
        displayName: "Member Module",
        kind: "core",
        enabled: true,
        dependencies: [],
        dependents: ["checkin"]
      },
      {
        name: "checkin",
        displayName: "Check-In Module",
        kind: "core",
        enabled: true,
        dependencies: ["member"],
        dependents: []
      }
    ]);
    apiMocks.togglePluginModule.mockResolvedValue({
      module: {
        name: "member",
        displayName: "Member Module",
        kind: "core",
        enabled: false,
        dependencies: [],
        dependents: ["checkin"]
      },
      changedModules: [
        {
          name: "checkin",
          displayName: "Check-In Module",
          kind: "core",
          enabled: false,
          dependencies: ["member"],
          dependents: []
        },
        {
          name: "member",
          displayName: "Member Module",
          kind: "core",
          enabled: false,
          dependencies: [],
          dependents: ["checkin"]
        }
      ]
    });

    const wrapper = mount(SettingsView, {
      global: {
        plugins: [buildI18n()]
      }
    });

    await flushPromises();
    await wrapper.get('[data-testid="settings-tab-modules"]').trigger("click");
    await wrapper.get('[data-testid="module-card-member"] .module-card__toggle').trigger("click");
    await wrapper.get(".danger-button").trigger("click");
    await flushPromises();

    expect(apiMocks.togglePluginModule).toHaveBeenCalledWith("member", false);
    expect(wrapper.get('[data-testid="module-card-member"]').text()).toContain("Disabled");
    expect(wrapper.get('[data-testid="module-card-checkin"]').text()).toContain("Disabled");
  });

  it("updates the theme preference from settings", async () => {
    apiMocks.getPluginModules.mockResolvedValue([]);

    const wrapper = mount(SettingsView, {
      global: {
        plugins: [buildI18n()]
      }
    });

    await flushPromises();
    await wrapper.get('[data-testid="theme-preference-select"]').setValue("dark");

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe("dark");
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(wrapper.get('[data-testid="theme-resolved"]').text()).toContain("Dark");
  });

  it("renders workflow chat assistant settings from config", async () => {
    apiMocks.getPluginModules.mockResolvedValue([]);
    apiMocks.getConfigValue.mockImplementation(async (key: string) => {
      const values: Record<string, string | null> = {
        "overlay.chat.assistant_display_name": "Helper Core",
        "overlay.chat.assistant_avatar_url": "https://cdn.test/helper.png",
        "overlay.chat.checkin_display_name": "Check-In HQ",
        "workflow.chat.output_destination": "overlay_only",
        "checkin.reset_time_local": "06:30",
        "checkin.repeat_card_enabled": "false"
      };
      return { key, value: values[key] ?? null };
    });

    const wrapper = mount(SettingsView, {
      global: {
        plugins: [buildI18n()]
      }
    });

    await flushPromises();
    await wrapper.get('[data-testid="settings-tab-workflow-chat"]').trigger("click");

    expect((wrapper.get('[data-testid="workflow-chat-display-name"]').element as HTMLInputElement).value).toBe("Helper Core");
    expect((wrapper.get('[data-testid="workflow-chat-avatar-url"]').element as HTMLInputElement).value).toBe("https://cdn.test/helper.png");
    expect((wrapper.get('[data-testid="workflow-chat-checkin-display-name"]').element as HTMLInputElement).value).toBe("Check-In HQ");
    expect((wrapper.get('[data-testid="workflow-chat-output-destination"]').element as HTMLSelectElement).value).toBe("overlay_only");
    expect(wrapper.get('[data-testid="assistant-preview"]').text()).toContain("Helper Core");
    expect(wrapper.get('[data-testid="checkin-preview"]').text()).toContain("Check-In HQ");
  });

  it("saves workflow chat assistant settings", async () => {
    apiMocks.getPluginModules.mockResolvedValue([]);
    apiMocks.setConfigValue.mockResolvedValue(undefined);

    const wrapper = mount(SettingsView, {
      global: {
        plugins: [buildI18n()]
      }
    });

    await flushPromises();
    await wrapper.get('[data-testid="settings-tab-workflow-chat"]').trigger("click");
    await wrapper.get('[data-testid="workflow-chat-display-name"]').setValue("System Pixie");
    await wrapper.get('[data-testid="workflow-chat-avatar-url"]').setValue("https://cdn.test/pixie.png");
    await wrapper.get('[data-testid="workflow-chat-checkin-display-name"]').setValue("Stamp Keeper");
    await wrapper.get('[data-testid="workflow-chat-output-destination"]').setValue("platform_only");
    await wrapper.get(".settings-section__action").trigger("click");
    await flushPromises();

    expect(apiMocks.setConfigValue).toHaveBeenCalledWith("overlay.chat.assistant_display_name", "System Pixie");
    expect(apiMocks.setConfigValue).toHaveBeenCalledWith("overlay.chat.assistant_avatar_url", "https://cdn.test/pixie.png");
    expect(apiMocks.setConfigValue).toHaveBeenCalledWith("overlay.chat.checkin_display_name", "Stamp Keeper");
    expect(apiMocks.setConfigValue).toHaveBeenCalledWith("workflow.chat.output_destination", "platform_only");
  });

  it("shows check-in settings only when the check-in module is enabled", async () => {
    apiMocks.getPluginModules.mockResolvedValue([
      {
        name: "checkin",
        displayName: "Check-In Module",
        kind: "core",
        enabled: true,
        dependencies: ["member"],
        dependents: []
      }
    ]);

    const wrapper = mount(SettingsView, {
      global: {
        plugins: [buildI18n()]
      }
    });

    await flushPromises();
    expect(wrapper.find('[data-testid="settings-tab-checkin"]').exists()).toBe(true);

    await wrapper.get('[data-testid="settings-tab-checkin"]').trigger("click");
    expect((wrapper.get('[data-testid="checkin-reset-time"]').element as HTMLInputElement).value).toBe("05:00");
    expect((wrapper.get('[data-testid="checkin-repeat-card-enabled"]').element as HTMLInputElement).checked).toBe(true);
  });

  it("saves check-in reset settings", async () => {
    apiMocks.getPluginModules.mockResolvedValue([
      {
        name: "checkin",
        displayName: "Check-In Module",
        kind: "core",
        enabled: true,
        dependencies: ["member"],
        dependents: []
      }
    ]);
    apiMocks.setConfigValue.mockResolvedValue(undefined);

    const wrapper = mount(SettingsView, {
      global: {
        plugins: [buildI18n()]
      }
    });

    await flushPromises();
    await wrapper.get('[data-testid="settings-tab-checkin"]').trigger("click");
    await wrapper.get('[data-testid="checkin-reset-time"]').setValue("06:30");
    await wrapper.get('[data-testid="checkin-repeat-card-enabled"]').setValue(false);
    await wrapper.get(".settings-section__action").trigger("click");
    await flushPromises();

    expect(apiMocks.setConfigValue).toHaveBeenCalledWith("checkin.reset_time_local", "06:30");
    expect(apiMocks.setConfigValue).toHaveBeenCalledWith("checkin.repeat_card_enabled", "false");
  });
});
