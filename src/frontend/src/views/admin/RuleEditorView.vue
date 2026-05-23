<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import OnFailureEditor from "@/components/admin/OnFailureEditor.vue";
import RuleJsonEditor from "@/components/admin/RuleJsonEditor.vue";
import StepConditionInput from "@/components/admin/StepConditionInput.vue";
import ThrottleEditor from "@/components/admin/ThrottleEditor.vue";
import TriggerEditor from "@/components/admin/TriggerEditor.vue";
import {
  ApiError,
  createRule,
  getRule,
  updateRule,
  type WorkflowThrottlePolicy,
  type WorkflowRuleUpsertRequest
} from "@/api/client";

const MAX_RULE_FILE_BYTES = 1_048_576;

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
const isSubWorkflow = ref(false);
const matchCondition = ref("");
const triggerFilter = ref<Record<string, string>>({});
const throttle = ref<WorkflowThrottlePolicy>({
  maxConcurrent: 0,
  cooldownSeconds: 0,
  perUserCooldown: false,
  perUserCooldownSeconds: 0
});
const timeoutSeconds = ref(30);
const conditionsText = ref("[]");
const actionsText = ref("[]");
const onFailureText = ref("[]");
const submitting = ref(false);
const loadingExisting = ref(false);
const submitError = ref<string | null>(null);
const submitDetail = ref<string | null>(null);
const editorRef = ref<{ focus: () => void } | null>(null);
const ruleFileInputRef = ref<HTMLInputElement | null>(null);

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
    isSubWorkflow.value = rule.isSubWorkflow;
    triggerFilter.value = rule.trigger?.filter ?? {};
    matchCondition.value = rule.matchCondition ?? rule.trigger?.matchCondition ?? "";
    throttle.value = rule.throttle;
    timeoutSeconds.value = rule.timeoutSeconds;
    conditionsText.value = JSON.stringify(rule.conditions, null, 2);
    actionsText.value = JSON.stringify(rule.actions, null, 2);
    onFailureText.value = JSON.stringify(rule.onFailureSteps, null, 2);
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
  let onFailureSteps: unknown[];
  try {
    conditions = JSON.parse(conditionsText.value) as unknown[];
    actions = JSON.parse(actionsText.value) as unknown[];
    onFailureSteps = JSON.parse(onFailureText.value) as unknown[];
  } catch (parseError) {
    submitError.value = "INVALID_JSON";
    submitDetail.value = parseError instanceof Error ? parseError.message : String(parseError);
    editorRef.value?.focus();
    return;
  }

  if (!Array.isArray(conditions) || !Array.isArray(actions) || !Array.isArray(onFailureSteps)) {
    submitError.value = "INVALID_JSON";
    submitDetail.value = "conditions, actions, and onFailureSteps must be JSON arrays";
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
    onFailureSteps,
    executionMode: "Serial",
    maxParallelism: 1,
    throttle: throttle.value,
    timeoutSeconds: timeoutSeconds.value,
    trigger: {
      eventTypeKey: eventTypeKey.value,
      filter: triggerFilter.value,
      matchCondition: matchCondition.value.trim().length > 0 ? matchCondition.value.trim() : null
    },
    matchCondition: matchCondition.value.trim().length > 0 ? matchCondition.value.trim() : null,
    isSubWorkflow: isSubWorkflow.value
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

async function onRuleFileChange(event: Event): Promise<void> {
  submitError.value = null;
  submitDetail.value = null;

  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;

  try {
    if (!/\.json$/i.test(file.name)) {
      throw new Error(t("ruleEditor.fileExtension"));
    }

    if (file.type && file.type !== "application/json" && file.type !== "text/json") {
      throw new Error(t("ruleEditor.fileMime"));
    }

    if (file.size > MAX_RULE_FILE_BYTES) {
      throw new Error(t("ruleEditor.tooLarge"));
    }

    const parsed = JSON.parse(await file.text()) as unknown;
    if (!isObjectRecord(parsed)) {
      throw new Error("Rule file must be a JSON object");
    }

    applyImportedRule(parsed);
  } catch (caught) {
    submitError.value = "INVALID_JSON";
    submitDetail.value = caught instanceof Error ? caught.message : String(caught);
  } finally {
    if (ruleFileInputRef.value) {
      ruleFileInputRef.value.value = "";
    }
  }
}

function applyImportedRule(rule: Record<string, unknown>): void {
  const trigger = isObjectRecord(rule.trigger) ? rule.trigger : null;
  const importedEventTypeKey = readString(rule.eventTypeKey)
    ?? (trigger ? readString(trigger.eventTypeKey) : null);

  name.value = readString(rule.name) ?? name.value;
  eventTypeKey.value = importedEventTypeKey ?? eventTypeKey.value;
  priority.value = readNumber(rule.priority) ?? priority.value;
  isEnabled.value = readBoolean(rule.isEnabled) ?? isEnabled.value;
  isSubWorkflow.value = readBoolean(rule.isSubWorkflow) ?? isSubWorkflow.value;
  triggerFilter.value = trigger && isStringRecord(trigger.filter) ? trigger.filter : {};
  matchCondition.value = readString(rule.matchCondition)
    ?? (trigger ? readString(trigger.matchCondition) : null)
    ?? "";
  throttle.value = readThrottle(rule.throttle) ?? throttle.value;
  timeoutSeconds.value = readNumber(rule.timeoutSeconds) ?? timeoutSeconds.value;
  conditionsText.value = JSON.stringify(readArray(rule.conditions) ?? [], null, 2);
  actionsText.value = JSON.stringify(readArray(rule.actions) ?? [], null, 2);
  onFailureText.value = JSON.stringify(readArray(rule.onFailureSteps) ?? [], null, 2);
}

function readThrottle(value: unknown): WorkflowThrottlePolicy | null {
  if (!isObjectRecord(value)) return null;
  return {
    maxConcurrent: readNumber(value.maxConcurrent) ?? 0,
    cooldownSeconds: readNumber(value.cooldownSeconds) ?? 0,
    perUserCooldown: readBoolean(value.perUserCooldown) ?? false,
    perUserCooldownSeconds: readNumber(value.perUserCooldownSeconds) ?? 0
  };
}

function readArray(value: unknown): unknown[] | null {
  return Array.isArray(value) ? value : null;
}

function readBoolean(value: unknown): boolean | null {
  return typeof value === "boolean" ? value : null;
}

function readNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function readString(value: unknown): string | null {
  return typeof value === "string" ? value : null;
}

function isStringRecord(value: unknown): value is Record<string, string> {
  return isObjectRecord(value)
    && Object.values(value).every((item) => typeof item === "string");
}

function isObjectRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
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
      <div class="form-field">
        <span class="form-label">{{ t("ruleEditor.importRule") }}</span>
        <label class="rule-editor-file">
          <input
            ref="ruleFileInputRef"
            type="file"
            accept=".json,application/json,text/json"
            data-testid="rule-import-file"
            @change="onRuleFileChange"
          />
          {{ t("ruleEditor.importRuleFile") }}
        </label>
      </div>

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

      <label class="form-field form-field-inline">
        <input v-model="isSubWorkflow" type="checkbox" data-testid="rule-editor-sub-workflow" />
        <span>{{ t("ruleEditor.isSubWorkflow") }}</span>
      </label>

      <TriggerEditor
        v-if="!isSubWorkflow"
        v-model:event-type-key="eventTypeKey"
        v-model:filter="triggerFilter"
        v-model:match-condition="matchCondition"
      />

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

      <label class="form-field">
        <span class="form-label">{{ t("ruleEditor.timeoutSeconds") }}</span>
        <input
          v-model.number="timeoutSeconds"
          type="number"
          min="0"
          max="86400"
          data-testid="rule-editor-timeout"
        />
      </label>

      <ThrottleEditor v-model="throttle" />

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

      <StepConditionInput v-model="actionsText" />
      <OnFailureEditor v-model="onFailureText" />

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
