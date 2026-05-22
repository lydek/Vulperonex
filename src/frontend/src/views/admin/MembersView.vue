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
              @click="selectMember(member.memberId)"
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
