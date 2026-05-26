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
      <table v-if="localEvents.length > 0" class="stream-table">
        <thead>
          <tr>
            <th class="col-time">{{ t("monitor.chat.col.time") }}</th>
            <th class="col-user">{{ t("monitor.chat.col.user") }}</th>
            <th class="col-msg">{{ t("monitor.chat.col.message") }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="ev in localEvents" :key="ev.eventId" class="stream-row" data-testid="chat-stream-row">
            <td class="cell-time">{{ formatTime(ev.timestamp ?? ev.sentAt) }}</td>
            <td class="cell-user">
              <div class="user-cell-wrapper">
                <span class="username" :style="{ color: ev.colorHex || '#6d4fc2' }">
                  {{ ev.displayName || "Anonymous" }}
                </span>

                <span
                  v-for="role in ev.roles ?? []"
                  :key="`${ev.eventId}-${role}`"
                  class="role-pill"
                >
                  {{ role }}
                </span>

                <div v-if="ev.memberSnapshot" class="member-chip-preview" data-testid="member-chip">
                  <img
                    v-if="ev.memberSnapshot.avatarUrl"
                    :src="ev.memberSnapshot.avatarUrl"
                    class="chip-avatar"
                    alt="avatar"
                  />
                  <span class="chip-count">打卡 {{ ev.memberSnapshot.checkInCount }}</span>
                </div>
              </div>
            </td>
            <td class="cell-msg">
              <span class="msg-text">{{ getSegmentsText(ev) || "-" }}</span>
            </td>
          </tr>
        </tbody>
      </table>

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
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(245, 248, 246, 0.98)),
    radial-gradient(circle at top left, rgba(45, 157, 120, 0.12), transparent 40%);
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
  background: #ffe9c2;
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
  color: #164f48;
  font-size: 1rem;
  font-weight: 800;
  letter-spacing: 0.02em;
}

.panel-subtitle {
  margin: 4px 0 0;
  color: #648077;
  font-size: 0.84rem;
}

.clear-btn {
  flex: 0 0 auto;
  border: 1px solid #cad8d1;
  border-radius: 999px;
  background: #ffffff;
  color: #355e56;
  padding: 0.55rem 1rem;
  font-size: 0.84rem;
  font-weight: 700;
  cursor: pointer;
  transition: background-color 160ms ease, border-color 160ms ease, color 160ms ease, transform 160ms ease;
}

.clear-btn:hover {
  background: #f4f8f6;
  border-color: #9fc1b3;
  color: #164f48;
  transform: translateY(-1px);
}

.table-container {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.stream-table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}

.stream-table th {
  position: sticky;
  top: 0;
  z-index: 1;
  padding: 12px 18px;
  background: rgba(236, 244, 240, 0.96);
  border-bottom: 1px solid #dde7e1;
  color: #58756c;
  font-size: 0.76rem;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  text-align: left;
}

.col-time {
  width: 112px;
}

.col-user {
  width: 240px;
}

.stream-row {
  border-bottom: 1px solid #edf2ef;
}

.stream-row:hover {
  background: rgba(241, 247, 244, 0.85);
}

.cell-time,
.cell-user,
.cell-msg {
  padding: 14px 18px;
  vertical-align: top;
}

.cell-time {
  color: #5f786f;
  font-family: Consolas, "Courier New", monospace;
  font-size: 0.82rem;
  white-space: nowrap;
}

.user-cell-wrapper {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.username {
  font-weight: 800;
  font-size: 0.95rem;
}

.role-pill {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  padding: 0.2rem 0.55rem;
  border: 1px solid #d8e3dd;
  background: #f4f8f6;
  color: #49685f;
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.member-chip-preview {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border-radius: 999px;
  padding: 0.22rem 0.6rem;
  background: rgba(242, 198, 63, 0.18);
  border: 1px solid rgba(197, 151, 21, 0.22);
  color: #7a5c08;
  font-size: 0.72rem;
  font-weight: 700;
}

.chip-avatar {
  width: 16px;
  height: 16px;
  border-radius: 999px;
  object-fit: cover;
}

.cell-msg {
  color: #213a34;
}

.msg-text {
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.5;
}

.empty-stream {
  display: grid;
  place-items: center;
  gap: 8px;
  min-height: 280px;
  padding: 32px;
  text-align: center;
  color: #5f786f;
}

.empty-icon {
  width: 44px;
  height: 44px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  background: rgba(45, 157, 120, 0.12);
  color: #2d9d78;
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
