import { computed, readonly, ref } from "vue";
import { defineStore } from "pinia";
import {
  getTriggerMetadata,
  type TriggerFilterFieldMetadata,
  type TriggerMetadataEntry
} from "@/api/client";

export const useTriggerMetadataStore = defineStore("triggerMetadata", () => {
  const entries = ref<TriggerMetadataEntry[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  let pendingLoad: Promise<void> | null = null;

  const entriesByKey = computed(() => {
    const next = new Map<string, TriggerMetadataEntry>();
    for (const entry of entries.value) {
      next.set(entry.key.toLowerCase(), entry);
    }
    return next;
  });

  async function load(signal?: AbortSignal): Promise<void> {
    if (entries.value.length > 0) {
      return;
    }

    if (pendingLoad) {
      return pendingLoad;
    }

    loading.value = true;
    error.value = null;
    pendingLoad = getTriggerMetadata(signal)
      .then(result => {
        entries.value = result;
      })
      .catch(err => {
        error.value = err instanceof Error ? err.message : String(err);
      })
      .finally(() => {
        loading.value = false;
        pendingLoad = null;
      });

    return pendingLoad;
  }

  function fieldsFor(eventTypeKey: string | null | undefined): TriggerFilterFieldMetadata[] {
    if (!eventTypeKey) {
      return [];
    }

    return entriesByKey.value.get(eventTypeKey.toLowerCase())?.filterFields ?? [];
  }

  function variablesFor(eventTypeKey: string | null | undefined): string[] {
    if (!eventTypeKey) {
      return [];
    }

    return entriesByKey.value.get(eventTypeKey.toLowerCase())?.validVariables ?? [];
  }

  function hasMetadataFor(eventTypeKey: string | null | undefined): boolean {
    if (!eventTypeKey) {
      return false;
    }

    return entriesByKey.value.has(eventTypeKey.toLowerCase());
  }

  return {
    entries: readonly(entries),
    loading: readonly(loading),
    error: readonly(error),
    load,
    fieldsFor,
    variablesFor,
    hasMetadataFor
  };
});
