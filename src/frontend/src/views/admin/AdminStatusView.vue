<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { getHealth, getTwitchAuthStatus, type TwitchAuthStatusResponse } from "@/api/client";
import { useOverlayHub, type OverlayHubName } from "@/composables/useOverlayHub";
import { useStreamEvents } from "@/composables/useStreamEvents";

const { t } = useI18n();
const health = ref<string>("...");
const twitchStatus = ref<TwitchAuthStatusResponse | null>(null);
const { events, state, error: streamError, start } = useStreamEvents();
const chatHub = useOverlayHub("chat");
const alertsHub = useOverlayHub("alerts");
const memberHub = useOverlayHub("member");

const confirmTarget = ref<OverlayHubName | null>(null);
const clearing = ref(false);

const overlayHubs = computed(() => [
  { name: "chat" as const, labelKey: "status.overlayChatHub", titleKey: "overlay.chat.title", hub: chatHub },
  { name: "alerts" as const, labelKey: "status.overlayAlertsHub", titleKey: "overlay.alerts.title", hub: alertsHub },
  { name: "member" as const, labelKey: "status.overlayMemberHub", titleKey: "overlay.member.title", hub: memberHub }
]);

const confirmTitleKey = computed(() => {
  if (!confirmTarget.value) return "";
  const entry = overlayHubs.value.find((row) => row.name === confirmTarget.value);
  return entry?.titleKey ?? "";
});

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

function requestClear(name: OverlayHubName): void {
  confirmTarget.value = name;
}

async function onConfirm(): Promise<void> {
  const target = confirmTarget.value;
  if (!target) return;

  const entry = overlayHubs.value.find((row) => row.name === target);
  if (!entry) {
    confirmTarget.value = null;
    return;
  }

  clearing.value = true;
  try {
    await entry.hub.clear();
  } finally {
    clearing.value = false;
    confirmTarget.value = null;
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
      <article
        v-for="entry in overlayHubs"
        :key="entry.name"
        class="status-card"
      >
        <p class="status-label">{{ t(entry.labelKey) }}</p>
        <HubStatusChip
          :state="entry.hub.state.value"
          :last-event-at="entry.hub.lastEventAt.value"
          :error="entry.hub.error.value"
        />
        <div class="status-card-actions">
          <button
            type="button"
            class="icon-button"
            :aria-label="t('overlay.clearAriaLabel', { hub: t(entry.titleKey) })"
            :data-testid="`clear-${entry.name}`"
            @click="requestClear(entry.name)"
          >
            {{ t("overlay.clear") }}
          </button>
        </div>
      </article>
    </div>

    <ConfirmDialog
      :open="confirmTarget !== null"
      :title="t('overlay.clearConfirmTitle')"
      :message="t('overlay.clearConfirmMessage', { hub: confirmTitleKey ? t(confirmTitleKey) : '' })"
      :confirm-label="t('overlay.clearConfirmAction')"
      :cancel-label="t('common.cancel')"
      :busy="clearing"
      @confirm="onConfirm"
      @cancel="confirmTarget = null"
    />
  </section>
</template>
