<script setup lang="ts">
import { computed, onMounted, watch } from "vue";
import { useI18n } from "vue-i18n";
import ConditionExpressionInput from "@/components/admin/ConditionExpressionInput.vue";
import EventTypeKeyDropdown from "@/components/admin/EventTypeKeyDropdown.vue";
import { useTriggerMetadataStore } from "@/stores/triggerMetadata";
import { useTwitchRewardsStore } from "@/stores/twitchRewards";
import type { TriggerFilterFieldMetadata } from "@/api/client";

const TWITCH_REWARDS_SOURCE = "twitch.rewards";

const props = defineProps<{
  eventTypeKey: string;
  filter: Record<string, string>;
  matchCondition: string;
}>();

const emit = defineEmits<{
  (event: "update:eventTypeKey", value: string): void;
  (event: "update:filter", value: Record<string, string>): void;
  (event: "update:matchCondition", value: string): void;
}>();

const { t } = useI18n();
const triggerMetadata = useTriggerMetadataStore();
const twitchRewards = useTwitchRewardsStore();

const filterFields = computed(() => triggerMetadata.fieldsFor(props.eventTypeKey));
const validVariables = computed(() => triggerMetadata.variablesFor(props.eventTypeKey));
const shouldPruneFilter = computed(() => triggerMetadata.hasMetadataFor(props.eventTypeKey));
const hasTwitchRewardField = computed(() =>
  filterFields.value.some(field => field.optionsSource === TWITCH_REWARDS_SOURCE)
);

const lastRewardRefreshLabel = computed(() => {
  const value = twitchRewards.lastRefreshedAt;
  if (!value) return "";
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: "short",
      timeStyle: "short"
    }).format(new Date(value));
  } catch {
    return value;
  }
});

onMounted(() => {
  void triggerMetadata.load();
});

watch(hasTwitchRewardField, has => {
  if (has) {
    void twitchRewards.load();
  }
}, { immediate: true });

async function refreshTwitchRewardsList(): Promise<void> {
  await twitchRewards.refresh();
}

watch([filterFields, () => props.filter], pruneUnknownFilterFields, { deep: true });

function updateEventTypeKey(value: string): void {
  emit("update:eventTypeKey", value);
}

function updateFilterField(field: TriggerFilterFieldMetadata, value: string): void {
  const normalized = field.type === "number" ? value.replace(/[^\d]/g, "") : value;
  const next = pruneFilter(props.filter, filterFields.value);
  if (normalized.trim().length === 0) {
    delete next[field.key];
  } else {
    next[field.key] = normalized;
  }
  emit("update:filter", next);
}

function blockNonIntegerKey(event: KeyboardEvent): void {
  if (["e", "E", "+", "-", ".", ","].includes(event.key)) {
    event.preventDefault();
  }
}

function pruneUnknownFilterFields(): void {
  if (!shouldPruneFilter.value) {
    return;
  }

  const next = pruneFilter(props.filter, filterFields.value);
  if (!isSameFilter(next, props.filter)) {
    emit("update:filter", next);
  }
}

function pruneFilter(
  filter: Record<string, string>,
  fields: TriggerFilterFieldMetadata[]
): Record<string, string> {
  const allowedKeys = new Set(fields.map(field => field.key));
  const next: Record<string, string> = {};
  for (const [key, value] of Object.entries(filter)) {
    if (allowedKeys.has(key)) {
      next[key] = value;
    }
  }
  return next;
}

function isSameFilter(left: Record<string, string>, right: Record<string, string>): boolean {
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  if (leftKeys.length !== rightKeys.length) {
    return false;
  }

  return leftKeys.every(key => left[key] === right[key]);
}

function fieldInputType(field: TriggerFilterFieldMetadata): string {
  return field.type === "number" ? "number" : "text";
}
</script>

<template>
  <section class="status-card" aria-labelledby="trigger-editor-title">
    <h2 id="trigger-editor-title" class="section-title">{{ t("ruleEditor.trigger.title") }}</h2>
    <div class="form-field">
      <span class="form-label">{{ t("ruleEditor.eventTypeKey") }}</span>
      <EventTypeKeyDropdown
        :model-value="eventTypeKey"
        @update:model-value="updateEventTypeKey"
      />
    </div>
    <div class="form-field">
      <span class="form-label">{{ t("ruleEditor.trigger.filter") }}</span>
      <p v-if="triggerMetadata.loading" role="status" class="monitor-help">
        {{ t("ruleEditor.trigger.loadingMetadata") }}
      </p>
      <p v-else-if="triggerMetadata.error" role="alert" class="ack-error-code">
        {{ triggerMetadata.error }}
      </p>
      <p
        v-else-if="filterFields.length === 0"
        role="status"
        class="monitor-help"
        data-testid="trigger-filter-empty"
      >
        {{ t("ruleEditor.trigger.noTypedFields") }}
      </p>
      <div v-else class="rule-filter-rows" data-testid="trigger-filter-fields">
        <label
          v-for="field in filterFields"
          :key="field.key"
          class="rule-filter-field"
          :data-testid="`trigger-filter-field-${field.key}`"
        >
          <span class="form-label">
            {{ field.label }}
            <span v-if="field.required" aria-hidden="true">*</span>
          </span>
          <select
            v-if="field.options?.length"
            :value="filter[field.key] ?? ''"
            :aria-label="field.label"
            @change="updateFilterField(field, ($event.target as HTMLSelectElement).value)"
          >
            <option value="">{{ t("ruleEditor.trigger.anyValue") }}</option>
            <option v-for="(option, index) in field.options" :key="option" :value="option">
              {{ field.optionLabels?.[index] ?? option }}
            </option>
          </select>
          <select
            v-else-if="field.type === 'boolean'"
            :value="filter[field.key] ?? ''"
            :aria-label="field.label"
            @change="updateFilterField(field, ($event.target as HTMLSelectElement).value)"
          >
            <option value="">{{ t("ruleEditor.trigger.anyValue") }}</option>
            <option value="true">true</option>
            <option value="false">false</option>
          </select>
          <input
            v-else-if="field.type === 'number'"
            :value="filter[field.key] ?? ''"
            :type="fieldInputType(field)"
            min="0"
            step="1"
            inputmode="numeric"
            :aria-label="field.label"
            @keydown="blockNonIntegerKey"
            @input="updateFilterField(field, ($event.target as HTMLInputElement).value)"
          />
          <template v-else-if="field.optionsSource === TWITCH_REWARDS_SOURCE">
            <div class="rule-filter-combo">
              <select
                :value="filter[field.key] ?? ''"
                :aria-label="field.label"
                :data-testid="`trigger-filter-input-${field.key}`"
                @change="updateFilterField(field, ($event.target as HTMLSelectElement).value)"
              >
                <option value="">{{ t("ruleEditor.trigger.anyValue") }}</option>
                <option
                  v-if="filter[field.key] && !twitchRewards.rewards.some(r => r.title === filter[field.key])"
                  :value="filter[field.key]"
                >
                  {{ filter[field.key] }} {{ t("ruleEditor.trigger.rewards.unknownSuffix") }}
                </option>
                <option
                  v-for="reward in twitchRewards.rewards"
                  :key="reward.id"
                  :value="reward.title"
                >
                  {{ reward.title }}
                </option>
              </select>
              <button
                type="button"
                class="icon-button rule-filter-refresh"
                :disabled="twitchRewards.loading"
                :aria-label="t('ruleEditor.trigger.rewards.refresh')"
                :title="t('ruleEditor.trigger.rewards.refresh')"
                :data-testid="`trigger-filter-refresh-${field.key}`"
                @click="refreshTwitchRewardsList"
              >
                {{ twitchRewards.loading ? "…" : "↻" }}
              </button>
            </div>
            <span
              v-if="twitchRewards.error"
              class="ack-error-code"
              :data-testid="`trigger-filter-rewards-error-${field.key}`"
            >
              {{ twitchRewards.error }}
            </span>
            <span
              v-else-if="!twitchRewards.ready && !twitchRewards.loading"
              class="monitor-help"
              :data-testid="`trigger-filter-rewards-empty-${field.key}`"
            >
              {{ t("ruleEditor.trigger.rewards.unauthorized") }}
            </span>
            <span v-else class="monitor-help">
              {{ t("ruleEditor.trigger.rewards.summary", {
                count: twitchRewards.rewards.length,
                updated: lastRewardRefreshLabel
              }) }}
            </span>
          </template>
          <input
            v-else
            type="text"
            :value="filter[field.key] ?? ''"
            :placeholder="field.label"
            :data-testid="`trigger-filter-input-${field.key}`"
            :aria-label="field.label"
            @input="updateFilterField(field, ($event.target as HTMLInputElement).value)"
          />
          <span v-if="field.help" class="monitor-help">{{ field.help }}</span>
        </label>
      </div>
    </div>
    <label class="form-field">
      <span class="form-label">{{ t("ruleEditor.matchCondition") }}</span>
      <ConditionExpressionInput
        :model-value="matchCondition"
        placeholder="Trigger.MessageText == '!go'"
        data-test-id="rule-editor-match-condition"
        :allowed-trigger-variables="validVariables"
        @update:model-value="emit('update:matchCondition', $event)"
      />
    </label>
  </section>
</template>

<style scoped>
.rule-filter-field {
  display: grid;
  gap: 6px;
}

.rule-filter-combo {
  display: flex;
  gap: 6px;
  align-items: center;
}

.rule-filter-combo select {
  flex: 1 1 auto;
}

.rule-filter-refresh {
  flex: 0 0 auto;
}
</style>
