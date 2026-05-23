<script setup lang="ts">
import { useI18n } from "vue-i18n";
import RuleJsonEditor from "@/components/admin/RuleJsonEditor.vue";

defineProps<{ modelValue: string }>();
const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();
const { t } = useI18n();
</script>

<template>
  <section class="status-card" aria-labelledby="on-failure-editor-title">
    <h2 id="on-failure-editor-title" class="section-title">{{ t("ruleEditor.onFailure.title") }}</h2>
    <RuleJsonEditor
      :model-value="modelValue"
      :aria-label="t('ruleEditor.onFailure.title')"
      @update:model-value="emit('update:modelValue', $event)"
    />
  </section>
</template>
