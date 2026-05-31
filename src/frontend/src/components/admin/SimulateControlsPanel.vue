<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useI18n } from "vue-i18n";
import ProgressBar from "primevue/progressbar";
import {
  ApiError,
  getTwitchBadges,
  postSimulate,
  postSimulateCheckIn,
  type SimulateAck,
  type SimulateAlias,
  type SimulateRequestBody,
  type PlatformBadgeDescriptor
} from "@/api/client";
import { useEventStore } from "@/stores/eventStore";
import { useTwitchRewardsStore } from "@/stores/twitchRewards";

const PLATFORM_CONNECTION_CHANGED = "platform.connection_changed";

interface Props {
  isEmbedded?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isEmbedded: false
});

const emit = defineEmits<{
  (e: "simulated", ack: SimulateAck): void;
}>();

const { t } = useI18n();

// Test mode — wired into /api/simulate/checkin via `isTest` flag. Backend
// skips DB writes (no IncrementCheckInAsync, no member resolve persist) but
// still publishes MemberCheckedInEvent so overlay preview reacts. Chat alias
// path is NOT yet test-mode aware (workflow rule writes data); test mode
// applies cleanly only to the alias=checkin direct path.
const isTestMode = ref(true);

const alias = ref<SimulateAlias>("chat");
const platformUserId = ref("");
const displayName = ref("");
const message = ref("");
const tier = ref("1000");
const recipientDisplayName = ref("");
const bits = ref(100);
const rewardId = ref("");
const twitchRewards = useTwitchRewardsStore();
const selectedRewardTitle = computed(() => {
  const match = twitchRewards.rewards.find(r => r.id === rewardId.value);
  return match?.title ?? null;
});
const stampCount = ref(1);
const rolesOptions = ["Broadcaster", "Subscriber", "Moderator", "VIP", "Follower"];
const selectedRoles = ref<string[]>([]);
const colorHex = ref("#FFCA28");
const selectedBadgeKeys = ref<string[]>([]);
const globalBadges = ref<PlatformBadgeDescriptor[]>([]);
const channelBadges = ref<PlatformBadgeDescriptor[]>([]);
const badgesReady = ref(false);
const badgesError = ref<string | null>(null);
const batchSize = ref(5);
const batchProgress = ref(0);
const batchRunning = ref(false);
const submitting = ref(false);
const ack = ref<SimulateAck | null>(null);
const errorCode = ref<string | null>(null);
const errorDetail = ref<string | null>(null);

const aliasOptions: SimulateAlias[] = [
  "chat",
  "follow",
  "sub",
  "giftsub",
  "raid",
  "bits",
  "redeem",
  "checkin"
];

const showMessageInput = computed(() => alias.value === "chat" || alias.value === "redeem");
const showIdentityInputs = computed(() => alias.value !== "checkin");
const featuredBadges = computed<PlatformBadgeDescriptor[]>(() => {
  const featuredSets = new Set(["broadcaster", "moderator", "vip", "subscriber", "founder", "premium"]);
  const featured = globalBadges.value.filter((b) => featuredSets.has(b.setId));
  return [...featured, ...channelBadges.value];
});

function toggleBadge(key: string): void {
  const idx = selectedBadgeKeys.value.indexOf(key);
  if (idx >= 0) {
    selectedBadgeKeys.value.splice(idx, 1);
  } else {
    selectedBadgeKeys.value.push(key);
  }
}

function isBadgeSelected(key: string): boolean {
  return selectedBadgeKeys.value.includes(key);
}

async function loadBadges(): Promise<void> {
  try {
    badgesError.value = null;
    const response = await getTwitchBadges();
    badgesReady.value = response?.ready ?? false;
    globalBadges.value = response?.global ?? [];
    channelBadges.value = response?.channel ?? [];
  } catch (caught) {
    badgesError.value = caught instanceof Error ? caught.message : String(caught);
  }
}

onMounted(() => {
  void loadBadges();
});

async function refreshRewards(): Promise<void> {
  await twitchRewards.refresh();
}

const eventStore = useEventStore();
const { events } = storeToRefs(eventStore);
watch(
  () => events.value[0]?.eventId,
  () => {
    const newest = events.value[0];
    if (
      newest?.type === PLATFORM_CONNECTION_CHANGED
      && newest.platform === "twitch"
    ) {
      void loadBadges();
    }
  }
);
const showTierInput = computed(() => alias.value === "sub" || alias.value === "giftsub");
const showRecipientInput = computed(() => alias.value === "giftsub");
const showBitsInput = computed(() => alias.value === "bits");
const showRewardInput = computed(() => alias.value === "redeem");
watch(showRewardInput, (visible) => {
  if (visible) {
    void twitchRewards.load();
  }
}, { immediate: true });
const showStampInput = computed(() => alias.value === "checkin");

const batchProgressLabel = computed(() => {
  if (batchSize.value <= 0) return "0%";
  const done = Math.round((batchProgress.value / 100) * batchSize.value);
  return `${done}/${batchSize.value}`;
});

async function onSubmit(event: Event): Promise<void> {
  event.preventDefault();
  if (batchRunning.value) return;

  submitting.value = true;
  ack.value = null;
  errorCode.value = null;
  errorDetail.value = null;

  try {
    let result: SimulateAck;

    if (alias.value === "checkin") {
      result = await postSimulateCheckIn({
        platformUserId: platformUserId.value.trim() || undefined,
        displayName: displayName.value.trim() || undefined,
        stampCount: stampCount.value,
        isTest: isTestMode.value
      });
    } else {
      const body: SimulateRequestBody = {};
      if (platformUserId.value.trim()) body.platformUserId = platformUserId.value.trim();
      if (displayName.value.trim()) body.displayName = displayName.value.trim();
      if (showMessageInput.value && message.value.trim()) body.message = message.value;
      if (showTierInput.value && tier.value.trim()) body.tier = tier.value.trim();
      if (showRecipientInput.value && recipientDisplayName.value.trim()) {
        body.recipientDisplayName = recipientDisplayName.value.trim();
      }
      if (showBitsInput.value) body.bits = bits.value;
      if (showRewardInput.value && rewardId.value.trim()) {
        body.rewardId = rewardId.value.trim();
        if (selectedRewardTitle.value) {
          body.rewardTitle = selectedRewardTitle.value;
        }
      }
      if (selectedRoles.value.length > 0) body.roles = [...selectedRoles.value];
      if (showIdentityInputs.value && selectedBadgeKeys.value.length > 0) {
        body.badges = [...selectedBadgeKeys.value];
      }
      if (showIdentityInputs.value && colorHex.value.trim()) {
        body.colorHex = colorHex.value.trim();
      }

      result = await postSimulate(alias.value, body);
    }

    ack.value = result;
    emit("simulated", result);
  } catch (caught) {
    if (caught instanceof ApiError) {
      errorCode.value = caught.errorCode ?? `HTTP_${caught.status}`;
      errorDetail.value = caught.body || null;
    } else {
      errorCode.value = "NETWORK_ERROR";
      errorDetail.value = caught instanceof Error ? caught.message : String(caught);
    }
  } finally {
    submitting.value = false;
  }
}

async function startBatchCheckin(): Promise<void> {
  if (batchRunning.value) return;

  batchRunning.value = true;
  batchProgress.value = 0;
  ack.value = null;
  errorCode.value = null;
  errorDetail.value = null;

  const total = batchSize.value;
  const userId = platformUserId.value.trim() || "batch-sim-user";
  const name = displayName.value.trim() || "Batch Sim User";

  try {
    // Sequential await is intentional: rate-limit batch checkin to avoid overwhelming
    // backend + give operator visible per-step progress feedback.
    // eslint-disable-next-line no-await-in-loop
    for (let index = 0; index < total; index += 1) {
      // eslint-disable-next-line no-await-in-loop
      const result = await postSimulateCheckIn({
        platformUserId: userId,
        displayName: name,
        stampCount: 1,
        isTest: isTestMode.value
      });

      batchProgress.value = Math.round(((index + 1) / total) * 100);
      ack.value = result;
      emit("simulated", result);
      // eslint-disable-next-line no-await-in-loop
      await new Promise((resolve) => setTimeout(resolve, 250));
    }
  } catch (caught) {
    if (caught instanceof ApiError) {
      errorCode.value = caught.errorCode ?? `HTTP_${caught.status}`;
      errorDetail.value = caught.body || null;
    } else {
      errorCode.value = "NETWORK_ERROR";
      errorDetail.value = caught instanceof Error ? caught.message : String(caught);
    }
  } finally {
    batchRunning.value = false;
    setTimeout(() => {
      batchProgress.value = 0;
    }, 1500);
  }
}
</script>

<template>
  <div class="simulate-container" :class="{ 'is-embedded': props.isEmbedded }" data-testid="simulate-controls">
    <header v-if="!props.isEmbedded" class="page-header">
      <h1 class="page-title">{{ t("simulate.title") }}</h1>
      <p class="page-subtitle">{{ t("simulate.subtitle") }}</p>
    </header>

    <section class="simulate-card">
      <h3 v-if="props.isEmbedded" class="card-title">{{ t("monitor.controls.cardTitle") }}</h3>

      <!-- Section 1: Test Mode -->
      <fieldset class="form-section section-test-mode" data-testid="section-test-mode">
        <legend class="section-legend">
          <span class="section-icon" aria-hidden="true">🛠️</span>
          {{ t("monitor.controls.section.testMode.title") }}
        </legend>
        <label class="toggle-row" data-testid="test-mode-toggle">
          <input
            v-model="isTestMode"
            type="checkbox"
            role="switch"
            :aria-checked="isTestMode"
            :disabled="batchRunning"
            class="toggle-switch"
          />
          <span class="toggle-label">
            <strong>{{ t("monitor.controls.testMode.label") }}</strong>
            <span class="toggle-helper">{{ t("monitor.controls.testMode.helper") }}</span>
          </span>
          <span class="toggle-state-chip" :class="{ on: isTestMode }">
            {{ isTestMode ? t("monitor.controls.testMode.on") : t("monitor.controls.testMode.off") }}
          </span>
        </label>
      </fieldset>

      <form class="simulate-form" @submit="onSubmit" novalidate>
        <!-- Section 2: Event Type -->
        <fieldset class="form-section section-event-type" data-testid="section-event-type">
          <legend class="section-legend">
            <span class="section-icon" aria-hidden="true">⚡</span>
            {{ t("monitor.controls.section.event.title") }}
          </legend>
          <label class="form-field">
            <span class="form-label">{{ t("simulate.alias") }}</span>
            <select v-model="alias" :disabled="batchRunning" :aria-label="t('simulate.alias')">
              <option v-for="option in aliasOptions" :key="option" :value="option">
                {{ t(`simulate.aliasOption.${option}`, option) }}
              </option>
            </select>
          </label>
        </fieldset>

        <!-- Section 3: Identity & Appearance (shown for non-checkin) -->
        <fieldset
          v-if="showIdentityInputs"
          class="form-section section-identity"
          data-testid="section-identity"
        >
          <legend class="section-legend">
            <span class="section-icon" aria-hidden="true">👤</span>
            {{ t("monitor.controls.section.identity.title") }}
          </legend>

          <div class="form-row">
            <label class="form-field">
              <span class="form-label">{{ t("simulate.platformUserId") }}</span>
              <input
                v-model="platformUserId"
                type="text"
                autocomplete="off"
                :disabled="batchRunning"
                placeholder="e.g. sim-user-id"
              />
            </label>

            <label class="form-field">
              <span class="form-label">{{ t("simulate.displayName") }}</span>
              <input
                v-model="displayName"
                type="text"
                autocomplete="off"
                :disabled="batchRunning"
                placeholder="e.g. Sim User"
              />
            </label>
          </div>

          <div class="form-field">
            <span class="form-label">{{ t("monitor.controls.identity.colorLabel") }}</span>
            <div class="color-picker-row">
              <input
                v-model="colorHex"
                type="color"
                class="color-swatch"
                :disabled="batchRunning"
                :aria-label="t('monitor.controls.identity.colorLabel')"
              />
              <input
                v-model="colorHex"
                type="text"
                class="color-hex-input"
                :disabled="batchRunning"
                placeholder="#FFCA28"
                maxlength="7"
              />
            </div>
          </div>

          <div class="form-field">
            <span class="form-label">{{ t("monitor.controls.identity.badgesLabel") }}</span>
            <p v-if="badgesError" class="field-help error">{{ badgesError }}</p>
            <p v-else-if="!badgesReady" class="field-help">
              {{ t("monitor.controls.identity.badgesNotReady") }}
            </p>
            <div v-if="featuredBadges.length > 0" class="badge-grid">
              <button
                v-for="badge in featuredBadges"
                :key="badge.key"
                type="button"
                class="badge-chip"
                :class="{ selected: isBadgeSelected(badge.key) }"
                :disabled="batchRunning"
                :title="`${badge.title ?? badge.setId} (${badge.key})`"
                @click="toggleBadge(badge.key)"
              >
                <img v-if="badge.imageUrl1x" :src="badge.imageUrl1x" alt="" class="badge-chip-img" />
                <span class="badge-chip-label">{{ badge.title ?? badge.setId }}</span>
              </button>
            </div>
            <p v-if="featuredBadges.length === 0 && badgesReady" class="field-help">
              {{ t("monitor.controls.identity.badgesEmpty") }}
            </p>
          </div>
        </fieldset>

        <!-- Section 4: Identity (compact — for checkin alias) -->
        <fieldset
          v-else
          class="form-section section-identity"
          data-testid="section-identity-compact"
        >
          <legend class="section-legend">
            <span class="section-icon" aria-hidden="true">👤</span>
            {{ t("monitor.controls.section.identity.title") }}
          </legend>
          <div class="form-row">
            <label class="form-field">
              <span class="form-label">{{ t("simulate.platformUserId") }}</span>
              <input
                v-model="platformUserId"
                type="text"
                autocomplete="off"
                :disabled="batchRunning"
                placeholder="e.g. sim-user-id"
              />
            </label>

            <label class="form-field">
              <span class="form-label">{{ t("simulate.displayName") }}</span>
              <input
                v-model="displayName"
                type="text"
                autocomplete="off"
                :disabled="batchRunning"
                placeholder="e.g. Sim User"
              />
            </label>
          </div>
        </fieldset>

        <!-- Section 5: Event-specific Fields -->
        <fieldset
          v-if="showMessageInput || showTierInput || showRecipientInput || showBitsInput || showRewardInput || showStampInput"
          class="form-section section-event-fields"
          data-testid="section-event-fields"
        >
          <legend class="section-legend">
            <span class="section-icon" aria-hidden="true">📝</span>
            {{ t("monitor.controls.section.eventFields.title") }}
          </legend>

          <label v-if="showMessageInput" class="form-field">
            <span class="form-label">
              {{ alias === 'redeem' ? t('monitor.controls.fields.redeemInput') : t("simulate.message") }}
            </span>
            <input
              v-model="message"
              type="text"
              autocomplete="off"
              :disabled="batchRunning"
              :placeholder="alias === 'chat' ? 'Type simulation message...' : 'Type redeem input...'"
            />
          </label>

          <label v-if="showTierInput" class="form-field">
            <span class="form-label">{{ t("simulate.tier") }}</span>
            <select v-model="tier" :disabled="batchRunning">
              <option value="1000">Tier 1 (1000)</option>
              <option value="2000">Tier 2 (2000)</option>
              <option value="3000">Tier 3 (3000)</option>
            </select>
          </label>

          <label v-if="showRecipientInput" class="form-field">
            <span class="form-label">{{ t("monitor.controls.fields.recipient") }}</span>
            <input
              v-model="recipientDisplayName"
              type="text"
              autocomplete="off"
              :disabled="batchRunning"
              placeholder="Recipient..."
            />
          </label>

          <label v-if="showBitsInput" class="form-field">
            <span class="form-label">{{ t("monitor.controls.fields.bits") }}</span>
            <input v-model="bits" type="number" min="1" :disabled="batchRunning" />
          </label>

          <label v-if="showRewardInput" class="form-field">
            <span class="form-label">{{ t("monitor.controls.fields.rewardId") }}</span>
            <div class="reward-select-row">
              <select
                v-model="rewardId"
                :disabled="batchRunning || twitchRewards.loading"
                data-testid="simulate-reward-select"
              >
                <option value="">{{ t("monitor.controls.fields.rewardPlaceholder") }}</option>
                <option
                  v-for="reward in twitchRewards.rewards"
                  :key="reward.id"
                  :value="reward.id"
                >
                  {{ reward.title }}
                </option>
              </select>
              <button
                type="button"
                class="icon-button"
                :disabled="twitchRewards.loading"
                :aria-label="t('ruleEditor.trigger.rewards.refresh')"
                :title="t('ruleEditor.trigger.rewards.refresh')"
                data-testid="simulate-reward-refresh"
                @click="refreshRewards"
              >
                {{ twitchRewards.loading ? "…" : "↻" }}
              </button>
            </div>
            <span
              v-if="twitchRewards.error"
              class="ack-error-code"
              data-testid="simulate-reward-error"
            >
              {{ twitchRewards.error }}
            </span>
            <span
              v-else-if="!twitchRewards.ready && !twitchRewards.loading"
              class="monitor-help"
              data-testid="simulate-reward-empty"
            >
              {{ t("ruleEditor.trigger.rewards.unauthorized") }}
            </span>
          </label>

          <label v-if="showStampInput" class="form-field">
            <span class="form-label">{{ t("monitor.controls.fields.stampCount") }}</span>
            <input v-model="stampCount" type="number" min="1" max="100" :disabled="batchRunning" />
          </label>
        </fieldset>

        <!-- Section 6: Batch Tools (checkin only) -->
        <fieldset
          v-if="alias === 'checkin'"
          class="form-section section-batch"
          data-testid="section-batch"
        >
          <legend class="section-legend">
            <span class="section-icon" aria-hidden="true">📦</span>
            {{ t("monitor.controls.section.batch.title") }}
          </legend>

          <div class="batch-input-row">
            <label class="form-field">
              <span class="form-label">{{ t("monitor.controls.batch.count") }}</span>
              <input
                v-model="batchSize"
                type="number"
                min="1"
                max="100"
                class="batch-size-input"
                :disabled="batchRunning"
                :title="t('monitor.controls.batch.count')"
              />
            </label>
            <button
              type="button"
              class="batch-button"
              :disabled="batchRunning"
              data-testid="batch-run-btn"
              @click="startBatchCheckin"
            >
              {{ t("monitor.controls.batch.run") }}
            </button>
          </div>

          <div v-if="batchProgress > 0 || batchRunning" class="batch-progress" data-testid="batch-progress">
            <div class="batch-progress-label">
              {{ t("monitor.controls.batch.progress") }}: {{ batchProgressLabel }}
            </div>
            <ProgressBar
              :value="batchProgress"
              :show-value="false"
              :pt="{
                root: { class: 'monitor-progress' },
                value: { class: 'monitor-progress__fill' }
              }"
              :aria-label="t('monitor.controls.batch.progress')"
              :aria-valuenow="batchProgress"
              aria-valuemin="0"
              aria-valuemax="100"
            />
          </div>
        </fieldset>

        <!-- Submit -->
        <div class="action-buttons-group">
          <button type="submit" class="primary-button" :disabled="submitting || batchRunning">
            {{ submitting ? t("simulate.submitting") : t("simulate.submit") }}
          </button>
        </div>
      </form>

      <div v-if="ack" class="ack-card" role="status" aria-live="polite" data-testid="simulate-ack">
        <p class="ack-headline">{{ t("simulate.ackHeadline") }}</p>
        <dl class="ack-grid">
          <dt>{{ t("simulate.field.accepted") }}</dt>
          <dd data-testid="ack-accepted">{{ ack.accepted }}</dd>
          <dt>{{ t("simulate.field.eventTypeKey") }}</dt>
          <dd class="monitor-mono">{{ ack.eventTypeKey }}</dd>
          <dt>{{ t("simulate.field.eventId") }}</dt>
          <dd class="monitor-mono" data-testid="ack-event-id">{{ ack.eventId }}</dd>
          <dt>{{ t("simulate.field.platformUserId", "Platform User ID") }}</dt>
          <dd data-testid="ack-platform-user-id">{{ ack.platformUserId ?? "-" }}</dd>
          <dt>{{ t("simulate.field.occurredAt") }}</dt>
          <dd>{{ new Date(ack.occurredAt).toLocaleTimeString() }}</dd>
        </dl>
      </div>

      <div v-if="errorCode" class="ack-error" role="alert" data-testid="simulate-error">
        <p class="ack-error-code">{{ errorCode }}</p>
        <p v-if="errorDetail" class="ack-error-detail">{{ errorDetail }}</p>
      </div>
    </section>
  </div>
</template>

<style scoped>
.simulate-container {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.simulate-container.is-embedded {
  padding: 0;
}

.page-header {
  margin-bottom: 1rem;
}

.page-title {
  margin: 0;
  color: var(--monitor-text-accent, #164f48);
  font-size: 1.35rem;
  font-weight: 800;
}

.page-subtitle {
  margin: 0.35rem 0 0;
  color: var(--monitor-text-muted, #6a847b);
}

.simulate-card {
  border: 1px solid var(--monitor-border, #d8e2dc);
  border-radius: var(--monitor-radius-card, 12px);
  background: var(--monitor-bg-elevated, #ffffff);
  box-shadow: var(--monitor-shadow-elevated, 0 18px 48px rgba(33, 58, 52, 0.12));
  padding: 20px;
}

.card-title {
  margin: 0 0 18px;
  color: var(--monitor-text-accent, #164f48);
  font-size: 1.05rem;
  font-weight: 800;
}

/* Section rhythm — dense control cards */
.form-section {
  border: 1px solid var(--monitor-border-subtle, rgba(214, 221, 229, 0.6));
  border-radius: 12px;
  padding: 14px 16px 16px;
  margin: 0 0 14px 0;
  background: var(--monitor-bg-elevated, #ffffff);
}

.form-section:last-of-type {
  margin-bottom: 0;
}

.section-legend {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 10px;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--monitor-text-muted, #5f6f80);
  background: var(--monitor-bg-surface, rgba(255, 255, 255, 0.92));
  border: 1px solid var(--monitor-border-subtle, rgba(214, 221, 229, 0.6));
  border-radius: var(--monitor-radius-pill, 999px);
}

.section-icon {
  font-size: 12px;
  line-height: 1;
}

/* Test mode toggle */
.toggle-row {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 4px 0;
  cursor: pointer;
}

.toggle-switch {
  width: 18px;
  height: 18px;
  accent-color: var(--monitor-accent, #2d9d78);
  cursor: pointer;
  flex-shrink: 0;
}

.toggle-label {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  font-size: 0.85rem;
  color: var(--monitor-text-primary, #213a34);
}

.toggle-helper {
  font-size: 0.75rem;
  color: var(--monitor-text-muted, #6a847b);
  font-weight: 400;
}

.toggle-state-chip {
  padding: 3px 10px;
  border-radius: var(--monitor-radius-pill, 999px);
  border: 1px solid var(--monitor-border, #d6dde5);
  background: var(--monitor-bg-elevated, #ffffff);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.04em;
  color: var(--monitor-text-muted, #6a847b);
}

.toggle-state-chip.on {
  color: var(--monitor-warning, #f59e0b);
  border-color: var(--monitor-warning-border, rgba(245, 158, 11, 0.22));
  background: var(--monitor-warning-subtle, #fff7e6);
}

.simulate-form {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.form-row {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.form-field + .form-field {
  margin-top: 10px;
}

.form-section .form-field + .form-field,
.form-section .form-row + .form-field {
  margin-top: 10px;
}

.form-label {
  color: var(--monitor-text-muted, #5b756c);
  font-size: 0.78rem;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.form-field input,
.form-field select {
  width: 100%;
  min-width: 0;
  border: 1px solid var(--monitor-border, #cfdcd6);
  border-radius: var(--monitor-radius-button, 8px);
  background: var(--monitor-bg-elevated, #ffffff);
  color: var(--monitor-text-primary, #213a34);
  padding: 0.65rem 0.75rem;
  font-size: 0.9rem;
  transition: border-color 160ms ease, box-shadow 160ms ease, background-color 160ms ease;
}

.form-field input:focus,
.form-field select:focus {
  outline: none;
  border-color: var(--monitor-accent, #2d9d78);
  box-shadow: 0 0 0 3px rgba(45, 157, 120, 0.14);
}

.form-field input:disabled,
.form-field select:disabled {
  background: var(--vp-bg-surface-muted);
  color: var(--vp-text-muted);
  cursor: not-allowed;
}

.color-picker-row {
  display: grid;
  grid-template-columns: 56px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
}

.color-swatch {
  width: 56px;
  height: 38px;
  border: 1px solid var(--monitor-border, #cfdcd6);
  border-radius: var(--monitor-radius-button, 8px);
  background: var(--vp-bg-surface);
  padding: 2px;
  cursor: pointer;
}

.color-hex-input {
  font-family: Consolas, "Courier New", monospace;
  text-transform: uppercase;
}

.badge-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(110px, 1fr));
  gap: 8px;
  padding: 10px;
  border: 1px dashed var(--monitor-border-subtle, rgba(214, 221, 229, 0.6));
  border-radius: 10px;
  background: var(--vp-bg-surface-muted);
  max-height: 200px;
  overflow-y: auto;
}

.badge-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 9px;
  border-radius: var(--monitor-radius-pill, 999px);
  border: 1px solid var(--monitor-border, #cfdcd6);
  background: var(--vp-bg-surface);
  color: var(--monitor-text-primary, #213a34);
  font-size: 0.78rem;
  font-weight: 700;
  cursor: pointer;
  transition: border-color 120ms ease, background-color 120ms ease, transform 120ms ease;
}

.badge-chip:hover:not(:disabled) {
  transform: translateY(-1px);
}

.badge-chip.selected {
  border-color: var(--monitor-accent, #2d9d78);
  background: var(--vp-bg-selected);
  color: var(--monitor-text-accent, #145a44);
}

.badge-chip:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.badge-chip-img {
  width: 16px;
  height: 16px;
  border-radius: 2px;
  flex-shrink: 0;
}

.badge-chip-label {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100px;
}

.field-help.error {
  color: var(--vp-text-danger);
}

.field-help {
  margin: 6px 0 0;
  color: var(--monitor-text-muted, #688279);
  font-size: 0.75rem;
  line-height: 1.5;
}

/* Batch */
.batch-input-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 10px;
  align-items: end;
}

.batch-size-input {
  text-align: center;
}

.batch-progress {
  margin-top: 12px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.batch-progress-label {
  color: var(--monitor-text-muted, #5d786f);
  font-size: 0.78rem;
  font-weight: 700;
}

/* PrimeVue ProgressBar — pt-driven */
:deep(.monitor-progress) {
  height: 8px;
  border-radius: var(--monitor-radius-pill, 999px);
  background: var(--vp-bg-surface-muted);
  overflow: hidden;
  position: relative;
}

:deep(.monitor-progress__fill) {
  height: 100%;
  background: var(--monitor-accent-gradient, linear-gradient(90deg, #2d9d78, #68b596));
  transition: width 200ms ease;
}

.action-buttons-group {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-top: 14px;
}

.primary-button,
.batch-button {
  border: none;
  border-radius: var(--monitor-radius-button, 10px);
  padding: 0.78rem 1rem;
  font-size: 0.92rem;
  font-weight: 800;
  cursor: pointer;
  transition: transform 160ms ease, box-shadow 160ms ease, opacity 160ms ease;
}

.primary-button {
  background: var(--monitor-accent-gradient, linear-gradient(135deg, #2d9d78, #267e62));
  color: var(--monitor-text-inverse, #ffffff);
  box-shadow: var(--monitor-shadow-accent, 0 8px 18px rgba(45, 157, 120, 0.22));
}

.primary-button:hover:not(:disabled),
.batch-button:hover:not(:disabled) {
  transform: translateY(-1px);
}

.primary-button:disabled,
.batch-button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
  transform: none;
}

.batch-button {
  background: var(--vp-bg-selected);
  color: var(--monitor-text-accent, #1d5d4f);
  border: 1px solid var(--vp-border-default);
  padding: 0.65rem 1.1rem;
}

.ack-card {
  margin-top: 16px;
  padding: 12px 14px;
  border-radius: 12px;
  border: 1px solid var(--monitor-success-border, #d7e3dc);
  background: var(--monitor-success-subtle, #f8fbf9);
}

.ack-headline {
  margin: 0 0 8px;
  color: var(--monitor-text-accent, #1d5d4f);
  font-size: 0.88rem;
  font-weight: 800;
}

.ack-grid {
  display: grid;
  grid-template-columns: 124px minmax(0, 1fr);
  gap: 4px 12px;
  margin: 0;
  font-size: 0.8rem;
}

.ack-grid dt {
  color: var(--monitor-text-muted, #688279);
  font-weight: 700;
}

.ack-grid dd {
  margin: 0;
  color: var(--monitor-text-primary, #213a34);
}

.monitor-mono {
  font-family: Consolas, "Courier New", monospace;
  overflow-wrap: anywhere;
}

.ack-error {
  margin-top: 16px;
  border-radius: 12px;
  border: 1px solid var(--monitor-danger-border, rgba(191, 88, 88, 0.22));
  background: var(--monitor-danger-subtle, rgba(244, 218, 218, 0.62));
  padding: 12px 14px;
}

.ack-error-code {
  margin: 0 0 4px;
  color: var(--monitor-danger, #b43a3a);
  font-weight: 800;
}

.ack-error-detail {
  margin: 0;
  color: var(--monitor-danger, #8a3f3f);
  font-size: 0.8rem;
  overflow-wrap: anywhere;
}

@media (max-width: 860px) {
  .simulate-card {
    padding: 16px;
  }

  .form-row,
  .batch-input-row,
  .ack-grid {
    grid-template-columns: 1fr;
  }

  .primary-button,
  .batch-button {
    width: 100%;
  }
}

.reward-select-row {
  display: flex;
  gap: 6px;
  align-items: center;
}

.reward-select-row select {
  flex: 1 1 auto;
}
</style>
