<script setup lang="ts">
import { useI18n } from "vue-i18n";
import type { WorkflowThrottlePolicy } from "@/api/client";

const props = defineProps<{ modelValue: WorkflowThrottlePolicy }>();
const emit = defineEmits<{ (event: "update:modelValue", value: WorkflowThrottlePolicy): void }>();
const { t } = useI18n();

function patch(value: Partial<WorkflowThrottlePolicy>): void {
  emit("update:modelValue", { ...props.modelValue, ...value });
}
</script>

<template>
  <section class="status-card" aria-labelledby="throttle-editor-title">
    <h2 id="throttle-editor-title" class="section-title">{{ t("ruleEditor.throttle.title") }}</h2>
    <div class="compact-grid">
      <label class="form-field">
        <span class="form-label">{{ t("ruleEditor.throttle.maxConcurrent") }}</span>
        <input
          :value="modelValue.maxConcurrent"
          type="number"
          min="0"
          max="64"
          @input="patch({ maxConcurrent: Number(($event.target as HTMLInputElement).value) })"
        />
      </label>
      <label class="form-field">
        <span class="form-label">{{ t("ruleEditor.throttle.cooldownSeconds") }}</span>
        <input
          :value="modelValue.cooldownSeconds"
          type="number"
          min="0"
          max="86400"
          @input="patch({ cooldownSeconds: Number(($event.target as HTMLInputElement).value) })"
        />
      </label>
      <label class="form-field form-field-inline">
        <input
          :checked="modelValue.perUserCooldown"
          type="checkbox"
          @change="patch({ perUserCooldown: ($event.target as HTMLInputElement).checked })"
        />
        <span>{{ t("ruleEditor.throttle.perUserCooldown") }}</span>
      </label>
      <label class="form-field">
        <span class="form-label">{{ t("ruleEditor.throttle.perUserCooldownSeconds") }}</span>
        <input
          :value="modelValue.perUserCooldownSeconds"
          type="number"
          min="0"
          max="86400"
          @input="patch({ perUserCooldownSeconds: Number(($event.target as HTMLInputElement).value) })"
        />
      </label>
    </div>
  </section>
</template>
