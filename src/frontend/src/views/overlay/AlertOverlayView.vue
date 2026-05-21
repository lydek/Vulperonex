<script setup lang="ts">
import { onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { useOverlayHub } from "@/composables/useOverlayHub";

const { t } = useI18n();
const { events, start } = useOverlayHub("alerts");

onMounted(() => {
  void start();
});
</script>

<template>
  <section class="overlay-panel" aria-labelledby="alert-overlay-title">
    <header class="page-header">
      <h1 id="alert-overlay-title" class="page-title">{{ t("overlay.alerts.title") }}</h1>
    </header>
    <p v-if="events.length === 0" role="status">{{ t("overlay.empty") }}</p>
    <ul v-else class="event-list" role="list">
      <li v-for="(event, eventIndex) in events" :key="event.eventId ?? `alert-${eventIndex}`" class="event-item">
        <strong>{{ event.displayName }}</strong>
        <span>{{ event.eventType }}</span>
      </li>
    </ul>
  </section>
</template>
