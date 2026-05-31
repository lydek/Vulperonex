<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import {
  getRules,
  isSubWorkflowSummary,
  type WorkflowRuleSummary
} from "@/api/client";
import ConditionExpressionInput from "@/components/admin/ConditionExpressionInput.vue";
import StepListShell from "@/components/admin/StepListShell.vue";
import VariableFieldInput from "@/components/admin/VariableFieldInput.vue";
import {
  asBoolean,
  asNumber,
  asString,
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
import { useActionMetadataStore } from "@/stores/actionMetadata";

const props = defineProps<{
  modelValue: string;
  title: string;
  emptyText: string;
  testIdPrefix?: string;
  notice?: string;
}>();

const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();
const { t, te } = useI18n();
const actionMetadata = useActionMetadataStore();

const items = ref<JsonRecord[]>([]);
const lastSerialized = ref("");
const availableRules = ref<WorkflowRuleSummary[]>([]);

defineExpose({ focus });

watch(() => props.modelValue, syncFromModel, { immediate: true });

const prefix = computed(() => props.testIdPrefix ?? "workflow-actions");
const actionDefinitions = computed(() => actionMetadata.definitions);
const metadataNotice = computed(() => {
  if (!actionMetadata.fallbackActive) {
    return props.notice;
  }

  return props.notice
    ?? fallbackLabel(
      "ruleEditor.actions.metadataFallback",
      "Action metadata is unavailable. The editor is using a minimal fallback action list."
    );
});

onMounted(() => {
  void actionMetadata.load();
  void loadRules();
});

async function loadRules(): Promise<void> {
  try {
    availableRules.value = await getRules();
  } catch {
    availableRules.value = [];
  }
}

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

function localizeMeta(key: string, fallback: string): string {
  return te(key) ? t(key) : fallback;
}

function actionLabel(definition: ActionDefinition): string {
  return localizeMeta(`ruleEditor.actionMeta.${definition.type}.label`, definition.label);
}

function actionDescription(definition: ActionDefinition): string {
  return localizeMeta(`ruleEditor.actionMeta.${definition.type}.description`, definition.description);
}

function fieldLabel(actionType: string, field: FieldDefinition): string {
  return localizeMeta(`ruleEditor.actionMeta.${actionType}.params.${field.key}.label`, field.label);
}

function fieldPlaceholder(actionType: string, field: FieldDefinition): string | undefined {
  const localized = localizeMeta(`ruleEditor.actionMeta.${actionType}.params.${field.key}.help`, field.placeholder ?? "");
  return localized.length > 0 ? localized : undefined;
}

function addItem(): void {
  items.value = [...items.value, actionDefinitions.value[0].create()];
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
  return actionMetadata.findDefinition(asString(item.type));
}

function basicFieldsOf(item: JsonRecord): FieldDefinition[] {
  return definitionFor(item)?.fields.filter(f => !f.advanced) ?? [];
}

function advancedFieldsOf(item: JsonRecord): FieldDefinition[] {
  return definitionFor(item)?.fields.filter(f => f.advanced) ?? [];
}

function usesImplicitTriggerUserTarget(item: JsonRecord): boolean {
  if (asString(item.type) !== "triggerCheckIn") {
    return false;
  }

  const userId = asString(item.userId).trim();
  const platform = asString(item.platform).trim();
  const usesDefaultUser = userId.length === 0 || userId === "{Member.UserId}";
  return usesDefaultUser && platform.length === 0;
}

function implicitTargetMessage(item: JsonRecord): string | null {
  if (!usesImplicitTriggerUserTarget(item)) {
    return null;
  }

  return fallbackLabel(
    "ruleEditor.actionMeta.triggerCheckIn.defaultTargetNotice",
    "Checks in the user who triggered this event."
  );
}

function isSubWorkflowIdField(item: JsonRecord, field: FieldDefinition): boolean {
  return asString(item.type) === "invokeSubWorkflow" && field.key === "workflowId";
}

function subWorkflowOptions(item: JsonRecord): Array<{ label: string; value: string }> {
  const options = availableRules.value
    .filter(isSubWorkflowSummary)
    .map(rule => ({
      label: rule.name,
      value: rule.id
    }));

  const currentValue = asString(item.workflowId).trim();
  if (currentValue.length > 0 && !options.some(option => option.value === currentValue)) {
    options.unshift({
      label: fallbackLabel(
        "ruleEditor.actionMeta.invokeSubWorkflow.params.workflowId.unknownOption",
        "Unknown workflow"
      ),
      value: currentValue
    });
  }

  return options;
}

function patchItem(index: number, patch: Partial<JsonRecord>): void {
  const next = items.value.slice();
  next[index] = cleanRecord({ ...next[index], ...patch });
  items.value = next;
  emitItems();
}

function getDefaultOutputVariable(type: string): string | undefined {
  switch (type) {
    case "triggerCheckIn":
      return "CheckIn";
    case "updateCounter":
      return "Counter";
    case "randomPicker":
      return "Picked";
    case "lookupTwitchUser":
      return "TwitchUser";
    case "invokePlugin":
      return "PluginResult";
    case "invokeSubWorkflow":
      return "SubWorkflowResult";
    default:
      return undefined;
  }
}

function onTypeChange(index: number, nextType: string): void {
  const definition = actionMetadata.findDefinition(nextType);
  if (!definition) {
    patchItem(index, { type: nextType });
    return;
  }

  const current = items.value[index] ?? {};
  const nextOutputVariable = (current.outputVariable && String(current.outputVariable).trim().length > 0)
    ? current.outputVariable
    : getDefaultOutputVariable(nextType);

  items.value = items.value.map((item, itemIndex) => {
    if (itemIndex !== index) {
      return item;
    }

    return cleanRecord({
      ...definition.create(),
      executionCondition: current.executionCondition,
      outputVariable: nextOutputVariable,
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
  <StepListShell
    :items="items"
    :title="title"
    :empty-text="emptyText"
    :prefix="prefix"
    :model-value="modelValue"
    :aria-label="title"
    @add="addItem"
    @remove="removeItem"
    @move="moveItem"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <template v-if="metadataNotice" #notice>
      <p class="workflow-builder__notice" role="note">{{ metadataNotice }}</p>
    </template>

    <template #identity="{ item, index }">
      <label class="workflow-builder__identity-field form-field-inline">
        <span class="form-label">{{ fallbackLabel("ruleEditor.steps.type", "Type") }}</span>
        <select
          :data-testid="`${prefix}-type-${index}`"
          :value="asString(item.type)"
          @change="onTypeChange(index, ($event.target as HTMLSelectElement).value)"
        >
          <option v-for="definition in actionDefinitions" :key="definition.type" :value="definition.type">
            {{ actionLabel(definition) }}
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
        {{
          definitionFor(item)
            ? actionDescription(definitionFor(item)!)
            : fallbackLabel(
              "ruleEditor.builder.unknownAction",
              "Unknown action type. Use advanced JSON fallback to edit unsupported fields."
            )
        }}
      </p>
    </template>

    <template #body="{ item, index }">
      <div v-if="definitionFor(item)" class="workflow-builder__grid">
        <p
          v-if="implicitTargetMessage(item)"
          class="workflow-builder__hint workflow-builder__wide"
          :data-testid="`${prefix}-implicit-target-${index}`"
        >
          {{ implicitTargetMessage(item) }}
        </p>
        <template v-for="field in basicFieldsOf(item)" :key="field.key">
          <label v-if="field.kind === 'text' || field.kind === 'number' || field.kind === 'select'" class="form-field">
            <span class="form-label">{{ fieldLabel(asString(item.type), field) }}</span>
            <select
              v-if="isSubWorkflowIdField(item, field)"
              :value="String(fieldValue(item, field))"
              :data-testid="`${prefix}-field-${field.key}-${index}`"
              @change="updateField(index, field, ($event.target as HTMLSelectElement).value)"
            >
              <option value="">
                {{ fallbackLabel("ruleEditor.actionMeta.invokeSubWorkflow.params.workflowId.placeholder", "Select a sub-workflow") }}
              </option>
              <option v-for="option in subWorkflowOptions(item)" :key="option.value" :value="option.value">
                {{ option.label }}
              </option>
            </select>
            <VariableFieldInput
              v-else-if="field.kind === 'text' && field.key !== 'condition'"
              :model-value="String(fieldValue(item, field))"
              :placeholder="fieldPlaceholder(asString(item.type), field)"
              :previous-steps="previousStepsFor(index)"
              :action-definitions="actionDefinitions"
              :filter-key="field.key"
              @update:model-value="updateField(index, field, $event)"
            />
            <ConditionExpressionInput
              v-else-if="field.kind === 'text' && field.key === 'condition'"
              :model-value="String(fieldValue(item, field))"
              :placeholder="fieldPlaceholder(asString(item.type), field)"
              :previous-steps="previousStepsFor(index)"
              :action-definitions="actionDefinitions"
              :data-test-id="`${prefix}-field-${field.key}-${index}`"
              @update:model-value="updateField(index, field, $event)"
            />
            <input
              v-else-if="field.kind === 'number'"
              :type="field.kind === 'number' ? 'number' : 'text'"
              :value="String(fieldValue(item, field))"
              :placeholder="fieldPlaceholder(asString(item.type), field)"
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
            <span>{{ fieldLabel(asString(item.type), field) }}</span>
          </label>

          <label v-else-if="field.kind === 'textarea'" class="form-field workflow-builder__wide">
            <span class="form-label">{{ fieldLabel(asString(item.type), field) }}</span>
            <VariableFieldInput
              :model-value="String(fieldValue(item, field))"
              multiline
              :rows="4"
              :placeholder="fieldPlaceholder(asString(item.type), field)"
              :previous-steps="previousStepsFor(index)"
              :action-definitions="actionDefinitions"
              :filter-key="field.key"
              @update:model-value="updateField(index, field, $event)"
            />
          </label>

          <label v-else-if="field.kind === 'string-map' || field.kind === 'json-object' || field.kind === 'string-list' || field.kind === 'number-list'" class="form-field workflow-builder__wide">
            <span class="form-label">{{ fieldLabel(asString(item.type), field) }}</span>
            <textarea
              :rows="3"
              :value="String(fieldValue(item, field))"
              :placeholder="fieldPlaceholder(asString(item.type), field)"
              @input="updateField(index, field, ($event.target as HTMLTextAreaElement).value)"
            />
          </label>
        </template>

        <details
          v-if="advancedFieldsOf(item).length > 0"
          class="workflow-builder__advanced workflow-builder__wide"
          :data-testid="`${prefix}-advanced-${index}`"
        >
          <summary>{{ fallbackLabel("ruleEditor.advancedOptions", "Advanced options") }}</summary>
          <div class="workflow-builder__advanced-grid">
            <template v-for="field in advancedFieldsOf(item)" :key="field.key">
              <label v-if="field.kind === 'text' || field.kind === 'number' || field.kind === 'select'" class="form-field">
                <span class="form-label">{{ fieldLabel(asString(item.type), field) }}</span>
                <VariableFieldInput
                  v-if="field.kind === 'text' && field.key !== 'condition'"
                  :model-value="String(fieldValue(item, field))"
                  :placeholder="fieldPlaceholder(asString(item.type), field)"
                  :previous-steps="previousStepsFor(index)"
                  :action-definitions="actionDefinitions"
                  :filter-key="field.key"
                  @update:model-value="updateField(index, field, $event)"
                />
                <input
                  v-else-if="field.kind === 'number'"
                  type="number"
                  :value="String(fieldValue(item, field))"
                  :placeholder="fieldPlaceholder(asString(item.type), field)"
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
                <span>{{ fieldLabel(asString(item.type), field) }}</span>
              </label>
            </template>
          </div>
        </details>

        <div class="workflow-builder__meta workflow-builder__wide">
          <label class="form-field workflow-builder__meta-condition">
            <span class="form-label">{{ fallbackLabel("ruleEditor.executionCondition", "Execution condition") }}</span>
            <ConditionExpressionInput
              :model-value="asString(item.executionCondition)"
              :previous-steps="previousStepsFor(index)"
              :action-definitions="actionDefinitions"
              placeholder="Trigger.MessageText == '!go'"
              :data-test-id="`${prefix}-execution-${index}`"
              @update:model-value="patchItem(index, { executionCondition: $event })"
            />
          </label>

          <label class="form-field workflow-builder__meta-output">
            <span class="form-label">{{ fallbackLabel("ruleEditor.outputVariable", "Output variable") }}</span>
            <input
              :value="asString(item.outputVariable)"
              :placeholder="fallbackLabel('ruleEditor.outputVariable.placeholder', 'e.g. CheckIn (referenced via {Step.CheckIn.Status})')"
              @input="patchItem(index, { outputVariable: ($event.target as HTMLInputElement).value })"
            />
          </label>
        </div>
      </div>

      <div v-else class="workflow-builder__unknown">
        {{
          fallbackLabel(
            "ruleEditor.builder.unknownAction",
            "Unknown action type. Use advanced JSON fallback to edit unsupported fields."
          )
        }}
      </div>
    </template>
  </StepListShell>
</template>

<style scoped>
.workflow-builder__summary,
.workflow-builder__unknown,
.workflow-builder__notice {
  margin: 4px 0 0;
  color: var(--vp-text-muted);
  font-size: 13px;
}

.workflow-builder__notice {
  padding: 8px 12px;
  background: var(--vp-bg-warning);
  border: 1px solid var(--vp-border-warning);
  border-radius: 6px;
  color: var(--vp-text-warning);
}

.workflow-builder__grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 200px), 1fr));
}

.workflow-builder__wide {
  grid-column: 1 / -1;
}

.workflow-builder__meta {
  display: grid;
  gap: 12px;
  grid-template-columns: 1fr;
  align-items: start;
}

.workflow-builder__advanced {
  border: 1px dashed var(--vp-border-default);
  border-radius: var(--vp-radius-card);
  padding: 8px 12px;
}

.workflow-builder__advanced summary {
  cursor: pointer;
  color: var(--vp-text-muted);
  font-size: 12px;
  font-weight: 600;
  user-select: none;
}

.workflow-builder__advanced[open] summary {
  margin-bottom: 8px;
  color: var(--vp-text-secondary);
}

.workflow-builder__advanced-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 200px), 1fr));
}

.workflow-builder__meta-condition {
  min-width: 0;
}

.workflow-builder__meta-output {
  min-width: 0;
  max-width: 320px;
}

textarea {
  border: 1px solid var(--vp-border-default);
  border-radius: 6px;
  padding: 10px 12px;
  resize: vertical;
}

.workflow-builder__identity-field {
  margin-bottom: 0;
  white-space: nowrap;
}

.workflow-builder__identity-field select {
  min-width: 180px;
  max-width: 320px;
}

@media (max-width: 720px) {
  .workflow-builder__meta {
    grid-template-columns: 1fr;
  }
}
</style>
