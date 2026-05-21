<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { useOverlayHub } from "@/composables/useOverlayHub";

const { t } = useI18n();
const { events, start, state, lastEventAt, error, clear } = useOverlayHub("chat");
const confirmOpen = ref(false);
const clearing = ref(false);

onMounted(() => {
  void start();
});

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
  <section class="overlay-panel" aria-labelledby="chat-overlay-title">
    <header class="page-header">
      <h1 id="chat-overlay-title" class="page-title">{{ t("overlay.chat.title") }}</h1>
      <div class="overlay-toolbar">
        <HubStatusChip :state="state" :last-event-at="lastEventAt" :error="error" />
        <button
          type="button"
          class="icon-button"
          :aria-label="t('overlay.clearAriaLabel', { hub: t('overlay.chat.title') })"
          @click="confirmOpen = true"
        >
          {{ t("overlay.clear") }}
        </button>
      </div>
    </header>
    <p v-if="events.length === 0" role="status">{{ t("overlay.empty") }}</p>
    <ul v-else class="event-list" role="list">
      <li v-for="(event, eventIndex) in events" :key="event.eventId ?? `chat-${eventIndex}`" class="event-item">
        <strong>{{ event.displayName }}</strong>
        <span v-for="(segment, segmentIndex) in event.segments" :key="`${event.eventId ?? eventIndex}-${segmentIndex}`">
          {{ segment.text }}
        </span>
      </li>
    </ul>

    <ConfirmDialog
      :open="confirmOpen"
      :title="t('overlay.clearConfirmTitle')"
      :message="t('overlay.clearConfirmMessage', { hub: t('overlay.chat.title') })"
      :confirm-label="t('overlay.clearConfirmAction')"
      :cancel-label="t('common.cancel')"
      :busy="clearing"
      @confirm="onConfirm"
      @cancel="confirmOpen = false"
    />
  </section>
</template>
