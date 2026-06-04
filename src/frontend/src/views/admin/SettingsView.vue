<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import { useTheme, type ThemePreference } from "@/composables/useTheme";
import {
  ApiError,
  getConfigValue,
  getPluginModules,
  setConfigValue,
  togglePluginModule,
  type PluginModule
} from "@/api/client";

const { t } = useI18n();
const { preference: themePreference, resolvedTheme, setThemePreference } = useTheme();

const modules = ref<PluginModule[]>([]);
const loading = ref(false);
const busyModule = ref<string | null>(null);
const errorCode = ref<string | null>(null);
const errorDetail = ref<string | null>(null);
const confirmOpen = ref(false);
const pendingTarget = ref<PluginModule | null>(null);
const pendingEnabled = ref(false);
const affectedModules = ref<PluginModule[]>([]);
const themeOptions: ThemePreference[] = ["system", "light", "dark"];
const assistantDisplayName = ref("");
const assistantAvatarUrl = ref("");
const checkInDisplayName = ref("");
const workflowChatOutputDestination = ref("dual");
const savingWorkflowChat = ref(false);
const workflowChatSaveSuccess = ref(false);
const checkInResetTimeLocal = ref("05:00");
const checkInRepeatCardEnabled = ref(true);
const savingCheckInSettings = ref(false);
const checkInSaveSuccess = ref(false);
const activeTab = ref<"general" | "workflow-chat" | "checkin" | "modules">("general");

const workflowChatOutputOptions = ["dual", "overlay_only", "platform_only"] as const;

onMounted(async () => {
  await Promise.all([loadModules(), loadWorkflowChatSettings()]);
});

const modulesByName = computed(() => new Map(modules.value.map((item) => [item.name, item])));
const isCheckInModuleEnabled = computed(() => modulesByName.value.get("checkin")?.enabled ?? false);
const confirmMessage = computed(() => {
  const names = affectedModules.value.map((item) => item.displayName).join(", ");
  const base = pendingEnabled.value
    ? t("settings.modules.enableMessage")
    : t("settings.modules.disableMessage");
  return names ? `${base} ${names}` : base;
});

async function loadModules(): Promise<void> {
  loading.value = true;
  errorCode.value = null;
  errorDetail.value = null;
  try {
    modules.value = await getPluginModules();
  } catch (caught) {
    applyError(caught);
  } finally {
    loading.value = false;
  }
}

async function loadWorkflowChatSettings(): Promise<void> {
  try {
    const [displayName, avatarUrl, checkInName, outputDestination, resetTime, repeatCardEnabled] = await Promise.all([
      getConfigValue("overlay.chat.assistant_display_name"),
      getConfigValue("overlay.chat.assistant_avatar_url"),
      getConfigValue("overlay.chat.checkin_display_name"),
      getConfigValue("workflow.chat.output_destination"),
      getConfigValue("checkin.reset_time_local"),
      getConfigValue("checkin.repeat_card_enabled")
    ]);
    assistantDisplayName.value = displayName.value || "";
    assistantAvatarUrl.value = avatarUrl.value || "";
    checkInDisplayName.value = checkInName.value || "";
    workflowChatOutputDestination.value = outputDestination.value || "dual";
    checkInResetTimeLocal.value = resetTime.value || "05:00";
    checkInRepeatCardEnabled.value = repeatCardEnabled.value !== "false";
  } catch (caught) {
    applyError(caught);
  }
}

function requestToggle(module: PluginModule): void {
  pendingTarget.value = module;
  pendingEnabled.value = !module.enabled;
  affectedModules.value = computeAffectedModules(module.name, !module.enabled);
  confirmOpen.value = true;
}

function computeAffectedModules(name: string, enable: boolean): PluginModule[] {
  const visited = new Set<string>();
  const impacted = new Map<string, PluginModule>();

  function visit(next: string): void {
    if (visited.has(next)) {
      return;
    }

    visited.add(next);
    const item = modulesByName.value.get(next);
    if (!item) {
      return;
    }

    impacted.set(next, item);
    const linked = enable ? item.dependencies : item.dependents;
    for (const linkedName of linked) {
      visit(linkedName);
    }
  }

  visit(name);
  return [...impacted.values()].sort((left, right) => left.displayName.localeCompare(right.displayName));
}

async function confirmToggle(): Promise<void> {
  if (!pendingTarget.value) {
    confirmOpen.value = false;
    return;
  }

  busyModule.value = pendingTarget.value.name;
  errorCode.value = null;
  errorDetail.value = null;
  try {
    const response = await togglePluginModule(pendingTarget.value.name, pendingEnabled.value);
    const nextByName = new Map(modules.value.map((item) => [item.name, item]));
    for (const changed of response.changedModules) {
      nextByName.set(changed.name, changed);
    }
    modules.value = [...nextByName.values()].sort((left, right) => left.displayName.localeCompare(right.displayName));
    confirmOpen.value = false;
  } catch (caught) {
    applyError(caught);
  } finally {
    busyModule.value = null;
    pendingTarget.value = null;
    affectedModules.value = [];
  }
}

function applyError(caught: unknown): void {
  if (caught instanceof ApiError) {
    errorCode.value = caught.errorCode ?? `HTTP_${caught.status}`;
    errorDetail.value = caught.body || null;
    return;
  }

  errorCode.value = "NETWORK_ERROR";
  errorDetail.value = caught instanceof Error ? caught.message : String(caught);
}

function updateTheme(event: Event): void {
  const nextPreference = (event.target as HTMLSelectElement).value as ThemePreference;
  setThemePreference(nextPreference);
}

async function saveWorkflowChatSettings(): Promise<void> {
  savingWorkflowChat.value = true;
  workflowChatSaveSuccess.value = false;
  errorCode.value = null;
  errorDetail.value = null;
  try {
    await Promise.all([
      setConfigValue("overlay.chat.assistant_display_name", assistantDisplayName.value.trim()),
      setConfigValue("overlay.chat.assistant_avatar_url", assistantAvatarUrl.value.trim()),
      setConfigValue("overlay.chat.checkin_display_name", checkInDisplayName.value.trim()),
      setConfigValue("workflow.chat.output_destination", workflowChatOutputDestination.value)
    ]);
    workflowChatSaveSuccess.value = true;
    setTimeout(() => {
      workflowChatSaveSuccess.value = false;
    }, 3000);
  } catch (caught) {
    applyError(caught);
  } finally {
    savingWorkflowChat.value = false;
  }
}

async function saveCheckInSettings(): Promise<void> {
  savingCheckInSettings.value = true;
  checkInSaveSuccess.value = false;
  errorCode.value = null;
  errorDetail.value = null;
  try {
    await Promise.all([
      setConfigValue("checkin.reset_time_local", checkInResetTimeLocal.value || "05:00"),
      setConfigValue("checkin.repeat_card_enabled", checkInRepeatCardEnabled.value ? "true" : "false")
    ]);
    checkInSaveSuccess.value = true;
    setTimeout(() => {
      checkInSaveSuccess.value = false;
    }, 3000);
  } catch (caught) {
    applyError(caught);
  } finally {
    savingCheckInSettings.value = false;
  }
}

watch(isCheckInModuleEnabled, (enabled) => {
  if (!enabled && activeTab.value === "checkin") {
    activeTab.value = "general";
  }
});
</script>

<template>
  <section aria-labelledby="settings-title">
    <header class="page-header">
      <h1 id="settings-title" class="page-title">{{ t("settings.modules.title") }}</h1>
      <p class="page-subtitle">{{ t("settings.modules.subtitle") }}</p>
    </header>

    <nav class="settings-tabs" aria-label="Settings tabs">
      <button
        type="button"
        class="settings-tab"
        :class="{ 'is-active': activeTab === 'general' }"
        data-testid="settings-tab-general"
        @click="activeTab = 'general'"
      >
        {{ t("settings.tabs.general") }}
      </button>
      <button
        type="button"
        class="settings-tab"
        :class="{ 'is-active': activeTab === 'workflow-chat' }"
        data-testid="settings-tab-workflow-chat"
        @click="activeTab = 'workflow-chat'"
      >
        {{ t("settings.tabs.workflowChat") }}
      </button>
      <button
        v-if="isCheckInModuleEnabled"
        type="button"
        class="settings-tab"
        :class="{ 'is-active': activeTab === 'checkin' }"
        data-testid="settings-tab-checkin"
        @click="activeTab = 'checkin'"
      >
        {{ t("settings.tabs.checkIn") }}
      </button>
      <button
        type="button"
        class="settings-tab"
        :class="{ 'is-active': activeTab === 'modules' }"
        data-testid="settings-tab-modules"
        @click="activeTab = 'modules'"
      >
        {{ t("settings.tabs.modules") }}
      </button>
    </nav>

    <section v-if="activeTab === 'general'" class="settings-section" aria-labelledby="theme-settings-title">
      <div>
        <h2 id="theme-settings-title" class="section-title">{{ t("settings.theme.title") }}</h2>
        <p class="settings-section__copy">{{ t("settings.theme.subtitle") }}</p>
      </div>
      <label class="form-field theme-select-field">
        <span class="form-label">{{ t("settings.theme.preference") }}</span>
        <select :value="themePreference" data-testid="theme-preference-select" @change="updateTheme">
          <option v-for="option in themeOptions" :key="option" :value="option">
            {{ t(`settings.theme.${option}`) }}
          </option>
        </select>
      </label>
      <p class="settings-section__meta" data-testid="theme-resolved">
        {{ t("settings.theme.resolved", { theme: t(`settings.theme.${resolvedTheme}`) }) }}
      </p>
    </section>

    <section v-if="activeTab === 'workflow-chat'" class="settings-section" aria-labelledby="workflow-chat-settings-title">
      <div class="settings-section__header">
        <div>
          <h2 id="workflow-chat-settings-title" class="section-title">{{ t("settings.workflowChat.title") }}</h2>
          <p class="settings-section__copy">{{ t("settings.workflowChat.subtitle") }}</p>
        </div>
        <div class="settings-preview-stack">
        <div class="assistant-preview" data-testid="assistant-preview">
          <img
            v-if="assistantAvatarUrl"
            :src="assistantAvatarUrl"
            class="assistant-preview__avatar"
            alt=""
          />
          <span v-else class="assistant-preview__fallback">🧷</span>
          <strong>{{ assistantDisplayName || t("overlay.chat.systemAssistant") }}</strong>
        </div>
        <div class="assistant-preview" data-testid="checkin-preview">
          <span class="assistant-preview__fallback assistant-preview__fallback--checkin">🟥</span>
          <strong>{{ checkInDisplayName || t("overlay.chat.checkInSystem") }}</strong>
        </div>
        </div>
      </div>

      <div class="settings-form-grid">
        <label class="form-field">
          <span class="form-label">{{ t("settings.workflowChat.displayName") }}</span>
          <input
            v-model="assistantDisplayName"
            type="text"
            :placeholder="t('settings.workflowChat.displayNamePlaceholder')"
            data-testid="workflow-chat-display-name"
          />
        </label>

        <label class="form-field">
          <span class="form-label">{{ t("settings.workflowChat.avatarUrl") }}</span>
          <input
            v-model="assistantAvatarUrl"
            type="url"
            :placeholder="t('settings.workflowChat.avatarUrlPlaceholder')"
            data-testid="workflow-chat-avatar-url"
          />
        </label>

        <label class="form-field">
          <span class="form-label">{{ t("settings.workflowChat.checkInDisplayName") }}</span>
          <input
            v-model="checkInDisplayName"
            type="text"
            :placeholder="t('settings.workflowChat.checkInDisplayNamePlaceholder')"
            data-testid="workflow-chat-checkin-display-name"
          />
        </label>

        <label class="form-field settings-form-grid__wide">
          <span class="form-label">{{ t("settings.workflowChat.outputDestination") }}</span>
          <select
            v-model="workflowChatOutputDestination"
            data-testid="workflow-chat-output-destination"
          >
            <option v-for="option in workflowChatOutputOptions" :key="option" :value="option">
              {{ t(`settings.workflowChat.destinations.${option}`) }}
            </option>
          </select>
        </label>
      </div>

      <p class="settings-section__meta">{{ t("settings.workflowChat.hint") }}</p>
      <p v-if="workflowChatSaveSuccess" class="settings-success-msg" role="status">
        {{ t("settings.workflowChat.saveSuccess") }}
      </p>
      <button
        type="button"
        class="primary-button settings-section__action"
        :disabled="savingWorkflowChat"
        @click="saveWorkflowChatSettings"
      >
        {{ savingWorkflowChat ? t("settings.workflowChat.saving") : t("settings.workflowChat.save") }}
      </button>
    </section>

    <section
      v-if="activeTab === 'checkin' && isCheckInModuleEnabled"
      class="settings-section"
      aria-labelledby="checkin-settings-title"
    >
      <div>
        <h2 id="checkin-settings-title" class="section-title">{{ t("settings.checkIn.title") }}</h2>
        <p class="settings-section__copy">{{ t("settings.checkIn.subtitle") }}</p>
      </div>

      <div class="settings-form-grid">
        <label class="form-field">
          <span class="form-label">{{ t("settings.checkIn.resetTime") }}</span>
          <input
            v-model="checkInResetTimeLocal"
            type="time"
            step="60"
            data-testid="checkin-reset-time"
          />
        </label>

        <div class="form-field checkin-repeat-display settings-form-grid__wide">
          <label class="checkin-repeat-display__toggle">
            <span>
              <span class="form-label">{{ t("settings.checkIn.repeatCardEnabled") }}</span>
              <span class="checkin-repeat-display__hint">{{ t("settings.checkIn.repeatCardHint") }}</span>
            </span>
            <input
              v-model="checkInRepeatCardEnabled"
              type="checkbox"
              data-testid="checkin-repeat-card-enabled"
            />
          </label>
          <div class="checkin-repeat-display__targets" data-testid="checkin-repeat-display-targets">
            <div class="checkin-repeat-display__target">
              <span class="checkin-repeat-display__target-icon" aria-hidden="true">▣</span>
              <span>{{ t("settings.checkIn.repeatMemberOverlay") }}</span>
            </div>
            <div class="checkin-repeat-display__target">
              <span class="checkin-repeat-display__target-icon" aria-hidden="true">☰</span>
              <span>{{ t("settings.checkIn.repeatChatOverlay") }}</span>
            </div>
          </div>
        </div>
      </div>

      <p class="settings-section__meta">{{ t("settings.checkIn.repeatPolicy") }}</p>
      <p class="settings-section__meta">{{ t("settings.checkIn.systemTimezone") }}</p>
      <p v-if="checkInSaveSuccess" class="settings-success-msg" role="status">
        {{ t("settings.checkIn.saveSuccess") }}
      </p>
      <button
        type="button"
        class="primary-button settings-section__action"
        :disabled="savingCheckInSettings"
        @click="saveCheckInSettings"
      >
        {{ savingCheckInSettings ? t("settings.checkIn.saving") : t("settings.checkIn.save") }}
      </button>
    </section>

    <section v-if="activeTab === 'modules'" aria-labelledby="module-settings-title">
      <div class="settings-toolbar">
        <div>
          <h2 id="module-settings-title" class="section-title">{{ t("settings.modules.sectionTitle") }}</h2>
          <p class="settings-section__copy">{{ t("settings.modules.sectionSubtitle") }}</p>
        </div>
        <button type="button" class="secondary-button" :disabled="loading" @click="loadModules">
          {{ t("common.refresh") }}
        </button>
      </div>

      <p v-if="errorCode" class="ack-error-code" role="alert" data-testid="settings-error-code">
        {{ errorCode }}
        <span v-if="errorDetail" class="ack-error-detail">{{ errorDetail }}</span>
      </p>

      <div class="module-grid" data-testid="module-grid">
        <article
          v-for="module in modules"
          :key="module.name"
          class="module-card"
          :data-testid="`module-card-${module.name}`"
        >
          <div class="module-card__header">
            <div>
              <p class="module-card__kind">{{ module.kind }}</p>
              <h3 class="module-card__title">{{ module.displayName }}</h3>
            </div>
            <span class="module-card__state" :class="module.enabled ? 'is-enabled' : 'is-disabled'">
              {{ module.enabled ? t("settings.modules.enabled") : t("settings.modules.disabled") }}
            </span>
          </div>

          <p class="module-card__meta">
            {{ t("settings.modules.dependsOn", { count: module.dependencies.length }) }}
            {{ module.dependencies.join(", ") || t("settings.modules.none") }}
          </p>
          <p class="module-card__meta">
            {{ t("settings.modules.usedBy", { count: module.dependents.length }) }}
            {{ module.dependents.join(", ") || t("settings.modules.none") }}
          </p>

          <button
            type="button"
            class="primary-button module-card__toggle"
            :disabled="busyModule === module.name"
            @click="requestToggle(module)"
          >
            {{ module.enabled ? t("settings.modules.disable") : t("settings.modules.enable") }}
          </button>
        </article>
      </div>
    </section>

    <ConfirmDialog
      :open="confirmOpen"
      :busy="busyModule !== null"
      :title="pendingEnabled ? t('settings.modules.enableTitle') : t('settings.modules.disableTitle')"
      :message="confirmMessage"
      :confirm-label="pendingEnabled ? t('settings.modules.enable') : t('settings.modules.disable')"
      :cancel-label="t('common.cancel')"
      @confirm="confirmToggle"
      @cancel="confirmOpen = false"
    />
  </section>
</template>

<style scoped>
.settings-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-bottom: 18px;
}

.settings-tab {
  border: 1px solid var(--vp-border-default);
  border-radius: var(--vp-radius-pill);
  padding: 8px 14px;
  background: var(--vp-bg-surface);
  color: var(--vp-text-secondary);
  font-weight: 600;
}

.settings-tab.is-active {
  background: var(--vp-bg-elevated);
  color: var(--vp-text-primary);
  border-color: var(--vp-border-accent, var(--vp-border-default));
}

.settings-toolbar {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
}

.settings-section {
  display: grid;
  gap: 14px;
  margin-bottom: 18px;
  border: 1px solid var(--vp-border-default);
  border-radius: var(--vp-radius-card);
  padding: 16px;
  background: var(--vp-bg-surface);
}

.settings-section__copy,
.settings-section__meta {
  margin: 0;
  color: var(--vp-text-muted);
  font-size: 13px;
}

.settings-section__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.settings-preview-stack {
  display: grid;
  gap: 10px;
}

.settings-form-grid {
  display: grid;
  gap: 16px;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
}

.settings-form-grid__wide {
  grid-column: 1 / -1;
}

.settings-section__action {
  justify-self: flex-start;
}

.theme-select-field {
  max-width: 280px;
}

.form-field-toggle {
  align-self: end;
}

.checkin-repeat-display {
  display: grid;
  gap: 12px;
  max-width: 760px;
}

.checkin-repeat-display__toggle {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 14px;
  border: 1px solid var(--vp-border-default);
  border-radius: var(--vp-radius-card);
  background: var(--vp-bg-elevated);
}

.checkin-repeat-display__toggle input {
  margin-top: 4px;
  accent-color: var(--vp-accent);
}

.checkin-repeat-display__hint {
  display: block;
  margin-top: 4px;
  color: var(--vp-text-muted);
  font-size: 13px;
  line-height: 1.5;
}

.checkin-repeat-display__targets {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
}

.checkin-repeat-display__target {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 44px;
  padding: 10px 12px;
  border: 1px solid var(--vp-border-subtle, var(--vp-border-default));
  border-radius: var(--vp-radius-card);
  background: var(--vp-bg-surface);
  color: var(--vp-text-secondary);
  font-size: 13px;
}

.checkin-repeat-display__target-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: var(--vp-radius-pill);
  background: var(--vp-bg-soft);
  color: var(--vp-text-primary);
  font-size: 12px;
}

.assistant-preview {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 56px;
  padding: 10px 14px;
  border: 1px solid var(--vp-border-default);
  border-radius: var(--vp-radius-card);
  background: var(--vp-bg-elevated);
}

.assistant-preview__avatar,
.assistant-preview__fallback {
  width: 40px;
  height: 40px;
  border-radius: 999px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.assistant-preview__avatar {
  object-fit: cover;
}

.assistant-preview__fallback {
  background: var(--vp-bg-soft);
  border: 1px solid var(--vp-border-default);
  font-size: 20px;
}

.assistant-preview__fallback--checkin {
  color: #d44949;
}

.module-grid {
  display: grid;
  gap: 16px;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
}

.module-card {
  border: 1px solid var(--vp-border-default);
  border-radius: var(--vp-radius-modal);
  padding: 18px;
  background: var(--vp-bg-surface);
  box-shadow: var(--vp-shadow-elevated);
}

.module-card__header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: flex-start;
}

.module-card__kind {
  margin: 0 0 4px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 11px;
  color: var(--vp-text-muted);
}

.module-card__title {
  margin: 0;
  font-size: 20px;
}

.module-card__state {
  border-radius: var(--vp-radius-pill);
  padding: 4px 10px;
  font-size: 12px;
  font-weight: 700;
}

.module-card__state.is-enabled {
  background: var(--vp-bg-success);
  color: var(--vp-text-success);
}

.module-card__state.is-disabled {
  background: var(--vp-bg-danger);
  color: var(--vp-text-danger);
}

.module-card__meta {
  margin: 12px 0 0;
  color: var(--vp-text-secondary);
  font-size: 13px;
  line-height: 1.5;
}

.module-card__toggle {
  margin-top: 16px;
  width: 100%;
}

</style>
