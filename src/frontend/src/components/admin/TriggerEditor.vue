<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import EventTypeKeyDropdown from "@/components/admin/EventTypeKeyDropdown.vue";

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

const filterRows = computed({
  get: () => Object.entries(props.filter).map(([key, value]) => ({ key, value })),
  set: (rows: Array<{ key: string; value: string }>) => {
    const next: Record<string, string> = {};
    for (const row of rows) {
      const key = row.key.trim();
      if (key.length > 0) next[key] = row.value;
    }
    emit("update:filter", next);
  }
});

function addRow(): void {
  filterRows.value = [...filterRows.value, { key: "", value: "" }];
}

function updateRow(index: number, field: "key" | "value", value: string): void {
  const rows = filterRows.value.slice();
  rows[index] = { ...rows[index], [field]: value };
  filterRows.value = rows;
}

function removeRow(index: number): void {
  filterRows.value = filterRows.value.filter((_, rowIndex) => rowIndex !== index);
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
          <input
            :aria-label="t('ruleEditor.trigger.filterValue')"
            :value="row.value"
            @input="updateRow(index, 'value', ($event.target as HTMLInputElement).value)"
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
      <input
        :value="matchCondition"
        type="text"
        data-testid="rule-editor-match-condition"
        @input="emit('update:matchCondition', ($event.target as HTMLInputElement).value)"
      />
    </label>
  </section>
</template>
