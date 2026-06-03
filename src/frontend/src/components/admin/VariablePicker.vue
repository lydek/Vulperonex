<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import {
  buildVariableGroups,
  parseArrayModel,
  type ActionDefinition,
  type JsonRecord,
  type VariableGroup
} from "@/components/admin/workflowEditor";

const { t, te } = useI18n();

function getVariableLabel(entry: { path: string; label: string }): string {
  const cleanPath = entry.path.replace(/[{}]/g, "");
  const translationKey = `variables.${cleanPath}`;
  if (te(translationKey)) {
    return t(translationKey);
  }
  return entry.label || cleanPath;
}

function getGroupLabel(group: { key: string; label: string }): string {
  const translationKey = `variables.group.${group.key}`;
  if (te(translationKey)) {
    return t(translationKey);
  }
  return group.label;
}

const props = defineProps<{
  previousSteps?: JsonRecord[];
  previousStepsJson?: string;
  expressionMode?: boolean;
  allowedTriggerVariables?: string[];
  actionDefinitions?: ActionDefinition[];
  filterKey?: string;
}>();

const emit = defineEmits<{ (event: "select", value: string): void }>();

const detailsEl = ref<HTMLDetailsElement | null>(null);
const summaryEl = ref<HTMLElement | null>(null);
const panelStyle = ref<Record<string, string>>({});
const activeGroupKey = ref<string>("");

const groups = computed<VariableGroup[]>(() => {
  const previousSteps = props.previousSteps
    ?? parseArrayModel(props.previousStepsJson ?? "[]");
  const rawGroups = filterTriggerVariables(
    buildVariableGroups(previousSteps, props.expressionMode ?? false, props.actionDefinitions),
    props.allowedTriggerVariables
  );
  if (!props.filterKey) {
    return rawGroups;
  }
  return filterByFieldProperty(rawGroups, props.filterKey);
});

const activeGroups = computed<VariableGroup[]>(() => {
  if (groups.value.length === 0) {
    return [];
  }

  const active = groups.value.find(group => group.key === activeGroupKey.value);
  return active ? [active] : [groups.value[0]];
});

watch(groups, (nextGroups) => {
  if (nextGroups.length === 0) {
    activeGroupKey.value = "";
    return;
  }

  if (!nextGroups.some(group => group.key === activeGroupKey.value)) {
    activeGroupKey.value = nextGroups[0].key;
  }
}, { immediate: true });

function filterByFieldProperty(rawGroups: VariableGroup[], filterKey: string): VariableGroup[] {
  const key = filterKey.toLowerCase();

  // 1. UserId field: only show paths ending in ".userid". Drop UserLogin /
  //    DisplayName (human-facing fields) since matchers + check-in resolve by
  //    platform numeric user id. Reduces variable picker noise.
  if (key === "userid" || key.endsWith(".userid")) {
    return rawGroups
      .map(group => {
        return {
          ...group,
          variables: group.variables.filter(v => {
            const pathLower = v.path.toLowerCase().replace(/[{}]/g, "");
            if (pathLower.includes(".status")) {
              return false;
            }
            return pathLower.endsWith(".userid");
          })
        };
      })
      .filter(group => group.variables.length > 0);
  }

  // 1b. Generic user/member fields (DisplayName, etc.) keep wider picker.
  if (key.includes("user") || key.includes("member")) {
    return rawGroups
      .map(group => {
        return {
          ...group,
          variables: group.variables.filter(v => {
            const pathLower = v.path.toLowerCase();
            if (pathLower.includes(".status")) {
              return false;
            }
            return (
              pathLower.includes("userid") ||
              pathLower.includes("userlogin") ||
              pathLower.includes("displayname")
            );
          })
        };
      })
      .filter(group => group.variables.length > 0);
  }

  // 2. Platform 相關欄位
  if (key.includes("platform")) {
    return rawGroups
      .map(group => {
        return {
          ...group,
          variables: group.variables.filter(v => {
            const pathLower = v.path.toLowerCase();
            return pathLower.includes("platform") && !pathLower.includes(".status");
          })
        };
      })
      .filter(group => group.variables.length > 0);
  }

  // 3. Channel 相關欄位
  if (key.includes("channel")) {
    return rawGroups
      .map(group => {
        return {
          ...group,
          variables: group.variables.filter(v => {
            const pathLower = v.path.toLowerCase();
            return pathLower.includes("channel") && !pathLower.includes(".status");
          })
        };
      })
      .filter(group => group.variables.length > 0);
  }

  // 4. Cooldown key (主要填 user id 做冷卻鍵，與 UserId 類似但可適度包容其他鍵)
  if (key === "key" || key.includes("cooldown")) {
    return rawGroups
      .map(group => {
        return {
          ...group,
          variables: group.variables.filter(v => {
            const pathLower = v.path.toLowerCase();
            if (pathLower.includes(".status")) {
              return false;
            }
            return (
              pathLower.includes("userid") ||
              pathLower.includes("userlogin") ||
              pathLower.includes("displayname") ||
              pathLower.includes("channel") ||
              pathLower.includes("platform")
            );
          })
        };
      })
      .filter(group => group.variables.length > 0);
  }

  return rawGroups;
}

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
      <div v-if="groups.length > 1" class="variable-picker__tabs" role="tablist" aria-label="Variable groups">
        <button
          v-for="group in groups"
          :key="group.key"
          type="button"
          class="variable-picker__tab"
          :class="{ 'is-active': group.key === activeGroupKey }"
          :aria-pressed="group.key === activeGroupKey"
          @click="activeGroupKey = group.key"
        >
          {{ getGroupLabel(group) }}
        </button>
      </div>

      <section v-for="group in activeGroups" :key="group.key" class="variable-picker__group">
        <h3 class="variable-picker__title">{{ getGroupLabel(group) }}</h3>
        <div class="variable-picker__list">
          <button
            v-for="entry in group.variables"
            :key="entry.path"
            type="button"
            class="variable-picker__item"
            @click="selectVariable(entry.path)"
          >
            <span class="variable-picker__token">{{ entry.path }}</span>
            <div class="variable-picker__desc-row">
              <span class="variable-picker__label">{{ getVariableLabel(entry) }}</span>
              <span v-if="entry.hint" class="variable-picker__hint-separator">·</span>
              <span v-if="entry.hint" class="variable-picker__hint">{{ entry.hint }}</span>
            </div>
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
  color: var(--vp-text-muted);
  font-size: 11px;
  font-weight: 600;
  list-style: none;
  padding: 4px 8px;
  border: 1px solid var(--vp-border-default);
  border-radius: 999px;
  background: var(--vp-bg-surface);
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
  border: 1px solid var(--vp-border-default);
  border-radius: 12px;
  background: var(--vp-bg-surface);
  box-shadow: var(--vp-shadow-elevated);
}

.variable-picker__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 12px;
}

.variable-picker__tab {
  padding: 4px 10px;
  border: 1px solid var(--vp-border-default);
  border-radius: 999px;
  background: var(--vp-bg-surface-subtle);
  color: var(--vp-text-muted);
  font-size: 11px;
  font-weight: 600;
}

.variable-picker__tab.is-active {
  border-color: var(--vp-accent);
  background: var(--vp-bg-selected);
  color: var(--vp-text-accent);
}

.variable-picker__group + .variable-picker__group {
  margin-top: 12px;
}

.variable-picker__title {
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--vp-text-muted);
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
  border: 1px solid var(--vp-border-default);
  border-radius: 8px;
  background: var(--vp-bg-surface);
  color: var(--vp-text-primary);
}

.variable-picker__item:hover {
  border-color: var(--vp-accent);
  background: var(--vp-bg-surface-muted);
}

.variable-picker__token {
  font-family: Consolas, "Courier New", monospace;
  font-size: 12px;
}

.variable-picker__hint {
  color: var(--vp-text-muted);
  font-size: 11px;
}

.variable-picker__desc-row {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  color: var(--vp-text-muted);
  margin-top: 2px;
}

.variable-picker__label {
  font-weight: 500;
}

.variable-picker__hint-separator {
  opacity: 0.5;
}
</style>
