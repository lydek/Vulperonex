import { computed, readonly, ref } from "vue";
import { defineStore } from "pinia";

export interface StreamEventEnvelope {
  type: string;
  eventId: string;
  platform: string;
  occurredAt?: string;
  timestamp?: string;
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

    eventsById.value = {
      ...eventsById.value,
      [envelope.eventId]: envelope
    };
  }

  function clear(): void {
    eventsById.value = {};
  }

  return {
    eventsById: readonly(eventsById),
    events,
    upsertEvent,
    clear
  };
});

function getEnvelopeTime(envelope: StreamEventEnvelope): number {
  const rawTimestamp = envelope.timestamp ?? envelope.occurredAt;
  if (!rawTimestamp) {
    return 0;
  }

  const timestamp = Date.parse(rawTimestamp);
  return Number.isNaN(timestamp) ? 0 : timestamp;
}
