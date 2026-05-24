<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  ApiError,
  getMember,
  getMembers,
  type MemberReadModel
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

async function loadCardSettings(): Promise<void> {
  try {
    const bgRes = await fetch("/api/config/overlay.member.background_url");
    if (bgRes.ok) {
      const bgData = await bgRes.json();
      backgroundUrl.value = bgData.value || "";
    }
    const stampRes = await fetch("/api/config/overlay.member.stamp_url");
    if (stampRes.ok) {
      const stampData = await stampRes.json();
      stampUrl.value = stampData.value || "";
    }
  } catch (caught) {
    saveError.value = t("members.cardSettings.loadFailed") + ": " + (caught instanceof Error ? caught.message : String(caught));
  }
}

async function saveCardSettings(): Promise<void> {
  savingSettings.value = true;
  saveMessage.value = null;
  saveError.value = null;
  try {
    const bgRes = await fetch("/api/config/overlay.member.background_url", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ value: backgroundUrl.value.trim() })
    });
    const stampRes = await fetch("/api/config/overlay.member.stamp_url", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ value: stampUrl.value.trim() })
    });
    if (!bgRes.ok || !stampRes.ok) {
      throw new Error(t("members.cardSettings.saveFailed"));
    }
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

function describeError(caught: unknown): string {
  if (caught instanceof ApiError) {
    return caught.errorCode ?? `HTTP_${caught.status}`;
  }
  return caught instanceof Error ? caught.message : String(caught);
}
</script>

<template>
  <section aria-labelledby="members-title">
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

      <aside class="members-detail-pane" aria-label="member-detail">
        <p
          v-if="!selected && !loadingDetail && !detailError"
          role="status"
          data-testid="members-detail-empty"
        >
          {{ t("members.detail.empty") }}
        </p>

        <p v-if="loadingDetail" role="status">{{ t("members.loading") }}</p>

        <p
          v-if="detailError"
          class="ack-error-code"
          role="alert"
          data-testid="members-detail-error"
        >
          {{ detailError }}
        </p>

        <article v-if="selected" class="status-card" data-testid="members-detail">
          <p class="status-label">{{ t("members.detail.memberId") }}</p>
          <p class="status-value monitor-mono">{{ selected.memberId }}</p>

          <p class="status-label">{{ t("members.detail.loyalty") }}</p>
          <p class="monitor-mono">
            {{ t("members.detail.loyaltyValue", {
              total: selected.loyalty.totalLoyalty,
              checkIn: selected.loyalty.checkInCount
            }) }}
          </p>

          <p class="status-label">{{ t("members.detail.identities") }}</p>
          <ul class="event-list">
            <li
              v-for="identity in selected.identities"
              :key="`${identity.platform}-${identity.platformUserId}`"
              class="event-item monitor-mono"
            >
              <strong>{{ identity.platform }}</strong>
              <span> · {{ identity.platformUserId }}</span>
            </li>
          </ul>
        </article>
      </aside>
    </div>
  </section>
</template>

<style scoped>
.card-settings-panel {
  background: rgba(26, 61, 110, 0.15);
  border: 1px solid rgba(189, 232, 232, 0.15);
  border-radius: 12px;
  padding: 20px;
  margin-bottom: 24px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.2);
}
.settings-panel-title {
  margin-top: 0;
  margin-bottom: 15px;
  font-size: 16px;
  font-weight: 700;
  color: #BDE8E8;
  letter-spacing: 0.5px;
}
.settings-fields {
  display: flex;
  gap: 16px;
  align-items: flex-end;
  flex-wrap: wrap;
}
.flex-1 {
  flex: 1;
  min-width: 250px;
}
.settings-save-btn {
  height: 38px;
  white-space: nowrap;
  box-shadow: 0 4px 12px rgba(189, 232, 232, 0.25);
}
.settings-success-msg {
  margin-top: 10px;
  margin-bottom: 0;
  color: #2ecc71;
  font-size: 13px;
  font-weight: 600;
}
.settings-error-msg {
  margin-top: 10px;
  margin-bottom: 0;
  color: #e74c3c;
  font-size: 13px;
  font-weight: 600;
}
</style>
