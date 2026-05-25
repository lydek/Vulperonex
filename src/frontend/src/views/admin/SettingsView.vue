<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import {
  ApiError,
  getPluginModules,
  togglePluginModule,
  type PluginModule
} from "@/api/client";

const { t } = useI18n();

const modules = ref<PluginModule[]>([]);
const loading = ref(false);
const busyModule = ref<string | null>(null);
const errorCode = ref<string | null>(null);
const errorDetail = ref<string | null>(null);
const confirmOpen = ref(false);
const pendingTarget = ref<PluginModule | null>(null);
const pendingEnabled = ref(false);
const affectedModules = ref<PluginModule[]>([]);

onMounted(async () => {
  await loadModules();
});

const modulesByName = computed(() => new Map(modules.value.map((item) => [item.name, item])));
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
</script>

<template>
  <section aria-labelledby="settings-title">
    <header class="page-header">
      <h1 id="settings-title" class="page-title">{{ t("settings.modules.title") }}</h1>
      <p class="page-subtitle">{{ t("settings.modules.subtitle") }}</p>
    </header>

    <div class="settings-toolbar">
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
            <h2 class="module-card__title">{{ module.displayName }}</h2>
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
.settings-toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 16px;
}

.module-grid {
  display: grid;
  gap: 16px;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
}

.module-card {
  border: 1px solid #d6dde5;
  border-radius: 16px;
  padding: 18px;
  background: linear-gradient(180deg, #ffffff 0%, #f6faf9 100%);
  box-shadow: 0 10px 28px rgba(15, 23, 32, 0.08);
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
  color: #5f6f80;
}

.module-card__title {
  margin: 0;
  font-size: 20px;
}

.module-card__state {
  border-radius: 999px;
  padding: 4px 10px;
  font-size: 12px;
  font-weight: 700;
}

.module-card__state.is-enabled {
  background: #dcfce7;
  color: #166534;
}

.module-card__state.is-disabled {
  background: #fee2e2;
  color: #991b1b;
}

.module-card__meta {
  margin: 12px 0 0;
  color: #394756;
  font-size: 13px;
  line-height: 1.5;
}

.module-card__toggle {
  margin-top: 16px;
  width: 100%;
}

</style>
