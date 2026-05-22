<script setup lang="ts">
import { HubConnectionState } from "@microsoft/signalr";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import {
  ApiError,
  getTwitchAuthStatus,
  resetTwitchAuth,
  startTwitchAuth,
  type TwitchAuthStatusResponse
} from "@/api/client";
import { useExponentialPollingFallback } from "@/composables/useExponentialPollingFallback";
import { useStreamEvents } from "@/composables/useStreamEvents";

const PLATFORM_CONNECTION_CHANGED = "platform.connection_changed";

const { t } = useI18n();
const status = ref<TwitchAuthStatusResponse | null>(null);
const loadingStatus = ref(false);
const starting = ref(false);
const resetting = ref(false);
const lastError = ref<string | null>(null);
const lastStartUrl = ref<string | null>(null);
const confirmResetOpen = ref(false);
const pollingActive = ref(false);

const { events, state: hubState, start: startHub } = useStreamEvents();
const pollingFallback = useExponentialPollingFallback({
  poll: async () => {
    await loadStatus();
  }
});

const noTwitchMode = computed(() => status.value !== null && !status.value.clientIdConfigured);
const startDisabled = computed(() =>
  starting.value || loadingStatus.value || !status.value?.clientIdConfigured
);
const resetDisabled = computed(() =>
  resetting.value || loadingStatus.value || !status.value?.hasRefreshToken
);

onMounted(async () => {
  await loadStatus();
  await startHub();
});

onUnmounted(() => {
  pollingFallback.stop();
});

watch(
  () => events.value[0]?.eventId,
  () => {
    const newest = events.value[0];
    if (newest?.type === PLATFORM_CONNECTION_CHANGED) {
      void loadStatus();
    }
  }
);

watch(hubState, (next, previous) => {
  if (next === HubConnectionState.Disconnected && previous !== undefined) {
    pollingFallback.start();
    pollingActive.value = true;
  } else if (next === HubConnectionState.Connected) {
    pollingFallback.stop();
    pollingActive.value = false;
  }
});

async function loadStatus(): Promise<void> {
  loadingStatus.value = true;
  lastError.value = null;
  try {
    status.value = await getTwitchAuthStatus();
  } catch (caught) {
    status.value = null;
    lastError.value = describeError(caught);
  } finally {
    loadingStatus.value = false;
  }
}

async function onStart(): Promise<void> {
  starting.value = true;
  lastError.value = null;
  lastStartUrl.value = null;
  try {
    const response = await startTwitchAuth();
    lastStartUrl.value = response.authorizeUrl;
    window.open(response.authorizeUrl, "_blank", "noopener,noreferrer");
  } catch (caught) {
    lastError.value = describeError(caught);
  } finally {
    starting.value = false;
  }
}

function requestReset(): void {
  lastError.value = null;
  confirmResetOpen.value = true;
}

async function onConfirmReset(): Promise<void> {
  resetting.value = true;
  try {
    await resetTwitchAuth();
    await loadStatus();
  } catch (caught) {
    lastError.value = describeError(caught);
  } finally {
    resetting.value = false;
    confirmResetOpen.value = false;
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
  <section aria-labelledby="twitch-auth-title">
    <header class="page-header">
      <h1 id="twitch-auth-title" class="page-title">{{ t("twitchAuth.title") }}</h1>
      <p class="page-subtitle">{{ t("twitchAuth.subtitle") }}</p>
    </header>

    <p
      v-if="noTwitchMode"
      class="status-error"
      role="status"
      data-testid="twitch-no-mode"
    >
      {{ t("twitchAuth.noTwitchMode") }}
    </p>

    <div class="status-grid">
      <article class="status-card">
        <p class="status-label">{{ t("twitchAuth.clientId") }}</p>
        <p class="status-value">{{ status?.clientIdConfigured ? t("twitchAuth.configured") : t("twitchAuth.missing") }}</p>
      </article>
      <article class="status-card">
        <p class="status-label">{{ t("twitchAuth.clientSecret") }}</p>
        <p class="status-value">{{ status?.clientSecretConfigured ? t("twitchAuth.configured") : t("twitchAuth.missing") }}</p>
      </article>
      <article class="status-card">
        <p class="status-label">{{ t("twitchAuth.refreshToken") }}</p>
        <p
          class="status-value"
          :data-testid="status?.hasRefreshToken ? 'twitch-has-token' : 'twitch-no-token'"
        >
          {{ status?.hasRefreshToken ? t("twitchAuth.present") : t("twitchAuth.absent") }}
        </p>
      </article>
    </div>

    <div class="members-toolbar">
      <button
        type="button"
        class="primary-button"
        :disabled="startDisabled"
        data-testid="twitch-start"
        @click="onStart"
      >
        {{ starting ? t("twitchAuth.starting") : t("twitchAuth.start") }}
      </button>
      <button
        type="button"
        class="icon-button"
        :disabled="resetDisabled"
        data-testid="twitch-reset"
        @click="requestReset"
      >
        {{ t("twitchAuth.reset") }}
      </button>
      <button
        type="button"
        class="icon-button"
        :disabled="loadingStatus"
        @click="loadStatus"
      >
        {{ loadingStatus ? t("twitchAuth.loading") : t("twitchAuth.refresh") }}
      </button>
    </div>

    <p
      v-if="lastStartUrl"
      class="rule-editor-hint"
      role="status"
      data-testid="twitch-start-url"
    >
      {{ t("twitchAuth.startHint") }} <code>{{ lastStartUrl }}</code>
    </p>

    <p
      v-if="lastError"
      class="ack-error-code"
      role="alert"
      data-testid="twitch-error"
    >
      {{ lastError }}
    </p>

    <ConfirmDialog
      :open="confirmResetOpen"
      :title="t('twitchAuth.resetConfirmTitle')"
      :message="t('twitchAuth.resetConfirmMessage')"
      :confirm-label="t('twitchAuth.resetConfirmAction')"
      :cancel-label="t('common.cancel')"
      :busy="resetting"
      @confirm="onConfirmReset"
      @cancel="confirmResetOpen = false"
    />
  </section>
</template>
