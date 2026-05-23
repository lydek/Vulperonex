<script setup lang="ts">
import { computed } from "vue";
import {
  buildVariableGroups,
  parseArrayModel,
  type JsonRecord,
  type VariableGroup
} from "@/components/admin/workflowEditor";

const props = defineProps<{
  previousSteps?: JsonRecord[];
  previousStepsJson?: string;
  expressionMode?: boolean;
}>();

const emit = defineEmits<{ (event: "select", value: string): void }>();

const groups = computed<VariableGroup[]>(() => {
  const previousSteps = props.previousSteps
    ?? parseArrayModel(props.previousStepsJson ?? "[]");
  return buildVariableGroups(previousSteps, props.expressionMode ?? false);
});

function selectVariable(value: string): void {
  emit("select", value);
}
</script>

<template>
  <details class="variable-picker">
    <summary class="variable-picker__summary" data-testid="variable-picker-toggle">{x} Variables</summary>
    <div class="variable-picker__panel">
      <section v-for="group in groups" :key="group.key" class="variable-picker__group">
        <h3 class="variable-picker__title">{{ group.label }}</h3>
        <div class="variable-picker__list">
          <button
            v-for="entry in group.variables"
            :key="entry.path"
            type="button"
            class="variable-picker__item"
            @click="selectVariable(entry.path)"
          >
            <span class="variable-picker__token">{{ entry.path }}</span>
            <span v-if="entry.hint" class="variable-picker__hint">{{ entry.hint }}</span>
          </button>
        </div>
      </section>
    </div>
  </details>
</template>

<style scoped>
.variable-picker {
  position: relative;
  display: inline-flex;
}

.variable-picker__summary {
  cursor: pointer;
  color: #5f6f80;
  font-size: 11px;
  font-weight: 600;
  list-style: none;
  padding: 4px 8px;
  border: 1px solid #d6dde5;
  border-radius: 999px;
  background: #ffffff;
  user-select: none;
  white-space: nowrap;
}

.variable-picker__summary::-webkit-details-marker {
  display: none;
}

.variable-picker__panel {
  position: absolute;
  right: 0;
  top: calc(100% + 6px);
  z-index: 20;
  width: min(340px, 80vw);
  max-height: 320px;
  overflow: auto;
  padding: 12px;
  border: 1px solid #d6dde5;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 12px 32px rgba(15, 23, 32, 0.18);
}

.variable-picker__group + .variable-picker__group {
  margin-top: 12px;
}

.variable-picker__title {
  margin: 0 0 8px;
  font-size: 12px;
  color: #5f6f80;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.variable-picker__list {
  display: grid;
  gap: 8px;
}

.variable-picker__item {
  display: grid;
  gap: 2px;
  width: 100%;
  padding: 8px 10px;
  text-align: left;
  border: 1px solid #d6dde5;
  border-radius: 8px;
  background: #ffffff;
  color: #18202a;
}

.variable-picker__item:hover {
  border-color: #1f6f64;
  background: #f4f6f8;
}

.variable-picker__token {
  font-family: Consolas, "Courier New", monospace;
  font-size: 12px;
}

.variable-picker__hint {
  color: #5f6f80;
  font-size: 11px;
}
</style>
