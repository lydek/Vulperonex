<script setup lang="ts">
import type { OverlayHubEvent } from "@/composables/useOverlayHub";
import ChatMemberChip from "./ChatMemberChip.vue";

defineProps<{ events: readonly OverlayHubEvent[]; emptyLabel: string; showMemberCard?: boolean }>();

function combinedText(event: OverlayHubEvent): string {
  return (event.segments ?? []).map((segment) => segment.text || segment.value || "").join(" ");
}
</script>

<template>
  <p v-if="events.length === 0" role="status">{{ emptyLabel }}</p>
  <div v-else class="member-inline-list" data-testid="chat-preset-member-card">
    <article
      v-for="(event, eventIndex) in events"
      :key="event.eventId ?? `chat-member-card-${eventIndex}`"
      class="member-inline-row"
    >
      <header class="member-inline-row__header">
        <strong class="member-inline-row__name">{{ event.displayName }}</strong>
        <ChatMemberChip
          v-if="showMemberCard && event.memberSnapshot"
          :display-name="event.memberSnapshot.displayName"
          :avatar-url="event.memberSnapshot.avatarUrl"
          :check-in-count="event.memberSnapshot.checkInCount"
        />
      </header>
      <p class="member-inline-row__message">{{ combinedText(event) }}</p>
    </article>
  </div>
</template>

<style scoped>
.member-inline-list {
  display: grid;
  gap: 10px;
}

.member-inline-row {
  padding: 10px 12px;
  border-radius: 14px;
  background: rgba(9, 18, 28, 0.66);
  border: 1px solid rgba(189, 232, 232, 0.18);
  box-shadow: 0 10px 28px rgba(0, 0, 0, 0.18);
}

.member-inline-row__header {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.member-inline-row__name {
  color: #f8fbff;
}

.member-inline-row__message {
  margin: 6px 0 0;
  color: #d6dde7;
}
</style>
