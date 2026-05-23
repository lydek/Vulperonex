<script setup lang="ts">
import type { OverlayHubEvent } from "@/composables/useOverlayHub";

defineProps<{ events: readonly OverlayHubEvent[]; emptyLabel: string }>();
</script>

<template>
  <p v-if="events.length === 0" role="status">{{ emptyLabel }}</p>
  <ul v-else class="event-list chat-preset-default" role="list" data-testid="chat-preset-default">
    <li
      v-for="(event, eventIndex) in events"
      :key="event.eventId ?? `chat-${eventIndex}`"
      class="event-item"
    >
      <strong>{{ event.displayName }}</strong>
      <span
        v-for="(segment, segmentIndex) in event.segments"
        :key="`${event.eventId ?? eventIndex}-${segmentIndex}`"
      >
        {{ segment.text }}
      </span>
    </li>
  </ul>
</template>

<style scoped>
.chat-preset-default {
  display: grid;
  gap: 8px;
}
</style>
