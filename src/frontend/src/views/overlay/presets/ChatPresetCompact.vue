<script setup lang="ts">
import { computed } from "vue";
import type { OverlayHubEvent } from "@/composables/useOverlayHub";

const props = defineProps<{ events: readonly OverlayHubEvent[]; emptyLabel: string; isPreview?: boolean }>();

function combinedText(event: OverlayHubEvent): string {
  if (!event.segments) {
    return "";
  }
  return event.segments.map((segment) => segment.text || segment.value || "").join(" ");
}

const recent = computed(() => [...props.events.slice(0, 10)].reverse());
</script>

<template>
  <p v-if="recent.length === 0 && !props.isPreview" role="status">{{ emptyLabel }}</p>
  <ol v-else class="chat-preset-compact" role="list" data-testid="chat-preset-compact">
    <li
      v-for="(event, eventIndex) in recent"
      :key="event.eventId ?? `chat-compact-${eventIndex}`"
      class="chat-preset-compact__row"
    >
      <span class="chat-preset-compact__name">{{ event.displayName }}</span>
      <span class="chat-preset-compact__divider">›</span>
      <span class="chat-preset-compact__message">{{ combinedText(event) }}</span>
    </li>
  </ol>
</template>

<style scoped>
.chat-preset-compact {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 4px;
  font-size: 14px;
  line-height: 1.4;
}

.chat-preset-compact__row {
  display: flex;
  gap: 6px;
  padding: 4px 8px;
  background: rgba(0, 0, 0, 0.04);
  border-radius: 6px;
}

.chat-preset-compact__name {
  font-weight: 600;
  color: #164f48;
}

.chat-preset-compact__divider {
  color: #97a3b3;
}

.chat-preset-compact__message {
  color: #1f2937;
  word-break: break-word;
}
</style>
