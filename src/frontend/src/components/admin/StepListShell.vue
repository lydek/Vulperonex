<script setup lang="ts" generic="T">
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import RuleJsonEditor from "@/components/admin/RuleJsonEditor.vue";

const props = defineProps<{
  items: readonly T[];
  title: string;
  emptyText: string;
  prefix: string;
  modelValue: string;
  ariaLabel?: string;
  showAdvanced?: boolean;
}>();

const emit = defineEmits<{
  (event: "add"): void;
  (event: "remove", index: number): void;
  (event: "move", index: number, direction: -1 | 1): void;
  (event: "update:modelValue", value: string): void;
}>();

const { t, te } = useI18n();
const collapsedIndexes = ref<Set<number>>(new Set());

function fallbackLabel(key: string, fallback: string): string {
  return te(key) ? t(key) : fallback;
}

function isCollapsed(index: number): boolean {
  return collapsedIndexes.value.has(index);
}

function toggleCollapsed(index: number): void {
  const next = new Set(collapsedIndexes.value);
  if (next.has(index)) {
    next.delete(index);
  } else {
    next.add(index);
  }
  collapsedIndexes.value = next;
}

const showAdvancedPanel = (): boolean => props.showAdvanced !== false;
</script>

<template>
  <section class="status-card workflow-builder" :aria-labelledby="`${prefix}-title`">
    <div class="workflow-builder__header">
      <h2 :id="`${prefix}-title`" class="section-title">{{ title }}</h2>
      <button
        type="button"
        class="secondary-button"
        :data-testid="`${prefix}-add`"
        @click="emit('add')"
      >
        {{ fallbackLabel("common.add", "Add") }}
      </button>
    </div>

    <slot name="notice" />

    <p v-if="items.length === 0" class="workflow-builder__empty" role="status">
      {{ emptyText }}
    </p>

    <div
      v-for="(item, index) in items"
      :key="`${prefix}-${index}`"
      class="workflow-builder__card"
    >
      <div class="workflow-builder__card-header">
        <div class="workflow-builder__identity">
          <span class="workflow-builder__badge">{{ index + 1 }}</span>
          <div class="workflow-builder__identity-body">
            <slot name="identity" :item="item" :index="index" />
          </div>
        </div>

        <div class="workflow-builder__controls">
          <button
            type="button"
            class="icon-button"
            :data-testid="`${prefix}-toggle-${index}`"
            :aria-expanded="!isCollapsed(index)"
            @click="toggleCollapsed(index)"
          >
            {{
              isCollapsed(index)
                ? fallbackLabel("ruleEditor.builder.expand", "Expand")
                : fallbackLabel("ruleEditor.builder.collapse", "Collapse")
            }}
          </button>
          <button
            type="button"
            class="icon-button"
            :data-testid="`${prefix}-up-${index}`"
            :disabled="index === 0"
            @click="emit('move', index, -1)"
          >
            {{ fallbackLabel("ruleEditor.builder.moveUp", "Up") }}
          </button>
          <button
            type="button"
            class="icon-button"
            :data-testid="`${prefix}-down-${index}`"
            :disabled="index === items.length - 1"
            @click="emit('move', index, 1)"
          >
            {{ fallbackLabel("ruleEditor.builder.moveDown", "Down") }}
          </button>
          <button
            type="button"
            class="icon-button"
            :data-testid="`${prefix}-remove-${index}`"
            @click="emit('remove', index)"
          >
            {{ fallbackLabel("common.remove", "Remove") }}
          </button>
        </div>
      </div>

      <div v-show="!isCollapsed(index)" class="workflow-builder__body">
        <slot name="body" :item="item" :index="index" />
      </div>
    </div>

    <details v-if="showAdvancedPanel()" class="workflow-builder__advanced">
      <summary>{{ fallbackLabel("ruleEditor.builder.advancedJson", "Advanced JSON") }}</summary>
      <RuleJsonEditor
        :model-value="modelValue"
        :aria-label="ariaLabel ?? title"
        @update:model-value="emit('update:modelValue', $event)"
      />
    </details>
  </section>
</template>

<style scoped>
.workflow-builder {
  display: grid;
  gap: 12px;
}

.workflow-builder__header,
.workflow-builder__card-header,
.workflow-builder__controls,
.workflow-builder__identity {
  display: flex;
  gap: 12px;
}

.workflow-builder__header,
.workflow-builder__card-header {
  justify-content: space-between;
  align-items: flex-start;
}

.workflow-builder__identity {
  align-items: flex-start;
  flex: 1;
  min-width: 0;
}

.workflow-builder__identity-body {
  flex: 1;
  min-width: 0;
}

.workflow-builder__controls {
  align-items: center;
  flex-wrap: wrap;
}

.workflow-builder__badge {
  display: inline-grid;
  place-items: center;
  width: 28px;
  height: 28px;
  border-radius: 999px;
  background: #e7f1ef;
  color: #164f48;
  font-weight: 700;
}

.workflow-builder__empty {
  margin: 4px 0 0;
  color: #5f6f80;
  font-size: 13px;
}

.workflow-builder__card {
  border: 1px solid #d6dde5;
  border-radius: 10px;
  padding: 14px;
  display: grid;
  gap: 12px;
}

.workflow-builder__body {
  display: grid;
  gap: 12px;
}

.workflow-builder__advanced summary {
  cursor: pointer;
  font-weight: 600;
  color: #394756;
  margin-bottom: 8px;
}

@media (max-width: 720px) {
  .workflow-builder__header,
  .workflow-builder__card-header,
  .workflow-builder__identity {
    flex-direction: column;
  }

  .workflow-builder__controls {
    justify-content: flex-end;
  }
}
</style>
