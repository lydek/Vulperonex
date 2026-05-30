import "@unocss/reset/tailwind.css";
import "virtual:uno.css";
import { createApp } from "vue";
import PrimeVue from "primevue/config";
import { createPinia } from "pinia";
import App from "./App.vue";
import { i18n } from "./i18n";
import { router } from "./router";
import { initializeTheme } from "./composables/useTheme";
import "./styles/app.css";
import "./styles/monitor-tokens.css";

initializeTheme();

createApp(App)
  .use(createPinia())
  .use(router)
  .use(i18n)
  .use(PrimeVue, { unstyled: true })
  .mount("#app");
