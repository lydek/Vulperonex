<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const roleOptions = [
  { labelKey: "ruleEditor.roles.everyone", value: "Everyone" },
  { labelKey: "ruleEditor.roles.broadcaster", value: "Broadcaster" },
  { labelKey: "ruleEditor.roles.moderator", value: "Moderator" },
  { labelKey: "ruleEditor.roles.subscriber", value: "Subscriber" },
  { labelKey: "ruleEditor.roles.vip", value: "Vip" }
] as const;

const props = defineProps<{
  modelValue: string[];
}>();

const emit = defineEmits<{ (event: "update:modelValue", value: string[]): void }>();

const { t } = useI18n();

const selected = computed(() => new Set(props.modelValue));

function toggleRole(role: string): void {
  if (role === "Everyone") {
    emit("update:modelValue", []);
    return;
  }

  const next = new Set(props.modelValue);
  if (next.has(role)) {
    next.delete(role);
  } else {
    next.add(role);
  }
  emit("update:modelValue", roleOptions
    .map(option => option.value)
    .filter(roleValue => roleValue !== "Everyone" && next.has(roleValue)));
}
</script>

<template>
  <section class="status-card role-chip-selector" aria-labelledby="role-chip-selector-title">
    <div>
      <h2 id="role-chip-selector-title" class="section-title">
        {{ t("ruleEditor.roles.title") }}
      </h2>
      <p class="status-label">{{ t("ruleEditor.roles.subtitle") }}</p>
    </div>
    <div class="role-chip-selector__chips" data-testid="role-chip-selector">
      <button
        v-for="option in roleOptions"
        :key="option.value"
        type="button"
        class="role-chip-selector__chip"
        :class="{ 'is-selected': option.value === 'Everyone' ? modelValue.length === 0 : selected.has(option.value) }"
        :aria-pressed="option.value === 'Everyone' ? modelValue.length === 0 : selected.has(option.value)"
        :data-testid="`role-chip-${option.value}`"
        @click="toggleRole(option.value)"
      >
        {{ t(option.labelKey) }}
      </button>
    </div>
  </section>
</template>

<style scoped>
.role-chip-selector {
  display: grid;
  gap: 10px;
}

.role-chip-selector__chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.role-chip-selector__chip {
  border: 1px solid var(--vp-border-default);
  border-radius: 999px;
  background: var(--vp-bg-surface);
  color: var(--vp-text-secondary);
  cursor: pointer;
  font-size: 13px;
  font-weight: 700;
  padding: 7px 12px;
}

.role-chip-selector__chip.is-selected {
  border-color: var(--vp-accent);
  background: var(--vp-bg-selected);
  color: var(--vp-text-accent);
}
</style>
