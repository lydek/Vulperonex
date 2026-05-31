<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { HubConnectionState } from "@microsoft/signalr";
import { useOverlayHub, type OverlayHubEvent } from "@/composables/useOverlayHub";
import { useHubConnectionState } from "@/composables/useHubConnectionState";

const { t } = useI18n();

const { events, start, connection } = useOverlayHub("chat");
const { state: hubState, reconnectAttempt, manualReconnect } = useHubConnectionState(connection);
const localEvents = ref<OverlayHubEvent[]>([]);

watch(
  events,
  (newEvents) => {
    const reversed = [...newEvents].reverse();
    for (const ev of reversed) {
      if (!ev.eventId) continue;

      const existingIndex = localEvents.value.findIndex((entry) => entry.eventId === ev.eventId);
      if (existingIndex === -1) {
        localEvents.value.unshift(ev);
        continue;
      }

      localEvents.value[existingIndex] = ev;
    }

    if (localEvents.value.length > 50) {
      localEvents.value = localEvents.value.slice(0, 50);
    }
  },
  { deep: true }
);

onMounted(() => {
  void start();
});

function clearLocal(): void {
  localEvents.value = [];
}

const statusColor = computed(() => {
  switch (hubState.value) {
    case HubConnectionState.Connected:
      return "#2d9d78";
    case HubConnectionState.Connecting:
    case HubConnectionState.Reconnecting:
      return "#d38a35";
    default:
      return "#d95f5f";
  }
});

const statusKey = computed<"connected" | "reconnecting" | "connecting" | "disconnected">(() => {
  switch (hubState.value) {
    case HubConnectionState.Connected:
      return "connected";
    case HubConnectionState.Reconnecting:
      return "reconnecting";
    case HubConnectionState.Connecting:
      return "connecting";
    default:
      return "disconnected";
  }
});

const showReconnectButton = computed(() => hubState.value === HubConnectionState.Disconnected);

function formatTime(sentAt?: string): string {
  if (!sentAt) return "";
  try {
    const date = new Date(sentAt);
    return date.toLocaleTimeString([], {
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit"
    });
  } catch {
    return "";
  }
}

function getSegmentsText(ev: OverlayHubEvent): string {
  if (ev.segments && ev.segments.length > 0) {
    return ev.segments.map((segment) => segment.text || segment.value || "").join("");
  }

  return "";
}
</script>

<template>
  <section class="chat-stream-card" data-testid="chat-stream-panel">
    <header class="panel-header">
      <div class="header-left">
        <span
          class="status-indicator"
          :style="{ backgroundColor: statusColor }"
          :title="t(`monitor.chat.live.${statusKey}`)"
          data-testid="chat-status-dot"
        ></span>
        <div>
          <h3 class="panel-title">{{ t("monitor.chat.title") }}</h3>
          <p class="panel-subtitle">{{ t("monitor.chat.subtitle") }}</p>
        </div>
      </div>

      <div class="header-right">
        <button
          v-if="showReconnectButton"
          type="button"
          class="reconnect-btn"
          data-testid="chat-reconnect-btn"
          :title="reconnectAttempt > 0 ? t('monitor.chat.reconnectRetry', { attempt: reconnectAttempt }) : t('monitor.chat.reconnect')"
          @click="manualReconnect"
        >
          ↻ {{ t("monitor.chat.reconnect") }}
        </button>
        <button type="button" class="clear-btn" @click="clearLocal">
          {{ t("monitor.chat.clear") }}
        </button>
      </div>
    </header>

    <div class="table-container">
      <div v-if="localEvents.length > 0" class="chat-message-list">
        <div
          v-for="ev in localEvents"
          :key="ev.eventId"
          class="chat-message-item"
          data-testid="chat-stream-row"
        >
          <span class="message-time">{{ formatTime(ev.timestamp ?? ev.sentAt) }}</span>
          <div class="message-content-wrap">
            <div class="message-user-info">
              <span class="message-username" :style="{ color: ev.colorHex || '#6d4fc2' }">
                {{ ev.displayName || "Anonymous" }}
              </span>

              <span
                v-for="role in ev.roles ?? []"
                :key="`${ev.eventId}-${role}`"
                class="message-role-badge"
              >
                {{ role }}
              </span>

              <div v-if="ev.memberSnapshot" class="message-member-badge" data-testid="member-chip">
                <img
                  v-if="ev.memberSnapshot.avatarUrl"
                  :src="ev.memberSnapshot.avatarUrl"
                  class="badge-avatar"
                  alt="avatar"
                />
                <span class="badge-count">打卡 {{ ev.memberSnapshot.checkInCount }}</span>
              </div>
            </div>
            <p class="message-text-body">{{ getSegmentsText(ev) || "-" }}</p>
          </div>
        </div>
      </div>

      <div v-else class="empty-stream">
        <div class="empty-icon">...</div>
        <p>{{ t("monitor.chat.emptyTitle") }}</p>
        <p class="empty-sub">{{ t("monitor.chat.emptyHint") }}</p>
      </div>
    </div>
  </section>
</template>

<style scoped>
.chat-stream-card {
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
  overflow: hidden;
  border: 1px solid var(--monitor-border, #d8e2dc);
  border-radius: var(--monitor-radius-card, 12px);
  background: var(--monitor-bg-elevated, #ffffff);
  box-shadow: var(--monitor-shadow-elevated, 0 18px 48px rgba(33, 58, 52, 0.12));
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--monitor-border-subtle, #dde7e1);
  background: var(--monitor-bg-surface, rgba(250, 252, 251, 0.9));
}

.header-right {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.reconnect-btn {
  border: 1px solid var(--monitor-warning-border, rgba(245, 158, 11, 0.22));
  border-radius: var(--monitor-radius-pill, 999px);
  background: var(--monitor-warning-subtle, #fff7e6);
  color: var(--monitor-warning, #b45a0a);
  padding: 0.45rem 0.85rem;
  font-size: 0.8rem;
  font-weight: 800;
  cursor: pointer;
  transition: transform 120ms ease, background-color 120ms ease;
}

.reconnect-btn:hover {
  transform: translateY(-1px);
  filter: brightness(0.97);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.status-indicator {
  width: 10px;
  height: 10px;
  border-radius: 999px;
  flex: 0 0 auto;
  box-shadow: 0 0 0 6px rgba(45, 157, 120, 0.12);
}

.panel-title {
  margin: 0;
  color: var(--vp-text-accent);
  font-size: 1rem;
  font-weight: 800;
  letter-spacing: 0.02em;
}

.panel-subtitle {
  margin: 4px 0 0;
  color: var(--vp-text-muted);
  font-size: 0.84rem;
}

.clear-btn {
  flex: 0 0 auto;
  border: 1px solid var(--vp-border-default);
  border-radius: 999px;
  background: var(--vp-bg-surface);
  color: var(--vp-text-secondary);
  padding: 0.55rem 1rem;
  font-size: 0.84rem;
  font-weight: 700;
  cursor: pointer;
  transition: background-color 160ms ease, border-color 160ms ease, color 160ms ease, transform 160ms ease;
}

.clear-btn:hover {
  background: var(--vp-bg-surface-muted);
  border-color: var(--vp-accent-muted);
  color: var(--vp-text-accent);
  transform: translateY(-1px);
}

.table-container {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.chat-message-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px;
  overflow-y: auto;
  height: 100%;
  box-sizing: border-box;
}

.chat-message-item {
  display: flex;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 10px;
  background: var(--vp-bg-surface-muted);
  border: 1px solid var(--vp-border-subtle);
  box-shadow: 0 1px 3px rgba(33, 58, 52, 0.02);
  transition: transform 120ms ease, box-shadow 120ms ease, background-color 120ms ease;
}

.chat-message-item:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 10px rgba(33, 58, 52, 0.05);
  background: var(--vp-bg-surface);
}

.message-time {
  font-family: Consolas, "Courier New", monospace;
  font-size: 0.72rem;
  color: var(--vp-text-muted);
  margin-top: 1px;
  flex-shrink: 0;
  white-space: nowrap;
}

.message-content-wrap {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
  flex: 1;
}

.message-user-info {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

.message-username {
  font-weight: 800;
  font-size: 0.88rem;
}

.message-role-badge {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  padding: 0.1rem 0.45rem;
  border: 1px solid var(--vp-border-default);
  background: var(--vp-bg-surface-muted);
  color: var(--vp-text-secondary);
  font-size: 0.62rem;
  font-weight: 800;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.message-member-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  border-radius: 999px;
  padding: 0.12rem 0.5rem;
  background: var(--vp-bg-warning);
  border: 1px solid var(--vp-border-warning);
  color: var(--vp-text-warning);
  font-size: 0.68rem;
  font-weight: 700;
}

.badge-avatar {
  width: 14px;
  height: 14px;
  border-radius: 999px;
  object-fit: cover;
}

.message-text-body {
  margin: 0;
  color: var(--vp-text-primary);
  font-size: 0.86rem;
  line-height: 1.45;
  word-break: break-word;
  white-space: pre-wrap;
}

.empty-stream {
  display: grid;
  place-items: center;
  gap: 8px;
  min-height: 280px;
  padding: 32px;
  text-align: center;
  color: var(--vp-text-muted);
}

.empty-icon {
  width: 44px;
  height: 44px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  background: var(--vp-bg-selected);
  color: var(--vp-text-accent);
  font-weight: 900;
  letter-spacing: 0.2em;
}

.empty-stream p {
  margin: 0;
}

.empty-sub {
  max-width: 360px;
  font-size: 0.86rem;
}

@media (max-width: 900px) {
  .panel-header {
    flex-direction: column;
    align-items: stretch;
  }

  .clear-btn {
    width: 100%;
  }

  .col-user {
    width: 190px;
  }
}
</style>
