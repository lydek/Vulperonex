<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import EventTypeKeyDropdown from "@/components/admin/EventTypeKeyDropdown.vue";
import RuleJsonEditor from "@/components/admin/RuleJsonEditor.vue";
import {
  ApiError,
  createRule,
  getRule,
  updateRule,
  type WorkflowRuleUpsertRequest
} from "@/api/client";

const route = useRoute();
const router = useRouter();
const { t } = useI18n();

const ruleId = computed(() => {
  const param = route.params.id;
  return typeof param === "string" ? param : null;
});
const isEdit = computed(() => ruleId.value !== null);

const name = ref("");
const eventTypeKey = ref("");
const priority = ref(100);
const isEnabled = ref(true);
const conditionsText = ref("[]");
const actionsText = ref("[]");
const submitting = ref(false);
const loadingExisting = ref(false);
const submitError = ref<string | null>(null);
const submitDetail = ref<string | null>(null);
const editorRef = ref<{ focus: () => void } | null>(null);

onMounted(async () => {
  if (!isEdit.value) return;
  await loadExisting();
});

watch(ruleId, async () => {
  if (isEdit.value) {
    await loadExisting();
  }
});

async function loadExisting(): Promise<void> {
  if (!ruleId.value) return;
  loadingExisting.value = true;
  submitError.value = null;
  try {
    const rule = await getRule(ruleId.value);
    name.value = rule.name;
    eventTypeKey.value = rule.eventTypeKey;
    priority.value = rule.priority;
    isEnabled.value = rule.isEnabled;
    conditionsText.value = JSON.stringify(rule.conditions, null, 2);
    actionsText.value = JSON.stringify(rule.actions, null, 2);
  } catch (caught) {
    submitError.value = describeError(caught);
  } finally {
    loadingExisting.value = false;
  }
}

async function onSubmit(event: Event): Promise<void> {
  event.preventDefault();
  submitError.value = null;
  submitDetail.value = null;

  let conditions: unknown[];
  let actions: unknown[];
  try {
    conditions = JSON.parse(conditionsText.value) as unknown[];
    actions = JSON.parse(actionsText.value) as unknown[];
  } catch (parseError) {
    submitError.value = "INVALID_JSON";
    submitDetail.value = parseError instanceof Error ? parseError.message : String(parseError);
    editorRef.value?.focus();
    return;
  }

  if (!Array.isArray(conditions) || !Array.isArray(actions)) {
    submitError.value = "INVALID_JSON";
    submitDetail.value = "conditions and actions must be JSON arrays";
    editorRef.value?.focus();
    return;
  }

  const body: WorkflowRuleUpsertRequest = {
    name: name.value.trim(),
    eventTypeKey: eventTypeKey.value,
    isEnabled: isEnabled.value,
    priority: priority.value,
    conditions,
    actions,
    executionMode: "Serial",
    maxParallelism: 1
  };

  submitting.value = true;
  try {
    if (isEdit.value && ruleId.value) {
      await updateRule(ruleId.value, body);
    } else {
      const created = await createRule(body);
      await router.push({ name: "rules" });
      // ensure detail visible after navigation by selecting it
      void created;
      return;
    }
    await router.push({ name: "rules" });
  } catch (caught) {
    if (caught instanceof ApiError) {
      submitError.value = caught.errorCode ?? `HTTP_${caught.status}`;
      submitDetail.value = caught.body || null;
    } else {
      submitError.value = "NETWORK_ERROR";
      submitDetail.value = caught instanceof Error ? caught.message : String(caught);
    }
    editorRef.value?.focus();
  } finally {
    submitting.value = false;
  }
}

function describeError(caught: unknown): string {
  if (caught instanceof ApiError) {
    return caught.errorCode ?? `HTTP_${caught.status}`;
  }
  return caught instanceof Error ? caught.message : String(caught);
}
</script>

<template>
  <section aria-labelledby="rule-editor-title">
    <header class="page-header">
      <h1 id="rule-editor-title" class="page-title">
        {{ isEdit ? t("ruleEditor.titleEdit") : t("ruleEditor.titleCreate") }}
      </h1>
      <p class="page-subtitle">{{ t("ruleEditor.subtitle") }}</p>
    </header>

    <form class="rule-editor-form" @submit="onSubmit" novalidate>
      <label class="form-field">
        <span class="form-label">{{ t("ruleEditor.name") }}</span>
        <input
          v-model="name"
          type="text"
          required
          maxlength="256"
          data-testid="rule-editor-name"
        />
      </label>

      <div class="form-field">
        <span class="form-label">{{ t("ruleEditor.eventTypeKey") }}</span>
        <EventTypeKeyDropdown v-model="eventTypeKey" />
      </div>

      <label class="form-field">
        <span class="form-label">{{ t("ruleEditor.priority") }}</span>
        <input
          v-model.number="priority"
          type="number"
          min="0"
          max="10000"
          data-testid="rule-editor-priority"
        />
      </label>

      <label class="form-field form-field-inline">
        <input v-model="isEnabled" type="checkbox" data-testid="rule-editor-enabled" />
        <span>{{ t("ruleEditor.isEnabled") }}</span>
      </label>

      <div class="form-field">
        <span class="form-label">{{ t("ruleEditor.conditions") }}</span>
        <RuleJsonEditor
          v-model="conditionsText"
          :aria-label="t('ruleEditor.conditions')"
        />
      </div>

      <div class="form-field">
        <span class="form-label">{{ t("ruleEditor.actions") }}</span>
        <RuleJsonEditor
          ref="editorRef"
          v-model="actionsText"
          :aria-label="t('ruleEditor.actions')"
        />
      </div>

      <p
        v-if="submitError"
        class="ack-error-code"
        role="alert"
        data-testid="rule-editor-submit-error"
      >
        {{ submitError }}
        <span v-if="submitDetail" class="ack-error-detail">{{ submitDetail }}</span>
      </p>

      <div class="rule-editor-actions">
        <button
          type="button"
          class="secondary-button"
          :disabled="submitting"
          @click="router.push({ name: 'rules' })"
        >
          {{ t("common.cancel") }}
        </button>
        <button
          type="submit"
          class="primary-button"
          :disabled="submitting || loadingExisting"
          data-testid="rule-editor-submit"
        >
          {{ submitting ? t("ruleEditor.submitting") : (isEdit ? t("ruleEditor.update") : t("ruleEditor.create")) }}
        </button>
      </div>
    </form>
  </section>
</template>
