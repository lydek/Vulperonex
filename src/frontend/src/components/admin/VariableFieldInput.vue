<script setup lang="ts">
import { nextTick, ref } from "vue";
import VariablePicker from "@/components/admin/VariablePicker.vue";
import type { JsonRecord } from "@/components/admin/workflowEditor";

const props = defineProps<{
  modelValue: string;
  placeholder?: string;
  multiline?: boolean;
  rows?: number;
  previousSteps?: JsonRecord[];
  previousStepsJson?: string;
  allowedTriggerVariables?: string[];
  dataTestId?: string;
}>();

const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();

const textRef = ref<HTMLInputElement | HTMLTextAreaElement | null>(null);

function updateValue(value: string): void {
  emit("update:modelValue", value);
}

async function insertVariable(variable: string): Promise<void> {
  const element = textRef.value;
  const current = props.modelValue;
  if (!element) {
    updateValue(`${current}${variable}`);
    return;
  }

  const start = element.selectionStart ?? current.length;
  const end = element.selectionEnd ?? start;
  const nextValue = `${current.slice(0, start)}${variable}${current.slice(end)}`;
  updateValue(nextValue);

  await nextTick();
  const nextCursor = start + variable.length;
  textRef.value?.focus();
  textRef.value?.setSelectionRange(nextCursor, nextCursor);
}
</script>

<template>
  <div class="variable-field">
    <input
      v-if="!multiline"
      :data-testid="dataTestId"
      ref="textRef"
      :value="modelValue"
      type="text"
      :placeholder="placeholder"
      @input="updateValue(($event.target as HTMLInputElement).value)"
    />
    <textarea
      v-else
      :data-testid="dataTestId"
      ref="textRef"
      :value="modelValue"
      :rows="rows ?? 4"
      :placeholder="placeholder"
      @input="updateValue(($event.target as HTMLTextAreaElement).value)"
    />
    <VariablePicker
      class="variable-field__picker"
      :previous-steps="previousSteps"
      :previous-steps-json="previousStepsJson"
      :allowed-trigger-variables="allowedTriggerVariables"
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

.variable-field :deep(input),
.variable-field :deep(textarea) {
  width: 100%;
  box-sizing: border-box;
  padding-right: 78px;
}

.variable-field :deep(textarea) {
  min-height: 88px;
  border: 1px solid #d6dde5;
  border-radius: 6px;
  padding: 10px 78px 10px 12px;
  resize: vertical;
  background: #ffffff;
}

.variable-field :deep(textarea) + .variable-field__picker,
.variable-field__picker:has(+ textarea) {
  top: 8px;
}
</style>
