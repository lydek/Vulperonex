<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import {
  ApiError,
  createTimer,
  deleteTimer,
  getTimer,
  getTimers,
  updateTimer,
  type WorkflowTimerDto
} from "@/api/client";

const { t } = useI18n();

const timers = ref<WorkflowTimerDto[]>([]);
const loading = ref(false);
const submitting = ref(false);
const error = ref<string | null>(null);
const confirmDeleteId = ref<string | null>(null);
const selectedTimer = ref<WorkflowTimerDto | null>(null);
const ruleId = ref("");
const intervalSeconds = ref(30);
const isEnabled = ref(true);
const nextFireAt = ref(new Date(Date.now() + 30_000).toISOString());
const editRuleId = ref("");
const editIntervalSeconds = ref(30);
const editIsEnabled = ref(true);
const editNextFireAt = ref("");

onMounted(() => {
  void loadTimers();
});

async function loadTimers(): Promise<void> {
  loading.value = true;
  error.value = null;
  try {
    timers.value = await getTimers();
    if (selectedTimer.value && !timers.value.some((timer) => timer.id === selectedTimer.value?.id)) {
      selectedTimer.value = null;
    }
  } catch (caught) {
    error.value = describeError(caught);
  } finally {
    loading.value = false;
  }
}

async function onCreate(): Promise<void> {
  submitting.value = true;
  error.value = null;
  try {
    await createTimer({
      ruleId: ruleId.value.trim(),
      intervalSeconds: intervalSeconds.value,
      isEnabled: isEnabled.value,
      nextFireAt: nextFireAt.value
    });
    ruleId.value = "";
    await loadTimers();
  } catch (caught) {
    error.value = describeError(caught);
  } finally {
    submitting.value = false;
  }
}

async function selectTimer(id: string): Promise<void> {
  error.value = null;
  try {
    selectedTimer.value = await getTimer(id);
    syncEditForm(selectedTimer.value);
  } catch (caught) {
    error.value = describeError(caught);
  }
}

async function onUpdate(): Promise<void> {
  if (!selectedTimer.value) return;
  submitting.value = true;
  error.value = null;
  try {
    const updated = await updateTimer(selectedTimer.value.id, {
      ruleId: editRuleId.value.trim(),
      intervalSeconds: editIntervalSeconds.value,
      isEnabled: editIsEnabled.value,
      nextFireAt: editNextFireAt.value
    });
    selectedTimer.value = updated;
    syncEditForm(updated);
    await loadTimers();
  } catch (caught) {
    error.value = describeError(caught);
  } finally {
    submitting.value = false;
  }
}

async function onConfirmDelete(): Promise<void> {
  const id = confirmDeleteId.value;
  if (!id) return;
  try {
    await deleteTimer(id);
    if (selectedTimer.value?.id === id) {
      selectedTimer.value = null;
    }
    await loadTimers();
  } catch (caught) {
    error.value = describeError(caught);
  } finally {
    confirmDeleteId.value = null;
  }
}

function syncEditForm(timer: WorkflowTimerDto): void {
  editRuleId.value = timer.ruleId;
  editIntervalSeconds.value = timer.intervalSeconds;
  editIsEnabled.value = timer.isEnabled;
  editNextFireAt.value = timer.nextFireAt;
}

function describeError(caught: unknown): string {
  if (caught instanceof ApiError) {
    return caught.errorCode ?? `HTTP_${caught.status}`;
  }
  return caught instanceof Error ? caught.message : String(caught);
}
</script>

<template>
  <section aria-labelledby="timers-title">
    <header class="page-header">
      <h1 id="timers-title" class="page-title">{{ t("timers.title") }}</h1>
      <p class="page-subtitle">{{ t("timers.subtitle") }}</p>
    </header>

    <form class="members-toolbar" data-testid="timer-form" @submit.prevent="onCreate">
      <label class="form-field">
        <span class="form-label">{{ t("timers.ruleId") }}</span>
        <input v-model="ruleId" required type="text" data-testid="timer-rule-id" />
      </label>
      <label class="form-field">
        <span class="form-label">{{ t("timers.intervalSeconds") }}</span>
        <input v-model.number="intervalSeconds" required min="1" type="number" />
      </label>
      <label class="form-field">
        <span class="form-label">{{ t("timers.nextFireAt") }}</span>
        <input v-model="nextFireAt" required type="datetime-local" />
      </label>
      <label class="form-field form-field-inline">
        <input v-model="isEnabled" type="checkbox" />
        <span>{{ t("timers.isEnabled") }}</span>
      </label>
      <button type="submit" class="primary-button" :disabled="submitting">
        {{ submitting ? t("timers.creating") : t("timers.create") }}
      </button>
      <button type="button" class="secondary-button" :disabled="loading" @click="loadTimers">
        {{ t("rules.refresh") }}
      </button>
    </form>

    <p v-if="error" class="ack-error-code" role="alert">{{ error }}</p>
    <p v-if="!loading && timers.length === 0 && !error" role="status">{{ t("timers.empty") }}</p>

    <table v-if="timers.length > 0" class="monitor-table" data-testid="timers-table">
      <thead>
        <tr>
          <th scope="col">{{ t("timers.id") }}</th>
          <th scope="col">{{ t("timers.ruleId") }}</th>
          <th scope="col">{{ t("timers.intervalSeconds") }}</th>
          <th scope="col">{{ t("timers.nextFireAt") }}</th>
          <th scope="col">{{ t("timers.isEnabled") }}</th>
          <th scope="col">{{ t("rules.col.actions") }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="timer in timers" :key="timer.id">
          <td class="monitor-mono">{{ timer.id }}</td>
          <td class="monitor-mono">{{ timer.ruleId }}</td>
          <td class="monitor-mono">{{ timer.intervalSeconds }}</td>
          <td class="monitor-mono">{{ timer.nextFireAt }}</td>
          <td class="monitor-mono">{{ timer.isEnabled }}</td>
          <td>
            <button
              type="button"
              class="icon-button"
              :data-testid="`timer-show-${timer.id}`"
              @click="void selectTimer(timer.id)"
            >
              {{ t("timers.show") }}
            </button>
            <button type="button" class="icon-button" @click="confirmDeleteId = timer.id">
              {{ t("rules.delete") }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>

    <section class="status-card" aria-labelledby="timer-detail-title">
      <h2 id="timer-detail-title" class="section-title">{{ t("timers.detail.title") }}</h2>
      <p v-if="!selectedTimer" role="status">{{ t("timers.detail.empty") }}</p>
      <form
        v-else
        class="members-toolbar"
        data-testid="timer-edit-form"
        @submit.prevent="onUpdate"
      >
        <p class="monitor-mono">{{ selectedTimer.id }}</p>
        <label class="form-field">
          <span class="form-label">{{ t("timers.ruleId") }}</span>
          <input v-model="editRuleId" required type="text" data-testid="timer-edit-rule-id" />
        </label>
        <label class="form-field">
          <span class="form-label">{{ t("timers.intervalSeconds") }}</span>
          <input v-model.number="editIntervalSeconds" required min="1" type="number" />
        </label>
        <label class="form-field">
          <span class="form-label">{{ t("timers.nextFireAt") }}</span>
          <input v-model="editNextFireAt" required type="datetime-local" />
        </label>
        <label class="form-field form-field-inline">
          <input v-model="editIsEnabled" type="checkbox" />
          <span>{{ t("timers.isEnabled") }}</span>
        </label>
        <button type="submit" class="primary-button" :disabled="submitting">
          {{ submitting ? t("timers.saving") : t("timers.save") }}
        </button>
      </form>
    </section>

    <ConfirmDialog
      :open="confirmDeleteId !== null"
      :title="t('timers.deleteConfirmTitle')"
      :message="t('timers.deleteConfirmMessage', { id: confirmDeleteId ?? '' })"
      :confirm-label="t('rules.delete')"
      :cancel-label="t('common.cancel')"
      @confirm="onConfirmDelete"
      @cancel="confirmDeleteId = null"
    />
  </section>
</template>
