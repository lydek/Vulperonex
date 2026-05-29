<script setup lang="ts">
import { computed, onMounted, watch } from "vue";
import { useI18n } from "vue-i18n";
import ConditionExpressionInput from "@/components/admin/ConditionExpressionInput.vue";
import EventTypeKeyDropdown from "@/components/admin/EventTypeKeyDropdown.vue";
import VariableFieldInput from "@/components/admin/VariableFieldInput.vue";
import { useTriggerMetadataStore } from "@/stores/triggerMetadata";
import type { TriggerFilterFieldMetadata } from "@/api/client";

const props = defineProps<{
  eventTypeKey: string;
  filter: Record<string, string>;
  matchCondition: string;
}>();

const emit = defineEmits<{
  (event: "update:eventTypeKey", value: string): void;
  (event: "update:filter", value: Record<string, string>): void;
  (event: "update:matchCondition", value: string): void;
}>();

const { t } = useI18n();
const triggerMetadata = useTriggerMetadataStore();

const filterFields = computed(() => triggerMetadata.fieldsFor(props.eventTypeKey));
const shouldPruneFilter = computed(() => triggerMetadata.hasMetadataFor(props.eventTypeKey));

onMounted(() => {
  void triggerMetadata.load();
});

watch([filterFields, () => props.filter], pruneUnknownFilterFields, { deep: true });

function updateEventTypeKey(value: string): void {
  emit("update:eventTypeKey", value);
}

function updateFilterField(field: TriggerFilterFieldMetadata, value: string): void {
  const next = pruneFilter(props.filter, filterFields.value);
  if (value.trim().length === 0) {
    delete next[field.key];
  } else {
    next[field.key] = value;
  }
  emit("update:filter", next);
}

function pruneUnknownFilterFields(): void {
  if (!shouldPruneFilter.value) {
    return;
  }

  const next = pruneFilter(props.filter, filterFields.value);
  if (!isSameFilter(next, props.filter)) {
    emit("update:filter", next);
  }
}

function pruneFilter(
  filter: Record<string, string>,
  fields: TriggerFilterFieldMetadata[]
): Record<string, string> {
  const allowedKeys = new Set(fields.map(field => field.key));
  const next: Record<string, string> = {};
  for (const [key, value] of Object.entries(filter)) {
    if (allowedKeys.has(key)) {
      next[key] = value;
    }
  }
  return next;
}

function isSameFilter(left: Record<string, string>, right: Record<string, string>): boolean {
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  if (leftKeys.length !== rightKeys.length) {
    return false;
  }

  return leftKeys.every(key => left[key] === right[key]);
}

function fieldInputType(field: TriggerFilterFieldMetadata): string {
  return field.type === "number" ? "number" : "text";
}
</script>

<template>
  <section class="status-card" aria-labelledby="trigger-editor-title">
    <h2 id="trigger-editor-title" class="section-title">{{ t("ruleEditor.trigger.title") }}</h2>
    <div class="form-field">
      <span class="form-label">{{ t("ruleEditor.eventTypeKey") }}</span>
      <EventTypeKeyDropdown
        :model-value="eventTypeKey"
        @update:model-value="updateEventTypeKey"
      />
    </div>
    <div class="form-field">
      <span class="form-label">{{ t("ruleEditor.trigger.filter") }}</span>
      <p v-if="triggerMetadata.loading" role="status" class="monitor-help">
        {{ t("ruleEditor.trigger.loadingMetadata") }}
      </p>
      <p v-else-if="triggerMetadata.error" role="alert" class="ack-error-code">
        {{ triggerMetadata.error }}
      </p>
      <p
        v-else-if="filterFields.length === 0"
        role="status"
        class="monitor-help"
        data-testid="trigger-filter-empty"
      >
        {{ t("ruleEditor.trigger.noTypedFields") }}
      </p>
      <div v-else class="rule-filter-rows" data-testid="trigger-filter-fields">
        <label
          v-for="field in filterFields"
          :key="field.key"
          class="rule-filter-field"
          :data-testid="`trigger-filter-field-${field.key}`"
        >
          <span class="form-label">
            {{ field.label }}
            <span v-if="field.required" aria-hidden="true">*</span>
          </span>
          <select
            v-if="field.options?.length"
            :value="filter[field.key] ?? ''"
            :aria-label="field.label"
            @change="updateFilterField(field, ($event.target as HTMLSelectElement).value)"
          >
            <option value="">{{ t("ruleEditor.trigger.anyValue") }}</option>
            <option v-for="option in field.options" :key="option" :value="option">
              {{ option }}
            </option>
          </select>
          <select
            v-else-if="field.type === 'boolean'"
            :value="filter[field.key] ?? ''"
            :aria-label="field.label"
            @change="updateFilterField(field, ($event.target as HTMLSelectElement).value)"
          >
            <option value="">{{ t("ruleEditor.trigger.anyValue") }}</option>
            <option value="true">true</option>
            <option value="false">false</option>
          </select>
          <input
            v-else-if="field.type === 'number'"
            :value="filter[field.key] ?? ''"
            :type="fieldInputType(field)"
            :aria-label="field.label"
            @input="updateFilterField(field, ($event.target as HTMLInputElement).value)"
          />
          <VariableFieldInput
            v-else
            :model-value="filter[field.key] ?? ''"
            :placeholder="field.label"
            :data-test-id="`trigger-filter-input-${field.key}`"
            @update:model-value="updateFilterField(field, $event)"
          />
          <span v-if="field.help" class="monitor-help">{{ field.help }}</span>
        </label>
      </div>
    </div>
    <label class="form-field">
      <span class="form-label">{{ t("ruleEditor.matchCondition") }}</span>
      <ConditionExpressionInput
        :model-value="matchCondition"
        placeholder="Trigger.MessageText == '!go'"
        data-test-id="rule-editor-match-condition"
        @update:model-value="emit('update:matchCondition', $event)"
      />
    </label>
  </section>
</template>

<style scoped>
.rule-filter-field {
  display: grid;
  gap: 6px;
}
</style>
