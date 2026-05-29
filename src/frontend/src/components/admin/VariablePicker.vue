<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from "vue";
import {
  buildVariableGroups,
  parseArrayModel,
  type ActionDefinition,
  type JsonRecord,
  type VariableGroup
} from "@/components/admin/workflowEditor";

const props = defineProps<{
  previousSteps?: JsonRecord[];
  previousStepsJson?: string;
  expressionMode?: boolean;
  allowedTriggerVariables?: string[];
  actionDefinitions?: ActionDefinition[];
}>();

const emit = defineEmits<{ (event: "select", value: string): void }>();

const detailsEl = ref<HTMLDetailsElement | null>(null);
const summaryEl = ref<HTMLElement | null>(null);
const panelStyle = ref<Record<string, string>>({});

const groups = computed<VariableGroup[]>(() => {
  const previousSteps = props.previousSteps
    ?? parseArrayModel(props.previousStepsJson ?? "[]");
  return filterTriggerVariables(
    buildVariableGroups(previousSteps, props.expressionMode ?? false, props.actionDefinitions),
    props.allowedTriggerVariables
  );
});

function selectVariable(value: string): void {
  emit("select", value);
  closePanel();
}

function onToggle(): void {
  if (detailsEl.value?.open) {
    positionPanel();
    document.addEventListener("mousedown", onOutsideMouseDown, true);
    window.addEventListener("resize", positionPanel);
    window.addEventListener("scroll", positionPanel, true);
  } else {
    removeListeners();
  }
}

function positionPanel(): void {
  const summary = summaryEl.value;
  if (!summary) {
    return;
  }

  const rect = summary.getBoundingClientRect();
  const margin = 8;
  const width = Math.min(340, window.innerWidth - margin * 2);
  const top = rect.bottom + 6;
  const maxLeft = window.innerWidth - margin - width;
  const left = Math.min(Math.max(rect.right - width, margin), Math.max(maxLeft, margin));
  const maxHeight = Math.max(window.innerHeight - top - 12, 120);

  panelStyle.value = {
    position: "fixed",
    top: `${top}px`,
    left: `${left}px`,
    width: `${width}px`,
    maxHeight: `${maxHeight}px`
  };
}

function onOutsideMouseDown(event: MouseEvent): void {
  if (detailsEl.value && !detailsEl.value.contains(event.target as Node)) {
    closePanel();
  }
}

function closePanel(): void {
  if (detailsEl.value) {
    detailsEl.value.open = false;
  }
  removeListeners();
}

function removeListeners(): void {
  document.removeEventListener("mousedown", onOutsideMouseDown, true);
  window.removeEventListener("resize", positionPanel);
  window.removeEventListener("scroll", positionPanel, true);
}

onBeforeUnmount(removeListeners);

function filterTriggerVariables(
  groups: VariableGroup[],
  allowedTriggerVariables: string[] | undefined
): VariableGroup[] {
  if (!allowedTriggerVariables) {
    return groups;
  }

  const allowed = new Set(allowedTriggerVariables.map(normalizeTriggerVariable));
  return groups
    .map(group => group.key !== "trigger"
      ? group
      : {
          ...group,
          variables: group.variables.filter(variable =>
            allowed.has(normalizeTriggerVariable(variable.path)))
        })
    .filter(group => group.variables.length > 0);
}

function normalizeTriggerVariable(value: string): string {
  const cleanValue = value.replace(/[{}]/g, "");
  return cleanValue.replace(/^Trigger\./i, "");
}
</script>

<template>
  <details ref="detailsEl" class="variable-picker" @toggle="onToggle">
    <summary
      ref="summaryEl"
      class="variable-picker__summary"
      data-testid="variable-picker-toggle"
      title="Insert variable"
    >{x}</summary>
    <div class="variable-picker__panel" :style="panelStyle">
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
