<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
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

const { t } = useI18n();

const mode = ref<"visual" | "raw">("visual");
const leftVar = ref("");
const operator = ref("==");
const rightValue = ref("");

const previousSteps = computed(() => props.previousSteps ?? parseArrayModel(props.previousStepsJson ?? "[]"));
const operatorOptions = computed(() => getOperatorOptions(leftVar.value));
const valueOptions = computed(() => {
  const contextual = getStepStatusOptions(leftVar.value, previousSteps.value, props.actionDefinitions);
  return contextual.length > 0 ? contextual : (getVariableInfo(leftVar.value)?.options ?? []);
});
const modeHint = computed(() =>
  getStepStatusModeHint(leftVar.value, previousSteps.value, props.actionDefinitions)
);

watch(valueOptions, (options, previousOptions) => {
  if (mode.value !== "visual") {
    return;
  }

  if (options.length === 0) {
    if ((previousOptions?.length ?? 0) > 0 && rightValue.value.length > 0) {
      rightValue.value = "";
    }
    return;
  }

  if (!options.includes(rightValue.value)) {
    rightValue.value = options[0];
  }
}, { immediate: true });

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

    <div v-if="mode === 'visual'" class="condition-expression__visual">
      <div class="condition-expression__labels" aria-hidden="true">
        <span>{{ t("condition.variableLabel") }}</span>
        <span>{{ t("condition.operatorLabel") }}</span>
        <span>{{ t("condition.valueLabel") }}</span>
      </div>
      <div class="condition-expression__builder">
        <div class="condition-expression__field condition-expression__operand">
          <span
            v-if="leftVar"
            class="condition-token"
          >
            <span
              class="condition-token__label"
              :data-testid="dataTestId ? `${dataTestId}-left-selected` : undefined"
            >{{ leftVar }}</span>
            <button
              type="button"
              class="condition-token__clear"
              :data-testid="dataTestId ? `${dataTestId}-left-clear` : undefined"
              :aria-label="`Clear ${leftVar}`"
              @click="leftVar = ''"
            >×</button>
          </span>
          <span v-else class="condition-expression__placeholder">{{ t("condition.variableHint") }}</span>
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
          <template v-else>
            <input
              :data-testid="dataTestId ? `${dataTestId}-right` : undefined"
              v-model="rightValue"
              type="text"
              class="condition-expression__value-input"
              :placeholder="t('condition.valueTextHint')"
            />
          </template>
          <div v-if="modeHint" class="condition-expression__hint">{{ modeHint }}</div>
        </div>
      </div>
    </div>

    <input
      v-else
      :data-testid="dataTestId"
      :value="modelValue"
      type="text"
      :placeholder="placeholder ?? 'Trigger.MessageText == \'!go\''"
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

.condition-expression__visual {
  display: grid;
  gap: 4px;
}

.condition-expression__labels {
  display: grid;
  gap: 8px;
  grid-template-columns: minmax(190px, 1.35fr) minmax(120px, 0.7fr) minmax(150px, 1fr);
}

.condition-expression__labels span {
  font-size: 11px;
  font-weight: 600;
  color: var(--vp-text-muted);
}

.condition-expression__builder {
  display: grid;
  gap: 8px;
  grid-template-columns: minmax(190px, 1.35fr) minmax(120px, 0.7fr) minmax(150px, 1fr);
  align-items: start;
}

.condition-expression__field :deep(.condition-expression__value-input) {
  padding-right: 44px;
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

/* Left operand slot: holds either a removable variable chip or a placeholder. */
.condition-expression__operand {
  display: flex;
  align-items: center;
  gap: 6px;
  min-height: 38px;
  padding: 6px 44px 6px 10px;
  border: 1px solid var(--vp-border-default);
  border-radius: 6px;
  background: var(--vp-bg-surface-subtle);
}

.condition-expression__placeholder {
  color: var(--vp-text-muted);
  font-family: Consolas, "Courier New", monospace;
  font-size: 13px;
}

.condition-token {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 1px 4px 1px 8px;
  border: 1px solid var(--vp-accent);
  border-radius: 999px;
  background: var(--vp-bg-selected);
  color: var(--vp-text-accent);
  font-family: Consolas, "Courier New", monospace;
  font-size: 12px;
  max-width: 100%;
}

.condition-token__label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.condition-token__clear {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: none;
  width: 16px;
  height: 16px;
  padding: 0;
  border: 0;
  border-radius: 999px;
  background: transparent;
  color: var(--vp-text-accent);
  font-size: 14px;
  line-height: 1;
  cursor: pointer;
}

.condition-token__clear:hover {
  background: var(--vp-bg-surface-muted);
}

@media (max-width: 720px) {
  .condition-expression__builder,
  .condition-expression__labels {
    grid-template-columns: 1fr;
  }
}
</style>
