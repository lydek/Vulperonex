<script setup lang="ts">
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ConditionExpressionInput from "@/components/admin/ConditionExpressionInput.vue";
import EventTypeKeyDropdown from "@/components/admin/EventTypeKeyDropdown.vue";
import VariableFieldInput from "@/components/admin/VariableFieldInput.vue";

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

type FilterRow = { key: string; value: string };

const filterRows = ref<FilterRow[]>([]);

watch(() => props.filter, syncRowsFromProps, { immediate: true, deep: true });

function syncRowsFromProps(filter: Record<string, string>): void {
  const nextRows = Object.entries(filter).map(([key, value]) => ({ key, value }));
  const hasBlankDraft = filterRows.value.some((row) => row.key.trim().length === 0 && row.value.trim().length === 0);
  filterRows.value = nextRows.length > 0 || !hasBlankDraft
    ? nextRows
    : [...nextRows, { key: "", value: "" }];
}

function emitFilter(rows: FilterRow[]): void {
  const next: Record<string, string> = {};
  for (const row of rows) {
    const key = row.key.trim();
    if (key.length > 0) {
      next[key] = row.value;
    }
  }
  emit("update:filter", next);
}

function replaceRows(rows: FilterRow[]): void {
  filterRows.value = rows;
  emitFilter(rows);
}

function ensureVisibleRow(): void {
  if (filterRows.value.length === 0) {
    filterRows.value = [{ key: "", value: "" }];
  }
}

watch(filterRows, ensureVisibleRow, { immediate: true });

function addRow(): void {
  filterRows.value = [...filterRows.value, { key: "", value: "" }];
}

function updateRow(index: number, field: "key" | "value", value: string): void {
  const rows = filterRows.value.slice();
  rows[index] = { ...rows[index], [field]: value };
  replaceRows(rows);
}

function removeRow(index: number): void {
  const rows = filterRows.value.filter((_, rowIndex) => rowIndex !== index);
  replaceRows(rows);
}
</script>

<template>
  <section class="status-card" aria-labelledby="trigger-editor-title">
    <h2 id="trigger-editor-title" class="section-title">{{ t("ruleEditor.trigger.title") }}</h2>
    <div class="form-field">
      <span class="form-label">{{ t("ruleEditor.eventTypeKey") }}</span>
      <EventTypeKeyDropdown
        :model-value="eventTypeKey"
        @update:model-value="emit('update:eventTypeKey', $event)"
      />
    </div>
    <div class="form-field">
      <span class="form-label">{{ t("ruleEditor.trigger.filter") }}</span>
      <div class="rule-filter-rows">
        <div v-for="(row, index) in filterRows" :key="index" class="rule-filter-row">
          <input
            :aria-label="t('ruleEditor.trigger.filterKey')"
            :value="row.key"
            @input="updateRow(index, 'key', ($event.target as HTMLInputElement).value)"
          />
          <VariableFieldInput
            :model-value="row.value"
            :placeholder="t('ruleEditor.trigger.filterValue')"
            @update:model-value="updateRow(index, 'value', $event)"
          />
          <button type="button" class="icon-button" @click="removeRow(index)">
            {{ t("common.remove") }}
          </button>
        </div>
        <button type="button" class="secondary-button" data-testid="trigger-filter-add" @click="addRow">
          {{ t("common.add") }}
        </button>
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
