<script setup lang="ts">
import { computed } from "vue";
import type { OverlayHubEvent } from "@/composables/useOverlayHub";
import ChatMemberChip from "./ChatMemberChip.vue";

const props = defineProps<{ events: readonly OverlayHubEvent[]; emptyLabel: string; showMemberCard?: boolean; isPreview?: boolean }>();

const orderedEvents = computed(() => [...props.events].reverse());

function combinedText(event: OverlayHubEvent): string {
  return (event.segments ?? []).map((segment) => segment.text || segment.value || "").join(" ");
}
</script>

<template>
  <p v-if="orderedEvents.length === 0 && !props.isPreview" role="status">{{ emptyLabel }}</p>
  <div v-else class="member-inline-list" data-testid="chat-preset-member-card">
    <article
      v-for="(event, eventIndex) in orderedEvents"
      :key="event.eventId ?? `chat-member-card-${eventIndex}`"
      class="member-inline-row"
    >
      <header class="member-inline-row__header">
        <strong class="member-inline-row__name">{{ event.displayName }}</strong>
        <span
          v-for="(role, roleIndex) in event.roles"
          :key="`member-role-${roleIndex}`"
          class="member-inline-row__role"
        >
          {{ role }}
        </span>
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

.member-inline-row__role {
  display: inline-flex;
  align-items: center;
  padding: 2px 7px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.1);
  color: #d6dde7;
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
}

.member-inline-row__message {
  margin: 6px 0 0;
  color: #d6dde7;
}
</style>
