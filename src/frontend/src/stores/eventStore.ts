import { computed, readonly, ref } from "vue";
import { defineStore } from "pinia";

export interface StreamEventEnvelope {
  type: string;
  eventId: string;
  platform: string;
  occurredAt?: string;
}

export const useEventStore = defineStore("events", () => {
  const eventsById = ref<Record<string, StreamEventEnvelope>>({});

  const events = computed(() =>
    Object.values(eventsById.value).sort((left, right) =>
      getEnvelopeTime(right) - getEnvelopeTime(left)));

  function upsertEvent(envelope: StreamEventEnvelope): void {
    const existing = eventsById.value[envelope.eventId];
    if (existing && getEnvelopeTime(existing) > getEnvelopeTime(envelope)) {
      return;
    }

    eventsById.value[envelope.eventId] = envelope;
  }

  function clear(): void {
    for (const key of Object.keys(eventsById.value)) {
      delete eventsById.value[key];
    }
  }

  return {
    eventsById: readonly(eventsById),
    events,
    upsertEvent,
    clear
  };
});

function getEnvelopeTime(envelope: StreamEventEnvelope): number {
  const rawOccurredAt = envelope.occurredAt;
  if (!rawOccurredAt) {
    return 0;
  }

  const parsed = Date.parse(rawOccurredAt);
  return Number.isNaN(parsed) ? 0 : parsed;
}
