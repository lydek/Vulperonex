<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import {
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
  TabsContent,
  TabsList,
  TabsRoot,
  TabsTrigger
} from "reka-ui";
import {
  ApiError,
  createRule,
  getRule,
  updateRule,
  type WorkflowRuleUpsertRequest,
  type WorkflowThrottlePolicy
} from "@/api/client";
import RoleChipSelector from "@/components/admin/RoleChipSelector.vue";
import ThrottleEditor from "@/components/admin/ThrottleEditor.vue";
import TriggerEditor from "@/components/admin/TriggerEditor.vue";
import WorkflowActionsEditor from "@/components/admin/WorkflowActionsEditor.vue";
import WorkflowConditionsEditor from "@/components/admin/WorkflowConditionsEditor.vue";

const props = defineProps<{
  open: boolean;
  ruleId: string | null;
}>();

const emit = defineEmits<{
  (event: "update:open", value: boolean): void;
  (event: "saved", ruleId: string): void;
}>();

const { t } = useI18n();

const activeTab = ref("basic");
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
const loading = ref(false);
const saving = ref(false);
const error = ref<string | null>(null);
const detail = ref<string | null>(null);

const canSave = computed(() => name.value.trim().length > 0 && !loading.value && !saving.value);
const selectedRoles = computed({
  get: () => extractSelectedRoles(conditionsText.value),
  set: (roles: string[]) => {
    conditionsText.value = stringifyConditionsWithRoles(conditionsText.value, roles);
  }
});

function resetFields() {
  activeTab.value = "basic";
  name.value = "";
  eventTypeKey.value = "";
  priority.value = 100;
  isEnabled.value = true;
  isSubWorkflow.value = false;
  matchCondition.value = "";
  triggerFilter.value = {};
  throttle.value = {
    maxConcurrent: 0,
    cooldownSeconds: 0,
    perUserCooldown: false,
    perUserCooldownSeconds: 0
  };
  timeoutSeconds.value = 30;
  conditionsText.value = "[]";
  actionsText.value = "[]";
  onFailureText.value = "[]";
  error.value = null;
  detail.value = null;
}

watch(
  () => [props.open, props.ruleId] as const,
  ([open, ruleId]) => {
    if (open) {
      if (ruleId) {
        void loadRule(ruleId);
      } else {
        resetFields();
      }
    }
  },
  { immediate: true }
);

function close(): void {
  emit("update:open", false);
}

async function loadRule(ruleId: string): Promise<void> {
  loading.value = true;
  error.value = null;
  detail.value = null;
  activeTab.value = "basic";
  try {
    const rule = await getRule(ruleId);
    name.value = rule.name;
    eventTypeKey.value = rule.eventTypeKey ?? "";
    priority.value = rule.priority;
    isEnabled.value = rule.isEnabled;
    isSubWorkflow.value = rule.isSubWorkflow;
    matchCondition.value = rule.matchCondition ?? rule.trigger?.matchCondition ?? "";
    triggerFilter.value = rule.trigger?.filter ?? {};
    throttle.value = rule.throttle;
    timeoutSeconds.value = rule.timeoutSeconds;
    conditionsText.value = JSON.stringify(rule.conditions, null, 2);
    actionsText.value = JSON.stringify(rule.actions, null, 2);
    onFailureText.value = JSON.stringify(rule.onFailureSteps, null, 2);
  } catch (caught) {
    error.value = describeError(caught);
  } finally {
    loading.value = false;
  }
}

async function save(): Promise<void> {
  error.value = null;
  detail.value = null;

  let conditions: unknown[];
  let actions: unknown[];
  let onFailureSteps: unknown[];
  try {
    conditions = JSON.parse(conditionsText.value) as unknown[];
    actions = JSON.parse(actionsText.value) as unknown[];
    onFailureSteps = JSON.parse(onFailureText.value) as unknown[];
  } catch (caught) {
    error.value = "INVALID_JSON";
    detail.value = caught instanceof Error ? caught.message : String(caught);
    return;
  }

  if (!Array.isArray(conditions) || !Array.isArray(actions) || !Array.isArray(onFailureSteps)) {
    error.value = "INVALID_JSON";
    detail.value = "conditions, actions, and onFailureSteps must be JSON arrays";
    return;
  }

  const trimmedMatchCondition = matchCondition.value.trim();
  const body: WorkflowRuleUpsertRequest = {
    name: name.value.trim(),
    eventTypeKey: isSubWorkflow.value ? null : eventTypeKey.value,
    isEnabled: isEnabled.value,
    priority: priority.value,
    conditions,
    actions,
    onFailureSteps,
    executionMode: "Serial",
    maxParallelism: 1,
    throttle: throttle.value,
    timeoutSeconds: timeoutSeconds.value,
    trigger: isSubWorkflow.value ? null : { filter: triggerFilter.value },
    matchCondition: isSubWorkflow.value ? null : (trimmedMatchCondition.length > 0 ? trimmedMatchCondition : null),
    isSubWorkflow: isSubWorkflow.value
  };

  saving.value = true;
  try {
    if (props.ruleId) {
      await updateRule(props.ruleId, body);
      emit("saved", props.ruleId);
    } else {
      const created = await createRule(body);
      emit("saved", created.id);
    }
    close();
  } catch (caught) {
    error.value = describeError(caught);
    detail.value = caught instanceof ApiError ? caught.body || null : null;
  } finally {
    saving.value = false;
  }
}

function describeError(caught: unknown): string {
  if (caught instanceof ApiError) {
    return caught.errorCode ?? `HTTP_${caught.status}`;
  }
  return caught instanceof Error ? caught.message : String(caught);
}

function extractSelectedRoles(modelValue: string): string[] {
  const conditions = parseConditions(modelValue);
  const userRoleCondition = conditions.find(condition => condition.type === "userRole");
  if (!userRoleCondition) {
    return [];
  }

  return parseRoles(userRoleCondition.roles);
}

function stringifyConditionsWithRoles(modelValue: string, roles: string[]): string {
  const conditions = parseConditions(modelValue)
    .filter(condition => condition.type !== "userRole");

  if (roles.length > 0) {
    conditions.unshift({
      type: "userRole",
      mode: "HasAny",
      roles: roles.join(", ")
    });
  }

  return JSON.stringify(conditions, null, 2);
}

function parseConditions(modelValue: string): Record<string, unknown>[] {
  try {
    const parsed = JSON.parse(modelValue) as unknown;
    return Array.isArray(parsed)
      ? parsed.filter((entry): entry is Record<string, unknown> =>
          typeof entry === "object" && entry !== null && !Array.isArray(entry))
      : [];
  } catch {
    return [];
  }
}

function parseRoles(value: unknown): string[] {
  if (typeof value === "string") {
    return value.split(",").map(role => normalizeRole(role)).filter(role => role.length > 0);
  }

  return [];
}

function normalizeRole(value: string): string {
  const trimmed = value.trim();
  if (trimmed.toLowerCase() === "vip") {
    return "Vip";
  }

  return trimmed.length === 0 ? "" : trimmed[0].toUpperCase() + trimmed.slice(1);
}
</script>

<template>
  <DialogRoot :open="open" @update:open="emit('update:open', $event)">
    <DialogPortal>
      <DialogOverlay class="rule-drawer-overlay" />
      <DialogContent class="rule-drawer" data-testid="rule-editor-drawer">
        <header class="rule-drawer__header">
          <div>
            <DialogTitle class="section-title">
              {{ props.ruleId ? t("ruleEditor.drawer.title") : (t("ruleEditor.drawer.titleCreate") || "建立新工作流程") }}
            </DialogTitle>
            <DialogDescription class="status-label">
              {{ t("ruleEditor.drawer.subtitle") }}
            </DialogDescription>
          </div>
          <DialogClose as-child>
            <button type="button" class="icon-button" data-testid="rule-drawer-close">
              {{ t("common.cancel") }}
            </button>
          </DialogClose>
        </header>

        <div v-if="loading" class="rule-drawer__skeleton animate-pulse" role="status" aria-label="Loading rule details" data-testid="rule-drawer-loading">
          <div class="skeleton-tabs">
            <div class="skeleton-tab active"></div>
            <div class="skeleton-tab"></div>
            <div class="skeleton-tab"></div>
          </div>
          <div class="skeleton-body">
            <div class="skeleton-field">
              <div class="skeleton-label"></div>
              <div class="skeleton-input"></div>
            </div>
            <div class="skeleton-field-inline">
              <div class="skeleton-checkbox"></div>
              <div class="skeleton-label-short"></div>
            </div>
            <div class="skeleton-field">
              <div class="skeleton-label"></div>
              <div class="skeleton-input-large"></div>
            </div>
            <div class="skeleton-grid">
              <div class="skeleton-field">
                <div class="skeleton-label"></div>
                <div class="skeleton-input"></div>
              </div>
              <div class="skeleton-field">
                <div class="skeleton-label"></div>
                <div class="skeleton-input"></div>
              </div>
            </div>
          </div>
          <div class="skeleton-footer">
            <div class="skeleton-button"></div>
            <div class="skeleton-button primary"></div>
          </div>
        </div>

        <p v-if="error" class="ack-error-code" role="alert" data-testid="rule-drawer-error">
          {{ error }}
          <span v-if="detail" class="ack-error-detail">{{ detail }}</span>
        </p>

        <TabsRoot v-if="!loading" v-model="activeTab" class="rule-drawer__tabs">
          <TabsList class="rule-drawer__tab-list" aria-label="Rule editor sections">
            <TabsTrigger class="rule-drawer__tab" value="basic" data-testid="rule-drawer-tab-basic">
              {{ t("ruleEditor.drawer.basic") }}
            </TabsTrigger>
            <TabsTrigger class="rule-drawer__tab" value="actions" data-testid="rule-drawer-tab-actions">
              {{ t("ruleEditor.drawer.actions") }}
            </TabsTrigger>
            <TabsTrigger class="rule-drawer__tab" value="errors" data-testid="rule-drawer-tab-errors">
              {{ t("ruleEditor.drawer.errors") }}
            </TabsTrigger>
          </TabsList>

          <form class="rule-drawer__form" @submit.prevent="save">
            <TabsContent class="rule-drawer__panel" value="basic">
              <label class="form-field">
                <span class="form-label">{{ t("ruleEditor.name") }}</span>
                <input v-model="name" type="text" required maxlength="256" data-testid="rule-drawer-name" />
              </label>

              <label class="form-field form-field-inline">
                <input v-model="isSubWorkflow" type="checkbox" data-testid="rule-drawer-sub-workflow" />
                <span>{{ t("ruleEditor.isSubWorkflow") }}</span>
              </label>

              <TriggerEditor
                v-if="!isSubWorkflow"
                v-model:event-type-key="eventTypeKey"
                v-model:filter="triggerFilter"
                v-model:match-condition="matchCondition"
              />

              <div class="rule-drawer__basic-grid">
                <label class="form-field">
                  <span class="form-label">{{ t("ruleEditor.priority") }}</span>
                  <input v-model.number="priority" type="number" min="0" max="10000" data-testid="rule-drawer-priority" />
                </label>
                <label class="form-field">
                  <span class="form-label">{{ t("ruleEditor.timeoutSeconds") }}</span>
                  <input v-model.number="timeoutSeconds" type="number" min="0" max="86400" data-testid="rule-drawer-timeout" />
                </label>
              </div>

              <label class="form-field form-field-inline">
                <input v-model="isEnabled" type="checkbox" data-testid="rule-drawer-enabled" />
                <span>{{ t("ruleEditor.isEnabled") }}</span>
              </label>

              <RoleChipSelector v-model="selectedRoles" />

              <ThrottleEditor v-model="throttle" />

              <WorkflowConditionsEditor
                v-model="conditionsText"
                :title="t('ruleEditor.conditions')"
                empty-text="No conditions yet. Add one to gate this workflow without editing raw JSON."
                test-id-prefix="rule-drawer-conditions"
                :match-condition="matchCondition"
              />
            </TabsContent>

            <TabsContent class="rule-drawer__panel" value="actions">
              <WorkflowActionsEditor
                v-model="actionsText"
                :title="t('ruleEditor.actions')"
                empty-text="No actions yet. Add a step to build the workflow visually."
                test-id-prefix="rule-drawer-actions"
              />
            </TabsContent>

            <TabsContent class="rule-drawer__panel" value="errors">
              <WorkflowActionsEditor
                v-model="onFailureText"
                :title="t('ruleEditor.onFailure')"
                empty-text="No failure recovery steps yet. Add fallback actions to run after an error."
                test-id-prefix="rule-drawer-on-failure"
              />
            </TabsContent>

            <footer class="rule-drawer__footer">
              <button type="button" class="secondary-button" :disabled="saving" @click="close">
                {{ t("common.cancel") }}
              </button>
              <button type="submit" class="primary-button" :disabled="!canSave" data-testid="rule-drawer-save">
                {{ saving ? t("ruleEditor.submitting") : (props.ruleId ? t("ruleEditor.update") : t("ruleEditor.create")) }}
              </button>
            </footer>
          </form>
        </TabsRoot>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>

<style scoped>
.rule-drawer-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 32, 0.45);
  z-index: 100;
}

.rule-drawer {
  position: fixed;
  inset: 0 0 0 auto;
  display: flex;
  flex-direction: column;
  gap: 12px;
  width: min(720px, 100vw);
  border-left: 1px solid #d6dde5;
  background: #ffffff;
  padding: 18px;
  box-shadow: 0 10px 30px rgba(15, 23, 32, 0.18);
  z-index: 101;
}

.rule-drawer[data-state="open"] {
  animation: rule-drawer-slide-in 0.18s ease-out;
}

.rule-drawer__header,
.rule-drawer__footer {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.rule-drawer__tabs,
.rule-drawer__form,
.rule-drawer__panel {
  min-height: 0;
}

.rule-drawer__tabs {
  flex: 1 1 auto;
  display: grid;
  grid-template-rows: auto 1fr;
  gap: 14px;
}

.rule-drawer__tab-list {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid #d6dde5;
}

.rule-drawer__tab {
  border: 0;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: #394756;
  cursor: pointer;
  font-weight: 700;
  padding: 8px 10px;
}

.rule-drawer__tab[data-state="active"] {
  border-color: #1f6f64;
  color: #164f48;
}

.rule-drawer__form {
  display: grid;
  grid-template-rows: 1fr auto;
  gap: 14px;
  overflow: hidden;
}

.rule-drawer__panel {
  display: grid;
  gap: 12px;
  align-content: start;
  overflow: auto;
  padding-right: 4px;
}

.rule-drawer__basic-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.rule-drawer__footer {
  align-items: center;
  justify-content: flex-end;
  border-top: 1px solid #d6dde5;
  padding-top: 12px;
}

@keyframes rule-drawer-slide-in {
  from {
    transform: translateX(24px);
    opacity: 0.75;
  }

  to {
    transform: translateX(0);
    opacity: 1;
  }
}

@media (max-width: 640px) {
  .rule-drawer {
    width: 100vw;
  }

  .rule-drawer__basic-grid {
    grid-template-columns: 1fr;
  }
}

/* 骨架屏脈動閃爍動畫 */
.animate-pulse {
  animation: pulse 1.5s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

@keyframes pulse {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.45;
  }
}

.rule-drawer__skeleton {
  flex: 1 1 auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
  min-height: 0;
  padding-top: 8px;
}

.skeleton-tabs {
  display: flex;
  gap: 8px;
  border-bottom: 1px solid #e6ebf0;
  padding-bottom: 8px;
}

.skeleton-tab {
  width: 72px;
  height: 28px;
  background: #e2e8f0;
  border-radius: 4px;
}

.skeleton-tab.active {
  background: #cbd5e1;
}

.skeleton-body {
  flex: 1 1 auto;
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow: hidden;
}

.skeleton-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.skeleton-field-inline {
  display: flex;
  align-items: center;
  gap: 8px;
}

.skeleton-label {
  width: 80px;
  height: 14px;
  background: #e2e8f0;
  border-radius: 3px;
}

.skeleton-label-short {
  width: 120px;
  height: 14px;
  background: #e2e8f0;
  border-radius: 3px;
}

.skeleton-input {
  width: 100%;
  height: 38px;
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
}

.skeleton-input-large {
  width: 100%;
  height: 88px;
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
}

.skeleton-checkbox {
  width: 16px;
  height: 16px;
  background: #e2e8f0;
  border-radius: 4px;
}

.skeleton-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.skeleton-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  border-top: 1px solid #e6ebf0;
  padding-top: 16px;
}

.skeleton-button {
  width: 80px;
  height: 38px;
  background: #f1f5f9;
  border-radius: 6px;
}

.skeleton-button.primary {
  background: #cbd5e1;
  width: 96px;
}
</style>
