<script setup lang="ts">
import { onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { useOverlayHub } from "@/composables/useOverlayHub";

const { t } = useI18n();
const { events, start } = useOverlayHub("chat");

onMounted(() => {
  void start();
});
</script>

<template>
  <section class="overlay-panel" aria-labelledby="chat-overlay-title">
    <header class="page-header">
      <h1 id="chat-overlay-title" class="page-title">{{ t("overlay.chat.title") }}</h1>
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
  </section>
</template>
