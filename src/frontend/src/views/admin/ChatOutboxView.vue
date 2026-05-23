<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  ApiError,
  getChatOutbox,
  type ChatOutboxItemDto,
  type ChatOutboxItemStatus
} from "@/api/client";

const { t, te } = useI18n();

const items = ref<ChatOutboxItemDto[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);
const statusFilter = ref<"" | ChatOutboxItemStatus>("");
const platformFilter = ref("");
const autoRefresh = ref(true);

let refreshTimer: ReturnType<typeof setInterval> | null = null;

const statusOptions: { value: "" | ChatOutboxItemStatus; labelKey: string }[] = [
  { value: "", labelKey: "chatOutbox.status.all" },
  { value: "Pending", labelKey: "chatOutbox.status.pending" },
  { value: "Processing", labelKey: "chatOutbox.status.processing" },
  { value: "Sent", labelKey: "chatOutbox.status.sent" },
  { value: "Skipped", labelKey: "chatOutbox.status.skipped" },
  { value: "Failed", labelKey: "chatOutbox.status.failed" }
];

const summary = computed(() => {
  const totals: Record<ChatOutboxItemStatus, number> = {
    Pending: 0,
    Processing: 0,
    Sent: 0,
    Skipped: 0,
    Failed: 0
  };
  for (const item of items.value) {
    totals[item.status] += 1;
  }
  return totals;
});

onMounted(() => {
  void load();
  startAutoRefresh();
});

onBeforeUnmount(() => {
  stopAutoRefresh();
});

function fallbackLabel(key: string, fallback: string): string {
  return te(key) ? t(key) : fallback;
}

function startAutoRefresh(): void {
  stopAutoRefresh();
  if (!autoRefresh.value) {
    return;
  }
  refreshTimer = setInterval(() => {
    void load();
  }, 5000);
}

function stopAutoRefresh(): void {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
}

function onAutoRefreshChange(value: boolean): void {
  autoRefresh.value = value;
  if (value) {
    startAutoRefresh();
  } else {
    stopAutoRefresh();
  }
}

async function load(): Promise<void> {
  loading.value = true;
  error.value = null;
  try {
    items.value = await getChatOutbox({
      status: statusFilter.value === "" ? undefined : statusFilter.value,
      platform: platformFilter.value.trim() || undefined,
      limit: 200
    });
  } catch (caught) {
    error.value = describeError(caught);
  } finally {
    loading.value = false;
  }
}

function describeError(caught: unknown): string {
  if (caught instanceof ApiError) {
    return caught.errorCode ?? `HTTP_${caught.status}`;
  }
  return caught instanceof Error ? caught.message : String(caught);
}

function statusBadgeClass(status: ChatOutboxItemStatus): string {
  return `chat-outbox-status chat-outbox-status--${status.toLowerCase()}`;
}
</script>

<template>
  <section aria-labelledby="chat-outbox-title">
    <header class="page-header">
      <h1 id="chat-outbox-title" class="page-title">
        {{ fallbackLabel("chatOutbox.title", "Chat outbox") }}
      </h1>
      <p class="page-subtitle">
        {{ fallbackLabel("chatOutbox.subtitle", "Inspect rendered chat messages produced by workflow SendChatMessage actions.") }}
      </p>
    </header>

    <form class="members-toolbar" data-testid="chat-outbox-toolbar" @submit.prevent="load">
      <label class="form-field">
        <span class="form-label">{{ fallbackLabel("chatOutbox.filter.status", "Status") }}</span>
        <select v-model="statusFilter" data-testid="chat-outbox-status-filter">
          <option v-for="option in statusOptions" :key="option.value" :value="option.value">
            {{ fallbackLabel(option.labelKey, option.value || "All") }}
          </option>
        </select>
      </label>

      <label class="form-field">
        <span class="form-label">{{ fallbackLabel("chatOutbox.filter.platform", "Platform") }}</span>
        <input
          v-model="platformFilter"
          type="text"
          data-testid="chat-outbox-platform-filter"
          placeholder="simulation"
        />
      </label>

      <button type="submit" class="primary-button" :disabled="loading" data-testid="chat-outbox-refresh">
        {{ loading
          ? fallbackLabel("chatOutbox.loading", "Loading…")
          : fallbackLabel("chatOutbox.refresh", "Refresh") }}
      </button>

      <label class="form-field form-field-inline">
        <input
          type="checkbox"
          :checked="autoRefresh"
          data-testid="chat-outbox-auto-refresh"
          @change="onAutoRefreshChange(($event.target as HTMLInputElement).checked)"
        />
        <span>{{ fallbackLabel("chatOutbox.autoRefresh", "Auto refresh") }}</span>
      </label>
    </form>

    <p v-if="error" class="ack-error-code" role="alert" data-testid="chat-outbox-error">{{ error }}</p>

    <ul class="chat-outbox-summary" data-testid="chat-outbox-summary">
      <li class="chat-outbox-status chat-outbox-status--pending">
        {{ fallbackLabel("chatOutbox.status.pending", "Pending") }}: {{ summary.Pending }}
      </li>
      <li class="chat-outbox-status chat-outbox-status--processing">
        {{ fallbackLabel("chatOutbox.status.processing", "Processing") }}: {{ summary.Processing }}
      </li>
      <li class="chat-outbox-status chat-outbox-status--sent">
        {{ fallbackLabel("chatOutbox.status.sent", "Sent") }}: {{ summary.Sent }}
      </li>
      <li class="chat-outbox-status chat-outbox-status--skipped">
        {{ fallbackLabel("chatOutbox.status.skipped", "Skipped") }}: {{ summary.Skipped }}
      </li>
      <li class="chat-outbox-status chat-outbox-status--failed">
        {{ fallbackLabel("chatOutbox.status.failed", "Failed") }}: {{ summary.Failed }}
      </li>
    </ul>

    <p v-if="!loading && items.length === 0 && !error" role="status" data-testid="chat-outbox-empty">
      {{ fallbackLabel("chatOutbox.empty", "No chat outbox items match the current filters.") }}
    </p>

    <table v-if="items.length > 0" class="monitor-table" data-testid="chat-outbox-table">
      <thead>
        <tr>
          <th scope="col">{{ fallbackLabel("chatOutbox.col.enqueuedAt", "Enqueued at") }}</th>
          <th scope="col">{{ fallbackLabel("chatOutbox.col.platform", "Platform") }}</th>
          <th scope="col">{{ fallbackLabel("chatOutbox.col.channel", "Channel") }}</th>
          <th scope="col">{{ fallbackLabel("chatOutbox.col.message", "Message") }}</th>
          <th scope="col">{{ fallbackLabel("chatOutbox.col.dedupKey", "Dedup key") }}</th>
          <th scope="col">{{ fallbackLabel("chatOutbox.col.status", "Status") }}</th>
          <th scope="col">{{ fallbackLabel("chatOutbox.col.error", "Error") }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in items" :key="item.id" :data-testid="`chat-outbox-row-${item.id}`">
          <td class="monitor-mono">{{ item.enqueuedAt }}</td>
          <td class="monitor-mono">{{ item.platform }}</td>
          <td class="monitor-mono">{{ item.channel ?? "—" }}</td>
          <td>{{ item.message }}</td>
          <td class="monitor-mono">{{ item.dedupKey ?? "—" }}</td>
          <td>
            <span :class="statusBadgeClass(item.status)" :data-testid="`chat-outbox-status-${item.id}`">
              {{ item.status }}
            </span>
          </td>
          <td class="monitor-mono">{{ item.errorMessage ?? "" }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.chat-outbox-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  list-style: none;
  padding: 0;
  margin: 12px 0;
}

.chat-outbox-status {
  display: inline-block;
  padding: 4px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
  border: 1px solid transparent;
}

.chat-outbox-status--pending { background: #fef3c7; color: #92400e; border-color: #fcd9b6; }
.chat-outbox-status--processing { background: #dbeafe; color: #1e40af; border-color: #bfdbfe; }
.chat-outbox-status--sent { background: #dcfce7; color: #166534; border-color: #bbf7d0; }
.chat-outbox-status--skipped { background: #e5e7eb; color: #374151; border-color: #d1d5db; }
.chat-outbox-status--failed { background: #fee2e2; color: #991b1b; border-color: #fecaca; }
</style>
