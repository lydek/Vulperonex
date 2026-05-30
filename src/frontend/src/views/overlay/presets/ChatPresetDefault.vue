<script setup lang="ts">
import { computed } from "vue";
import type { OverlayHubEvent } from "@/composables/useOverlayHub";
import ChatMemberChip from "./ChatMemberChip.vue";

const props = defineProps<{ events: readonly OverlayHubEvent[]; emptyLabel: string; showMemberCard?: boolean; isPreview?: boolean }>();

const orderedEvents = computed(() => [...props.events].reverse());

function getSegmentText(segment: { text?: string; value?: string }): string {
  return segment.text || segment.value || "";
}

function getSegmentType(segment: { kind?: string; type?: string }): string {
  return segment.kind || segment.type || "text";
}
</script>

<template>
  <p v-if="orderedEvents.length === 0 && !props.isPreview" role="status" class="empty-label">{{ emptyLabel }}</p>

  <transition-group
    v-else
    name="ice-spring"
    tag="div"
    :class="['chat-container chat-preset-default', { 'is-preview': props.isPreview }]"
    role="list"
    data-testid="chat-preset-default"
  >
    <div
      v-for="(event, eventIndex) in orderedEvents"
      :key="event.eventId ?? `chat-${eventIndex}`"
      class="chat-line"
      role="listitem"
    >
      <img
        v-for="(badgeUrl, badgeIndex) in event.badges"
        :key="`badge-${badgeIndex}`"
        :src="badgeUrl"
        class="chat-badge"
        alt="badge"
        @error="($event.target as HTMLImageElement).style.display = 'none'"
      />

      <span class="chat-username" :style="{ color: event.colorHex || 'var(--twitch-purple-light)' }">
        {{ event.displayName || "Unknown user" }}
      </span>

      <span class="chat-colon">:</span>

      <span class="chat-content">
        <template v-for="(segment, segmentIndex) in event.segments" :key="`seg-${segmentIndex}`">
          <img
            v-if="getSegmentType(segment) === 'emote'"
            :src="getSegmentText(segment)"
            class="chat-emote"
            alt="emote"
          />
          <span v-else>{{ getSegmentText(segment) }}</span>
        </template>
      </span>

      <ChatMemberChip
        v-if="showMemberCard && event.memberSnapshot"
        :display-name="event.memberSnapshot.displayName"
        :avatar-url="event.memberSnapshot.avatarUrl"
        :check-in-count="event.memberSnapshot.checkInCount"
      />
    </div>
  </transition-group>
</template>

<style scoped>
.chat-preset-default {
  --twitch-purple-light: #bf94ff;
  --text-shadow-heavy: 1px 1px 2px rgba(0, 0, 0, 0.85);
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  align-items: flex-start;
  height: calc(100vh - 120px);
  gap: 8px;
  width: 100%;
  box-sizing: border-box;
  overflow: hidden;
  padding: 10px;
  background: transparent;
}

.chat-preset-default.is-preview {
  height: 100vh;
  padding: 18px 24px;
}

.empty-label {
  color: #97a3b3;
  font-style: italic;
  padding: 10px;
}

.chat-line {
  position: relative;
  border: none;
  padding: 2px 4px;
  display: block;
  width: 100%;
  word-wrap: break-word;
  box-sizing: border-box;
  transition: transform 0.3s ease;
  line-height: 1.4;
}

.chat-badge {
  height: 18px;
  vertical-align: middle;
  margin-right: 6px;
  margin-bottom: 2px;
  border-radius: 2px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
  display: inline-block;
}

.chat-username,
.chat-colon,
.chat-content {
  vertical-align: middle;
  text-shadow: var(--text-shadow-heavy);
}

.chat-username,
.chat-colon {
  font-size: 1.05rem;
  font-weight: 900;
}

.chat-colon {
  color: #efeff1;
  margin-right: 6px;
}

.chat-content {
  font-size: 1.05rem;
  color: #fff;
  line-height: 1.4;
  font-weight: 700;
  word-break: break-word;
  overflow-wrap: anywhere;
}

.chat-emote {
  vertical-align: middle;
  margin: -4px 2px;
  height: 1.8em;
  display: inline-block;
}

.ice-spring-enter-active {
  animation: iceSpring 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275) both;
}

.ice-spring-leave-active {
  transition: all 0.3s ease;
  position: absolute;
}

.ice-spring-leave-to {
  opacity: 0;
  transform: translateX(-30px);
}

.ice-spring-move {
  transition: transform 0.3s ease;
}

@keyframes iceSpring {
  0% {
    opacity: 0;
    transform: translateX(-20px) scale(0.8);
  }

  100% {
    opacity: 1;
    transform: translateX(0) scale(1);
  }
}
</style>
