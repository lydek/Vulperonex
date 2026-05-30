import { computed, ref } from "vue";

export type ThemePreference = "light" | "dark" | "system";
export type ResolvedTheme = "light" | "dark";

export const THEME_STORAGE_KEY = "vulperonex.theme";

const preference = ref<ThemePreference>("system");
const resolvedTheme = ref<ResolvedTheme>("light");
let initialized = false;
let mediaQuery: MediaQueryList | null = null;
let mediaQueryCleanup: (() => void) | null = null;

function isThemePreference(value: string | null): value is ThemePreference {
  return value === "light" || value === "dark" || value === "system";
}

function getSystemTheme(): ResolvedTheme {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
    return "light";
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function applyTheme(nextPreference: ThemePreference): void {
  const nextResolved = nextPreference === "system" ? getSystemTheme() : nextPreference;
  preference.value = nextPreference;
  resolvedTheme.value = nextResolved;

  if (typeof document === "undefined") {
    return;
  }

  document.documentElement.dataset.theme = nextResolved;
  document.documentElement.dataset.themePreference = nextPreference;
  document.documentElement.style.colorScheme = nextResolved;
}

function readStoredPreference(): ThemePreference {
  if (typeof window === "undefined") {
    return "system";
  }

  try {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
    return isThemePreference(stored) ? stored : "system";
  } catch {
    return "system";
  }
}

function persistPreference(nextPreference: ThemePreference): void {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, nextPreference);
  } catch {
    // Keep theme switching functional even when storage is unavailable.
  }
}

function watchSystemTheme(): void {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
    return;
  }

  mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
  const handleChange = () => {
    if (preference.value === "system") {
      applyTheme("system");
    }
  };

  if (typeof mediaQuery.addEventListener === "function") {
    mediaQuery.addEventListener("change", handleChange);
    mediaQueryCleanup = () => mediaQuery?.removeEventListener("change", handleChange);
    return;
  }

  mediaQuery.addListener(handleChange);
  mediaQueryCleanup = () => mediaQuery?.removeListener(handleChange);
}

export function initializeTheme(): void {
  if (initialized) {
    return;
  }

  initialized = true;
  applyTheme(readStoredPreference());
  watchSystemTheme();
}

export function setThemePreference(nextPreference: ThemePreference): void {
  persistPreference(nextPreference);
  applyTheme(nextPreference);
}

export function useTheme() {
  initializeTheme();

  return {
    preference,
    resolvedTheme: computed(() => resolvedTheme.value),
    setThemePreference
  };
}

export function resetThemeForTests(): void {
  mediaQueryCleanup?.();
  mediaQueryCleanup = null;
  mediaQuery = null;
  initialized = false;
  preference.value = "system";
  resolvedTheme.value = "light";

  if (typeof document !== "undefined") {
    delete document.documentElement.dataset.theme;
    delete document.documentElement.dataset.themePreference;
    document.documentElement.style.colorScheme = "";
  }
}
