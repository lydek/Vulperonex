<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { useOverlayHub, type OverlayHubEvent } from "@/composables/useOverlayHub";

const { t } = useI18n();
const { events, start, state, lastEventAt, error, clear } = useOverlayHub("alerts");
const confirmOpen = ref(false);
const clearing = ref(false);
const liveAlert = ref<OverlayHubEvent | null>(null);

onMounted(() => {
  void start();
});

const sortedEvents = computed(() => events.value);

watch(events, (next, prev) => {
  if (!next.length) {
    liveAlert.value = null;
    return;
  }

  const newest = next[0];
  if (newest.replayed) {
    return;
  }

  const previousNewest = prev?.[0];
  if (!previousNewest || newest.eventId !== previousNewest.eventId) {
    triggerLiveAlert(newest);
  }
});

function triggerLiveAlert(payload: OverlayHubEvent): void {
  liveAlert.value = payload;
  window.setTimeout(() => {
    if (liveAlert.value === payload) {
      liveAlert.value = null;
    }
  }, 3500);
}

async function onConfirm(): Promise<void> {
  clearing.value = true;
  try {
    await clear();
  } finally {
    clearing.value = false;
    confirmOpen.value = false;
  }
}
</script>

<template>
  <section class="overlay-panel" aria-labelledby="alert-overlay-title">
    <header class="page-header">
      <h1 id="alert-overlay-title" class="page-title">{{ t("overlay.alerts.title") }}</h1>
      <div class="overlay-toolbar">
        <HubStatusChip :state="state" :last-event-at="lastEventAt" :error="error" />
        <button
          type="button"
          class="icon-button"
          :aria-label="t('overlay.clearAriaLabel', { hub: t('overlay.alerts.title') })"
          @click="confirmOpen = true"
        >
          {{ t("overlay.clear") }}
        </button>
      </div>
    </header>

    <div
      v-if="liveAlert"
      class="live-alert"
      role="alert"
      data-testid="live-alert"
    >
      <strong>{{ liveAlert.displayName }}</strong>
      <span>{{ liveAlert.eventType }}</span>
    </div>

    <p v-if="sortedEvents.length === 0" role="status">{{ t("overlay.empty") }}</p>
    <ul v-else class="event-list" role="list">
      <li
        v-for="(event, eventIndex) in sortedEvents"
        :key="event.eventId ?? `alert-${eventIndex}`"
        class="event-item"
        :data-replayed="event.replayed ? 'true' : 'false'"
      >
        <strong>{{ event.displayName }}</strong>
        <span>{{ event.eventType }}</span>
      </li>
    </ul>

    <ConfirmDialog
      :open="confirmOpen"
      :title="t('overlay.clearConfirmTitle')"
      :message="t('overlay.clearConfirmMessage', { hub: t('overlay.alerts.title') })"
      :confirm-label="t('overlay.clearConfirmAction')"
      :cancel-label="t('common.cancel')"
      :busy="clearing"
      @confirm="onConfirm"
      @cancel="confirmOpen = false"
    />
  </section>
</template>
