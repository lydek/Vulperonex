<script setup lang="ts">
import { onMounted } from "vue";
import { useI18n } from "vue-i18n";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { useStreamEvents } from "@/composables/useStreamEvents";

const { t } = useI18n();
const { events, state, error, start } = useStreamEvents();

onMounted(() => {
  void start();
});

function formatOccurredAt(occurredAt?: string): string {
  if (!occurredAt) return "-";
  const parsed = Date.parse(occurredAt);
  if (Number.isNaN(parsed)) return occurredAt;
  return new Date(parsed).toISOString();
}
</script>

<template>
  <section aria-labelledby="event-monitor-title">
    <header class="page-header">
      <h1 id="event-monitor-title" class="page-title">{{ t("monitor.title") }}</h1>
      <p class="page-subtitle">{{ t("monitor.subtitle") }}</p>
    </header>

    <div class="monitor-toolbar">
      <HubStatusChip :state="state" :last-event-at="null" :error="error" />
      <span class="monitor-count" data-testid="monitor-count">
        {{ t("monitor.count", { count: events.length }) }}
      </span>
    </div>

    <p v-if="events.length === 0" role="status" data-testid="monitor-empty">
      {{ t("monitor.empty") }}
    </p>

    <table v-else class="monitor-table" data-testid="monitor-table">
      <thead>
        <tr>
          <th scope="col">{{ t("monitor.col.type") }}</th>
          <th scope="col">{{ t("monitor.col.platform") }}</th>
          <th scope="col">{{ t("monitor.col.eventId") }}</th>
          <th scope="col">{{ t("monitor.col.occurredAt") }}</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="event in events"
          :key="event.eventId"
          data-testid="monitor-row"
        >
          <td class="monitor-mono">{{ event.type }}</td>
          <td class="monitor-mono">{{ event.platform }}</td>
          <td class="monitor-mono">{{ event.eventId }}</td>
          <td class="monitor-mono">{{ formatOccurredAt(event.occurredAt) }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
