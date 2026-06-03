<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { OverlayHubEvent } from "@/composables/useOverlayHub";
import ChatCheckInCard from "./ChatCheckInCard.vue";

const props = defineProps<{ events: readonly OverlayHubEvent[]; emptyLabel: string; isPreview?: boolean }>();
const { t } = useI18n();

function combinedText(event: OverlayHubEvent): string {
  return (event.segments ?? []).map((segment) => segment.text || segment.value || "").join(" ");
}

function getTitle(event: OverlayHubEvent): string {
  if (event.variant === "assistant") {
    return event.displayName || t("overlay.chat.systemAssistant");
  }

  if (event.variant === "checkin-card") {
    return event.displayName || t("overlay.chat.checkInSystem");
  }

  return event.displayName || "Unknown user";
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
      <template v-if="event.variant === 'checkin-card' && event.memberSnapshot">
        <div class="chat-preset-compact__system-card">
          <strong class="chat-preset-compact__name">{{ getTitle(event) }}</strong>
          <ChatCheckInCard
            :display-name="event.memberSnapshot.displayName"
            :avatar-url="event.memberSnapshot.avatarUrl"
            :check-in-count="event.memberSnapshot.checkInCount"
          />
        </div>
      </template>
      <template v-else>
        <span class="chat-preset-compact__name">{{ getTitle(event) }}</span>
        <span class="chat-preset-compact__divider">›</span>
        <span class="chat-preset-compact__message">{{ combinedText(event) }}</span>
      </template>
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

.chat-preset-compact__system-card {
  display: grid;
  gap: 8px;
  width: 100%;
}
</style>
