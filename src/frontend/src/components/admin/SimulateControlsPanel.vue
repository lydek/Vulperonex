<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  ApiError,
  postSimulate,
  postSimulateCheckIn,
  type SimulateAck,
  type SimulateAlias,
  type SimulateRequestBody
} from "@/api/client";

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

const alias = ref<SimulateAlias>("chat");
const platformUserId = ref("");
const displayName = ref("");
const message = ref("");
const tier = ref("1000");
const recipientDisplayName = ref("");
const bits = ref(100);
const rewardId = ref("custom-reward");
const stampCount = ref(1);
const rolesOptions = ["Subscriber", "Moderator", "VIP", "Follower"];
const selectedRoles = ref<string[]>([]);
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
const showTierInput = computed(() => alias.value === "sub" || alias.value === "giftsub");
const showRecipientInput = computed(() => alias.value === "giftsub");
const showBitsInput = computed(() => alias.value === "bits");
const showRewardInput = computed(() => alias.value === "redeem");
const showStampInput = computed(() => alias.value === "checkin");

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
        stampCount: stampCount.value
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
      if (showRewardInput.value && rewardId.value.trim()) body.rewardId = rewardId.value.trim();
      if (selectedRoles.value.length > 0) body.roles = [...selectedRoles.value];

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
    for (let index = 0; index < total; index += 1) {
      const result = await postSimulateCheckIn({
        platformUserId: userId,
        displayName: name,
        stampCount: 1
      });

      batchProgress.value = Math.round(((index + 1) / total) * 100);
      ack.value = result;
      emit("simulated", result);
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
  <div class="simulate-container" :class="{ 'is-embedded': props.isEmbedded }">
    <header v-if="!props.isEmbedded" class="page-header">
      <h1 class="page-title">{{ t("simulate.title") }}</h1>
      <p class="page-subtitle">{{ t("simulate.subtitle") }}</p>
    </header>

    <section class="simulate-card">
      <h3 v-if="props.isEmbedded" class="card-title">模擬事件</h3>

      <form class="simulate-form" @submit="onSubmit" novalidate>
        <label class="form-field">
          <span class="form-label">{{ t("simulate.alias") }}</span>
          <select v-model="alias" :disabled="batchRunning" :aria-label="t('simulate.alias')">
            <option v-for="option in aliasOptions" :key="option" :value="option">
              {{ t(`simulate.aliasOption.${option}`, option) }}
            </option>
          </select>
        </label>

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

        <label v-if="showMessageInput" class="form-field">
          <span class="form-label">{{ alias === 'redeem' ? 'Redeem Input' : t("simulate.message") }}</span>
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
          <span class="form-label">Recipient Display Name</span>
          <input
            v-model="recipientDisplayName"
            type="text"
            autocomplete="off"
            :disabled="batchRunning"
            placeholder="Recipient..."
          />
        </label>

        <label v-if="showBitsInput" class="form-field">
          <span class="form-label">Bits Amount</span>
          <input v-model="bits" type="number" min="1" :disabled="batchRunning" />
        </label>

        <label v-if="showRewardInput" class="form-field">
          <span class="form-label">Reward ID</span>
          <input v-model="rewardId" type="text" autocomplete="off" :disabled="batchRunning" />
        </label>

        <label v-if="showStampInput" class="form-field">
          <span class="form-label">Stamp Count</span>
          <input v-model="stampCount" type="number" min="1" max="100" :disabled="batchRunning" />
        </label>

        <div v-if="alias !== 'checkin'" class="form-field">
          <span class="form-label">Streamer Roles</span>
          <div class="roles-grid">
            <label v-for="role in rolesOptions" :key="role" class="role-checkbox">
              <input v-model="selectedRoles" type="checkbox" :value="role" :disabled="batchRunning" />
              <span>{{ role }}</span>
            </label>
          </div>
          <p class="field-help">聊天事件現在會把這些角色一起送進聊天室 overlay 與事件流。</p>
        </div>

        <div class="action-buttons-group">
          <button type="submit" class="primary-button" :disabled="submitting || batchRunning">
            {{ submitting ? t("simulate.submitting") : t("simulate.submit") }}
          </button>

          <div v-if="alias === 'checkin'" class="batch-controls">
            <div class="batch-input-row">
              <input
                v-model="batchSize"
                type="number"
                min="1"
                max="100"
                class="batch-size-input"
                :disabled="batchRunning"
                title="Batch Size"
              />
              <button type="button" class="batch-button" :disabled="batchRunning" @click="startBatchCheckin">
                連續打卡
              </button>
            </div>
          </div>
        </div>

        <div v-if="batchProgress > 0" class="progress-bar-container">
          <div class="progress-bar-label">Batch CheckIn Progress: {{ batchProgress }}%</div>
          <div class="progress-bar">
            <div class="progress-fill" :style="{ width: `${batchProgress}%` }"></div>
          </div>
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
  color: #164f48;
  font-size: 1.35rem;
  font-weight: 800;
}

.page-subtitle {
  margin: 0.35rem 0 0;
  color: #6a847b;
}

.simulate-card {
  border: 1px solid #d8e2dc;
  border-radius: 24px;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(246, 249, 247, 0.98)),
    radial-gradient(circle at top left, rgba(45, 157, 120, 0.12), transparent 42%);
  box-shadow: 0 18px 48px rgba(33, 58, 52, 0.12);
  padding: 22px;
}

.card-title {
  margin: 0 0 18px;
  color: #164f48;
  font-size: 1.05rem;
  font-weight: 800;
}

.simulate-form {
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.form-row {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
}

.form-label {
  color: #5b756c;
  font-size: 0.82rem;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.form-field input,
.form-field select {
  width: 100%;
  min-width: 0;
  border: 1px solid #cfdcd6;
  border-radius: 14px;
  background: #ffffff;
  color: #213a34;
  padding: 0.88rem 0.95rem;
  font-size: 0.96rem;
  transition: border-color 160ms ease, box-shadow 160ms ease, background-color 160ms ease;
}

.form-field input:focus,
.form-field select:focus {
  outline: none;
  border-color: #2d9d78;
  box-shadow: 0 0 0 3px rgba(45, 157, 120, 0.14);
}

.form-field input:disabled,
.form-field select:disabled {
  background: #f5f8f6;
  color: #7f958d;
  cursor: not-allowed;
}

.roles-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  padding: 12px;
  border: 1px solid #d6e0db;
  border-radius: 16px;
  background: rgba(244, 248, 246, 0.9);
}

.role-checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  color: #355e56;
  font-size: 0.92rem;
  font-weight: 600;
}

.role-checkbox span {
  overflow-wrap: anywhere;
}

.role-checkbox input {
  accent-color: #2d9d78;
}

.field-help {
  margin: 8px 0 0;
  color: #688279;
  font-size: 0.78rem;
  line-height: 1.5;
}

.action-buttons-group {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-top: 4px;
}

.primary-button,
.batch-button {
  border: none;
  border-radius: 16px;
  padding: 0.95rem 1rem;
  font-size: 0.96rem;
  font-weight: 800;
  cursor: pointer;
  transition: transform 160ms ease, box-shadow 160ms ease, opacity 160ms ease;
}

.primary-button {
  background: linear-gradient(135deg, #2d9d78, #267e62);
  color: #ffffff;
  box-shadow: 0 14px 28px rgba(45, 157, 120, 0.22);
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

.batch-controls {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.batch-input-row {
  display: grid;
  grid-template-columns: 92px minmax(0, 1fr);
  gap: 10px;
}

.batch-size-input {
  text-align: center;
}

.batch-button {
  background: linear-gradient(135deg, #eef7f3, #dbeee5);
  color: #1d5d4f;
  border: 1px solid #bfd8cc;
}

.progress-bar-container {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px 14px;
  border-radius: 16px;
  border: 1px solid #d6e0db;
  background: rgba(244, 248, 246, 0.9);
}

.progress-bar-label {
  color: #5d786f;
  font-size: 0.8rem;
  font-weight: 700;
}

.progress-bar {
  height: 10px;
  border-radius: 999px;
  background: #dbe8e1;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #2d9d78, #68b596);
  transition: width 160ms ease;
}

.ack-card {
  margin-top: 16px;
  padding: 14px 16px;
  border-radius: 18px;
  border: 1px solid #d7e3dc;
  background: #f8fbf9;
}

.ack-headline {
  margin: 0 0 10px;
  color: #1d5d4f;
  font-size: 0.92rem;
  font-weight: 800;
}

.ack-grid {
  display: grid;
  grid-template-columns: 124px minmax(0, 1fr);
  gap: 6px 12px;
  margin: 0;
  font-size: 0.82rem;
}

.ack-grid dt {
  color: #688279;
  font-weight: 700;
}

.ack-grid dd {
  margin: 0;
  color: #213a34;
}

.monitor-mono {
  font-family: Consolas, "Courier New", monospace;
  overflow-wrap: anywhere;
}

.ack-error {
  margin-top: 16px;
  border-radius: 18px;
  border: 1px solid rgba(191, 88, 88, 0.22);
  background: rgba(244, 218, 218, 0.62);
  padding: 14px 16px;
}

.ack-error-code {
  margin: 0 0 6px;
  color: #b43a3a;
  font-weight: 800;
}

.ack-error-detail {
  margin: 0;
  color: #8a3f3f;
  font-size: 0.82rem;
  overflow-wrap: anywhere;
}

@media (max-width: 860px) {
  .simulate-card {
    padding: 18px;
  }

  .form-row,
  .roles-grid,
  .batch-input-row,
  .ack-grid {
    grid-template-columns: 1fr;
  }

  .primary-button,
  .batch-button {
    width: 100%;
  }
}
</style>
