<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import RuleEditorDrawer from "@/components/admin/RuleEditorDrawer.vue";
import {
  ApiError,
  deleteRule,
  getRule,
  getRules,
  setRuleEnabled,
  type WorkflowRuleDetail,
  type WorkflowRuleSummary
} from "@/api/client";

const { t } = useI18n();

const rules = ref<WorkflowRuleSummary[]>([]);
const selected = ref<WorkflowRuleDetail | null>(null);
const loadingList = ref(false);
const loadingDetail = ref(false);
const listError = ref<string | null>(null);
const detailError = ref<string | null>(null);
const conflictNotice = ref<string | null>(null);
const confirmDeleteId = ref<string | null>(null);
const deleting = ref(false);
const drawerRuleId = ref<string | null>(null);
const isDrawerOpen = ref(false);

onMounted(() => {
  void loadList();
});

async function loadList(): Promise<void> {
  loadingList.value = true;
  listError.value = null;
  try {
    rules.value = await getRules();
  } catch (caught) {
    rules.value = [];
    listError.value = describeError(caught);
  } finally {
    loadingList.value = false;
  }
}

async function selectRule(id: string): Promise<void> {
  loadingDetail.value = true;
  detailError.value = null;
  selected.value = null;
  try {
    selected.value = await getRule(id);
  } catch (caught) {
    detailError.value = describeError(caught);
  } finally {
    loadingDetail.value = false;
  }
}

async function toggleEnabled(rule: WorkflowRuleSummary): Promise<void> {
  conflictNotice.value = null;
  try {
    await setRuleEnabled(rule.id, !rule.isEnabled);
    await loadList();
    if (selected.value?.id === rule.id) {
      await selectRule(rule.id);
    }
  } catch (caught) {
    if (caught instanceof ApiError && caught.status === 409) {
      conflictNotice.value = t("rules.conflictMessage");
      await loadList();
    } else {
      conflictNotice.value = describeError(caught);
    }
  }
}

function requestDelete(id: string): void {
  conflictNotice.value = null;
  confirmDeleteId.value = id;
}

function openDrawer(id: string | null): void {
  conflictNotice.value = null;
  drawerRuleId.value = id;
  isDrawerOpen.value = true;
}

async function onDrawerSaved(id: string): Promise<void> {
  await loadList();
  await selectRule(id);
}

async function onConfirmDelete(): Promise<void> {
  const id = confirmDeleteId.value;
  if (!id) return;
  deleting.value = true;
  try {
    await deleteRule(id);
    if (selected.value?.id === id) {
      selected.value = null;
    }
    await loadList();
  } catch (caught) {
    if (caught instanceof ApiError && caught.status === 409) {
      conflictNotice.value = t("rules.conflictMessage");
    } else {
      conflictNotice.value = describeError(caught);
    }
  } finally {
    deleting.value = false;
    confirmDeleteId.value = null;
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
  <section aria-labelledby="rules-title">
    <header class="page-header">
      <h1 id="rules-title" class="page-title">{{ t("rules.title") }}</h1>
      <p class="page-subtitle">{{ t("rules.subtitle") }}</p>
    </header>

    <div class="members-toolbar">
      <button
        type="button"
        class="primary-button"
        :disabled="loadingList"
        @click="loadList"
      >
        {{ loadingList ? t("rules.loading") : t("rules.refresh") }}
      </button>
      <button
        type="button"
        class="primary-button"
        data-testid="rules-new"
        @click="openDrawer(null)"
      >
        {{ t("rules.new") }}
      </button>
    </div>

    <p
      v-if="conflictNotice"
      class="ack-error-code"
      role="alert"
      data-testid="rules-conflict"
    >
      {{ conflictNotice }}
    </p>

    <p
      v-if="listError"
      class="ack-error-code"
      role="alert"
      data-testid="rules-error"
    >
      {{ listError }}
    </p>

    <div class="members-layout">
      <div class="members-list-pane">
        <p
          v-if="!loadingList && rules.length === 0 && !listError"
          role="status"
          data-testid="rules-empty"
        >
          {{ t("rules.empty") }}
        </p>
        <table v-else-if="rules.length > 0" class="monitor-table" data-testid="rules-table">
          <thead>
            <tr>
              <th scope="col">{{ t("rules.col.name") }}</th>
              <th scope="col">{{ t("rules.col.eventType") }}</th>
              <th scope="col">{{ t("rules.col.enabled") }}</th>
              <th scope="col">{{ t("rules.col.priority") }}</th>
              <th scope="col">{{ t("rules.col.version") }}</th>
              <th scope="col">{{ t("rules.col.actions") }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="rule in rules"
              :key="rule.id"
              :class="['members-row', { 'members-row-selected': selected?.id === rule.id }]"
              data-testid="rules-row"
            >
              <td class="monitor-mono" @click="selectRule(rule.id)">{{ rule.name }}</td>
              <td class="monitor-mono" @click="selectRule(rule.id)">{{ rule.eventTypeKey }}</td>
              <td class="monitor-mono" @click="selectRule(rule.id)">{{ rule.isEnabled }}</td>
              <td class="monitor-mono" @click="selectRule(rule.id)">{{ rule.priority }}</td>
              <td class="monitor-mono" @click="selectRule(rule.id)">{{ rule.version }}</td>
              <td>
                <div class="rules-row-actions">
                  <button
                    type="button"
                    class="icon-button"
                    :data-testid="`edit-${rule.id}`"
                    @click.stop="openDrawer(rule.id)"
                  >
                    {{ t("rules.edit") }}
                  </button>
                  <button
                    type="button"
                    class="icon-button"
                    :data-testid="`toggle-${rule.id}`"
                    @click.stop="toggleEnabled(rule)"
                  >
                    {{ rule.isEnabled ? t("rules.disable") : t("rules.enable") }}
                  </button>
                  <button
                    type="button"
                    class="icon-button"
                    :data-testid="`delete-${rule.id}`"
                    @click.stop="requestDelete(rule.id)"
                  >
                    {{ t("rules.delete") }}
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <aside class="members-detail-pane" aria-label="rule-detail">
        <p
          v-if="!selected && !loadingDetail && !detailError"
          role="status"
          data-testid="rules-detail-empty"
        >
          {{ t("rules.detail.empty") }}
        </p>

        <p v-if="loadingDetail" role="status">{{ t("rules.loading") }}</p>

        <p
          v-if="detailError"
          class="ack-error-code"
          role="alert"
          data-testid="rules-detail-error"
        >
          {{ detailError }}
        </p>

        <article v-if="selected" class="status-card" data-testid="rules-detail">
          <p class="status-label">{{ t("rules.detail.id") }}</p>
          <p class="status-value monitor-mono">{{ selected.id }}</p>

          <p class="status-label">{{ t("rules.detail.createdAt") }}</p>
          <p class="monitor-mono">{{ selected.createdAt }}</p>

          <p class="status-label">{{ t("rules.detail.actions") }}</p>
          <pre class="rules-json">{{ JSON.stringify(selected.actions, null, 2) }}</pre>

          <p class="status-label">{{ t("rules.detail.conditions") }}</p>
          <pre class="rules-json">{{ JSON.stringify(selected.conditions, null, 2) }}</pre>
        </article>
      </aside>
    </div>

    <ConfirmDialog
      :open="confirmDeleteId !== null"
      :title="t('rules.deleteConfirmTitle')"
      :message="t('rules.deleteConfirmMessage', { id: confirmDeleteId ?? '' })"
      :confirm-label="t('rules.deleteConfirmAction')"
      :cancel-label="t('common.cancel')"
      :busy="deleting"
      @confirm="onConfirmDelete"
      @cancel="confirmDeleteId = null"
    />

    <RuleEditorDrawer
      v-model:open="isDrawerOpen"
      :rule-id="drawerRuleId"
      @saved="onDrawerSaved"
    />
  </section>
</template>
