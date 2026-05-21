<script setup lang="ts">
import { RouterLink, RouterView } from "vue-router";
</script>

<template>
  <div class="app-shell">
    <aside class="app-sidebar" aria-label="Primary">
      <div class="brand-block">
        <span class="brand-mark" aria-hidden="true">V</span>
        <span class="brand-name">Vulperonex</span>
      </div>
      <nav class="nav-list">
        <RouterLink to="/" class="nav-link">{{ $t("nav.status") }}</RouterLink>
        <RouterLink to="/overlay/chat" class="nav-link">{{ $t("nav.chatOverlay") }}</RouterLink>
        <RouterLink to="/overlay/alerts" class="nav-link">{{ $t("nav.alertOverlay") }}</RouterLink>
        <RouterLink to="/overlay/member" class="nav-link">{{ $t("nav.memberOverlay") }}</RouterLink>
      </nav>
    </aside>
    <main class="app-main">
      <RouterView />
    </main>
  </div>
</template>
