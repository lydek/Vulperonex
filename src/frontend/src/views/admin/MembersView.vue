<script setup lang="ts">
import { onMounted, ref, onBeforeUnmount } from "vue";
import { useI18n } from "vue-i18n";
import {
  ApiError,
  getMember,
  getMembers,
  getMemberAuditLogs,
  adjustMemberLoyalty,
  resetMemberLoyalty,
  generateDeleteToken,
  deleteMemberWithToken,
  getConfigValue,
  setConfigValue,
  type MemberReadModel,
  type MemberAuditLog
} from "@/api/client";

const { t } = useI18n();

const members = ref<MemberReadModel[]>([]);
const selected = ref<MemberReadModel | null>(null);
const platformFilter = ref<string>("");
const loadingList = ref(false);
const loadingDetail = ref(false);
const listError = ref<string | null>(null);
const detailError = ref<string | null>(null);

const backgroundUrl = ref("");
const stampUrl = ref("");
const savingSettings = ref(false);
const saveMessage = ref<string | null>(null);
const saveError = ref<string | null>(null);

// --- Adjust Loyalty Modal State ---
const showAdjustModal = ref(false);
const adjustTotal = ref<number | null>(null);
const adjustCheckIn = ref<number | null>(null);
const adjustReason = ref("");
const adjustError = ref<string | null>(null);
const adjusting = ref(false);

// --- Reset Modal State ---
const showResetModal = ref(false);
const resetLoyalty = ref(false);
const resetCheckIn = ref(false);
const resetReason = ref("");
const resetError = ref<string | null>(null);
const resetting = ref(false);

// --- Safe Delete Modal State ---
const showDeleteModal = ref(false);
const deleteToken = ref("");
const deleteReason = ref("");
const deleteError = ref<string | null>(null);
const deleting = ref(false);
const deleteSecondsLeft = ref(30);
let deleteTimerInterval: number | null = null;

// --- Audit Log Drawer State ---
const showAuditDrawer = ref(false);
const auditLogs = ref<MemberAuditLog[]>([]);
const loadingAudit = ref(false);
const auditError = ref<string | null>(null);

async function loadCardSettings(): Promise<void> {
  try {
    const bgData = await getConfigValue("overlay.member.background_url");
    backgroundUrl.value = bgData.value || "";
    
    const stampData = await getConfigValue("overlay.member.stamp_url");
    stampUrl.value = stampData.value || "";
  } catch (caught) {
    saveError.value = t("members.cardSettings.loadFailed") + ": " + (caught instanceof Error ? caught.message : String(caught));
  }
}

async function saveCardSettings(): Promise<void> {
  savingSettings.value = true;
  saveMessage.value = null;
  saveError.value = null;
  try {
    await setConfigValue("overlay.member.background_url", backgroundUrl.value.trim());
    await setConfigValue("overlay.member.stamp_url", stampUrl.value.trim());
    
    saveMessage.value = t("members.cardSettings.saveSuccess");
    setTimeout(() => { saveMessage.value = null; }, 3000);
  } catch (caught) {
    saveError.value = caught instanceof Error ? caught.message : String(caught);
  } finally {
    savingSettings.value = false;
  }
}

onMounted(() => {
  void loadList();
  void loadCardSettings();
});

onBeforeUnmount(() => {
  if (deleteTimerInterval) {
    clearInterval(deleteTimerInterval);
  }
});

async function loadList(): Promise<void> {
  loadingList.value = true;
  listError.value = null;
  try {
    const query = platformFilter.value.trim()
      ? { platform: platformFilter.value.trim() }
      : {};
    members.value = await getMembers(query);
  } catch (caught) {
    members.value = [];
    listError.value = describeError(caught);
  } finally {
    loadingList.value = false;
  }
}

async function selectMember(memberId: string): Promise<void> {
  loadingDetail.value = true;
  detailError.value = null;
  selected.value = null;
  try {
    selected.value = await getMember(memberId);
  } catch (caught) {
    detailError.value = describeError(caught);
  } finally {
    loadingDetail.value = false;
  }
}

// --- Adjust Loyalty Actions ---
function openAdjustModal() {
  if (!selected.value) return;
  adjustTotal.value = selected.value.loyalty.totalLoyalty;
  adjustCheckIn.value = selected.value.loyalty.checkInCount;
  adjustReason.value = "";
  adjustError.value = null;
  showAdjustModal.value = true;
}

async function submitAdjust() {
  if (!selected.value) return;
  const reasonStr = adjustReason.value.trim();
  if (!reasonStr || reasonStr.length < 3 || reasonStr.length > 500) {
    adjustError.value = t("members.error.adjustReasonRequired");
    return;
  }
  
  adjusting.value = true;
  adjustError.value = null;
  try {
    await adjustMemberLoyalty(
      selected.value.memberId,
      selected.value.etag || "",
      {
        totalLoyalty: adjustTotal.value ?? undefined,
        checkInCount: adjustCheckIn.value ?? undefined,
        reason: reasonStr
      }
    );
    showAdjustModal.value = false;
    await selectMember(selected.value.memberId);
    await loadList();
  } catch (caught) {
    if (caught instanceof ApiError && caught.errorCode === "MEMBER_CONCURRENCY_CONFLICT") {
      adjustError.value = t("errorCode.MEMBER_CONCURRENCY_CONFLICT");
    // Optimistic lock conflict, update current model immediately to help user get latest version to edit again
      await selectMember(selected.value.memberId);
    } else {
      adjustError.value = describeError(caught);
    }
  } finally {
    adjusting.value = false;
  }
}

// --- Reset Data Actions ---
function openResetModal() {
  if (!selected.value) return;
  resetLoyalty.value = false;
  resetCheckIn.value = false;
  resetReason.value = "";
  resetError.value = null;
  showResetModal.value = true;
}

async function submitReset() {
  if (!selected.value) return;
  if (!resetLoyalty.value && !resetCheckIn.value) {
    resetError.value = t("members.error.selectResetItem");
    return;
  }
  const reasonStr = resetReason.value.trim();
  if (!reasonStr || reasonStr.length < 3 || reasonStr.length > 500) {
    resetError.value = t("members.error.changeReasonRequired");
    return;
  }

  resetting.value = true;
  resetError.value = null;
  try {
    await resetMemberLoyalty(
      selected.value.memberId,
      selected.value.etag || "",
      {
        resetLoyalty: resetLoyalty.value,
        resetCheckIn: resetCheckIn.value,
        reason: reasonStr
      }
    );
    showResetModal.value = false;
    await selectMember(selected.value.memberId);
    await loadList();
  } catch (caught) {
    if (caught instanceof ApiError && caught.errorCode === "MEMBER_CONCURRENCY_CONFLICT") {
      resetError.value = t("errorCode.MEMBER_CONCURRENCY_CONFLICT");
      await selectMember(selected.value.memberId);
    } else {
      resetError.value = describeError(caught);
    }
  } finally {
    resetting.value = false;
  }
}

// --- Safe Delete Actions (30s Token) ---
async function startDelete() {
  if (!selected.value) return;
  deleting.value = true;
  deleteError.value = null;
  deleteReason.value = "";
  try {
    const token = await generateDeleteToken(selected.value.memberId);
    deleteToken.value = token;
    deleteSecondsLeft.value = 30;
    showDeleteModal.value = true;

    if (deleteTimerInterval) {
      clearInterval(deleteTimerInterval);
    }
    deleteTimerInterval = window.setInterval(() => {
      if (deleteSecondsLeft.value > 0) {
        deleteSecondsLeft.value--;
      } else {
        if (deleteTimerInterval) {
          clearInterval(deleteTimerInterval);
          deleteTimerInterval = null;
        }
      }
    }, 1000);
  } catch (caught) {
    deleteError.value = describeError(caught);
  } finally {
    deleting.value = false;
  }
}

async function submitDelete() {
  if (!selected.value) return;
  if (deleteSecondsLeft.value <= 0) {
    deleteError.value = t("members.delete.expired");
    return;
  }
  const reasonStr = deleteReason.value.trim();
  if (!reasonStr || reasonStr.length < 3 || reasonStr.length > 500) {
    deleteError.value = t("members.error.deleteReasonRequired");
    return;
  }

  deleting.value = true;
  deleteError.value = null;
  try {
    await deleteMemberWithToken(
      selected.value.memberId,
      deleteToken.value,
      reasonStr
    );
    showDeleteModal.value = false;
    if (deleteTimerInterval) {
      clearInterval(deleteTimerInterval);
      deleteTimerInterval = null;
    }
    selected.value = null;
    await loadList();
  } catch (caught) {
    deleteError.value = describeError(caught);
  } finally {
    deleting.value = false;
  }
}

// --- Audit Log Query ---
async function openAuditDrawer() {
  if (!selected.value) return;
  showAuditDrawer.value = true;
  loadingAudit.value = true;
  auditError.value = null;
  auditLogs.value = [];
  try {
    auditLogs.value = await getMemberAuditLogs(selected.value.memberId, 100, 0);
  } catch (caught) {
    auditError.value = describeError(caught);
  } finally {
    loadingAudit.value = false;
  }
}

function formatDate(isoString: string): string {
  try {
    return new Date(isoString).toLocaleString();
  } catch {
    return isoString;
  }
}

function describeError(caught: unknown): string {
  if (caught instanceof ApiError) {
    return t(`errorCode.${caught.errorCode}`) || caught.errorCode || `HTTP_${caught.status}`;
  }
  return caught instanceof Error ? caught.message : String(caught);
}
</script>

<template>
  <section aria-labelledby="members-title" class="members-view-container">
    <header class="page-header">
      <h1 id="members-title" class="page-title">{{ t("members.title") }}</h1>
      <p class="page-subtitle">{{ t("members.subtitle") }}</p>
    </header>

    <div class="card-settings-panel">
      <h2 class="settings-panel-title">{{ t("members.cardSettings.title") }}</h2>
      <div class="settings-fields">
        <label class="form-field flex-1">
          <span class="form-label">{{ t("members.cardSettings.background.label") }}</span>
          <input
            v-model="backgroundUrl"
            type="text"
            :placeholder="t('members.cardSettings.background.placeholder')"
            autocomplete="off"
          />
        </label>
        <label class="form-field flex-1">
          <span class="form-label">{{ t("members.cardSettings.stamp.label") }}</span>
          <input
            v-model="stampUrl"
            type="text"
            :placeholder="t('members.cardSettings.stamp.placeholder')"
            autocomplete="off"
          />
        </label>
        <button
          type="button"
          class="primary-button settings-save-btn"
          :disabled="savingSettings"
          @click="saveCardSettings"
        >
          {{ savingSettings ? t("members.cardSettings.saving") : t("members.cardSettings.save") }}
        </button>
      </div>
      <p v-if="saveMessage" class="settings-success-msg" role="status">{{ saveMessage }}</p>
      <p v-if="saveError" class="settings-error-msg" role="alert">{{ saveError }}</p>
    </div>

    <form class="members-toolbar" @submit.prevent="loadList">
      <label class="form-field">
        <span class="form-label">{{ t("members.filterPlatform") }}</span>
        <input
          v-model="platformFilter"
          type="text"
          autocomplete="off"
          :aria-label="t('members.filterPlatform')"
        />
      </label>
      <button type="submit" class="primary-button" :disabled="loadingList">
        {{ loadingList ? t("members.loading") : t("members.refresh") }}
      </button>
    </form>

    <p
      v-if="listError"
      class="ack-error-code"
      role="alert"
      data-testid="members-error"
    >
      {{ listError }}
    </p>

    <div class="members-layout">
      <!-- Members List Pane -->
      <div class="members-list-pane">
        <p
          v-if="!loadingList && members.length === 0 && !listError"
          role="status"
          data-testid="members-empty"
        >
          {{ t("members.empty") }}
        </p>
        <table v-else-if="members.length > 0" class="monitor-table" data-testid="members-table">
          <thead>
            <tr>
              <th scope="col">{{ t("members.col.memberId") }}</th>
              <th scope="col">{{ t("members.col.platforms") }}</th>
              <th scope="col">{{ t("members.col.loyalty") }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="member in members"
              :key="member.memberId"
              :class="['members-row', { 'members-row-selected': selected?.memberId === member.memberId }]"
              data-testid="members-row"
              tabindex="0"
              role="button"
              :aria-label="t('members.col.memberId') + ': ' + member.memberId"
              @click="selectMember(member.memberId)"
              @keydown.enter.prevent="selectMember(member.memberId)"
              @keydown.space.prevent="selectMember(member.memberId)"
            >
              <td class="monitor-mono">{{ member.memberId }}</td>
              <td class="monitor-mono">
                {{ member.identities.map((identity) => identity.platform).join(", ") || "-" }}
              </td>
              <td class="monitor-mono">{{ member.loyalty.totalLoyalty }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Member Details & Action Pane -->
      <aside class="members-detail-pane" aria-label="member-detail">
        <p
          v-if="!selected && !loadingDetail && !detailError"
          role="status"
          data-testid="members-detail-empty"
          class="empty-detail-hint"
        >
          {{ t("members.detail.empty") }}
        </p>

        <p v-if="loadingDetail" role="status" class="empty-detail-hint">{{ t("members.loading") }}</p>

        <p
          v-if="detailError"
          class="ack-error-code"
          role="alert"
          data-testid="members-detail-error"
        >
          {{ detailError }}
        </p>

        <article v-if="selected" class="status-card glow-panel" data-testid="members-detail">
          <div class="detail-header-row">
            <h3 class="detail-sec-title monitor-mono">{{ selected.memberId }}</h3>
            <span class="version-badge">v.{{ selected.updatedAtTicks }}</span>
          </div>

          <div class="detail-section">
            <p class="status-label">{{ t("members.detail.loyalty") }}</p>
            <p class="monitor-mono loyalty-summary">
              {{ t("members.detail.loyaltyValue", {
                total: selected.loyalty.totalLoyalty,
                checkIn: selected.loyalty.checkInCount
              }) }}
            </p>
          </div>

          <div class="detail-section">
            <p class="status-label">{{ t("members.detail.identities") }}</p>
            <ul class="identity-list">
              <li
                v-for="identity in selected.identities"
                :key="`${identity.platform}-${identity.platformUserId}`"
                class="identity-item monitor-mono"
              >
                <span class="platform-name-tag">{{ identity.platform }}</span>
                <span class="platform-id-value"> · {{ identity.platformUserId }}</span>
              </li>
            </ul>
          </div>

          <!-- Action Buttons Section -->
          <div class="detail-actions-sec">
            <button class="action-btn adjust-btn" @click="openAdjustModal">
              {{ t("members.actions.adjust") }}
            </button>
            <button class="action-btn reset-btn" @click="openResetModal">
              {{ t("members.actions.reset") }}
            </button>
            <button class="action-btn audit-btn" @click="openAuditDrawer">
              {{ t("members.actions.audit") }}
            </button>
            <button class="action-btn delete-btn" @click="startDelete">
              {{ t("members.actions.delete") }}
            </button>
          </div>
        </article>
      </aside>
    </div>

    <!-- 1. Adjust Loyalty Dialog (Modal) -->
    <div v-if="showAdjustModal" class="modal-overlay" @click.self="showAdjustModal = false">
      <div class="modal-content glow-panel" role="dialog" aria-modal="true">
        <h3 class="modal-title">{{ t("members.adjust.title") }}</h3>
        
        <div class="modal-fields">
          <label class="form-field">
            <span class="form-label">{{ t("members.adjust.total") }}</span>
            <input v-model.number="adjustTotal" type="number" min="0" />
          </label>
          <label class="form-field">
            <span class="form-label">{{ t("members.adjust.checkIn") }}</span>
            <input v-model.number="adjustCheckIn" type="number" min="0" />
          </label>
          <label class="form-field">
            <span class="form-label">{{ t("members.dialog.reason") }}</span>
            <textarea
              v-model="adjustReason"
              :placeholder="t('members.dialog.reasonPlaceholder')"
              rows="3"
            ></textarea>
          </label>
        </div>

        <p v-if="adjustError" class="modal-error-msg" role="alert">{{ adjustError }}</p>

        <div class="modal-actions">
          <button class="action-btn cancel-btn" :disabled="adjusting" @click="showAdjustModal = false">
            {{ t("members.dialog.cancel") }}
          </button>
          <button class="action-btn confirm-btn adjust-confirm" :disabled="adjusting" @click="submitAdjust">
            {{ adjusting ? t("members.loading") : t("members.dialog.submit") }}
          </button>
        </div>
      </div>
    </div>

    <!-- 2. Reset Data Dialog (Modal) -->
    <div v-if="showResetModal" class="modal-overlay" @click.self="showResetModal = false">
      <div class="modal-content glow-panel" role="dialog" aria-modal="true">
        <h3 class="modal-title">{{ t("members.reset.title") }}</h3>

        <div class="modal-fields">
          <label class="checkbox-field">
            <input v-model="resetLoyalty" type="checkbox" />
            <span class="checkbox-label">{{ t("members.reset.loyalty") }}</span>
          </label>
          <label class="checkbox-field">
            <input v-model="resetCheckIn" type="checkbox" />
            <span class="checkbox-label">{{ t("members.reset.checkIn") }}</span>
          </label>
          <label class="form-field">
            <span class="form-label">{{ t("members.dialog.reason") }}</span>
            <textarea
              v-model="resetReason"
              :placeholder="t('members.dialog.reasonPlaceholder')"
              rows="3"
            ></textarea>
          </label>
        </div>

        <p v-if="resetError" class="modal-error-msg" role="alert">{{ resetError }}</p>

        <div class="modal-actions">
          <button class="action-btn cancel-btn" :disabled="resetting" @click="showResetModal = false">
            {{ t("members.dialog.cancel") }}
          </button>
          <button class="action-btn confirm-btn reset-confirm" :disabled="resetting" @click="submitReset">
            {{ resetting ? t("members.loading") : t("members.dialog.submit") }}
          </button>
        </div>
      </div>
    </div>

    <!-- 3. Safe Delete Dialog (Modal with 30s Countdown) -->
    <div v-if="showDeleteModal" class="modal-overlay" @click.self="showDeleteModal = false">
      <div class="modal-content glow-panel border-danger" role="dialog" aria-modal="true">
        <h3 class="modal-title text-danger">{{ t("members.delete.title") }}</h3>
        
        <div class="danger-banner">
          <p>{{ t("members.delete.warning") }}</p>
          <!-- Countdown Timer Visual Bar and Seconds -->
          <div class="timer-section">
            <div class="timer-progress-bg">
              <div
                class="timer-progress-fill"
                :style="{ width: `${(deleteSecondsLeft / 30) * 100}%` }"
                :class="{ 'bg-danger-pulse': deleteSecondsLeft <= 10 }"
              ></div>
            </div>
            <p class="timer-countdown-text">
              {{ t("members.delete.timer", { seconds: deleteSecondsLeft }) }}
            </p>
          </div>
        </div>

        <div class="modal-fields">
          <label class="form-field">
            <span class="form-label">{{ t("members.delete.token") }}</span>
            <input v-model="deleteToken" type="text" disabled class="token-input" />
          </label>
          <label class="form-field">
            <span class="form-label">{{ t("members.dialog.reason") }}</span>
            <textarea
              v-model="deleteReason"
              :placeholder="t('members.dialog.reasonPlaceholder')"
              rows="3"
              :disabled="deleteSecondsLeft <= 0"
            ></textarea>
          </label>
        </div>

        <p v-if="deleteError" class="modal-error-msg" role="alert">{{ deleteError }}</p>

        <div class="modal-actions">
          <button class="action-btn cancel-btn" :disabled="deleting" @click="showDeleteModal = false">
            {{ t("members.dialog.cancel") }}
          </button>
          <button
            class="action-btn confirm-danger-btn delete-confirm"
            :disabled="deleting || deleteSecondsLeft <= 0"
            @click="submitDelete"
          >
            {{ deleting ? t("members.loading") : t("members.dialog.submit") }}
          </button>
        </div>
      </div>
    </div>

    <!-- 4. Audit Log Drawer -->
    <div v-if="showAuditDrawer" class="drawer-overlay" @click.self="showAuditDrawer = false">
      <div class="drawer-content glow-panel" role="complementary" aria-label="Audit Logs">
        <div class="drawer-header">
          <h3 class="drawer-title">{{ t("members.audit.title") }}</h3>
          <button class="close-drawer-btn" @click="showAuditDrawer = false">&times;</button>
        </div>

        <div class="drawer-body">
          <p v-if="loadingAudit" class="drawer-loading-text">{{ t("members.loading") }}</p>
          <p v-if="auditError" class="ack-error-code" role="alert">{{ auditError }}</p>

          <p v-if="!loadingAudit && auditLogs.length === 0 && !auditError" class="drawer-empty-text">
            {{ t("members.audit.empty") }}
          </p>

          <!-- Professional Timeline Log Presentation -->
          <div v-if="auditLogs.length > 0" class="timeline-container">
            <div v-for="log in auditLogs" :key="log.id" class="timeline-item">
              <div class="timeline-dot-connector">
                <span class="timeline-dot" :class="log.operation"></span>
                <span class="timeline-connector"></span>
              </div>
              <div class="timeline-card glow-panel">
                <div class="timeline-header-row">
                  <span class="log-op-tag" :class="log.operation">{{ log.operation.toUpperCase() }}</span>
                  <span class="log-time monitor-mono">{{ formatDate(log.occurredAt) }}</span>
                </div>
                <p class="log-reason-text"><strong>{{ t("members.audit.reason") }}：</strong>{{ log.reason }}</p>
                <div class="log-actor-kind monitor-mono">
                  <span><strong>{{ t("members.audit.actor") }}：</strong>{{ log.actorKind }}</span>
                  <span v-if="log.actorId">({{ log.actorId }})</span>
                </div>

                <!-- Snapshot Comparison Panel -->
                <div class="log-snap-sec" v-if="log.beforeJson || log.afterJson">
                  <div class="snap-box" v-if="log.beforeJson">
                    <p class="snap-title text-muted">{{ t("members.audit.before") }}</p>
                    <pre class="snap-content font-mono">{{ JSON.stringify(JSON.parse(log.beforeJson), null, 2) }}</pre>
                  </div>
                  <div class="snap-arrow" v-if="log.beforeJson && log.afterJson">&rarr;</div>
                  <div class="snap-box" v-if="log.afterJson">
                    <p class="snap-title text-success">{{ t("members.audit.after") }}</p>
                    <pre class="snap-content font-mono">{{ JSON.stringify(JSON.parse(log.afterJson), null, 2) }}</pre>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="drawer-footer">
          <button class="action-btn audit-close-btn" @click="showAuditDrawer = false">
            {{ t("members.audit.close") }}
          </button>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
/* Core Container */
.members-view-container {
  position: relative;
  min-height: 80vh;
}

/* Top Visual Panel */
.card-settings-panel {
  background: rgba(30, 41, 59, 0.45);
  border: 1px solid rgba(189, 232, 232, 0.12);
  border-radius: 16px;
  padding: 24px;
  margin-bottom: 24px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
  backdrop-filter: blur(10px);
}
.settings-panel-title {
  margin-top: 0;
  margin-bottom: 20px;
  font-size: 18px;
  font-weight: 700;
  color: #BDE8E8;
  letter-spacing: 0.5px;
  text-shadow: 0 0 10px rgba(189, 232, 232, 0.3);
}
.settings-fields {
  display: flex;
  gap: 20px;
  align-items: flex-end;
  flex-wrap: wrap;
}
.flex-1 {
  flex: 1;
  min-width: 280px;
}
.settings-save-btn {
  height: 42px;
  white-space: nowrap;
  box-shadow: 0 4px 15px rgba(6, 182, 212, 0.25);
}

/* Table & Layout */
.members-layout {
  display: flex;
  gap: 24px;
  align-items: flex-start;
  margin-top: 24px;
}
.members-list-pane {
  flex: 3;
  background: rgba(30, 41, 59, 0.3);
  border: 1px solid rgba(189, 232, 232, 0.08);
  border-radius: 14px;
  overflow: hidden;
}
.members-row {
  cursor: pointer;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}
.members-row:hover {
  background: rgba(189, 232, 232, 0.06);
}
.members-row-selected {
  background: rgba(6, 182, 212, 0.15) !important;
  border-left: 4px solid #06b6d4;
}

/* Detail & Action Card */
.members-detail-pane {
  flex: 2;
  position: sticky;
  top: 20px;
}
.empty-detail-hint {
  text-align: center;
  padding: 40px;
  color: rgba(189, 232, 232, 0.4);
  font-style: italic;
}
.glow-panel {
  background: rgba(30, 41, 59, 0.65);
  border: 1px solid rgba(189, 232, 232, 0.15);
  border-radius: 18px;
  backdrop-filter: blur(12px);
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.35);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}
.glow-panel:hover {
  box-shadow: 0 15px 45px rgba(6, 182, 212, 0.15);
}
.status-card {
  padding: 24px;
}
.detail-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid rgba(189, 232, 232, 0.1);
  padding-bottom: 16px;
  margin-bottom: 20px;
}
.detail-sec-title {
  font-size: 20px;
  margin: 0;
  color: #BDE8E8;
  font-weight: 700;
}
.version-badge {
  font-size: 11px;
  background: rgba(6, 182, 212, 0.15);
  color: #06b6d4;
  padding: 4px 10px;
  border-radius: 12px;
  border: 1px solid rgba(6, 182, 212, 0.25);
}
.detail-section {
  margin-bottom: 20px;
}
.loyalty-summary {
  font-size: 18px;
  color: #10b981;
  font-weight: 600;
  text-shadow: 0 0 10px rgba(16, 185, 129, 0.2);
}
.identity-list {
  list-style: none;
  padding: 0;
  margin: 0;
}
.identity-item {
  padding: 8px 12px;
  background: rgba(30, 41, 59, 0.4);
  border-radius: 8px;
  margin-bottom: 8px;
  border: 1px solid rgba(189, 232, 232, 0.05);
}
.platform-name-tag {
  color: #06b6d4;
  font-weight: 700;
}

/* Action Buttons Section */
.detail-actions-sec {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-top: 24px;
  border-top: 1px solid rgba(189, 232, 232, 0.1);
  padding-top: 20px;
}
.action-btn {
  padding: 10px 16px;
  border-radius: 10px;
  font-weight: 600;
  font-size: 13px;
  cursor: pointer;
  border: none;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  display: flex;
  justify-content: center;
  align-items: center;
  text-align: center;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}
.action-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 15px rgba(0,0,0,0.2);
}
.adjust-btn {
  background: linear-gradient(135deg, #0f766e, #0d9488);
  color: #fff;
}
.adjust-btn:hover {
  background: linear-gradient(135deg, #0d9488, #14b8a6);
}
.reset-btn {
  background: linear-gradient(135deg, #374151, #4b5563);
  color: #fff;
}
.reset-btn:hover {
  background: linear-gradient(135deg, #4b5563, #6b7280);
}
.audit-btn {
  background: linear-gradient(135deg, #1e3a8a, #2563eb);
  color: #fff;
}
.audit-btn:hover {
  background: linear-gradient(135deg, #2563eb, #3b82f6);
}
.delete-btn {
  background: linear-gradient(135deg, #7f1d1d, #b91c1c);
  color: #fff;
}
.delete-btn:hover {
  background: linear-gradient(135deg, #b91c1c, #dc2626);
  box-shadow: 0 4px 15px rgba(220, 38, 38, 0.35);
}

/* Modals Dialogs */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(15, 23, 42, 0.75);
  backdrop-filter: blur(8px);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
  animation: fadeIn 0.25s ease-out;
}
.modal-content {
  width: 100%;
  max-width: 480px;
  padding: 28px;
  margin: 16px;
  animation: scaleIn 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.border-danger {
  border-color: rgba(239, 68, 68, 0.4) !important;
}
.border-danger:hover {
  box-shadow: 0 15px 45px rgba(239, 68, 68, 0.2) !important;
}
.modal-title {
  margin-top: 0;
  margin-bottom: 20px;
  font-size: 20px;
  font-weight: 700;
  color: #BDE8E8;
  letter-spacing: 0.5px;
}
.text-danger {
  color: #ef4444 !important;
}
.modal-fields {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.checkbox-field {
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  user-select: none;
  padding: 8px 0;
}
.checkbox-label {
  font-size: 14px;
  color: #e2e8f0;
}
.modal-error-msg {
  color: #ef4444;
  background: rgba(239, 68, 68, 0.12);
  border: 1px solid rgba(239, 68, 68, 0.2);
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 13px;
  margin-top: 16px;
  margin-bottom: 0;
  font-weight: 500;
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 24px;
  border-top: 1px solid rgba(189, 232, 232, 0.08);
  padding-top: 18px;
}
.cancel-btn {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #cbd5e1;
}
.cancel-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}
.confirm-btn {
  background: linear-gradient(135deg, #06b6d4, #0891b2);
  color: #fff;
}
.confirm-btn:hover {
  background: linear-gradient(135deg, #0891b2, #06b6d4);
  box-shadow: 0 4px 15px rgba(6, 182, 212, 0.35);
}
.confirm-danger-btn {
  background: linear-gradient(135deg, #ef4444, #dc2626);
  color: #fff;
}
.confirm-danger-btn:hover {
  background: linear-gradient(135deg, #dc2626, #b91c1c);
  box-shadow: 0 4px 15px rgba(239, 68, 68, 0.35);
}
.confirm-danger-btn:disabled {
  background: #374151 !important;
  color: #6b7280 !important;
  cursor: not-allowed;
  transform: none !important;
  box-shadow: none !important;
}

/* Safe Delete & Countdown */
.danger-banner {
  background: rgba(239, 68, 68, 0.12);
  border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: 12px;
  padding: 16px;
  margin-bottom: 20px;
}
.danger-banner p {
  margin: 0;
  font-size: 13px;
  color: #f87171;
  line-height: 1.5;
  font-weight: 500;
}
.timer-section {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 12px;
}
.timer-progress-bg {
  flex: 1;
  height: 6px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
  overflow: hidden;
}
.timer-progress-fill {
  height: 100%;
  background: #ef4444;
  border-radius: 3px;
  transition: width 1s linear;
}
.timer-countdown-text {
  font-size: 12px !important;
  color: #fca5a5 !important;
  font-weight: 700 !important;
  white-space: nowrap;
}
.bg-danger-pulse {
  background: #f87171;
  animation: pulse 1s infinite alternate;
}
.token-input {
  background: rgba(0, 0, 0, 0.2) !important;
  color: #a5f3fc !important;
  font-family: monospace;
  font-weight: 700;
  letter-spacing: 0.5px;
}

/* Drawers */
.drawer-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(15, 23, 42, 0.65);
  backdrop-filter: blur(6px);
  display: flex;
  justify-content: flex-end;
  z-index: 1000;
  animation: fadeIn 0.25s ease-out;
}
.drawer-content {
  width: 100%;
  max-width: 560px;
  height: 100vh;
  border-radius: 0;
  border-left: 1px solid rgba(189, 232, 232, 0.18);
  display: flex;
  flex-direction: column;
  animation: slideLeft 0.28s cubic-bezier(0.25, 0.46, 0.45, 0.94);
}
.drawer-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 24px;
  border-bottom: 1px solid rgba(189, 232, 232, 0.1);
}
.drawer-title {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
  color: #BDE8E8;
}
.close-drawer-btn {
  background: transparent;
  border: none;
  font-size: 28px;
  color: rgba(189, 232, 232, 0.5);
  cursor: pointer;
  transition: color 0.2s;
  line-height: 1;
}
.close-drawer-btn:hover {
  color: #BDE8E8;
}
.drawer-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
}
.drawer-loading-text, .drawer-empty-text {
  text-align: center;
  padding: 40px;
  color: rgba(189, 232, 232, 0.4);
}
.drawer-footer {
  padding: 20px 24px;
  border-top: 1px solid rgba(189, 232, 232, 0.1);
  display: flex;
  justify-content: flex-end;
}
.audit-close-btn {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #cbd5e1;
}

/* Timeline */
.timeline-container {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-left: 10px;
}
.timeline-item {
  display: flex;
  gap: 16px;
}
.timeline-dot-connector {
  display: flex;
  flex-direction: column;
  align-items: center;
}
.timeline-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: #6b7280;
  box-shadow: 0 0 6px rgba(107, 114, 128, 0.6);
  z-index: 1;
}
.timeline-dot.adjust_loyalty {
  background: #0ea5e9;
  box-shadow: 0 0 8px rgba(14, 165, 233, 0.6);
}
.timeline-dot.reset {
  background: #f59e0b;
  box-shadow: 0 0 8px rgba(245, 158, 11, 0.6);
}
.timeline-dot.delete {
  background: #ef4444;
  box-shadow: 0 0 8px rgba(239, 68, 68, 0.6);
}
.timeline-connector {
  flex: 1;
  width: 2px;
  background: rgba(189, 232, 232, 0.1);
  margin-top: 6px;
}
.timeline-item:last-child .timeline-connector {
  display: none;
}
.timeline-card {
  flex: 1;
  padding: 16px;
  border-radius: 12px !important;
  background: rgba(30, 41, 59, 0.4) !important;
}
.timeline-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  flex-wrap: wrap;
  gap: 6px;
}
.log-op-tag {
  font-size: 11px;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 6px;
}
.log-op-tag.adjust_loyalty {
  background: rgba(14, 165, 233, 0.12);
  color: #38bdf8;
  border: 1px solid rgba(14, 165, 233, 0.25);
}
.log-op-tag.reset {
  background: rgba(245, 158, 11, 0.12);
  color: #fbbf24;
  border: 1px solid rgba(245, 158, 11, 0.25);
}
.log-op-tag.delete {
  background: rgba(239, 68, 68, 0.12);
  color: #f87171;
  border: 1px solid rgba(239, 68, 68, 0.25);
}
.log-time {
  font-size: 12px;
  color: rgba(189, 232, 232, 0.5);
}
.log-reason-text {
  font-size: 13px;
  color: #f1f5f9;
  margin: 0 0 6px 0;
  line-height: 1.4;
}
.log-actor-kind {
  font-size: 11px;
  color: rgba(189, 232, 232, 0.4);
}

/* Snapshot Comparison */
.log-snap-sec {
  margin-top: 14px;
  display: flex;
  align-items: stretch;
  gap: 8px;
  background: rgba(0, 0, 0, 0.15);
  border-radius: 8px;
  padding: 12px;
  border: 1px solid rgba(189, 232, 232, 0.05);
}
.snap-box {
  flex: 1;
  overflow: hidden;
}
.snap-title {
  margin: 0 0 6px 0;
  font-size: 11px;
  font-weight: 700;
}
.text-muted {
  color: #94a3b8;
}
.text-success {
  color: #10b981;
}
.snap-content {
  margin: 0;
  font-size: 11px;
  line-height: 1.3;
  color: #e2e8f0;
  white-space: pre-wrap;
  word-break: break-all;
}
.snap-arrow {
  display: flex;
  align-items: center;
  color: rgba(189, 232, 232, 0.2);
  font-size: 18px;
}

/* Animations */
@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}
@keyframes scaleIn {
  from { opacity: 0; transform: scale(0.92); }
  to { opacity: 1; transform: scale(1); }
}
@keyframes slideLeft {
  from { transform: translateX(100%); }
  to { transform: translateX(0); }
}
@keyframes pulse {
  from { opacity: 0.6; }
  to { opacity: 1; }
}
</style>
