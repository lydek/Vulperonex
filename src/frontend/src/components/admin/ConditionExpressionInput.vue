<script setup lang="ts">
import { computed, ref, watch } from "vue";
import VariablePicker from "@/components/admin/VariablePicker.vue";
import {
  asString,
  getOperatorOptions,
  getStepStatusModeHint,
  getStepStatusOptions,
  getVariableInfo,
  parseArrayModel,
  type ActionDefinition,
  type JsonRecord
} from "@/components/admin/workflowEditor";

const props = defineProps<{
  modelValue: string;
  placeholder?: string;
  previousSteps?: JsonRecord[];
  previousStepsJson?: string;
  allowedTriggerVariables?: string[];
  actionDefinitions?: ActionDefinition[];
  dataTestId?: string;
}>();

const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();

const mode = ref<"visual" | "raw">("visual");
const leftVar = ref("");
const operator = ref("==");
const rightValue = ref("");

const previousSteps = computed(() => props.previousSteps ?? parseArrayModel(props.previousStepsJson ?? "[]"));
const operatorOptions = computed(() => getOperatorOptions(leftVar.value));
const valueOptions = computed(() => {
  const contextual = getStepStatusOptions(leftVar.value);
  return contextual.length > 0 ? contextual : (getVariableInfo(leftVar.value)?.options ?? []);
});
const modeHint = computed(() =>
  getStepStatusModeHint(leftVar.value, previousSteps.value, props.actionDefinitions)
);

watch(() => props.modelValue, (value) => {
  if (!tryParseExpression(value)) {
    mode.value = "raw";
  }
}, { immediate: true });

watch([leftVar, operator, rightValue, mode], () => {
  if (mode.value !== "visual") {
    return;
  }
  emit("update:modelValue", buildExpression());
});

function buildExpression(): string {
  const variable = asString(leftVar.value).trim();
  if (variable.length === 0) {
    return "";
  }

  const variableInfo = getVariableInfo(variable);
  const normalizedRight = rightValue.value.trim();
  if (normalizedRight.length === 0) {
    return variable;
  }

  if (variableInfo?.type === "number") {
    return `${variable} ${operator.value} ${normalizedRight}`;
  }

  const lower = normalizedRight.toLowerCase();
  if (lower === "true" || lower === "false") {
    return `${variable} ${operator.value} ${lower}`;
  }

  return `${variable} ${operator.value} '${normalizedRight.replace(/'/g, "\\'")}'`;
}

function tryParseExpression(expression: string): boolean {
  const trimmed = expression.trim();
  if (trimmed.length === 0) {
    leftVar.value = "";
    operator.value = "==";
    rightValue.value = "";
    mode.value = "visual";
    return true;
  }

  const match = trimmed.match(/^((?:Trigger|Member|Args|Step|Failure)\.[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)*)(?:\s*(==|!=|>=|<=|>|<|contains)\s*(.*))?$/i);
  if (!match) {
    return false;
  }

  leftVar.value = match[1];
  operator.value = match[2] ?? "==";
  rightValue.value = (match[3] ?? "").trim().replace(/^'(.*)'$/s, "$1").replace(/^"(.*)"$/s, "$1");
  mode.value = "visual";
  return true;
}

function switchMode(nextMode: "visual" | "raw"): void {
  mode.value = nextMode;
  if (nextMode === "visual") {
    tryParseExpression(props.modelValue);
    emit("update:modelValue", buildExpression());
  }
}

function emitRaw(value: string): void {
  emit("update:modelValue", value);
}
</script>

<template>
  <div class="condition-expression">
    <div class="condition-expression__mode">
      <button
        type="button"
        class="condition-expression__mode-button"
        :class="{ 'is-active': mode === 'visual' }"
        :data-testid="dataTestId ? `${dataTestId}-visual-toggle` : undefined"
        @click="switchMode('visual')"
      >
        Visual
      </button>
      <button
        type="button"
        class="condition-expression__mode-button"
        :class="{ 'is-active': mode === 'raw' }"
        :data-testid="dataTestId ? `${dataTestId}-raw-toggle` : undefined"
        @click="switchMode('raw')"
      >
        Raw
      </button>
    </div>

    <div v-if="mode === 'visual'" class="condition-expression__builder">
      <div class="condition-expression__field">
        <input
          :data-testid="dataTestId ? `${dataTestId}-left` : undefined"
          :value="leftVar"
          type="text"
          placeholder="Trigger.MessageText"
          readonly
        />
        <VariablePicker
          :previous-steps="previousSteps"
          :allowed-trigger-variables="allowedTriggerVariables"
          :action-definitions="actionDefinitions"
          expression-mode
          @select="leftVar = $event"
        />
      </div>
      <select v-model="operator" :data-testid="dataTestId ? `${dataTestId}-operator` : undefined">
        <option v-for="option in operatorOptions" :key="option.value" :value="option.value">
          {{ option.label }}
        </option>
      </select>
      <div class="condition-expression__field">
        <select v-if="valueOptions.length > 0" v-model="rightValue" :data-testid="dataTestId ? `${dataTestId}-right` : undefined">
          <option v-for="option in valueOptions" :key="option" :value="option">
            {{ option }}
          </option>
        </select>
        <input
          v-else
          :data-testid="dataTestId ? `${dataTestId}-right` : undefined"
          v-model="rightValue"
          type="text"
          :placeholder="placeholder ?? 'value'"
        />
        <div v-if="modeHint" class="condition-expression__hint">{{ modeHint }}</div>
      </div>
    </div>

    <input
      v-else
      :data-testid="dataTestId"
      :value="modelValue"
      type="text"
      :placeholder="placeholder"
      @input="emitRaw(($event.target as HTMLInputElement).value)"
    />
  </div>
</template>

<style scoped>
.condition-expression {
  display: grid;
  gap: 8px;
  position: relative;
}

.condition-expression__mode {
  position: absolute;
  top: -28px;
  right: 0;
  display: flex;
  gap: 6px;
  justify-content: flex-end;
}

.condition-expression__mode-button {
  padding: 4px 10px;
  border: 1px solid var(--vp-border-default);
  border-radius: 999px;
  background: var(--vp-bg-surface);
  color: var(--vp-text-muted);
  font-size: 11px;
  font-weight: 600;
}

.condition-expression__mode-button.is-active {
  background: var(--vp-bg-selected);
  color: var(--vp-text-accent);
  border-color: var(--vp-accent);
}

.condition-expression__builder {
  display: grid;
  gap: 8px;
  grid-template-columns: minmax(0, 1.5fr) minmax(120px, 0.8fr) minmax(0, 1.2fr);
}

.condition-expression__field {
  position: relative;
  min-width: 0;
}

.condition-expression :deep(input),
.condition-expression :deep(select) {
  width: 100%;
  min-width: 0;
  box-sizing: border-box;
  border: 1px solid var(--vp-border-default);
  border-radius: 6px;
  padding: 8px 10px;
  background: var(--vp-bg-surface);
  color: var(--vp-text-primary);
}

.condition-expression__field :deep(input[readonly]) {
  padding-right: 78px;
  color: var(--vp-text-secondary);
  background: var(--vp-bg-surface-subtle);
}

.condition-expression__field :deep(.variable-picker) {
  position: absolute;
  top: 7px;
  right: 8px;
}

.condition-expression__hint {
  margin-top: 6px;
  font-size: 11px;
  color: var(--vp-text-muted);
}

@media (max-width: 720px) {
  .condition-expression__builder {
    grid-template-columns: 1fr;
  }
}
</style>
