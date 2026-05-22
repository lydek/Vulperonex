<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { ApiError, getEventTypes, type EventTypeMetadata } from "@/api/client";

const props = defineProps<{
  modelValue: string;
  ariaLabel?: string;
}>();

const emit = defineEmits<{
  (event: "update:modelValue", value: string): void;
}>();

const { t } = useI18n();
const eventTypes = ref<EventTypeMetadata[]>([]);
const loading = ref(false);
const errorCode = ref<string | null>(null);

onMounted(() => {
  void load();
});

const simulatableKeys = computed(() =>
  eventTypes.value.filter((entry) => entry.isSimulatable).map((entry) => entry.key)
);

async function load(): Promise<void> {
  loading.value = true;
  errorCode.value = null;
  try {
    eventTypes.value = await getEventTypes();
  } catch (caught) {
    eventTypes.value = [];
    errorCode.value = caught instanceof ApiError
      ? (caught.errorCode ?? `HTTP_${caught.status}`)
      : "NETWORK_ERROR";
  } finally {
    loading.value = false;
  }
}

function onChange(event: Event): void {
  const target = event.target as HTMLSelectElement;
  emit("update:modelValue", target.value);
}
</script>

<template>
  <div class="event-type-dropdown">
    <select
      class="event-type-select"
      :value="modelValue"
      :aria-label="ariaLabel ?? t('eventTypeKey.ariaLabel')"
      :disabled="loading"
      data-testid="event-type-select"
      @change="onChange"
    >
      <option value="" disabled>{{ t("eventTypeKey.placeholder") }}</option>
      <option
        v-for="entry in eventTypes"
        :key="entry.key"
        :value="entry.key"
        :data-simulatable="entry.isSimulatable ? 'true' : 'false'"
      >
        {{ entry.key }}
        <template v-if="entry.isSimulatable">— {{ t("eventTypeKey.badge.simulatable") }}</template>
        <template v-else>— {{ t("eventTypeKey.badge.unsupported") }}</template>
      </option>
    </select>

    <ul class="event-type-legend" data-testid="event-type-legend" aria-hidden="true">
      <li
        v-for="key in simulatableKeys"
        :key="key"
        class="event-type-badge event-type-badge-simulatable"
        data-testid="event-type-badge"
      >
        {{ key }}
      </li>
    </ul>

    <p v-if="errorCode" class="ack-error-code" role="alert" data-testid="event-type-error">
      {{ errorCode }}
    </p>
  </div>
</template>
