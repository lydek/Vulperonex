import { flushPromises, mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import { resetThemeForTests, THEME_STORAGE_KEY } from "@/composables/useTheme";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import SettingsView from "./SettingsView.vue";

const apiMocks = vi.hoisted(() => ({
  getPluginModules: vi.fn(),
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
  getPluginModules: apiMocks.getPluginModules,
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
    apiMocks.getPluginModules.mockReset();
    apiMocks.togglePluginModule.mockReset();
    resetThemeForTests();
    window.localStorage.clear();
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
});
