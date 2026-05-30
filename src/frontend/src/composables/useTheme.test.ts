import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  initializeTheme,
  resetThemeForTests,
  setThemePreference,
  THEME_STORAGE_KEY
} from "./useTheme";

function stubMatchMedia(matches: boolean) {
  let listener: ((event: MediaQueryListEvent) => void) | null = null;
  const mediaQuery = {
    matches,
    media: "(prefers-color-scheme: dark)",
    onchange: null,
    addEventListener: vi.fn((_event: string, callback: (event: MediaQueryListEvent) => void) => {
      listener = callback;
    }),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    dispatch(nextMatches: boolean) {
      this.matches = nextMatches;
      listener?.({ matches: nextMatches } as MediaQueryListEvent);
    }
  };

  vi.stubGlobal("matchMedia", vi.fn(() => mediaQuery as unknown as MediaQueryList));
  return mediaQuery;
}

describe("useTheme", () => {
  beforeEach(() => {
    window.localStorage.clear();
    resetThemeForTests();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    resetThemeForTests();
    window.localStorage.clear();
  });

  it("initializes from stored light preference", () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, "light");
    stubMatchMedia(true);

    initializeTheme();

    expect(document.documentElement.dataset.theme).toBe("light");
    expect(document.documentElement.dataset.themePreference).toBe("light");
  });

  it("resolves system preference and responds to OS changes", () => {
    const mediaQuery = stubMatchMedia(true);

    initializeTheme();

    expect(document.documentElement.dataset.theme).toBe("dark");

    mediaQuery.dispatch(false);

    expect(document.documentElement.dataset.theme).toBe("light");
    expect(document.documentElement.dataset.themePreference).toBe("system");
  });

  it("persists explicit theme changes", () => {
    stubMatchMedia(false);
    initializeTheme();

    setThemePreference("dark");

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe("dark");
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(document.documentElement.style.colorScheme).toBe("dark");
  });
});
