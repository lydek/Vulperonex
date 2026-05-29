<script setup lang="ts">
import { computed, ref, watch } from "vue";
import VariablePicker from "@/components/admin/VariablePicker.vue";
import {
  asString,
  getOperatorOptions,
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
const valueOptions = computed(() => getVariableInfo(leftVar.value)?.options ?? []);

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
}

.condition-expression__mode {
  display: flex;
  gap: 6px;
  justify-content: flex-end;
}

.condition-expression__mode-button {
  padding: 4px 10px;
  border: 1px solid #d6dde5;
  border-radius: 999px;
  background: #ffffff;
  color: #5f6f80;
  font-size: 11px;
  font-weight: 600;
}

.condition-expression__mode-button.is-active {
  background: #e7f1ef;
  color: #164f48;
  border-color: #1f6f64;
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
  border: 1px solid #d6dde5;
  border-radius: 6px;
  padding: 8px 10px;
  background: #ffffff;
}

.condition-expression__field :deep(input[readonly]) {
  padding-right: 78px;
  color: #394756;
  background: #fdfefe;
}

.condition-expression__field :deep(.variable-picker) {
  position: absolute;
  top: 7px;
  right: 8px;
}

@media (max-width: 720px) {
  .condition-expression__builder {
    grid-template-columns: 1fr;
  }
}
</style>
