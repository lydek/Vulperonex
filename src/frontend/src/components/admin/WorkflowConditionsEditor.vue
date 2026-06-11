<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import StepListShell from "@/components/admin/StepListShell.vue";
import VariableFieldInput from "@/components/admin/VariableFieldInput.vue";
import { detectLegacyRoleExpressions } from "@/lib/legacyRoleExpressionDetector";
import {
  asBoolean,
  asNumber,
  asString,
  conditionDefinitions,
  findConditionDefinition,
  parseArrayModel,
  roleCheckboxState,
  stringifyArrayModel,
  updateRoleSelection,
  type ConditionDefinition,
  type FieldDefinition,
  type JsonRecord
} from "@/components/admin/workflowEditor";

const props = defineProps<{
  modelValue: string;
  title: string;
  emptyText: string;
  testIdPrefix?: string;
  matchCondition?: string;
}>();

const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();
const { t, te } = useI18n();

const items = ref<JsonRecord[]>([]);
const lastSerialized = ref("");
const showMigrationDialog = ref(false);

const migrationSuggestions = computed(() =>
  detectLegacyRoleExpressions(items.value, props.matchCondition)
);

defineExpose({ focus });

watch(() => props.modelValue, syncFromModel, { immediate: true });

const prefix = computed(() => props.testIdPrefix ?? "workflow-conditions");
const roleChoices = ["Broadcaster", "Subscriber", "Moderator", "Vip", "Follower"];

function focus(): void {
  const firstInput = document.querySelector<HTMLInputElement>(`[data-testid="${prefix.value}-type-0"]`);
  firstInput?.focus();
}

function syncFromModel(modelValue: string): void {
  if (modelValue === lastSerialized.value) {
    return;
  }
  items.value = pinUserRoleFirst(parseArrayModel(modelValue));
}

function pinUserRoleFirst(records: JsonRecord[]): JsonRecord[] {
  const firstRoleIndex = records.findIndex((record) => asString(record.type) === "userRole");
  if (firstRoleIndex <= 0) {
    return records;
  }

  const next = records.slice();
  const [roleCondition] = next.splice(firstRoleIndex, 1);
  next.unshift(roleCondition);
  return next;
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
  items.value = [...items.value, conditionDefinitions[0].create()];
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

function definitionFor(item: JsonRecord): ConditionDefinition | undefined {
  return findConditionDefinition(asString(item.type));
}

function patchItem(index: number, patch: Partial<JsonRecord>): void {
  const next = items.value.slice();
  next[index] = cleanRecord({ ...next[index], ...patch });
  items.value = next;
  emitItems();
}

function onTypeChange(index: number, nextType: string): void {
  const definition = findConditionDefinition(nextType);
  if (!definition) {
    patchItem(index, { type: nextType });
    return;
  }

  items.value = items.value.map((item, itemIndex) => (
    itemIndex === index ? cleanRecord(definition.create()) : item
  ));
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
    default:
      patchItem(index, { [field.key]: String(rawValue) });
  }
}

function updateRole(index: number, role: string, checked: boolean): void {
  const current = items.value[index]?.roles;
  patchItem(index, { roles: updateRoleSelection(current, role, checked) });
}
</script>

<template>
  <div v-if="migrationSuggestions.length > 0" class="workflow-conditions__migration">
    <button
      type="button"
      class="workflow-conditions__migration-chip"
      :data-testid="`${prefix}-migration-chip`"
      @click="showMigrationDialog = true"
    >
      {{ fallbackLabel("ruleEditor.migration.chip", "Convert role expressions to UserRole condition") }}
      ({{ migrationSuggestions.length }})
    </button>
  </div>

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
    <template #identity="{ item, index }">
      <label class="workflow-builder__identity-field form-field-inline">
        <span class="form-label">{{ fallbackLabel("ruleEditor.conditions", "Conditions") }}</span>
        <select
          :data-testid="`${prefix}-type-${index}`"
          :value="asString(item.type)"
          @change="onTypeChange(index, ($event.target as HTMLSelectElement).value)"
        >
          <option v-for="definition in conditionDefinitions" :key="definition.type" :value="definition.type">
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
        {{
          definitionFor(item)?.description
            ?? fallbackLabel(
              "ruleEditor.builder.unknownCondition",
              "Unknown condition type. Use advanced JSON fallback to edit unsupported fields."
            )
        }}
      </p>
    </template>

    <template #body="{ item, index }">
      <div v-if="definitionFor(item)" class="workflow-builder__grid">
        <template v-for="field in definitionFor(item)?.fields" :key="field.key">
          <div v-if="item.type === 'userRole' && field.key === 'roles'" class="form-field workflow-builder__wide">
            <span class="form-label">{{ field.label }}</span>
            <div class="workflow-builder__roles">
              <label v-for="role in roleChoices" :key="role" class="form-field-inline">
                <input
                  type="checkbox"
                  :checked="roleCheckboxState(item.roles, role)"
                  @change="updateRole(index, role, ($event.target as HTMLInputElement).checked)"
                />
                <span>{{ role }}</span>
              </label>
            </div>
            <input
              :value="asString(item.roles)"
              placeholder="Subscriber, Vip"
              @input="patchItem(index, { roles: ($event.target as HTMLInputElement).value })"
            />
          </div>

          <div v-else-if="field.kind === 'text' || field.kind === 'number' || field.kind === 'select'" class="form-field">
            <span class="form-label">{{ field.label }}</span>
            <VariableFieldInput
              v-if="field.kind === 'text'"
              :model-value="String(fieldValue(item, field))"
              :placeholder="field.placeholder"
              :filter-key="field.key"
              @update:model-value="updateField(index, field, $event)"
            />
            <input
              v-else-if="field.kind === 'number'"
              type="number"
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
          </div>

          <label v-else-if="field.kind === 'checkbox'" class="form-field form-field-inline">
            <input
              type="checkbox"
              :checked="Boolean(fieldValue(item, field))"
              @change="updateField(index, field, ($event.target as HTMLInputElement).checked)"
            />
            <span>{{ field.label }}</span>
          </label>
        </template>
      </div>

      <div v-else class="workflow-builder__unknown">
        {{
          fallbackLabel(
            "ruleEditor.builder.unknownCondition",
            "Unknown condition type. Use advanced JSON fallback to edit unsupported fields."
          )
        }}
      </div>
    </template>
  </StepListShell>

  <div
    v-if="showMigrationDialog"
    class="workflow-conditions__dialog-backdrop"
    role="dialog"
    aria-modal="true"
    aria-labelledby="conditions-migration-title"
    :data-testid="`${prefix}-migration-dialog`"
  >
    <div class="workflow-conditions__dialog-card">
      <h2 id="conditions-migration-title" class="workflow-conditions__dialog-title">
        {{ fallbackLabel("ruleEditor.migration.title", "Suggested role migrations") }}
      </h2>
      <p class="workflow-builder__summary">
        {{
          fallbackLabel(
            "ruleEditor.migration.intro",
            "These NCalc expressions can be replaced with a UserRole condition. Apply manually — nothing is changed automatically."
          )
        }}
      </p>
      <ul class="workflow-conditions__migration-list">
        <li v-for="(suggestion, index) in migrationSuggestions" :key="index">
          <code>{{ suggestion.token }}</code>
          <span class="workflow-conditions__migration-source">{{ suggestion.source }}</span>
          <span aria-hidden="true">→</span>
          <code>{{ JSON.stringify(suggestion.replacement) }}</code>
        </li>
      </ul>
      <div class="workflow-conditions__dialog-actions">
        <button
          type="button"
          class="secondary-button"
          :data-testid="`${prefix}-migration-close`"
          @click="showMigrationDialog = false"
        >
          {{ fallbackLabel("ruleEditor.migration.close", "Close") }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.workflow-builder__summary,
.workflow-builder__unknown {
  margin: 4px 0 0;
  color: var(--vp-text-muted);
  font-size: 13px;
}

.workflow-builder__grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
}

.workflow-builder__wide {
  grid-column: 1 / -1;
}

.workflow-builder__roles {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 8px;
}

.workflow-conditions__migration {
  margin-bottom: 10px;
}

.workflow-conditions__migration-chip {
  border: 1px solid var(--vp-border-warning);
  border-radius: 999px;
  background: var(--vp-bg-warning);
  color: var(--vp-text-warning);
  cursor: pointer;
  font-size: 13px;
  font-weight: 700;
  padding: 7px 12px;
}

.workflow-conditions__migration-chip:hover {
  filter: brightness(0.97);
}

.workflow-conditions__dialog-backdrop {
  position: fixed;
  inset: 0;
  display: grid;
  place-items: center;
  background: var(--vp-bg-backdrop);
  z-index: 50;
}

.workflow-conditions__dialog-card {
  background: var(--vp-bg-surface);
  border-radius: 12px;
  padding: 20px;
  max-width: 560px;
  width: calc(100% - 32px);
  box-shadow: var(--vp-shadow-elevated);
}

.workflow-conditions__dialog-title {
  margin: 0 0 8px;
  font-size: 17px;
}

.workflow-conditions__migration-list {
  margin: 12px 0;
  padding-left: 18px;
  display: grid;
  gap: 8px;
  font-size: 13px;
}

.workflow-conditions__migration-source {
  color: var(--vp-text-muted);
  margin: 0 6px;
}

.workflow-conditions__dialog-actions {
  display: flex;
  justify-content: flex-end;
}

.workflow-builder__identity-field {
  margin-bottom: 0;
  white-space: nowrap;
}

.workflow-builder__identity-field select {
  min-width: 180px;
  max-width: 320px;
}
</style>
