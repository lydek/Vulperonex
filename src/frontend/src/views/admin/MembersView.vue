<script setup lang="ts">
import { computed, onMounted, ref, watch, onBeforeUnmount } from "vue";
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
  type MemberReadModel,
  type MemberAuditLog
} from "@/api/client";

const { t } = useI18n();

const members = ref<MemberReadModel[]>([]);
const activeTab = ref<'identity' | 'checkin'>('identity');
const selected = ref<MemberReadModel | null>(null);
const platformFilter = ref<string>("");
const platformFilterOptions = ["simulation", "twitch", "youtube"];
const loadingList = ref(false);
const loadingDetail = ref(false);
const listError = ref<string | null>(null);
const detailError = ref<string | null>(null);
const checkInPageSizeOptions = [10, 20, 50, 100];
const checkInPageSize = ref(10);
const checkInPage = ref(1);
const checkInSortKey = ref<"user" | "checkIns" | "stamps">("checkIns");
const checkInSortDirection = ref<"asc" | "desc">("desc");

// --- Overhauled Premium Popconfirm & MemberDetailModal State ---
const showDetailModal = ref(false);
const activeDetailTab = ref('profile');
const modalAdjustTotal = ref<number>(0);
const modalAdjustReason = ref<string>("管理員手動調整");
const modalAdjusting = ref(false);
const modalAdjustError = ref<string | null>(null);
const loadingModalAudit = ref(false);
const modalAuditLogs = ref<MemberAuditLog[]>([]);
const deletePopId = ref<string | null>(null);
const resetPopId = ref<string | null>(null);

function showDeletePop(memberId: string) {
  deletePopId.value = memberId;
  resetPopId.value = null;
}

function showResetPop(memberId: string) {
  resetPopId.value = memberId;
  deletePopId.value = null;
}

async function confirmReset(memberId: string) {
  resetPopId.value = null;
  loadingList.value = true;
  try {
    const member = await getMember(memberId);
    await resetMemberLoyalty(
      memberId,
      member.etag || "",
      {
        resetLoyalty: false,
        resetCheckIn: true,
        reason: "管理員手動重設打卡次數"
      }
    );
    await loadList();
  } catch (caught) {
    listError.value = describeError(caught);
  } finally {
    loadingList.value = false;
  }
}

async function openDetailModal(memberId: string) {
  loadingDetail.value = true;
  detailError.value = null;
  selected.value = null;
  activeDetailTab.value = 'profile';
  try {
    selected.value = await getMember(memberId);
    modalAdjustTotal.value = selected.value.loyalty.checkInCount;
    modalAdjustReason.value = "管理員手動調整";
    modalAdjustError.value = null;
    showDetailModal.value = true;
  } catch (caught) {
    detailError.value = describeError(caught);
  } finally {
    loadingDetail.value = false;
  }
}

function closeDetailModal() {
  showDetailModal.value = false;
  selected.value = null;
  showModalDeletePop.value = false;
  modalDeleteError.value = null;
}

async function executeInlineDelete(memberId: string) {
  deletePopId.value = null;
  loadingList.value = true;
  try {
    const token = await generateDeleteToken(memberId);
    await deleteMemberWithToken(memberId, token, "管理員手動氣泡刪除");
    await loadList();
  } catch (caught) {
    listError.value = describeError(caught);
  } finally {
    loadingList.value = false;
  }
}

async function handleInlineRefresh(memberId: string) {
  loadingList.value = true;
  try {
    await getMember(memberId);
    await loadList();
  } catch (caught) {
    console.error(caught);
  } finally {
    loadingList.value = false;
  }
}

async function loadModalAuditLogs() {
  if (!selected.value) return;
  loadingModalAudit.value = true;
  modalAuditLogs.value = [];
  try {
    modalAuditLogs.value = await getMemberAuditLogs(selected.value.memberId, 100, 0);
  } catch (caught) {
    console.error(caught);
  } finally {
    loadingModalAudit.value = false;
  }
}

function getSnapCheckIn(jsonStr: string): number {
  try {
    const data = JSON.parse(jsonStr);
    return data.loyalty?.checkInCount ?? data.checkInCount ?? 0;
  } catch {
    return 0;
  }
}

async function submitModalAdjust() {
  if (!selected.value) return;
  const reasonStr = modalAdjustReason.value.trim();
  if (!reasonStr || reasonStr.length < 3 || reasonStr.length > 500) {
    modalAdjustError.value = t("members.error.adjustReasonRequired");
    return;
  }
  
  modalAdjusting.value = true;
  modalAdjustError.value = null;
  try {
    await adjustMemberLoyalty(
      selected.value.memberId,
      selected.value.etag || "",
      {
        totalLoyalty: selected.value.loyalty.totalLoyalty,
        checkInCount: modalAdjustTotal.value,
        reason: reasonStr
      }
    );
    selected.value = await getMember(selected.value.memberId);
    await loadList();
    await loadModalAuditLogs();
  } catch (caught) {
    modalAdjustError.value = describeError(caught);
  } finally {
    modalAdjusting.value = false;
  }
}

async function refreshModalTwitch() {
  if (!selected.value) return;
  loadingDetail.value = true;
  try {
    await loadList();
    selected.value = await getMember(selected.value.memberId);
  } catch (caught) {
    console.error(caught);
  } finally {
    loadingDetail.value = false;
  }
}

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

// --- Modal Delete State ---
const showModalDeletePop = ref(false);
const modalDeleteError = ref<string | null>(null);
const modalDeleting = ref(false);

// --- Audit Log Drawer State ---
const showAuditDrawer = ref(false);
const auditLogs = ref<MemberAuditLog[]>([]);
const loadingAudit = ref(false);
const auditError = ref<string | null>(null);

onMounted(() => {
  void loadList();
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

const sortedCheckInMembers = computed(() => {
  const direction = checkInSortDirection.value === "asc" ? 1 : -1;
  return [...members.value].sort((left, right) => {
    if (checkInSortKey.value === "user") {
      return getDisplayName(left).localeCompare(getDisplayName(right), undefined, { sensitivity: "base" }) * direction;
    }

    const leftValue = checkInSortKey.value === "stamps"
      ? left.loyalty.totalLoyalty
      : left.loyalty.checkInCount;
    const rightValue = checkInSortKey.value === "stamps"
      ? right.loyalty.totalLoyalty
      : right.loyalty.checkInCount;
    return (leftValue - rightValue) * direction;
  });
});

const checkInTotalPages = computed(() => Math.max(1, Math.ceil(sortedCheckInMembers.value.length / checkInPageSize.value)));
const pagedCheckInMembers = computed(() => {
  const start = (checkInPage.value - 1) * checkInPageSize.value;
  return sortedCheckInMembers.value.slice(start, start + checkInPageSize.value);
});
const checkInPageStart = computed(() => sortedCheckInMembers.value.length === 0 ? 0 : (checkInPage.value - 1) * checkInPageSize.value + 1);
const checkInPageEnd = computed(() => Math.min(checkInPage.value * checkInPageSize.value, sortedCheckInMembers.value.length));

function setCheckInSort(key: "user" | "checkIns" | "stamps") {
  if (checkInSortKey.value === key) {
    checkInSortDirection.value = checkInSortDirection.value === "asc" ? "desc" : "asc";
  } else {
    checkInSortKey.value = key;
    checkInSortDirection.value = key === "user" ? "asc" : "desc";
  }
  checkInPage.value = 1;
}

function getCheckInSortLabel(key: "user" | "checkIns" | "stamps") {
  if (checkInSortKey.value !== key) return "";
  return checkInSortDirection.value === "asc" ? "↑" : "↓";
}

watch([platformFilter, checkInPageSize], () => {
  checkInPage.value = 1;
});

watch(checkInTotalPages, (totalPages) => {
  if (checkInPage.value > totalPages) {
    checkInPage.value = totalPages;
  }
});

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

// --- Modal Delete Action ---
async function executeModalDelete() {
  if (!selected.value) return;
  const memberId = selected.value.memberId;
  modalDeleting.value = true;
  modalDeleteError.value = null;
  try {
    const token = await generateDeleteToken(memberId);
    await deleteMemberWithToken(memberId, token, "管理員手動彈窗刪除");
    showModalDeletePop.value = false;
    showDetailModal.value = false;
    selected.value = null;
    await loadList();
  } catch (caught) {
    modalDeleteError.value = describeError(caught);
  } finally {
    modalDeleting.value = false;
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

function getPrimaryIdentity(member: MemberReadModel | null) {
  if (!member || !member.identities || member.identities.length === 0) return null;
  return member.identities.find(id => id.platform === 'twitch') || member.identities[0];
}

function getAvatarUrl(member: MemberReadModel) {
  const primary = getPrimaryIdentity(member);
  return primary?.avatarUrl || "https://static-cdn.jtvnw.net/user-default-pictures-uv/13e5fa74def228c3-profile_image-70x70.png";
}

function getDisplayName(member: MemberReadModel) {
  const primary = getPrimaryIdentity(member);
  return primary?.displayName || member.memberId;
}

function getLoginHandle(member: MemberReadModel | null) {
  const primary = getPrimaryIdentity(member);
  return primary?.login || primary?.platformUserId || member?.memberId || "";
}

function getIsSubscriber(member: MemberReadModel) {
  const primary = getPrimaryIdentity(member);
  return primary?.isSubscriber ?? false;
}
</script>

<template>
  <section aria-labelledby="members-title" class="members-view-container">
    <header class="page-header">
      <h1 id="members-title" class="page-title">{{ t("members.title") }}</h1>
      <p class="page-subtitle">{{ t("members.subtitle") }}</p>
    </header>



    <form class="members-toolbar" @submit.prevent="loadList">
      <label class="form-field">
        <span class="form-label">{{ t("members.filterPlatform") }}</span>
        <select
          v-model="platformFilter"
          :aria-label="t('members.filterPlatform')"
          data-testid="members-platform-filter"
        >
          <option value="">{{ t("members.filterPlatform.all") }}</option>
          <option v-for="platform in platformFilterOptions" :key="platform" :value="platform">
            {{ platform }}
          </option>
        </select>
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

    <!-- Premium Tabs Switcher -->
    <div class="members-tabs">
      <button
        type="button"
        class="tab-btn"
        :class="{ active: activeTab === 'identity' }"
        @click="activeTab = 'identity'"
      >
        <span class="tab-icon">👤</span>身分管理
      </button>
      <button
        type="button"
        class="tab-btn"
        :class="{ active: activeTab === 'checkin' }"
        @click="activeTab = 'checkin'"
      >
        <span class="tab-icon">📅</span>簽到管理
      </button>
    </div>

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
        
        <!-- Tab 1: Identity Management Table -->
        <table v-else-if="members.length > 0 && activeTab === 'identity'" class="monitor-table" data-testid="members-table">
          <thead>
            <tr>
              <th scope="col" style="width: 70px; text-align: center;">{{ t("members.col.avatar") || "頭像" }}</th>
              <th scope="col">{{ t("members.col.displayName") || "顯示名稱" }}</th>
              <th scope="col">{{ t("members.col.twitchAccount") || "Twitch 帳號" }}</th>
              <th scope="col" style="width: 140px; text-align: center;">{{ t("members.col.isSubscriber") || "身分狀態" }}</th>
              <th scope="col" style="width: 220px; text-align: center;">操作</th>
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
              :aria-label="getDisplayName(member)"
              @click="selectMember(member.memberId)"
              @keydown.enter.prevent="selectMember(member.memberId)"
              @keydown.space.prevent="selectMember(member.memberId)"
            >
              <td style="text-align: center;">
                <div class="table-avatar-wrapper">
                  <img :src="getAvatarUrl(member)" class="table-avatar" alt="Avatar" />
                </div>
              </td>
              <td>
                <span class="table-display-name">{{ getDisplayName(member) }}</span>
              </td>
              <td>
                <span class="table-login-name font-mono">@{{ getLoginHandle(member) }}</span>
              </td>
              <td style="text-align: center;">
                <span :class="['role-badge-tag', getIsSubscriber(member) ? 'subscriber' : 'viewer']">
                  {{ getIsSubscriber(member) ? '訂閱者' : '一般觀眾' }}
                </span>
              </td>
              <td>
                <div class="row-actions-wrapper" @click.stop>
                  <button type="button" class="row-action-btn solo-refresh-btn" @click="handleInlineRefresh(member.memberId)">
                    刷新
                  </button>
                  <button type="button" class="row-action-btn detail-btn-quaternary" @click="openDetailModal(member.memberId)">
                    詳情
                  </button>
                  <div class="popconfirm-anchor">
                    <button type="button" class="row-action-btn delete-btn-quaternary" @click="showDeletePop(member.memberId)">
                      刪除
                    </button>
                    <div v-if="deletePopId === member.memberId" class="popconfirm-bubble shadow-lg">
                      <div class="popconfirm-arrow"></div>
                      <div class="popconfirm-content">
                        <span class="popconfirm-icon">⚠️</span>
                        <span class="popconfirm-text">確定要永久刪除 {{ getDisplayName(member) }} 嗎？此操作不可逆。</span>
                      </div>
                      <div class="popconfirm-actions">
                        <button type="button" class="popconfirm-btn cancel" @click="deletePopId = null">取消</button>
                        <button type="button" class="popconfirm-btn confirm-delete" @click="executeInlineDelete(member.memberId)">確認刪除</button>
                      </div>
                    </div>
                  </div>
                </div>
              </td>
            </tr>
          </tbody>
        </table>

        <!-- Tab 2: Checkin Management Table -->
        <div v-else-if="members.length > 0 && activeTab === 'checkin'" class="checkin-table-section">
          <div class="checkin-table-controls">
            <label class="checkin-page-size">
              <span>{{ t("members.checkIn.pageSize") }}</span>
              <select v-model.number="checkInPageSize" data-testid="members-checkin-page-size">
                <option v-for="size in checkInPageSizeOptions" :key="size" :value="size">
                  {{ size }}
                </option>
              </select>
            </label>
            <div class="checkin-page-status" data-testid="members-checkin-page-status">
              {{ t("members.checkIn.pageStatus", { start: checkInPageStart, end: checkInPageEnd, total: sortedCheckInMembers.length }) }}
            </div>
          </div>
          <p class="checkin-table-hint">{{ t("members.checkIn.resetWindowHint") }}</p>
          <table class="monitor-table" data-testid="members-table-checkin">
          <thead>
            <tr>
              <th scope="col" style="width: 70px; text-align: center;">{{ t("members.col.avatar") || "頭像" }}</th>
              <th scope="col">
                <button type="button" class="sortable-th" data-testid="members-checkin-sort-user" @click="setCheckInSort('user')">
                  {{ t("members.col.user") || "使用者" }} <span aria-hidden="true">{{ getCheckInSortLabel("user") }}</span>
                </button>
              </th>
              <th scope="col" style="text-align: center; width: 130px;">
                <button type="button" class="sortable-th center" data-testid="members-checkin-sort-checkins" @click="setCheckInSort('checkIns')">
                  {{ t("members.col.checkIns") || "累積打卡" }} <span aria-hidden="true">{{ getCheckInSortLabel("checkIns") }}</span>
                </button>
              </th>
              <th scope="col" style="text-align: center; width: 130px;">
                <button type="button" class="sortable-th center" data-testid="members-checkin-sort-stamps" @click="setCheckInSort('stamps')">
                  {{ t("members.col.stampsCount") || "目前印章" }} <span aria-hidden="true">{{ getCheckInSortLabel("stamps") }}</span>
                </button>
              </th>
              <th scope="col" style="width: 250px; text-align: center;">{{ t("members.checkIn.actions") }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="member in pagedCheckInMembers"
              :key="member.memberId"
              :class="['members-row', { 'members-row-selected': selected?.memberId === member.memberId }]"
              data-testid="members-row"
              tabindex="0"
              role="button"
              :aria-label="getDisplayName(member)"
              @click="openDetailModal(member.memberId).then(() => activeDetailTab = 'loyalty')"
              @keydown.enter.prevent="openDetailModal(member.memberId).then(() => activeDetailTab = 'loyalty')"
              @keydown.space.prevent="openDetailModal(member.memberId).then(() => activeDetailTab = 'loyalty')"
            >
              <td style="text-align: center;">
                <div class="table-avatar-wrapper">
                  <img :src="getAvatarUrl(member)" class="table-avatar" alt="Avatar" />
                </div>
              </td>
              <td>
                <div class="table-user-info">
                  <span class="table-display-name">{{ getDisplayName(member) }}</span>
                  <span class="table-login-name font-mono">@{{ getLoginHandle(member) }}</span>
                </div>
              </td>
              <td style="text-align: center;" class="monitor-mono text-success-highlight">
                {{ member.loyalty.checkInCount }}
              </td>
              <td style="text-align: center;" class="monitor-mono text-info-highlight">
                {{ member.loyalty.totalLoyalty }}
              </td>
              <td>
                <div class="row-actions-wrapper" @click.stop>
                  <div class="popconfirm-anchor">
                    <button type="button" class="row-action-btn reset-btn-quaternary" @click="showResetPop(member.memberId)">
                      {{ t("members.checkIn.resetCount") }}
                    </button>
                    <div v-if="resetPopId === member.memberId" class="popconfirm-bubble shadow-lg">
                      <div class="popconfirm-arrow"></div>
                      <div class="popconfirm-content">
                        <span class="popconfirm-icon">⚠️</span>
                        <span class="popconfirm-text">{{ t("members.checkIn.resetCountConfirm") }}</span>
                      </div>
                      <div class="popconfirm-actions">
                        <button type="button" class="popconfirm-btn cancel" @click="resetPopId = null">{{ t("members.dialog.cancel") }}</button>
                        <button type="button" class="popconfirm-btn confirm-reset" @click="confirmReset(member.memberId)">{{ t("members.checkIn.resetCount") }}</button>
                      </div>
                    </div>
                  </div>
                  <button type="button" class="row-action-btn detail-btn-quaternary-bordered" @click="openDetailModal(member.memberId).then(() => activeDetailTab = 'loyalty')">
                    {{ t("members.checkIn.details") }}
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
          </table>
          <div class="checkin-pagination" aria-label="Check-in table pagination">
            <button
              type="button"
              class="row-action-btn"
              data-testid="members-checkin-prev"
              :disabled="checkInPage <= 1"
              @click="checkInPage = Math.max(1, checkInPage - 1)"
            >
              {{ t("members.checkIn.previous") }}
            </button>
            <span data-testid="members-checkin-page">{{ checkInPage }} / {{ checkInTotalPages }}</span>
            <button
              type="button"
              class="row-action-btn"
              data-testid="members-checkin-next"
              :disabled="checkInPage >= checkInTotalPages"
              @click="checkInPage = Math.min(checkInTotalPages, checkInPage + 1)"
            >
              {{ t("members.checkIn.next") }}
            </button>
          </div>
        </div>
      </div>
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

    <!-- 3. Safe Delete Dialog Removed (Unified with inline popconfirm) -->

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

    <!-- Overhauled MemberDetailModal Component -->
    <div v-if="showDetailModal && selected" class="detail-modal-overlay" @click.self="closeDetailModal">
      <div class="detail-modal-card glow-panel-elevated">
        <!-- Modal Header -->
        <div class="detail-modal-header">
          <div class="header-profile-section">
            <img :src="getAvatarUrl(selected)" class="header-modal-avatar" alt="Avatar" />
            <div class="header-profile-text">
              <div class="header-name-row">
                <span class="header-display-name-text">{{ getDisplayName(selected) }}</span>
                <div class="header-badges-row">
                  <span v-if="getIsSubscriber(selected)" class="role-badge-tag subscriber small-badge">訂閱者</span>
                  <span v-else class="role-badge-tag viewer small-badge">一般觀眾</span>
                </div>
              </div>
              <span class="header-sub-text font-mono">ID: {{ selected.memberId }} | @{{ getLoginHandle(selected) }}</span>
            </div>
          </div>
          <button type="button" class="modal-close-btn" @click="closeDetailModal">✕</button>
        </div>

        <!-- Modal Tabs Switcher -->
        <div class="detail-modal-tabs">
          <button 
            type="button" 
            class="modal-tab-btn" 
            :class="{ active: activeDetailTab === 'profile' }" 
            @click="activeDetailTab = 'profile'"
          >
            👤 身分資料
          </button>
          <button 
            type="button" 
            class="modal-tab-btn" 
            :class="{ active: activeDetailTab === 'loyalty' }" 
            @click="activeDetailTab = 'loyalty'"
          >
            📅 簽到進度
          </button>
          <button 
            type="button" 
            class="modal-tab-btn" 
            :class="{ active: activeDetailTab === 'rewards' }" 
            @click="activeDetailTab = 'rewards'"
          >
            🎪 點數與票券
          </button>
          <button 
            type="button" 
            class="modal-tab-btn" 
            :class="{ active: activeDetailTab === 'audit' }" 
            @click="activeDetailTab = 'audit'; loadModalAuditLogs();"
          >
            📋 稽核紀錄
          </button>
        </div>

        <!-- Tab Contents -->
        <div class="detail-modal-body">
          <!-- 1. 身分資料 -->
          <div v-if="activeDetailTab === 'profile'" class="tab-pane-content">
            <div class="premium-descriptions-table">
              <div class="desc-row">
                <div class="desc-label">Twitch ID</div>
                <div class="desc-val font-mono">{{ getLoginHandle(selected) }}</div>
                <div class="desc-label">顯示名稱</div>
                <div class="desc-val">{{ getDisplayName(selected) }}</div>
              </div>
              <div class="desc-row full-width">
                <div class="desc-label">身分組 (內建)</div>
                <div class="desc-val">
                  <span :class="['role-badge-tag', getIsSubscriber(selected) ? 'subscriber' : 'viewer']">
                    {{ getIsSubscriber(selected) ? '訂閱者' : '一般觀眾' }}
                  </span>
                </div>
              </div>
              <div class="desc-row full-width">
                <div class="desc-label">自定義角色</div>
                <div class="desc-val text-muted-italic">無自定義標籤</div>
              </div>
              <div class="desc-row">
                <div class="desc-label">環境標註</div>
                <div class="desc-val">
                  <div class="switch-container">
                    <span class="switch-label">測試模式</span>
                    <label class="switch-toggle">
                      <input type="checkbox" disabled />
                      <span class="switch-slider"></span>
                    </label>
                  </div>
                </div>
                <div class="desc-label">資料同步日期</div>
                <div class="desc-val text-info-highlight font-mono">{{ selected.updatedAtTicks ? new Date(selected.updatedAtTicks / 10000).toLocaleString() : '從未同步' }}</div>
              </div>
            </div>
          </div>

          <!-- 2. 簽到進度 -->
          <div v-if="activeDetailTab === 'loyalty'" class="tab-pane-content flex-column-gap">
            <div class="premium-descriptions-table">
              <div class="desc-row full-width">
                <div class="desc-label">累計打卡總數</div>
                <div class="desc-val text-success-highlight-large">{{ selected.loyalty.checkInCount }} 次</div>
              </div>
              <div class="desc-row">
                <div class="desc-label">目前冷卻狀態</div>
                <div class="desc-val">
                  <span class="role-badge-tag success-badge">✅ 可簽到</span>
                </div>
                <div class="desc-label">最後打卡日期</div>
                <div class="desc-val font-mono text-muted-italic">---</div>
              </div>
            </div>

            <!-- 快速調整區域 -->
            <div class="action-form-container glow-panel-subtle">
              <div class="form-title">✏️ 手動調整簽到次數</div>
              <div class="form-grid">
                <div class="form-item">
                  <span class="form-label">設置新次數</span>
                  <input v-model.number="modalAdjustTotal" type="number" min="0" />
                </div>
                <div class="form-item">
                  <span class="form-label">異動原因</span>
                  <input v-model="modalAdjustReason" type="text" placeholder="為什麼做此調整？" />
                </div>
                <div class="form-action-row">
                  <button type="button" class="confirm-btn adjust-confirm-btn" :disabled="modalAdjusting" @click="submitModalAdjust">
                    {{ modalAdjusting ? '調整中...' : '套用變更' }}
                  </button>
                </div>
              </div>
              <p v-if="modalAdjustError" class="modal-error-msg" role="alert">{{ modalAdjustError }}</p>
            </div>
          </div>

          <!-- 3. 點數與票券 -->
          <div v-if="activeDetailTab === 'rewards'" class="tab-pane-content">
            <div class="premium-descriptions-table">
              <div class="desc-row full-width">
                <div class="desc-label">目前剩餘印章 (點數)</div>
                <div class="desc-val text-accent-highlight-large">{{ selected.loyalty.totalLoyalty }} Pts</div>
              </div>
            </div>
            <div class="action-form-container glow-panel-subtle mt-4 empty-card-panel">
              <span class="empty-icon">🎪</span>
              <span class="empty-text">此功能開發中：將顯示持有的抽獎券清單與兌換歷史</span>
            </div>
          </div>

          <!-- 4. 稽核紀錄 -->
          <div v-if="activeDetailTab === 'audit'" class="tab-pane-content overflow-y-auto max-height-320">
            <div v-if="loadingModalAudit" class="spinner-container">
              <div class="spinner"></div>
              <span class="spinner-text">正在讀取紀錄...</span>
            </div>
            <div v-else-if="modalAuditLogs.length === 0" class="action-form-container glow-panel-subtle empty-card-panel">
              <span class="empty-icon">📝</span>
              <span class="empty-text">目前尚無任何變更紀錄</span>
            </div>
            <div v-else class="timeline-container-simple">
              <div v-for="log in modalAuditLogs" :key="log.id" class="timeline-item-simple">
                <div class="timeline-dot-simple"></div>
                <div class="timeline-card-simple glow-panel-subtle">
                  <div class="timeline-header-simple">
                    <span class="timeline-title-simple">{{ log.operation.toUpperCase() }}</span>
                    <span class="timeline-time-simple font-mono">{{ formatDate(log.occurredAt) }}</span>
                  </div>
                  <div class="timeline-body-simple">
                    <div class="snap-box-row" v-if="log.beforeJson || log.afterJson">
                      <span class="snap-old delete-line" v-if="log.beforeJson">{{ getSnapCheckIn(log.beforeJson) }} 次</span>
                      <span class="snap-arrow-simple">→</span>
                      <span class="snap-new add-line" v-if="log.afterJson">{{ getSnapCheckIn(log.afterJson) }} 次</span>
                    </div>
                    <div class="timeline-reason-simple">原因: {{ log.reason }}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Modal Footer -->
        <div class="detail-modal-footer">
          <div class="footer-left-buttons">
            <div class="popconfirm-anchor">
              <button type="button" class="action-btn-line-danger" @click="showModalDeletePop = !showModalDeletePop">
                🗑️ 刪除使用者
              </button>
              <div v-if="showModalDeletePop" class="popconfirm-bubble shadow-lg modal-popconfirm-placement">
                <div class="popconfirm-arrow modal-popconfirm-arrow-placement"></div>
                <div class="popconfirm-content">
                  <span class="popconfirm-icon">⚠️</span>
                  <span class="popconfirm-text">確定要永久刪除 {{ getDisplayName(selected) }} 嗎？此操作不可逆。</span>
                </div>
                <p v-if="modalDeleteError" class="popconfirm-error-text">{{ modalDeleteError }}</p>
                <div class="popconfirm-actions">
                  <button type="button" class="popconfirm-btn cancel" @click="showModalDeletePop = false">取消</button>
                  <button type="button" class="popconfirm-btn confirm-delete" @click="executeModalDelete" :disabled="modalDeleting">
                    {{ modalDeleting ? "刪除中..." : "確認刪除" }}
                  </button>
                </div>
              </div>
            </div>
            <button type="button" class="action-btn-dark-primary" @click="refreshModalTwitch">
              🔄 刷新 Twitch 快取
            </button>
          </div>
          <button type="button" class="action-btn-close-grey" @click="closeDetailModal">
            關閉
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
  overflow: visible !important;
}
.checkin-table-section {
  display: grid;
  gap: 12px;
}
.checkin-table-controls,
.checkin-pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 16px;
}
.checkin-page-size {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  color: #cbd5e1;
  font-size: 13px;
  font-weight: 700;
}
.checkin-page-size select {
  min-width: 84px;
}
.checkin-page-status {
  color: rgba(189, 232, 232, 0.72);
  font-size: 13px;
}
.checkin-table-hint {
  margin: 0;
  padding: 0 16px;
  color: rgba(189, 232, 232, 0.64);
  font-size: 13px;
}
.sortable-th {
  width: 100%;
  padding: 0;
  border: 0;
  background: transparent;
  color: inherit;
  cursor: pointer;
  font: inherit;
  font-weight: 700;
  text-align: left;
}
.sortable-th.center {
  text-align: center;
}
.sortable-th:hover {
  color: #67e8f9;
}
.checkin-pagination {
  justify-content: flex-end;
  color: rgba(189, 232, 232, 0.72);
  font-size: 13px;
}
.checkin-pagination .row-action-btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
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

/* Custom Table Styles */
.table-avatar-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
}
.table-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  border: 1px solid rgba(189, 232, 232, 0.2);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.25);
  background: rgba(30, 41, 59, 0.6);
  object-fit: cover;
}
.table-user-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.table-display-name {
  font-weight: 700;
  color: #BDE8E8;
  font-size: 13px;
}
.table-login-name {
  font-size: 11px;
  color: rgba(189, 232, 232, 0.4);
}
.role-badge-tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
  text-align: center;
  border: 1px solid transparent;
}
.role-badge-tag.subscriber {
  color: #a855f7;
  background: rgba(168, 85, 247, 0.1);
  border-color: rgba(168, 85, 247, 0.2);
}
.role-badge-tag.viewer {
  color: rgba(189, 232, 232, 0.6);
  background: rgba(255, 255, 255, 0.05);
  border-color: rgba(255, 255, 255, 0.1);
}

/* Detail Card Header */
.detail-profile-header {
  display: flex;
  align-items: center;
  gap: 16px;
  border-bottom: 1px solid rgba(189, 232, 232, 0.1);
  padding-bottom: 16px;
  margin-bottom: 20px;
  position: relative;
}
.detail-avatar {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  border: 2px solid rgba(6, 182, 212, 0.3);
  box-shadow: 0 4px 12px rgba(6, 182, 212, 0.15);
  object-fit: cover;
}
.detail-profile-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
  min-width: 0;
}
.detail-display-name {
  font-size: 18px;
  font-weight: 800;
  color: #BDE8E8;
  margin: 0;
  text-overflow: ellipsis;
  overflow: hidden;
  white-space: nowrap;
}
.detail-username {
  font-size: 11px;
  color: rgba(189, 232, 232, 0.5);
  margin: 0;
  text-overflow: ellipsis;
  overflow: hidden;
  white-space: nowrap;
}

/* Premium Switcher Tab Styles */
.members-tabs {
  display: flex;
  gap: 12px;
  margin-bottom: 24px;
  background: rgba(15, 23, 42, 0.4);
  border: 1px solid rgba(189, 232, 232, 0.1);
  padding: 6px;
  border-radius: 12px;
  width: fit-content;
  backdrop-filter: blur(10px);
}
.tab-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  border-radius: 8px;
  border: none;
  background: transparent;
  color: rgba(189, 232, 232, 0.6);
  font-weight: 700;
  font-size: 13.5px;
  cursor: pointer;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}
.tab-btn:hover {
  color: #BDE8E8;
  background: rgba(255, 255, 255, 0.04);
}
.tab-btn.active {
  color: #fff;
  background: linear-gradient(135deg, #7c3aed, #4f46e5);
  box-shadow: 0 4px 15px rgba(124, 58, 237, 0.35);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
}
.tab-icon {
  font-size: 15px;
}

/* Quaternary (Ghost) Action Buttons inside Rows */
.row-actions-wrapper {
  display: flex;
  gap: 6px;
  justify-content: center;
  align-items: center;
}
.row-action-btn {
  padding: 5px 12px;
  border-radius: 6px;
  font-weight: 700;
  font-size: 12px;
  cursor: pointer;
  border: 1px solid transparent;
  background: transparent;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}
.detail-btn-quaternary {
  color: #38bdf8;
  border-color: rgba(56, 189, 248, 0.15);
  background: rgba(56, 189, 248, 0.04);
}
.detail-btn-quaternary:hover {
  color: #fff;
  background: #0284c7;
  box-shadow: 0 0 10px rgba(2, 132, 199, 0.4);
}
.audit-btn-quaternary {
  color: #a78bfa;
  border-color: rgba(167, 139, 250, 0.15);
  background: rgba(167, 139, 250, 0.04);
}
.audit-btn-quaternary:hover {
  color: #fff;
  background: #7c3aed;
  box-shadow: 0 0 10px rgba(124, 58, 237, 0.4);
}
.delete-btn-quaternary {
  color: #f87171;
  border-color: rgba(248, 113, 113, 0.15);
  background: rgba(248, 113, 113, 0.04);
}
.delete-btn-quaternary:hover {
  color: #fff;
  background: #dc2626;
  box-shadow: 0 0 10px rgba(220, 38, 38, 0.4);
}
.adjust-btn-quaternary {
  color: #34d399;
  border-color: rgba(52, 211, 153, 0.15);
  background: rgba(52, 211, 153, 0.04);
}
.adjust-btn-quaternary:hover {
  color: #fff;
  background: #059669;
  box-shadow: 0 0 10px rgba(5, 150, 105, 0.4);
}
.reset-btn-quaternary {
  color: #fbbf24;
  border-color: rgba(251, 191, 36, 0.15);
  background: rgba(251, 191, 36, 0.04);
}
.reset-btn-quaternary:hover {
  color: #fff;
  background: #d97706;
  box-shadow: 0 0 10px rgba(217, 119, 6, 0.4);
}

/* Custom Table Row Highlight */
.text-success-highlight {
  color: #34d399 !important;
  font-weight: 700 !important;
}
.text-info-highlight {
  color: #38bdf8 !important;
  font-weight: 700 !important;
}

/* Popconfirm Bubble & Layout Overhaul */
.popconfirm-anchor {
  position: relative;
  display: inline-block;
}
.popconfirm-bubble {
  position: absolute;
  bottom: 135%;
  right: 0;
  background: rgba(20, 24, 33, 0.96) !important;
  border: 1px solid rgba(189, 232, 232, 0.16) !important;
  border-radius: 12px;
  padding: 16px;
  width: 300px;
  z-index: 10000;
  backdrop-filter: blur(16px);
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.6) !important;
  animation: scaleIn 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.popconfirm-arrow {
  position: absolute;
  top: 100%;
  right: 20px;
  border-width: 6px;
  border-style: solid;
  border-color: rgba(20, 24, 33, 0.96) transparent transparent transparent;
}
.popconfirm-content {
  display: flex;
  gap: 10px;
  align-items: flex-start;
  margin-bottom: 12px;
}
.popconfirm-icon {
  font-size: 16px;
  color: #fbbf24;
}
.popconfirm-text {
  font-size: 13px;
  color: #f1f5f9;
  line-height: 1.5;
  text-align: left;
  font-weight: 500;
}
.popconfirm-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
.popconfirm-btn {
  padding: 5px 12px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  border: 1px solid transparent;
  transition: all 0.2s;
}
.popconfirm-btn.cancel {
  background: rgba(255, 255, 255, 0.06);
  color: #cbd5e1;
  border-color: rgba(255, 255, 255, 0.1);
}
.popconfirm-btn.cancel:hover {
  background: rgba(255, 255, 255, 0.12);
}
.popconfirm-btn.confirm-delete, .popconfirm-btn.confirm-reset {
  background: #a855f7;
  color: #fff;
}
.popconfirm-btn.confirm-delete:hover, .popconfirm-btn.confirm-reset:hover {
  background: #9333ea;
  box-shadow: 0 0 10px rgba(168, 85, 247, 0.4);
}

/* Overhauled MemberDetailModal Styles */
.detail-modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(15, 23, 42, 0.75);
  backdrop-filter: blur(12px);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 9999;
  animation: fadeIn 0.25s ease-out;
}
.detail-modal-card {
  width: 700px;
  max-height: 90vh;
  background: rgba(23, 27, 38, 0.95) !important;
  border: 1px solid rgba(189, 232, 232, 0.16) !important;
  border-radius: 20px;
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.6) !important;
  display: flex;
  flex-direction: column;
  overflow: visible !important;
  animation: scaleIn 0.28s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.detail-modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 24px;
  border-bottom: 1px solid rgba(189, 232, 232, 0.08);
}
.header-profile-section {
  display: flex;
  align-items: center;
  gap: 16px;
}
.header-modal-avatar {
  width: 54px;
  height: 54px;
  border-radius: 50%;
  border: 2px solid rgba(168, 85, 247, 0.4);
  box-shadow: 0 4px 15px rgba(168, 85, 247, 0.2);
  object-fit: cover;
}
.header-profile-text {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.header-name-row {
  display: flex;
  align-items: center;
  gap: 8px;
}
.header-display-name-text {
  font-size: 20px;
  font-weight: 800;
  color: #fff;
}
.header-badges-row {
  display: flex;
  gap: 4px;
}
.small-badge {
  font-size: 10px !important;
  padding: 1px 6px !important;
}
.header-sub-text {
  font-size: 11px;
  color: rgba(189, 232, 232, 0.4);
}
.modal-close-btn {
  background: transparent;
  border: none;
  font-size: 20px;
  color: rgba(189, 232, 232, 0.4);
  cursor: pointer;
  transition: color 0.2s;
}
.modal-close-btn:hover {
  color: #fff;
}

/* Modal Tabs */
.detail-modal-tabs {
  display: flex;
  gap: 8px;
  padding: 0 24px;
  border-bottom: 1px solid rgba(189, 232, 232, 0.08);
  background: rgba(0, 0, 0, 0.15);
}
.modal-tab-btn {
  padding: 14px 20px;
  background: transparent;
  border: none;
  border-bottom: 3px solid transparent;
  color: rgba(189, 232, 232, 0.6);
  font-size: 13.5px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s;
}
.modal-tab-btn:hover {
  color: #fff;
}
.modal-tab-btn.active {
  color: #a855f7;
  border-bottom-color: #a855f7;
}

/* Modal Body */
.detail-modal-body {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}
.tab-pane-content {
  display: flex;
  flex-direction: column;
  animation: fadeIn 0.25s ease-out;
}
.flex-column-gap {
  gap: 20px;
}

/* Premium Descriptions Table */
.premium-descriptions-table {
  display: flex;
  flex-direction: column;
  border: 1px solid rgba(189, 232, 232, 0.08);
  border-radius: 12px;
  overflow: hidden;
  background: rgba(0, 0, 0, 0.12);
}
.desc-row {
  display: grid;
  grid-template-columns: 140px 1fr 140px 1fr;
  border-bottom: 1px solid rgba(189, 232, 232, 0.08);
}
.desc-row:last-child {
  border-bottom: none;
}
.desc-row.full-width {
  grid-template-columns: 140px 1fr;
}
.desc-label {
  background: rgba(189, 232, 232, 0.03);
  padding: 14px 18px;
  font-size: 13px;
  font-weight: 700;
  color: rgba(189, 232, 232, 0.7);
  border-right: 1px solid rgba(189, 232, 232, 0.08);
  display: flex;
  align-items: center;
}
.desc-val {
  padding: 14px 18px;
  font-size: 13px;
  color: #e2e8f0;
  display: flex;
  align-items: center;
}
.desc-row .desc-val:nth-child(2) {
  border-right: 1px solid rgba(189, 232, 232, 0.08);
}
.desc-row.full-width .desc-val:nth-child(2) {
  border-right: none;
}
.text-muted-italic {
  color: rgba(189, 232, 232, 0.4);
  font-style: italic;
  font-size: 12.5px;
}
.text-success-highlight-large {
  color: #34d399 !important;
  font-weight: 800;
  font-size: 18px;
}
.text-accent-highlight-large {
  color: #a855f7 !important;
  font-weight: 800;
  font-size: 18px;
}

/* Switch styling */
.switch-container {
  display: flex;
  align-items: center;
  gap: 8px;
}
.switch-label {
  font-size: 12px;
  color: rgba(189, 232, 232, 0.4);
}
.switch-toggle {
  position: relative;
  display: inline-block;
  width: 32px;
  height: 18px;
}
.switch-toggle input {
  opacity: 0;
  width: 0;
  height: 0;
}
.switch-slider {
  position: absolute;
  cursor: not-allowed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(255, 255, 255, 0.1);
  transition: .3s;
  border-radius: 9px;
}
.switch-slider:before {
  position: absolute;
  content: "";
  height: 14px;
  width: 14px;
  left: 2px;
  bottom: 2px;
  background-color: #cbd5e1;
  transition: .3s;
  border-radius: 50%;
}
.success-badge {
  color: #34d399;
  background: rgba(52, 211, 153, 0.08);
  border-color: rgba(52, 211, 153, 0.15);
}

/* Loyalty adjustment form inside modal */
.form-title {
  font-size: 13.5px;
  font-weight: 800;
  color: #fff;
  margin-bottom: 12px;
}
.form-grid {
  display: grid;
  grid-template-columns: 180px 1fr auto;
  gap: 16px;
  align-items: flex-end;
}
.form-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.form-item input {
  background: rgba(0, 0, 0, 0.25);
  border: 1px solid rgba(189, 232, 232, 0.1);
  border-radius: 6px;
  padding: 8px 12px;
  color: #fff;
  font-size: 13px;
  height: 36px;
}
.form-item input:focus {
  border-color: #a855f7;
  outline: none;
}
.form-action-row {
  display: flex;
}
.adjust-confirm-btn {
  height: 36px;
  padding: 0 20px;
  font-size: 13px;
  background: linear-gradient(135deg, #a855f7, #7c3aed);
}
.adjust-confirm-btn:hover {
  background: linear-gradient(135deg, #9333ea, #6d28d9);
  box-shadow: 0 0 10px rgba(168, 85, 247, 0.3);
}

/* Empty Panel */
.empty-card-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px;
  border-radius: 12px;
  text-align: center;
}
.empty-icon {
  font-size: 24px;
  margin-bottom: 8px;
  opacity: 0.6;
}
.empty-text {
  font-size: 13px;
  color: rgba(189, 232, 232, 0.4);
}

/* Audit logs list inside modal */
.overflow-y-auto {
  overflow-y: auto;
}
.max-height-320 {
  max-height: 320px;
}
.spinner-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 40px;
  gap: 12px;
}
.spinner {
  width: 28px;
  height: 28px;
  border: 3px solid rgba(168, 85, 247, 0.1);
  border-top-color: #a855f7;
  border-radius: 50%;
  animation: pulse 1s infinite linear;
}
.spinner-text {
  font-size: 13px;
  color: rgba(189, 232, 232, 0.4);
}
.timeline-container-simple {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding-left: 8px;
}
.timeline-item-simple {
  display: flex;
  gap: 14px;
  position: relative;
}
.timeline-dot-simple {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #a855f7;
  box-shadow: 0 0 6px rgba(168, 85, 247, 0.6);
  margin-top: 14px;
  z-index: 1;
}
.timeline-card-simple {
  flex: 1;
  padding: 12px 16px;
  border-radius: 10px;
}
.timeline-header-simple {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.timeline-title-simple {
  font-size: 11px;
  font-weight: 800;
  color: #a855f7;
  background: rgba(168, 85, 247, 0.08);
  border: 1px solid rgba(168, 85, 247, 0.15);
  padding: 1px 6px;
  border-radius: 4px;
}
.timeline-time-simple {
  font-size: 11px;
  color: rgba(189, 232, 232, 0.4);
}
.timeline-body-simple {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.snap-box-row {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}
.delete-line {
  text-decoration: line-through;
  color: #f87171;
  background: rgba(248, 113, 113, 0.08);
  padding: 1px 4px;
  border-radius: 3px;
}
.add-line {
  color: #34d399;
  background: rgba(52, 211, 153, 0.08);
  padding: 1px 4px;
  border-radius: 3px;
  font-weight: 700;
}
.snap-arrow-simple {
  color: rgba(255, 255, 255, 0.2);
}
.timeline-reason-simple {
  font-size: 12px;
  color: rgba(189, 232, 232, 0.5);
  font-style: italic;
}

/* Modal Footer Styles */
.detail-modal-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 18px 24px;
  background: rgba(0, 0, 0, 0.15);
  border-top: 1px solid rgba(189, 232, 232, 0.08);
}
.footer-left-buttons {
  display: flex;
  gap: 12px;
}
.action-btn-line-danger {
  background: transparent;
  border: 1px solid #ef4444;
  color: #ef4444;
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 12.5px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s;
}
.action-btn-line-danger:hover {
  background: rgba(239, 68, 68, 0.06);
  box-shadow: 0 0 8px rgba(239, 68, 68, 0.2);
}
.action-btn-dark-primary {
  background: #1e1b4b;
  border: 1px solid rgba(99, 102, 241, 0.2);
  color: #818cf8;
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 12.5px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s;
}
.action-btn-dark-primary:hover {
  background: #312e81;
  color: #fff;
  border-color: rgba(99, 102, 241, 0.4);
}
.action-btn-close-grey {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #cbd5e1;
  padding: 8px 20px;
  border-radius: 8px;
  font-size: 12.5px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s;
}
.action-btn-close-grey:hover {
  background: rgba(255, 255, 255, 0.08);
}

/* Custom styled action buttons in tables */
.solo-refresh-btn {
  color: #38bdf8;
  border-color: rgba(56, 189, 248, 0.15);
  background: rgba(56, 189, 248, 0.04);
}
.solo-refresh-btn:hover {
  color: #fff;
  background: #0284c7;
  box-shadow: 0 0 10px rgba(2, 132, 199, 0.4);
}
.detail-btn-quaternary-bordered {
  color: #a855f7;
  border: 1px solid rgba(168, 85, 247, 0.25);
  background: rgba(168, 85, 247, 0.04);
  padding: 5px 12px;
  border-radius: 6px;
  font-weight: 700;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}
.detail-btn-quaternary-bordered:hover {
  color: #fff;
  background: #7c3aed;
  box-shadow: 0 0 10px rgba(124, 58, 237, 0.4);
}

/* Explicitly round corner cells to preserve border-radius when overflow is visible */
:deep(.monitor-table) {
  overflow: visible !important;
}
:deep(.monitor-table thead th:first-child) {
  border-top-left-radius: 8px;
}
:deep(.monitor-table thead th:last-child) {
  border-top-right-radius: 8px;
}
:deep(.monitor-table tbody tr:last-child td:first-child) {
  border-bottom-left-radius: 8px;
}
:deep(.monitor-table tbody tr:last-child td:last-child) {
  border-bottom-right-radius: 8px;
}

/* Modal popconfirm positioning */
.modal-popconfirm-placement {
  left: 0 !important;
  right: auto !important;
}
.modal-popconfirm-arrow-placement {
  left: 20px !important;
  right: auto !important;
}
.popconfirm-error-text {
  font-size: 11px;
  color: #f87171;
  margin-bottom: 8px;
  text-align: left;
  line-height: 1.4;
}
</style>
