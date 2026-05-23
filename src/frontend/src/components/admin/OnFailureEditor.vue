<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import WorkflowActionsEditor from "@/components/admin/WorkflowActionsEditor.vue";

const props = defineProps<{ modelValue: string }>();
const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();
const { t, te } = useI18n();

function fallbackLabel(key: string, fallback: string): string {
  return te(key) ? t(key) : fallback;
}

const title = computed(() => t("ruleEditor.onFailure.title"));
const emptyText = computed(() =>
  fallbackLabel(
    "ruleEditor.onFailure.empty",
    "No failure steps configured. Add steps to recover from main pipeline failures."
  )
);
const notice = computed(() =>
  fallbackLabel(
    "ruleEditor.onFailure.notice",
    "OnFailure steps cannot define a nested onFailure handler."
  )
);
</script>

<template>
  <WorkflowActionsEditor
    :model-value="props.modelValue"
    :title="title"
    :empty-text="emptyText"
    :notice="notice"
    test-id-prefix="on-failure-actions"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>
