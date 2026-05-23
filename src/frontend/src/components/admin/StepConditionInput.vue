<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{ modelValue: string }>();
const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();
const { t } = useI18n();

const actions = computed(() => {
  try {
    const parsed = JSON.parse(props.modelValue) as unknown;
    return Array.isArray(parsed) ? parsed as Array<Record<string, unknown>> : [];
  } catch {
    return [];
  }
});

function patch(index: number, key: "executionCondition" | "outputVariable", value: string): void {
  const next = actions.value.map((action) => ({ ...action }));
  if (value.trim().length === 0) {
    delete next[index][key];
  } else {
    next[index][key] = value;
  }
  emit("update:modelValue", JSON.stringify(next, null, 2));
}
</script>

<template>
  <section class="status-card" aria-labelledby="step-condition-title">
    <h2 id="step-condition-title" class="section-title">{{ t("ruleEditor.steps.title") }}</h2>
    <p v-if="actions.length === 0" role="status">{{ t("ruleEditor.steps.empty") }}</p>
    <table v-else class="monitor-table" data-testid="step-condition-table">
      <thead>
        <tr>
          <th scope="col">{{ t("ruleEditor.steps.type") }}</th>
          <th scope="col">{{ t("ruleEditor.executionCondition") }}</th>
          <th scope="col">{{ t("ruleEditor.outputVariable") }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(action, index) in actions" :key="index">
          <td class="monitor-mono">{{ action.type }}</td>
          <td>
            <input
              :value="String(action.executionCondition ?? '')"
              :aria-label="t('ruleEditor.executionCondition')"
              @input="patch(index, 'executionCondition', ($event.target as HTMLInputElement).value)"
            />
          </td>
          <td>
            <input
              :value="String(action.outputVariable ?? '')"
              :aria-label="t('ruleEditor.outputVariable')"
              @input="patch(index, 'outputVariable', ($event.target as HTMLInputElement).value)"
            />
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
