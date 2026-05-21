<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { getHealth, getTwitchAuthStatus, type TwitchAuthStatusResponse } from "@/api/client";
import { useOverlayHub } from "@/composables/useOverlayHub";
import { useStreamEvents } from "@/composables/useStreamEvents";

const { t } = useI18n();
const health = ref<string>("...");
const twitchStatus = ref<TwitchAuthStatusResponse | null>(null);
const { events, state, error: streamError, start } = useStreamEvents();
const chatHub = useOverlayHub("chat");
const alertsHub = useOverlayHub("alerts");
const memberHub = useOverlayHub("member");

const twitchLabel = computed(() => {
  if (!twitchStatus.value?.clientIdConfigured) {
    return t("status.noTwitchMode");
  }

  return twitchStatus.value.hasRefreshToken
    ? t("status.connected")
    : t("status.configured");
});

onMounted(async () => {
  await Promise.all([
    loadHealth(),
    loadTwitchStatus(),
    start(),
    chatHub.start(),
    alertsHub.start(),
    memberHub.start()
  ]);
});

async function loadHealth(): Promise<void> {
  try {
    health.value = (await getHealth()).status;
  } catch {
    health.value = t("status.disconnected");
  }
}

async function loadTwitchStatus(): Promise<void> {
  try {
    twitchStatus.value = await getTwitchAuthStatus();
  } catch {
    twitchStatus.value = null;
  }
}
</script>

<template>
  <section aria-labelledby="status-title">
    <header class="page-header">
      <h1 id="status-title" class="page-title">{{ t("status.title") }}</h1>
      <p class="page-subtitle">{{ t("status.subtitle") }}</p>
    </header>

    <div class="status-grid">
      <article class="status-card">
        <p class="status-label">{{ t("status.apiHealth") }}</p>
        <p class="status-value">{{ health }}</p>
      </article>
      <article class="status-card">
        <p class="status-label">{{ t("status.twitchAuth") }}</p>
        <p class="status-value">{{ twitchLabel }}</p>
      </article>
      <article class="status-card">
        <p class="status-label">SignalR</p>
        <p class="status-value">{{ state }}</p>
        <p v-if="streamError" class="status-error" role="alert">{{ streamError }}</p>
      </article>
      <article class="status-card">
        <p class="status-label">{{ t("status.eventCount") }}</p>
        <p class="status-value">{{ events.length }}</p>
      </article>
      <article class="status-card">
        <p class="status-label">{{ t("status.overlayChatHub") }}</p>
        <HubStatusChip :state="chatHub.state.value" :last-event-at="chatHub.lastEventAt.value" :error="chatHub.error.value" />
      </article>
      <article class="status-card">
        <p class="status-label">{{ t("status.overlayAlertsHub") }}</p>
        <HubStatusChip :state="alertsHub.state.value" :last-event-at="alertsHub.lastEventAt.value" :error="alertsHub.error.value" />
      </article>
      <article class="status-card">
        <p class="status-label">{{ t("status.overlayMemberHub") }}</p>
        <HubStatusChip :state="memberHub.state.value" :last-event-at="memberHub.lastEventAt.value" :error="memberHub.error.value" />
      </article>
    </div>
  </section>
</template>
