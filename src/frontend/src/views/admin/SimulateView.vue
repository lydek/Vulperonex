<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  ApiError,
  postSimulate,
  type SimulateAck,
  type SimulateAlias,
  type SimulateRequestBody
} from "@/api/client";

const { t } = useI18n();

const alias = ref<SimulateAlias>("chat");
const platformUserId = ref<string>("");
const displayName = ref<string>("");
const message = ref<string>("");
const tier = ref<string>("1000");
const submitting = ref(false);
const ack = ref<SimulateAck | null>(null);
const errorCode = ref<string | null>(null);
const errorDetail = ref<string | null>(null);

const aliasOptions: SimulateAlias[] = ["chat", "follow", "sub"];

const showMessageInput = computed(() => alias.value === "chat");
const showTierInput = computed(() => alias.value === "sub");

async function onSubmit(event: Event): Promise<void> {
  event.preventDefault();
  submitting.value = true;
  ack.value = null;
  errorCode.value = null;
  errorDetail.value = null;

  const body: SimulateRequestBody = {};
  if (platformUserId.value.trim()) body.platformUserId = platformUserId.value.trim();
  if (displayName.value.trim()) body.displayName = displayName.value.trim();
  if (showMessageInput.value && message.value.trim()) body.message = message.value;
  if (showTierInput.value && tier.value.trim()) body.tier = tier.value.trim();

  try {
    ack.value = await postSimulate(alias.value, body);
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
</script>

<template>
  <section aria-labelledby="simulate-title">
    <header class="page-header">
      <h1 id="simulate-title" class="page-title">{{ t("simulate.title") }}</h1>
      <p class="page-subtitle">{{ t("simulate.subtitle") }}</p>
    </header>

    <form class="simulate-form" @submit="onSubmit" novalidate>
      <label class="form-field">
        <span class="form-label">{{ t("simulate.alias") }}</span>
        <select v-model="alias" :aria-label="t('simulate.alias')">
          <option v-for="option in aliasOptions" :key="option" :value="option">
            {{ t(`simulate.aliasOption.${option}`) }}
          </option>
        </select>
      </label>

      <label class="form-field">
        <span class="form-label">{{ t("simulate.platformUserId") }}</span>
        <input v-model="platformUserId" type="text" autocomplete="off" />
      </label>

      <label class="form-field">
        <span class="form-label">{{ t("simulate.displayName") }}</span>
        <input v-model="displayName" type="text" autocomplete="off" />
      </label>

      <label v-if="showMessageInput" class="form-field">
        <span class="form-label">{{ t("simulate.message") }}</span>
        <input v-model="message" type="text" autocomplete="off" />
      </label>

      <label v-if="showTierInput" class="form-field">
        <span class="form-label">{{ t("simulate.tier") }}</span>
        <input v-model="tier" type="text" autocomplete="off" />
      </label>

      <button type="submit" class="primary-button" :disabled="submitting">
        {{ submitting ? t("simulate.submitting") : t("simulate.submit") }}
      </button>
    </form>

    <div
      v-if="ack"
      class="ack-card"
      role="status"
      aria-live="polite"
      data-testid="simulate-ack"
    >
      <p class="ack-headline">{{ t("simulate.ackHeadline") }}</p>
      <p class="ack-hint">
        {{ t("simulate.openOverlayHint") }}
        <a
          href="/overlay/chat"
          target="_blank"
          rel="noopener noreferrer"
          class="ack-hint-link"
          data-testid="open-chat-overlay"
        >{{ t("simulate.openChatOverlay") }}</a>
        <a
          href="/overlay/alerts"
          target="_blank"
          rel="noopener noreferrer"
          class="ack-hint-link"
          data-testid="open-alerts-overlay"
        >{{ t("simulate.openAlertsOverlay") }}</a>
      </p>
      <dl class="ack-grid">
        <dt>{{ t("simulate.field.accepted") }}</dt>
        <dd data-testid="ack-accepted">{{ ack.accepted }}</dd>
        <dt>{{ t("simulate.field.eventTypeKey") }}</dt>
        <dd>{{ ack.eventTypeKey }}</dd>
        <dt>{{ t("simulate.field.eventId") }}</dt>
        <dd data-testid="ack-event-id">{{ ack.eventId }}</dd>
        <dt>{{ t("simulate.field.platform") }}</dt>
        <dd>{{ ack.platform }}</dd>
        <dt>{{ t("simulate.field.platformUserId") }}</dt>
        <dd data-testid="ack-platform-user-id">{{ ack.platformUserId ?? "-" }}</dd>
        <dt>{{ t("simulate.field.displayName") }}</dt>
        <dd>{{ ack.displayName ?? "-" }}</dd>
        <dt>{{ t("simulate.field.occurredAt") }}</dt>
        <dd>{{ ack.occurredAt }}</dd>
      </dl>
    </div>

    <div
      v-if="errorCode"
      class="ack-error"
      role="alert"
      data-testid="simulate-error"
    >
      <p class="ack-error-code">{{ errorCode }}</p>
      <p v-if="errorDetail" class="ack-error-detail">{{ errorDetail }}</p>
    </div>
  </section>
</template>
