import { createI18n } from "vue-i18n";
import manifest from "./manifest.json";
import enUS from "./en-US.json";
import zhTW from "./zh-TW.json";

export const i18n = createI18n({
  legacy: false,
  locale: manifest.default,
  fallbackLocale: "en-US",
  missing: (_locale, key) => key,
  messages: {
    "zh-TW": zhTW,
    "en-US": enUS
  }
});
