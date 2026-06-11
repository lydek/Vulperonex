<script setup lang="ts">
import { computed, ref } from "vue";
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

const searchQuery = ref("");
const isOpen = ref(false);
const activeGroupKey = ref("all");

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
  return groups.value
    .filter(group => activeGroupKey.value === "all" || group.key === activeGroupKey.value)
    .map(group => ({
      ...group,
      variables: query.length === 0
        ? group.variables
        : group.variables.filter(entry => {
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

const groupFilters = computed(() => [
  {
    key: "all",
    label: t("variables.picker.filterAll"),
    count: groups.value.reduce((total, group) => total + group.variables.length, 0)
  },
  ...groups.value.map(group => ({
    key: group.key,
    label: getGroupLabel(group),
    count: group.variables.length
  }))
]);

const variableCount = computed(() =>
  filteredGroups.value.reduce((total, group) => total + group.variables.length, 0)
);

function filterByFieldProperty(rawGroups: VariableGroup[], filterKey: string): VariableGroup[] {
  const key = filterKey.toLowerCase();

  if (key.includes("plugin") || key.includes("actionid") || key === "params" || key === "args") {
    return filterPluginVariables(rawGroups);
  }

  if (
    key === "target" ||
    key.includes("targetlogin") ||
    key.includes("targetuser") ||
    key.includes("recipient")
  ) {
    return filterTargetUserVariables(rawGroups);
  }

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
          const pathLower = v.path.toLowerCase().replace(/[{}]/g, "");
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
          const pathLower = v.path.toLowerCase().replace(/[{}]/g, "");
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

  if (key.includes("reward") || key.includes("redemption")) {
    return rawGroups
      .map(group => ({
        ...group,
        variables: group.variables.filter(v => {
          const pathLower = v.path.toLowerCase();
          if (pathLower.includes(".status")) {
            return false;
          }
          return pathLower.includes("reward") || pathLower.includes("redemption");
        })
      }))
      .filter(group => group.variables.length > 0);
  }

  if (key === "key" || key.includes("cooldown")) {
    return rawGroups
      .map(group => ({
        ...group,
        variables: group.variables.filter(v => {
          const pathLower = v.path.toLowerCase().replace(/[{}]/g, "");
          if (pathLower.includes(".status")) {
            return false;
          }
          return (
            pathLower.includes("userid") ||
            pathLower.includes("userlogin") ||
            pathLower.endsWith(".login") ||
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

function filterPluginVariables(rawGroups: VariableGroup[]): VariableGroup[] {
  return rawGroups
    .map(group => ({
      ...group,
      variables: group.variables.filter(v => {
        const pathLower = v.path.toLowerCase().replace(/[{}]/g, "");
        if (pathLower.includes(".status")) {
          return false;
        }

        if (pathLower.startsWith("trigger.")) {
          return pathLower.includes("plugin")
            || pathLower.includes("module")
            || pathLower.includes("action")
            || pathLower.includes("payload");
        }

        if (pathLower.startsWith("args.")) {
          return true;
        }

        if (pathLower.startsWith("step.")) {
          return pathLower.includes("plugin") || pathLower.includes("action");
        }

        return false;
      })
    }))
    .filter(group => group.variables.length > 0);
}

function filterTargetUserVariables(rawGroups: VariableGroup[]): VariableGroup[] {
  return rawGroups
    .map(group => ({
      ...group,
      variables: group.variables.filter(v => {
        const pathLower = v.path.toLowerCase().replace(/[{}]/g, "");
        if (pathLower.includes(".status")) {
          return false;
        }

        if (pathLower.startsWith("trigger.command.")) {
          return pathLower.endsWith(".target");
        }

        if (pathLower.startsWith("trigger.")) {
          return false;
        }

        if (pathLower.startsWith("member.")) {
          return pathLower.endsWith(".userid")
            || pathLower.endsWith(".login")
            || pathLower.endsWith(".displayname");
        }

        if (pathLower.startsWith("step.")) {
          return pathLower.endsWith(".userid")
            || pathLower.endsWith(".login")
            || pathLower.endsWith(".displayname")
            || pathLower.endsWith(".target")
            || pathLower.endsWith(".targetlogin")
            || pathLower.endsWith(".targetdisplayname");
        }

        return false;
      })
    }))
    .filter(group => group.variables.length > 0);
}

function selectVariable(value: string): void {
  emit("select", value);
}

function togglePanel(): void {
  isOpen.value = !isOpen.value;
}

function closePanel(): void {
  isOpen.value = false;
}

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
  <div class="variable-picker">
    <button
      type="button"
      class="variable-picker__summary"
      data-testid="variable-picker-toggle"
      title="Insert variable"
      :aria-expanded="isOpen"
      @click="togglePanel"
    >
      {x}
    </button>
    <aside
      class="variable-picker__panel"
      :class="{ 'variable-picker__panel--open': isOpen }"
      :aria-hidden="!isOpen"
      role="dialog"
      @keydown.esc="closePanel"
    >
      <div class="variable-picker__header">
        <div>
          <h2 class="variable-picker__heading">{{ t("variables.picker.title") }}</h2>
          <p class="variable-picker__summary-text">{{ t("variables.picker.count", { count: variableCount }) }}</p>
        </div>
        <button type="button" class="variable-picker__close" @click="closePanel">
          {{ t("common.close") }}
        </button>
      </div>

      <input
        v-model="searchQuery"
        class="variable-picker__search"
        data-testid="variable-picker-search"
        type="search"
        :placeholder="t('variables.picker.searchPlaceholder')"
        :aria-label="t('variables.picker.searchLabel')"
      />

      <div class="variable-picker__content">
        <nav class="variable-picker__filters" :aria-label="t('variables.picker.filters')">
          <button
            v-for="filter in groupFilters"
            :key="filter.key"
            type="button"
            class="variable-picker__filter"
            :class="{ 'variable-picker__filter--active': activeGroupKey === filter.key }"
            :data-testid="`variable-picker-filter-${filter.key}`"
            @click="activeGroupKey = filter.key"
          >
            <span>{{ filter.label }}</span>
            <span class="variable-picker__count">{{ filter.count }}</span>
          </button>
        </nav>

        <div v-if="filteredGroups.length > 0" class="variable-picker__sections">
          <section
            v-for="group in filteredGroups"
            :key="group.key"
            class="variable-picker__group"
            :data-testid="`variable-picker-group-${group.key}`"
          >
            <h3 class="variable-picker__title">
              <span>{{ getGroupLabel(group) }}</span>
              <span class="variable-picker__count">{{ group.variables.length }}</span>
            </h3>
            <div class="variable-picker__list">
              <button
                v-for="entry in group.variables"
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
    </aside>
  </div>
</template>

<style scoped>
.variable-picker {
  position: relative;
  display: inline-flex;
}

.variable-picker__summary {
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--vp-text-muted);
  font-size: 11px;
  font-weight: 600;
  padding: 4px 8px;
  border: 1px solid var(--vp-border-default);
  border-radius: 999px;
  background: var(--vp-bg-surface);
  user-select: none;
  white-space: nowrap;
}

.variable-picker__panel {
  position: fixed;
  right: 0;
  top: 0;
  bottom: 0;
  z-index: 40;
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr);
  gap: 14px;
  width: min(720px, 44vw);
  min-width: 560px;
  padding: 18px;
  border-left: 1px solid var(--vp-border-default);
  background: var(--vp-bg-surface);
  box-shadow: var(--vp-shadow-elevated);
  transform: translateX(104%);
  transition: transform 160ms ease-out;
  pointer-events: none;
}

.variable-picker__panel--open {
  transform: translateX(0);
  pointer-events: auto;
}

.variable-picker__header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
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

.variable-picker__close {
  padding: 6px 10px;
  border: 1px solid var(--vp-border-default);
  border-radius: 8px;
  background: var(--vp-bg-surface-subtle);
  color: var(--vp-text-primary);
  cursor: pointer;
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
  grid-template-columns: 190px minmax(0, 1fr);
  gap: 16px;
  min-height: 0;
}

.variable-picker__filters {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 0;
  overflow: auto;
  padding-right: 12px;
  border-right: 1px solid var(--vp-border-subtle);
}

.variable-picker__filter {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  width: 100%;
  padding: 9px 10px;
  border: 1px solid transparent;
  border-radius: 8px;
  background: transparent;
  color: var(--vp-text-primary);
  cursor: pointer;
  text-align: left;
}

.variable-picker__filter:hover,
.variable-picker__filter--active {
  border-color: var(--vp-accent);
  background: var(--vp-bg-surface-muted);
}

.variable-picker__sections {
  display: grid;
  gap: 16px;
  align-content: start;
  min-height: 0;
  overflow: auto;
  padding-right: 4px;
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
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--vp-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.variable-picker__list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(230px, 1fr));
  gap: 8px;
}

.variable-picker__item {
  display: grid;
  gap: 4px;
  width: 100%;
  padding: 10px 12px;
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
  .variable-picker__panel {
    top: auto;
    width: 100vw;
    min-width: 0;
    max-height: 86vh;
    border-left: 0;
    border-top: 1px solid var(--vp-border-default);
    transform: translateY(104%);
  }

  .variable-picker__panel--open {
    transform: translateY(0);
  }

  .variable-picker__content {
    grid-template-columns: 1fr;
  }

  .variable-picker__filters {
    flex-direction: row;
    overflow-x: auto;
    padding-right: 0;
    padding-bottom: 8px;
    border-right: 0;
    border-bottom: 1px solid var(--vp-border-subtle);
  }

  .variable-picker__filter {
    min-width: 150px;
  }

  .variable-picker__list {
    grid-template-columns: 1fr;
  }
}
</style>
