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
const searchQuery = ref("");

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

const filteredGroups = computed<VariableGroup[]>(() => {
  const query = searchQuery.value.trim().toLowerCase();
  if (query.length === 0) {
    return groups.value;
  }

  return groups.value
    .map(group => ({
      ...group,
      variables: group.variables.filter(entry => {
        const haystack = [
          entry.path,
          entry.label,
          entry.hint ?? "",
          getVariableLabel(entry),
          getGroupLabel(group)
        ].join(" ").toLowerCase();
        return haystack.includes(query);
      })
    }))
    .filter(group => group.variables.length > 0);
});

const variableCount = computed(() =>
  filteredGroups.value.reduce((total, group) => total + group.variables.length, 0)
);

const activeGroup = computed<VariableGroup | null>(() => {
  if (filteredGroups.value.length === 0) {
    return null;
  }

  return filteredGroups.value.find(group => group.key === activeGroupKey.value) ?? filteredGroups.value[0];
});

watch(filteredGroups, (nextGroups) => {
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

  if (key === "userid" || key.endsWith(".userid")) {
    return rawGroups
      .map(group => ({
        ...group,
        variables: group.variables.filter(v => {
          const pathLower = v.path.toLowerCase().replace(/[{}]/g, "");
          return pathLower.endsWith(".userid") && !pathLower.includes(".status");
        })
      }))
      .filter(group => group.variables.length > 0);
  }

  if (key.includes("user") || key.includes("member")) {
    return rawGroups
      .map(group => ({
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
      }))
      .filter(group => group.variables.length > 0);
  }

  if (key.includes("platform")) {
    return rawGroups
      .map(group => ({
        ...group,
        variables: group.variables.filter(v => {
          const pathLower = v.path.toLowerCase();
          return pathLower.includes("platform") && !pathLower.includes(".status");
        })
      }))
      .filter(group => group.variables.length > 0);
  }

  if (key.includes("channel")) {
    return rawGroups
      .map(group => ({
        ...group,
        variables: group.variables.filter(v => {
          const pathLower = v.path.toLowerCase();
          return pathLower.includes("channel") && !pathLower.includes(".status");
        })
      }))
      .filter(group => group.variables.length > 0);
  }

  if (key === "key" || key.includes("cooldown")) {
    return rawGroups
      .map(group => ({
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
      }))
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
  const width = Math.min(620, window.innerWidth - margin * 2);
  const top = rect.bottom + 6;
  const maxLeft = window.innerWidth - margin - width;
  const left = Math.min(Math.max(rect.right - width, margin), Math.max(maxLeft, margin));
  const maxHeight = Math.max(window.innerHeight - top - 12, 180);

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
      <div class="variable-picker__header">
        <div>
          <h2 class="variable-picker__heading">{{ t("variables.picker.title") }}</h2>
          <p class="variable-picker__summary-text">{{ t("variables.picker.count", { count: variableCount }) }}</p>
        </div>
        <input
          v-model="searchQuery"
          class="variable-picker__search"
          data-testid="variable-picker-search"
          type="search"
          :placeholder="t('variables.picker.searchPlaceholder')"
          :aria-label="t('variables.picker.searchLabel')"
        />
      </div>

      <div v-if="filteredGroups.length > 0" class="variable-picker__content">
        <nav class="variable-picker__group-list" aria-label="Variable groups">
          <button
            v-for="group in filteredGroups"
            :key="group.key"
            type="button"
            class="variable-picker__group-button"
            :class="{ 'is-active': group.key === activeGroupKey }"
            :aria-pressed="group.key === activeGroupKey"
            :data-testid="`variable-picker-group-${group.key}`"
            @click="activeGroupKey = group.key"
          >
            <span>{{ getGroupLabel(group) }}</span>
            <span class="variable-picker__count">{{ group.variables.length }}</span>
          </button>
        </nav>

        <section v-if="activeGroup" class="variable-picker__group">
          <h3 class="variable-picker__title">{{ getGroupLabel(activeGroup) }}</h3>
          <div class="variable-picker__list">
            <button
              v-for="entry in activeGroup.variables"
              :key="entry.path"
              type="button"
              class="variable-picker__item"
              @click="selectVariable(entry.path)"
            >
              <span class="variable-picker__token">{{ entry.path }}</span>
              <span class="variable-picker__label">{{ getVariableLabel(entry) }}</span>
              <span v-if="entry.hint" class="variable-picker__hint">{{ entry.hint }}</span>
            </button>
          </div>
        </section>
      </div>

      <p v-else class="variable-picker__empty" data-testid="variable-picker-empty">
        {{ t("variables.picker.empty") }}
      </p>
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
  width: min(620px, 92vw);
  max-height: 420px;
  overflow: auto;
  padding: 14px;
  border: 1px solid var(--vp-border-default);
  border-radius: 12px;
  background: var(--vp-bg-surface);
  box-shadow: var(--vp-shadow-elevated);
}

.variable-picker__header {
  display: grid;
  grid-template-columns: minmax(150px, 0.55fr) minmax(180px, 1fr);
  align-items: end;
  gap: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--vp-border-subtle);
  margin-bottom: 12px;
}

.variable-picker__heading {
  margin: 0;
  color: var(--vp-text-primary);
  font-size: 14px;
  font-weight: 700;
}

.variable-picker__summary-text {
  margin: 4px 0 0;
  color: var(--vp-text-muted);
  font-size: 11px;
}

.variable-picker__search {
  width: 100%;
  min-width: 0;
  box-sizing: border-box;
  padding: 8px 10px;
  border: 1px solid var(--vp-border-default);
  border-radius: 8px;
  background: var(--vp-bg-surface-subtle);
  color: var(--vp-text-primary);
  font-size: 13px;
}

.variable-picker__content {
  display: grid;
  grid-template-columns: minmax(150px, 0.45fr) minmax(0, 1fr);
  gap: 12px;
  min-height: 220px;
}

.variable-picker__group-list {
  display: grid;
  align-content: start;
  gap: 6px;
  padding-right: 10px;
  border-right: 1px solid var(--vp-border-subtle);
}

.variable-picker__group-button {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 7px 9px;
  border: 1px solid transparent;
  border-radius: 8px;
  background: transparent;
  color: var(--vp-text-secondary);
  font-size: 12px;
  font-weight: 600;
  text-align: left;
}

.variable-picker__group-button:hover {
  border-color: var(--vp-border-default);
  background: var(--vp-bg-surface-subtle);
}

.variable-picker__group-button.is-active {
  border-color: var(--vp-accent);
  background: var(--vp-bg-selected);
  color: var(--vp-text-accent);
}

.variable-picker__count {
  min-width: 22px;
  padding: 2px 6px;
  border-radius: 999px;
  background: var(--vp-bg-surface-muted);
  color: var(--vp-text-muted);
  font-size: 11px;
  text-align: center;
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
  max-height: 280px;
  overflow: auto;
  padding-right: 2px;
}

.variable-picker__item {
  display: grid;
  gap: 4px;
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

.variable-picker__label,
.variable-picker__hint {
  color: var(--vp-text-muted);
  font-size: 11px;
}

.variable-picker__label {
  font-weight: 500;
}

.variable-picker__empty {
  margin: 0;
  padding: 18px;
  border: 1px dashed var(--vp-border-default);
  border-radius: 8px;
  color: var(--vp-text-muted);
  text-align: center;
}

@media (max-width: 560px) {
  .variable-picker__header,
  .variable-picker__content {
    grid-template-columns: 1fr;
  }

  .variable-picker__group-list {
    grid-template-columns: repeat(auto-fit, minmax(130px, 1fr));
    padding-right: 0;
    padding-bottom: 10px;
    border-right: 0;
    border-bottom: 1px solid var(--vp-border-subtle);
  }
}
</style>
