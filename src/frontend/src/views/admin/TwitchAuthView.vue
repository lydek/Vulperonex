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
  startTwitchDeviceAuth,
  completeTwitchDeviceAuth,
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

// Device flow states
const deviceAuth = ref<{
  deviceCode: string;
  userCode: string;
  verificationUri: string;
  expiresIn: number;
  interval: number;
} | null>(null);
const deviceAuthStatus = ref<"idle" | "pending" | "success" | "expired">("idle");
let deviceAuthTimer: ReturnType<typeof setInterval> | null = null;
let deviceAuthTimeoutTimer: ReturnType<typeof setTimeout> | null = null;

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

  // Detect URL errors from Twitch callback redirect
  const params = new URLSearchParams(window.location.search);
  const oauthParam = params.get("oauth");
  if (oauthParam) {
    if (oauthParam === "client_secret_missing") {
      lastError.value = "TWITCH_CLIENT_SECRET_MISSING: 標準 OAuth 需要設定 Client Secret。若無，請將其保留為空並使用裝置授權流程。";
    } else if (oauthParam === "state_invalid") {
      lastError.value = "TWITCH_OAUTH_STATE_INVALID: 授權狀態過期或不合法。";
    } else if (oauthParam === "exchange_failed") {
      lastError.value = "TWITCH_OAUTH_EXCHANGE_FAILED: 金鑰交換失敗。";
    } else if (oauthParam === "error") {
      const errorType = params.get("error") || "";
      const desc = params.get("desc") || "";
      lastError.value = `Twitch 授權錯誤: ${errorType} - ${desc}`;
    } else if (oauthParam === "success") {
      // Show success if needed (the UI will also reload states)
    }
    // Clean up URL query parameters without reloading the page
    window.history.replaceState({}, document.title, window.location.pathname);
  }
});

onUnmounted(() => {
  pollingFallback.stop();
  stopDevicePolling();
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
  deviceAuth.value = null;
  deviceAuthStatus.value = "idle";
  stopDevicePolling();

  try {
    if (status.value?.clientSecretConfigured) {
      // Standard OAuth PKCE Flow
      const response = await startTwitchAuth();
      lastStartUrl.value = response.authorizeUrl;
      window.open(response.authorizeUrl, "_blank", "noopener,noreferrer");
    } else {
      // Device Flow (No Client Secret)
      const response = await startTwitchDeviceAuth();
      deviceAuth.value = response;
      deviceAuthStatus.value = "pending";
      
      // Auto open verification link
      window.open(response.verificationUri, "_blank", "noopener,noreferrer");
      
      // Start polling
      startDevicePolling(response.deviceCode, response.interval, response.expiresIn);
    }
  } catch (caught) {
    lastError.value = describeError(caught);
  } finally {
    starting.value = false;
  }
}

function startDevicePolling(deviceCode: string, intervalSeconds: number, expiresSeconds: number): void {
  const intervalMs = Math.max(intervalSeconds, 1) * 1000;
  deviceAuthTimer = setInterval(async () => {
    try {
      const success = await completeTwitchDeviceAuth(deviceCode);
      if (success) {
        stopDevicePolling();
        deviceAuthStatus.value = "success";
        deviceAuth.value = null;
        await loadStatus();
      }
    } catch (caught) {
      stopDevicePolling();
      lastError.value = describeError(caught);
      deviceAuthStatus.value = "idle";
    }
  }, intervalMs);

  deviceAuthTimeoutTimer = setTimeout(() => {
    stopDevicePolling();
    if (deviceAuthStatus.value === "pending") {
      deviceAuthStatus.value = "expired";
      lastError.value = "裝置授權超時，請點擊啟動授權重新試一次。";
    }
  }, expiresSeconds * 1000);
}

function stopDevicePolling(): void {
  if (deviceAuthTimer) {
    clearInterval(deviceAuthTimer);
    deviceAuthTimer = null;
  }
  if (deviceAuthTimeoutTimer) {
    clearTimeout(deviceAuthTimeoutTimer);
    deviceAuthTimeoutTimer = null;
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

    <p
      v-if="lastError"
      class="status-error"
      role="alert"
      style="margin-bottom: 1.5rem; text-align: left; padding: 1rem; border-radius: 6px;"
    >
      ⚠️ <strong>授權問題</strong>：<span data-testid="twitch-error">{{ lastError }}</span>
    </p>

    <div class="status-grid">
      <article class="status-card">
        <p class="status-label">{{ t("twitchAuth.clientId") }}</p>
        <p class="status-value">{{ status?.clientIdConfigured ? t("twitchAuth.configured") : t("twitchAuth.missing") }}</p>
      </article>
      <article class="status-card">
        <p class="status-label">{{ t("twitchAuth.clientSecret") }}</p>
        <p class="status-value">{{ status?.clientSecretConfigured ? t("twitchAuth.configured") : t("twitchAuth.missing") }} (選填)</p>
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

    <!-- Device Flow Guide Panel -->
    <div v-if="deviceAuth && deviceAuthStatus === 'pending'" class="status-card" style="margin-top: 1.5rem; border: 1.5px solid #6441a5; background-color: rgba(100, 65, 165, 0.05); padding: 1.5rem; border-radius: 8px; text-align: center;">
      <h3 style="margin-top: 0; color: #6441a5; display: flex; align-items: center; justify-content: center; gap: 0.5rem;">
        <span>🔑</span> Twitch 裝置授權流程中
      </h3>
      <p style="margin-bottom: 1rem;">系統已在新分頁開啟 Twitch 驗證網頁。若新分頁被瀏覽器阻擋，請手動點選下方按鈕前往：</p>
      <div style="margin: 1.5rem 0;">
        <a :href="deviceAuth.verificationUri" target="_blank" class="primary-button" style="text-decoration: none; display: inline-block; padding: 0.6rem 1.5rem; background-color: #6441a5; border-color: #6441a5;">
          前往 Twitch 驗證網頁
        </a>
      </div>
      <p style="font-size: 1.1rem; margin-bottom: 0.5rem;">請於驗證網頁中輸入以下代碼完成登入：</p>
      <div style="background-color: rgba(0, 0, 0, 0.05); border: 1px dashed #6441a5; padding: 0.8rem; border-radius: 6px; display: inline-block; min-width: 200px;">
        <strong style="font-size: 1.8rem; letter-spacing: 2px; color: #6441a5; font-family: monospace;">
          {{ deviceAuth.userCode }}
        </strong>
      </div>
      <p class="rule-editor-hint" style="margin-top: 1rem; color: #666;">
        ⏳ 正在偵測授權結果... 請在代碼失效前完成輸入。
      </p>
    </div>

    <div class="members-toolbar">
      <button
        type="button"
        class="primary-button"
        :disabled="startDisabled"
        data-testid="twitch-start"
        @click="onStart"
      >
        {{ starting ? t("twitchAuth.starting") : (status?.clientSecretConfigured ? t("twitchAuth.start") : "啟動裝置授權") }}
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
