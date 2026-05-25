<script setup lang="ts">
import { computed, onBeforeUnmount, watchEffect } from "vue";
import { RouterLink, RouterView } from "vue-router";
import { useRoute } from "vue-router";

const route = useRoute();

const isPreviewShell = computed(() =>
  route.path.startsWith("/overlay/") && route.query.preview === "1"
);

watchEffect(() => {
  const enabled = isPreviewShell.value;
  document.documentElement.classList.toggle("overlay-preview-shell", enabled);
  document.body.classList.toggle("overlay-preview-shell", enabled);
});

onBeforeUnmount(() => {
  document.documentElement.classList.remove("overlay-preview-shell");
  document.body.classList.remove("overlay-preview-shell");
});
</script>

<template>
  <div :class="isPreviewShell ? 'app-shell app-shell--bare' : 'app-shell'">
    <aside v-if="!isPreviewShell" class="app-sidebar" aria-label="Primary">
      <div class="app-sidebar__inner">
        <div class="brand-block">
          <span class="brand-mark" aria-hidden="true">V</span>
          <span class="brand-name">Vulperonex</span>
        </div>
        <nav class="nav-list">
          <RouterLink to="/monitor" class="nav-link">{{ $t("nav.monitor") }}</RouterLink>
          <RouterLink to="/status" class="nav-link">{{ $t("nav.status") }}</RouterLink>
          <RouterLink to="/simulate" class="nav-link">{{ $t("nav.simulate") }}</RouterLink>
          <RouterLink to="/events" class="nav-link">{{ $t("nav.eventMonitor") }}</RouterLink>
          <RouterLink to="/members" class="nav-link">{{ $t("nav.members") }}</RouterLink>
          <RouterLink to="/overlay-presets" class="nav-link">{{ $t("nav.overlayPresets") }}</RouterLink>
          <RouterLink to="/settings" class="nav-link">{{ $t("nav.settings") }}</RouterLink>
          <RouterLink to="/rules" class="nav-link">{{ $t("nav.rules") }}</RouterLink>
          <RouterLink to="/timers" class="nav-link">{{ $t("nav.timers") }}</RouterLink>
          <RouterLink to="/chat-outbox" class="nav-link">{{ $t("nav.chatOutbox") }}</RouterLink>
          <RouterLink to="/twitch" class="nav-link">{{ $t("nav.twitchAuth") }}</RouterLink>
          <RouterLink to="/overlay/chat" class="nav-link">{{ $t("nav.chatOverlay") }}</RouterLink>
          <RouterLink to="/overlay/alerts" class="nav-link">{{ $t("nav.alertOverlay") }}</RouterLink>
          <RouterLink to="/overlay/member" class="nav-link">{{ $t("nav.memberOverlay") }}</RouterLink>
        </nav>
      </div>
    </aside>
    <main :class="isPreviewShell ? 'app-main app-main--bare' : 'app-main'">
      <RouterView />
    </main>
  </div>
</template>
