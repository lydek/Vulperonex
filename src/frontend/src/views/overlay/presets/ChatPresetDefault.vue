<script setup lang="ts">
import type { OverlayHubEvent } from "@/composables/useOverlayHub";

defineProps<{ events: readonly OverlayHubEvent[]; emptyLabel: string }>();

// ?? Segment ??摮摰?(?詨捆 text ??value)
function getSegmentText(segment: any): string {
  return segment.text || segment.value || "";
}

// ?? Segment ????(?詨捆 kind ??type)
function getSegmentType(segment: any): string {
  return segment.kind || segment.type || "text";
}
</script>

<template>
  <p v-if="events.length === 0" role="status" class="empty-label">{{ emptyLabel }}</p>

  <transition-group
    v-else
    name="ice-spring"
    tag="div"
    class="chat-container chat-preset-default"
    role="list"
    data-testid="chat-preset-default"
  >
    <div
      v-for="(event, eventIndex) in events"
      :key="event.eventId ?? `chat-${eventIndex}`"
      class="chat-line"
      role="listitem"
    >
      <!-- 蝬 KapChat: 敺賜??典?蝔曹???-->
      <img
        v-for="(badgeUrl, badgeIndex) in event.badges"
        :key="`badge-${badgeIndex}`"
        :src="badgeUrl"
        class="chat-badge"
        alt="badge"
      />

      <!-- ?梁迂 -->
      <span
        class="chat-username"
        :style="{ color: event.colorHex || 'var(--twitch-purple-light)' }"
      >
        {{ event.displayName || "?芰雿輻?? }}
      </span>

      <!-- ?? -->
      <span class="chat-colon">:</span>

      <!-- ?批捆 -->
      <span class="chat-content">
        <template
          v-for="(segment, segmentIndex) in event.segments"
          :key="`seg-${segmentIndex}`"
        >
          <!-- ?亦銵冽??內嚗葡???? -->
          <img
            v-if="getSegmentType(segment) === 'emote'"
            :src="getSegmentText(segment)"
            class="chat-emote"
            alt="emote"
          />
          <!-- ?血?嚗?仿＊蝷箇??? -->
          <span v-else>{{ getSegmentText(segment) }}</span>
        </template>
      </span>
    </div>
  </transition-group>
</template>

<style scoped>
/* ?詨?閮剛?霈 - 蝬?? KapChat 憸冽 */
.chat-preset-default {
  --twitch-purple-light: #bf94ff;
  --text-shadow-heavy: 1px 1px 2px rgba(0, 0, 0, 0.9), 0 0 1px rgba(0, 0, 0, 0.9);

  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  align-items: flex-start;
  height: calc(100vh - 120px);
  gap: 8px; /* 銵?銵???撖???*/
  width: 100%;
  box-sizing: border-box;
  overflow: hidden;
  padding: 10px;
  background: transparent;
}

.empty-label {
  color: #97a3b3;
  font-style: italic;
  padding: 10px;
}

/* 蝬 KapChat ?株???銵?*/
.chat-line {
  position: relative;
  background: transparent; /* 摰?? */
  border: none;
  border-radius: 0;
  padding: 2px 4px;
  display: block; /* 撖砍漲 100% ?芸??? */
  width: 100%;
  word-wrap: break-word;
  box-sizing: border-box;
  transition: transform 0.3s ease;
  line-height: 1.4;
}

/* 蝬 KapChat 鞎潮??迂撌血?噬蝡?*/
.chat-badge {
  height: 18px;
  vertical-align: middle;
  margin-right: 6px;
  margin-bottom: 2px;
  border-radius: 2px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
  display: inline-block;
}

.chat-username {
  font-size: 1.05rem;
  font-weight: 900;
  letter-spacing: 0.5px;
  text-shadow: var(--text-shadow-heavy);
  display: inline;
  vertical-align: middle;
}

.chat-colon {
  font-size: 1.05rem;
  font-weight: 900;
  color: #EFEFF1;
  margin-right: 6px;
  text-shadow: var(--text-shadow-heavy);
  display: inline;
  vertical-align: middle;
}

.chat-content {
  font-size: 1.05rem;
  color: #FFFFFF; /* 蝝?脣?擃??梢敶望???霅霈??*/
  line-height: 1.4;
  font-weight: 700; /* 蝎?摮?璆菔皜 */
  display: inline;
  word-break: break-word;
  overflow-wrap: anywhere;
  text-shadow: var(--text-shadow-heavy);
  vertical-align: middle;
}

.chat-emote {
  vertical-align: middle;
  margin: -4px 2px;
  height: 1.8em;
  display: inline-block;
}

/* ??ice-spring ?脣?宏????*/
.ice-spring-enter-active {
  animation: iceSpring 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275) both;
}

.ice-spring-leave-active {
  transition: all 0.3s ease;
  position: absolute; /* 撟單??Ｗ皛曉? */
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
