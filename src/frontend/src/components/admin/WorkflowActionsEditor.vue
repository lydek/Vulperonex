<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ConditionExpressionInput from "@/components/admin/ConditionExpressionInput.vue";
import RuleJsonEditor from "@/components/admin/RuleJsonEditor.vue";
import VariableFieldInput from "@/components/admin/VariableFieldInput.vue";
import {
  actionDefinitions,
  asBoolean,
  asNumber,
  asString,
  findActionDefinition,
  fromJsonObjectText,
  fromNumberListText,
  fromStringListText,
  fromStringMapText,
  isJsonRecord,
  parseArrayModel,
  stringifyArrayModel,
  toJsonObjectText,
  toNumberListText,
  toStringListText,
  toStringMapText,
  type ActionDefinition,
  type FieldDefinition,
  type JsonRecord
} from "@/components/admin/workflowEditor";

const props = defineProps<{
  modelValue: string;
  title: string;
  emptyText: string;
  testIdPrefix?: string;
}>();

const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();
const { t, te } = useI18n();

const items = ref<JsonRecord[]>([]);
const lastSerialized = ref("");

defineExpose({ focus });

watch(() => props.modelValue, syncFromModel, { immediate: true });

const prefix = computed(() => props.testIdPrefix ?? "workflow-actions");

function focus(): void {
  const firstInput = document.querySelector<HTMLInputElement>(`[data-testid="${prefix.value}-type-0"]`);
  firstInput?.focus();
}

function syncFromModel(modelValue: string): void {
  if (modelValue === lastSerialized.value) {
    return;
  }
  items.value = parseArrayModel(modelValue);
}

function emitItems(): void {
  const serialized = stringifyArrayModel(items.value);
  lastSerialized.value = serialized;
  emit("update:modelValue", serialized);
}

function fallbackLabel(key: string, text: string): string {
  return te(key) ? t(key) : text;
}

function addItem(): void {
  items.value = [...items.value, actionDefinitions[0].create()];
  emitItems();
}

function removeItem(index: number): void {
  items.value = items.value.filter((_, itemIndex) => itemIndex !== index);
  emitItems();
}

function moveItem(index: number, direction: -1 | 1): void {
  const targetIndex = index + direction;
  if (targetIndex < 0 || targetIndex >= items.value.length) {
    return;
  }

  const next = items.value.slice();
  const [current] = next.splice(index, 1);
  next.splice(targetIndex, 0, current);
  items.value = next;
  emitItems();
}

function definitionFor(item: JsonRecord): ActionDefinition | undefined {
  return findActionDefinition(asString(item.type));
}

function patchItem(index: number, patch: Partial<JsonRecord>): void {
  const next = items.value.slice();
  next[index] = cleanRecord({ ...next[index], ...patch });
  items.value = next;
  emitItems();
}

function onTypeChange(index: number, nextType: string): void {
  const definition = findActionDefinition(nextType);
  if (!definition) {
    patchItem(index, { type: nextType });
    return;
  }

  const current = items.value[index] ?? {};
  items.value = items.value.map((item, itemIndex) => {
    if (itemIndex !== index) {
      return item;
    }

    return cleanRecord({
      ...definition.create(),
      executionCondition: current.executionCondition,
      outputVariable: current.outputVariable,
      timeoutMs: current.timeoutMs,
      maxRetries: current.maxRetries,
      backoffMs: current.backoffMs,
      errorBehavior: current.errorBehavior
    });
  });
  emitItems();
}

function cleanRecord(record: JsonRecord): JsonRecord {
  const next: JsonRecord = {};
  for (const [key, value] of Object.entries(record)) {
    if (value === null || value === undefined) {
      continue;
    }

    if (typeof value === "string" && value.trim().length === 0 && key !== "type") {
      continue;
    }

    if (Array.isArray(value) && value.length === 0) {
      continue;
    }

    if (isJsonRecord(value) && Object.keys(value).length === 0) {
      continue;
    }

    next[key] = value;
  }
  return next;
}

function fieldValue(item: JsonRecord, field: FieldDefinition): string | number | boolean {
  switch (field.kind) {
    case "checkbox":
      return asBoolean(item[field.key], false);
    case "number":
      return asNumber(item[field.key]) ?? 0;
    case "string-list":
      return toStringListText(item[field.key]);
    case "number-list":
      return toNumberListText(item[field.key]);
    case "string-map":
      return toStringMapText(item[field.key]);
    case "json-object":
      return toJsonObjectText(item[field.key]);
    default:
      return asString(item[field.key]);
  }
}

function updateField(index: number, field: FieldDefinition, rawValue: string | boolean): void {
  switch (field.kind) {
    case "checkbox":
      patchItem(index, { [field.key]: Boolean(rawValue) });
      return;
    case "number": {
      const parsed = Number(rawValue);
      patchItem(index, { [field.key]: Number.isFinite(parsed) ? parsed : 0 });
      return;
    }
    case "string-list":
      patchItem(index, { [field.key]: fromStringListText(String(rawValue)) });
      return;
    case "number-list":
      patchItem(index, { [field.key]: fromNumberListText(String(rawValue)) });
      return;
    case "string-map":
      patchItem(index, { [field.key]: fromStringMapText(String(rawValue)) });
      return;
    case "json-object":
      patchItem(index, { [field.key]: fromJsonObjectText(String(rawValue)) });
      return;
    default:
      patchItem(index, { [field.key]: String(rawValue) });
  }
}

function previousStepsFor(index: number): JsonRecord[] {
  return items.value.slice(0, index);
}
</script>

<template>
  <section class="status-card workflow-builder" :aria-labelledby="`${prefix}-title`">
    <div class="workflow-builder__header">
      <h2 :id="`${prefix}-title`" class="section-title">{{ title }}</h2>
      <button type="button" class="secondary-button" :data-testid="`${prefix}-add`" @click="addItem">
        {{ fallbackLabel("common.add", "Add") }}
      </button>
    </div>

    <p v-if="items.length === 0" class="workflow-builder__empty" role="status">{{ emptyText }}</p>

    <div v-for="(item, index) in items" :key="`${prefix}-${index}`" class="workflow-builder__card">
      <div class="workflow-builder__card-header">
        <div class="workflow-builder__identity">
          <span class="workflow-builder__badge">{{ index + 1 }}</span>
          <div>
            <label class="form-field">
              <span class="form-label">{{ fallbackLabel("ruleEditor.steps.type", "Type") }}</span>
              <select
                :data-testid="`${prefix}-type-${index}`"
                :value="asString(item.type)"
                @change="onTypeChange(index, ($event.target as HTMLSelectElement).value)"
              >
                <option v-for="definition in actionDefinitions" :key="definition.type" :value="definition.type">
                  {{ definition.label }}
                </option>
                <option
                  v-if="!definitionFor(item) && asString(item.type).length > 0"
                  :value="asString(item.type)"
                >
                  {{ asString(item.type) }}
                </option>
              </select>
            </label>
            <p class="workflow-builder__summary">
              {{ definitionFor(item)?.description ?? fallbackLabel("ruleEditor.builder.unknownAction", "Unknown action type. Use advanced JSON fallback to edit unsupported fields.") }}
            </p>
          </div>
        </div>

        <div class="workflow-builder__controls">
          <button type="button" class="icon-button" :disabled="index === 0" @click="moveItem(index, -1)">
            Up
          </button>
          <button type="button" class="icon-button" :disabled="index === items.length - 1" @click="moveItem(index, 1)">
            Down
          </button>
          <button type="button" class="icon-button" @click="removeItem(index)">
            {{ fallbackLabel("common.remove", "Remove") }}
          </button>
        </div>
      </div>

      <div class="workflow-builder__grid" v-if="definitionFor(item)">
        <template v-for="field in definitionFor(item)?.fields" :key="field.key">
          <label v-if="field.kind === 'text' || field.kind === 'number' || field.kind === 'select'" class="form-field">
            <span class="form-label">{{ field.label }}</span>
            <VariableFieldInput
              v-if="field.kind === 'text' && field.key !== 'condition'"
              :model-value="String(fieldValue(item, field))"
              :placeholder="field.placeholder"
              :previous-steps="previousStepsFor(index)"
              @update:model-value="updateField(index, field, $event)"
            />
            <ConditionExpressionInput
              v-else-if="field.kind === 'text' && field.key === 'condition'"
              :model-value="String(fieldValue(item, field))"
              :placeholder="field.placeholder"
              :previous-steps="previousStepsFor(index)"
              :data-test-id="`${prefix}-field-${field.key}-${index}`"
              @update:model-value="updateField(index, field, $event)"
            />
            <input
              v-else-if="field.kind === 'number'"
              :type="field.kind === 'number' ? 'number' : 'text'"
              :value="String(fieldValue(item, field))"
              :placeholder="field.placeholder"
              @input="updateField(index, field, ($event.target as HTMLInputElement).value)"
            />
            <select
              v-else
              :value="String(fieldValue(item, field))"
              @change="updateField(index, field, ($event.target as HTMLSelectElement).value)"
            >
              <option v-for="option in field.options" :key="option.value" :value="option.value">
                {{ option.label }}
              </option>
            </select>
          </label>

          <label v-else-if="field.kind === 'checkbox'" class="form-field form-field-inline">
            <input
              type="checkbox"
              :checked="Boolean(fieldValue(item, field))"
              @change="updateField(index, field, ($event.target as HTMLInputElement).checked)"
            />
            <span>{{ field.label }}</span>
          </label>

          <label v-else-if="field.kind === 'textarea'" class="form-field workflow-builder__wide">
            <span class="form-label">{{ field.label }}</span>
            <VariableFieldInput
              :model-value="String(fieldValue(item, field))"
              multiline
              :rows="4"
              :placeholder="field.placeholder"
              :previous-steps="previousStepsFor(index)"
              @update:model-value="updateField(index, field, $event)"
            />
          </label>

          <label v-else-if="field.kind === 'string-map' || field.kind === 'json-object' || field.kind === 'string-list' || field.kind === 'number-list'" class="form-field workflow-builder__wide">
            <span class="form-label">{{ field.label }}</span>
            <textarea
              :rows="3"
              :value="String(fieldValue(item, field))"
              :placeholder="field.placeholder"
              @input="updateField(index, field, ($event.target as HTMLTextAreaElement).value)"
            />
          </label>
        </template>

        <div class="workflow-builder__meta workflow-builder__wide">
          <label class="form-field workflow-builder__meta-condition">
            <span class="form-label">{{ fallbackLabel("ruleEditor.executionCondition", "Execution condition") }}</span>
            <ConditionExpressionInput
              :model-value="asString(item.executionCondition)"
              :previous-steps="previousStepsFor(index)"
              placeholder="Trigger.MessageText == '!go'"
              :data-test-id="`${prefix}-execution-${index}`"
              @update:model-value="patchItem(index, { executionCondition: $event })"
            />
          </label>

          <label class="form-field workflow-builder__meta-output">
            <span class="form-label">{{ fallbackLabel("ruleEditor.outputVariable", "Output variable") }}</span>
            <input
              :value="asString(item.outputVariable)"
              placeholder="Result"
              @input="patchItem(index, { outputVariable: ($event.target as HTMLInputElement).value })"
            />
          </label>
        </div>
      </div>

      <div v-else class="workflow-builder__unknown">
        {{ fallbackLabel("ruleEditor.builder.unknownAction", "Unknown action type. Use advanced JSON fallback to edit unsupported fields.") }}
      </div>
    </div>

    <details class="workflow-builder__advanced">
      <summary>{{ fallbackLabel("ruleEditor.builder.advancedJson", "Advanced JSON") }}</summary>
      <RuleJsonEditor :model-value="modelValue" :aria-label="title" @update:model-value="emit('update:modelValue', $event)" />
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

.workflow-builder__summary,
.workflow-builder__empty,
.workflow-builder__unknown {
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

.workflow-builder__grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
}

.workflow-builder__wide {
  grid-column: 1 / -1;
}

.workflow-builder__meta {
  display: grid;
  gap: 12px;
  grid-template-columns: minmax(0, 1.7fr) minmax(220px, 0.8fr);
  align-items: start;
}

.workflow-builder__meta-condition,
.workflow-builder__meta-output {
  min-width: 0;
}

.workflow-builder__advanced summary {
  cursor: pointer;
  font-weight: 600;
  color: #394756;
  margin-bottom: 8px;
}

textarea {
  border: 1px solid #d6dde5;
  border-radius: 6px;
  padding: 10px 12px;
  resize: vertical;
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

  .workflow-builder__meta {
    grid-template-columns: 1fr;
  }
}
</style>
