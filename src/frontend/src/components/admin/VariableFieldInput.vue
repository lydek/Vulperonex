<script setup lang="ts">
import { ref } from "vue";
import VariablePicker from "@/components/admin/VariablePicker.vue";
import VariableTokenInput, { type VariableTokenInputExpose } from "@/components/admin/VariableTokenInput.vue";
import type { ActionDefinition, JsonRecord } from "@/components/admin/workflowEditor";

const props = defineProps<{
  modelValue: string;
  placeholder?: string;
  multiline?: boolean;
  rows?: number;
  previousSteps?: JsonRecord[];
  previousStepsJson?: string;
  allowedTriggerVariables?: string[];
  actionDefinitions?: ActionDefinition[];
  dataTestId?: string;
  filterKey?: string;
}>();

const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();

const tokenInput = ref<VariableTokenInputExpose | null>(null);

function updateValue(value: string): void {
  emit("update:modelValue", value);
}

function insertVariable(variable: string): void {
  tokenInput.value?.insertToken(variable);
}
</script>

<template>
  <div class="variable-field">
    <VariableTokenInput
      ref="tokenInput"
      :model-value="modelValue"
      :placeholder="placeholder"
      :multiline="multiline"
      :data-test-id="dataTestId"
      @update:model-value="updateValue"
    />
    <VariablePicker
      class="variable-field__picker"
      :previous-steps="previousSteps"
      :previous-steps-json="previousStepsJson"
      :allowed-trigger-variables="allowedTriggerVariables"
      :action-definitions="actionDefinitions"
      :filter-key="filterKey"
      @select="insertVariable"
    />
  </div>
</template>

<style scoped>
.variable-field {
  position: relative;
  display: block;
}

.variable-field__picker {
  position: absolute;
  top: 7px;
  right: 8px;
}
</style>
