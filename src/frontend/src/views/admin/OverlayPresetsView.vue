<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  getConfigValue,
  getOverlayLanInfo,
  getOverlayPresetCatalog,
  setConfigValue,
  uploadOverlayAsset,
  type OverlayLanInfo,
  type OverlayPresetDescriptor
} from "@/api/client";

const { t } = useI18n();

const catalog = ref<OverlayPresetDescriptor[]>([]);
const chatPreset = ref("vulperonex-default");
const memberPreset = ref("rotan-checkin");
const alertsPreset = ref("vulperonex-alerts");
const showMemberCard = ref(false);
const lanInfo = ref<OverlayLanInfo | null>(null);
const loading = ref(false);
const saving = ref(false);
const message = ref<string | null>(null);
const error = ref<string | null>(null);

// --- Overlay customization (A2) ---
const assistantDisplayName = ref("");
const assistantAvatarUrl = ref("");
const checkInDisplayName = ref("");
const memberBackgroundUrl = ref("");
const memberStampUrl = ref("");
const savingCustomize = ref(false);
const uploadingBackground = ref(false);
const uploadingStamp = ref(false);

const chatOptions = computed(() => catalog.value.filter((entry) => entry.hubName === "chat"));
const memberOptions = computed(() => catalog.value.filter((entry) => entry.hubName === "member"));
const alertsOptions = computed(() => catalog.value.filter((entry) => entry.hubName === "alerts"));

type ObsOverlayKey = "chat" | "member" | "alerts";

const obsOverlays = computed<{ key: ObsOverlayKey; label: string }[]>(() => [
  { key: "chat", label: t("overlayPresets.lan.chatOverlay") },
  { key: "member", label: t("overlayPresets.lan.memberOverlay") },
  { key: "alerts", label: t("overlayPresets.lan.alertsOverlay") }
]);

function buildObsUrl(key: ObsOverlayKey, info: OverlayLanInfo | null, mode: "local" | "lan"): string | null {
  if (mode === "local") {
    return `${window.location.origin}/overlay/${key}`;
  }

  const hasLanAccess = Boolean(info?.enabled && info.accessKey);
  const host = info?.suggestedHosts[0]?.trim();
  if (!hasLanAccess || !host) {
    return null;
  }

  return `http://${host}:${info!.overlayPort}/overlay/${key}?k=${encodeURIComponent(info!.accessKey!)}`;
}

async function copyObsUrl(key: ObsOverlayKey, mode: "local" | "lan"): Promise<void> {
  try {
    const latestInfo = mode === "lan" ? await getOverlayLanInfo() : lanInfo.value;
    if (latestInfo) {
      lanInfo.value = latestInfo;
    }

    const url = buildObsUrl(key, latestInfo, mode);
    if (!url) {
      error.value = t("overlayPresets.lan.unavailable");
      return;
    }

    await navigator.clipboard.writeText(url);
    message.value = t("overlayPresets.lan.copied");
    error.value = null;
  } catch {
    // Clipboard may be unavailable; user can select manually.
  }
}

onMounted(() => {
  void reload();
});

async function reload(): Promise<void> {
  loading.value = true;
  error.value = null;
  try {
    catalog.value = await getOverlayPresetCatalog();
    chatPreset.value = (await getConfigValue("overlay.chat.preset")).value || "vulperonex-default";
    memberPreset.value = (await getConfigValue("overlay.member.preset")).value || "rotan-checkin";
    alertsPreset.value = (await getConfigValue("overlay.alerts.preset")).value || "vulperonex-alerts";
    showMemberCard.value = (await getConfigValue("overlay.chat.show_member_card")).value === "true";
    assistantDisplayName.value = (await getConfigValue("overlay.chat.assistant_display_name")).value || "";
    assistantAvatarUrl.value = (await getConfigValue("overlay.chat.assistant_avatar_url")).value || "";
    checkInDisplayName.value = (await getConfigValue("overlay.chat.checkin_display_name")).value || "";
    memberBackgroundUrl.value = (await getConfigValue("overlay.member.background_url")).value || "";
    memberStampUrl.value = (await getConfigValue("overlay.member.stamp_url")).value || "";
    lanInfo.value = await getOverlayLanInfo();
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : String(caught);
  } finally {
    loading.value = false;
  }
}

async function saveSettings(): Promise<void> {
  saving.value = true;
  message.value = null;
  error.value = null;
  try {
    await setConfigValue("overlay.chat.preset", chatPreset.value);
    await setConfigValue("overlay.member.preset", memberPreset.value);
    await setConfigValue("overlay.alerts.preset", alertsPreset.value);
    await setConfigValue("overlay.chat.show_member_card", showMemberCard.value ? "true" : "false");
    message.value = t("overlayPresets.saveSuccess");
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : String(caught);
  } finally {
    saving.value = false;
  }
}

async function saveCustomization(): Promise<void> {
  savingCustomize.value = true;
  message.value = null;
  error.value = null;
  try {
    await setConfigValue("overlay.chat.assistant_display_name", assistantDisplayName.value.trim());
    await setConfigValue("overlay.chat.assistant_avatar_url", assistantAvatarUrl.value.trim());
    await setConfigValue("overlay.chat.checkin_display_name", checkInDisplayName.value.trim());
    await setConfigValue("overlay.member.background_url", memberBackgroundUrl.value.trim());
    await setConfigValue("overlay.member.stamp_url", memberStampUrl.value.trim());
    message.value = t("overlayPresets.customize.saveSuccess");
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : String(caught);
  } finally {
    savingCustomize.value = false;
  }
}

async function uploadImage(event: Event, slot: "background" | "stamp"): Promise<void> {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) {
    return;
  }

  const uploadingFlag = slot === "background" ? uploadingBackground : uploadingStamp;
  uploadingFlag.value = true;
  message.value = null;
  error.value = null;
  try {
    const result = await uploadOverlayAsset(file);
    if (slot === "background") {
      memberBackgroundUrl.value = result.url;
    } else {
      memberStampUrl.value = result.url;
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : String(caught);
  } finally {
    uploadingFlag.value = false;
    input.value = "";
  }
}
</script>

<template>
  <section aria-labelledby="overlay-presets-title">
    <header class="page-header">
      <h1 id="overlay-presets-title" class="page-title">{{ t("overlayPresets.title") }}</h1>
      <p class="page-subtitle">{{ t("overlayPresets.subtitle") }}</p>
    </header>

    <p v-if="loading" role="status">{{ t("overlayPresets.loading") }}</p>
    <p v-if="message" role="status" class="settings-success-msg">{{ message }}</p>
    <p v-if="error" role="alert" class="ack-error-code">{{ error }}</p>

    <section class="status-card overlay-preset-panel">
      <h2 class="section-title">{{ t("overlayPresets.settingsTitle") }}</h2>
      <div class="overlay-preset-grid">
        <label class="form-field">
          <span class="form-label">{{ t("overlayPresets.chatPreset") }}</span>
          <select v-model="chatPreset">
            <option v-for="entry in chatOptions" :key="`chat-${entry.key}`" :value="entry.key">{{ entry.label }}</option>
          </select>
        </label>
        <label class="form-field">
          <span class="form-label">{{ t("overlayPresets.memberPreset") }}</span>
          <select v-model="memberPreset">
            <option v-for="entry in memberOptions" :key="`member-${entry.key}`" :value="entry.key">{{ entry.label }}</option>
          </select>
        </label>
        <label class="form-field">
          <span class="form-label">{{ t("overlayPresets.alertsPreset") }}</span>
          <select v-model="alertsPreset">
            <option v-for="entry in alertsOptions" :key="`alerts-${entry.key}`" :value="entry.key">{{ entry.label }}</option>
          </select>
        </label>
        <label class="form-field overlay-preset-checkbox">
          <span class="form-label">{{ t("overlayPresets.memberChip") }}</span>
          <input v-model="showMemberCard" type="checkbox" />
        </label>
      </div>
      <button type="button" class="primary-button" :disabled="saving" @click="saveSettings">
        {{ saving ? t("overlayPresets.saving") : t("overlayPresets.save") }}
      </button>
    </section>

    <section class="status-card overlay-preset-panel">
      <h2 class="section-title">{{ t("overlayPresets.lan.title") }}</h2>
      <p class="page-subtitle">{{ t("overlayPresets.lan.hint") }}</p>
      <ul class="event-list" data-testid="overlay-obs-url-list">
        <li
          v-for="entry in obsOverlays"
          :key="entry.key"
          class="event-item overlay-preset-list-item overlay-obs-url-row"
          :data-testid="`overlay-obs-url-${entry.key}`"
        >
          <div class="overlay-obs-url-row__content">
            <strong>{{ entry.label }}</strong>
            <span>{{ t("overlayPresets.lan.copyHint") }}</span>
          </div>
          <div class="overlay-obs-url-row__actions">
            <button
              type="button"
              class="icon-button"
              :data-testid="`overlay-obs-copy-local-${entry.key}`"
              @click="copyObsUrl(entry.key, 'local')"
            >
              {{ t("overlayPresets.lan.copyLocal") }}
            </button>
            <button
              type="button"
              class="icon-button"
              :disabled="!lanInfo?.enabled"
              :data-testid="`overlay-obs-copy-lan-${entry.key}`"
              @click="copyObsUrl(entry.key, 'lan')"
            >
              {{ t("overlayPresets.lan.copyLan") }}
            </button>
          </div>
        </li>
      </ul>
    </section>

    <section class="status-card overlay-preset-panel" data-testid="overlay-customize-panel">
      <h2 class="section-title">{{ t("overlayPresets.customize.title") }}</h2>
      <p class="page-subtitle">{{ t("overlayPresets.customize.hint") }}</p>

      <h3 class="overlay-customize-subtitle">{{ t("overlayPresets.customize.textTitle") }}</h3>
      <div class="overlay-preset-grid">
        <label class="form-field">
          <span class="form-label">{{ t("overlayPresets.customize.assistantDisplayName") }}</span>
          <input v-model="assistantDisplayName" type="text" data-testid="overlay-customize-assistant-name" />
        </label>
        <label class="form-field">
          <span class="form-label">{{ t("overlayPresets.customize.checkInDisplayName") }}</span>
          <input v-model="checkInDisplayName" type="text" data-testid="overlay-customize-checkin-name" />
        </label>
        <label class="form-field">
          <span class="form-label">{{ t("overlayPresets.customize.assistantAvatarUrl") }}</span>
          <input v-model="assistantAvatarUrl" type="text" placeholder="/overlay/assets/..." data-testid="overlay-customize-assistant-avatar" />
        </label>
      </div>

      <h3 class="overlay-customize-subtitle">{{ t("overlayPresets.customize.imageTitle") }}</h3>
      <div class="overlay-preset-grid">
        <div class="form-field overlay-customize-image">
          <span class="form-label">{{ t("overlayPresets.customize.background") }}</span>
          <img v-if="memberBackgroundUrl" :src="memberBackgroundUrl" class="overlay-customize-preview" alt="" />
          <div class="overlay-customize-image-actions">
            <input
              type="file"
              accept="image/png,image/jpeg,image/webp,image/gif"
              :disabled="uploadingBackground"
              data-testid="overlay-customize-background-file"
              @change="uploadImage($event, 'background')"
            />
            <button
              v-if="memberBackgroundUrl"
              type="button"
              class="icon-button"
              @click="memberBackgroundUrl = ''"
            >
              {{ t("overlayPresets.customize.clear") }}
            </button>
          </div>
        </div>
        <div class="form-field overlay-customize-image">
          <span class="form-label">{{ t("overlayPresets.customize.stamp") }}</span>
          <img v-if="memberStampUrl" :src="memberStampUrl" class="overlay-customize-preview" alt="" />
          <div class="overlay-customize-image-actions">
            <input
              type="file"
              accept="image/png,image/jpeg,image/webp,image/gif"
              :disabled="uploadingStamp"
              data-testid="overlay-customize-stamp-file"
              @change="uploadImage($event, 'stamp')"
            />
            <button
              v-if="memberStampUrl"
              type="button"
              class="icon-button"
              @click="memberStampUrl = ''"
            >
              {{ t("overlayPresets.customize.clear") }}
            </button>
          </div>
        </div>
      </div>

      <button
        type="button"
        class="primary-button"
        :disabled="savingCustomize || uploadingBackground || uploadingStamp"
        data-testid="overlay-customize-save"
        @click="saveCustomization"
      >
        {{ savingCustomize ? t("overlayPresets.saving") : t("overlayPresets.customize.save") }}
      </button>
    </section>
  </section>
</template>

<style scoped>
.overlay-preset-panel {
  margin-bottom: 20px;
}

.overlay-preset-grid {
  display: grid;
  gap: 14px;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  margin-bottom: 14px;
}

.overlay-preset-checkbox {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.overlay-customize-subtitle {
  margin: 8px 0 12px;
  font-size: 13px;
  font-weight: 700;
  color: var(--vp-text-secondary);
}

.overlay-customize-image {
  gap: 8px;
}

.overlay-customize-preview {
  max-width: 100%;
  max-height: 120px;
  width: auto;
  object-fit: contain;
  border: 1px solid var(--vp-border-default);
  border-radius: 8px;
  background: var(--vp-bg-surface-subtle);
}

.overlay-customize-image-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.overlay-preset-list-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.overlay-obs-url-row__content {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.overlay-obs-url-row__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: flex-end;
}

@media (max-width: 720px) {
  .overlay-obs-url-row {
    align-items: stretch;
    flex-direction: column;
  }

  .overlay-obs-url-row__actions {
    justify-content: stretch;
  }

  .overlay-obs-url-row__actions button {
    flex: 1;
  }
}
</style>
