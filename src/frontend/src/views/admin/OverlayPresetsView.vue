<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  deleteOverlayCustomPreset,
  getConfigValue,
  getOverlayCustomPresets,
  getOverlayPresetCatalog,
  setConfigValue,
  uploadOverlayCustomPreset,
  type OverlayCustomPresetMetadata,
  type OverlayPresetDescriptor
} from "@/api/client";

const { t } = useI18n();

const catalog = ref<OverlayPresetDescriptor[]>([]);
const customPresets = ref<OverlayCustomPresetMetadata[]>([]);
const chatPreset = ref("vulperonex-default");
const memberPreset = ref("rotan-checkin");
const alertsPreset = ref("vulperonex-alerts");
const showMemberCard = ref(false);
const uploadSlug = ref("");
const uploadFile = ref<File | null>(null);
const loading = ref(false);
const saving = ref(false);
const uploading = ref(false);
const message = ref<string | null>(null);
const error = ref<string | null>(null);

const chatOptions = computed(() => catalog.value.filter((entry) => entry.hubName === "chat"));
const memberOptions = computed(() => catalog.value.filter((entry) => entry.hubName === "member"));
const alertsOptions = computed(() => catalog.value.filter((entry) => entry.hubName === "alerts"));

onMounted(() => {
  void reload();
});

async function reload(): Promise<void> {
  loading.value = true;
  error.value = null;
  try {
    catalog.value = await getOverlayPresetCatalog();
    customPresets.value = await getOverlayCustomPresets();
    chatPreset.value = (await getConfigValue("overlay.chat.preset")).value || "vulperonex-default";
    memberPreset.value = (await getConfigValue("overlay.member.preset")).value || "rotan-checkin";
    alertsPreset.value = (await getConfigValue("overlay.alerts.preset")).value || "vulperonex-alerts";
    showMemberCard.value = (await getConfigValue("overlay.chat.show_member_card")).value === "true";
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

async function uploadPreset(): Promise<void> {
  if (!uploadFile.value) {
    error.value = t("overlayPresets.uploadMissing");
    return;
  }

  uploading.value = true;
  message.value = null;
  error.value = null;
  try {
    await uploadOverlayCustomPreset(uploadSlug.value.trim(), uploadFile.value);
    uploadSlug.value = "";
    uploadFile.value = null;
    message.value = t("overlayPresets.uploadSuccess");
    await reload();
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : String(caught);
  } finally {
    uploading.value = false;
  }
}

async function removePreset(slug: string): Promise<void> {
  error.value = null;
  await deleteOverlayCustomPreset(slug);
  await reload();
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
      <h2 class="section-title">{{ t("overlayPresets.uploadTitle") }}</h2>
      <div class="overlay-preset-grid">
        <label class="form-field">
          <span class="form-label">{{ t("overlayPresets.slug") }}</span>
          <input v-model="uploadSlug" type="text" placeholder="my-overlay" />
        </label>
        <label class="form-field">
          <span class="form-label">{{ t("overlayPresets.file") }}</span>
          <input type="file" accept=".html,.zip" @change="uploadFile = (($event.target as HTMLInputElement).files?.[0] ?? null)" />
        </label>
      </div>
      <button type="button" class="primary-button" :disabled="uploading" @click="uploadPreset">
        {{ uploading ? t("overlayPresets.uploading") : t("overlayPresets.upload") }}
      </button>
    </section>

    <section class="status-card overlay-preset-panel">
      <h2 class="section-title">{{ t("overlayPresets.customTitle") }}</h2>
      <ul v-if="customPresets.length > 0" class="event-list">
        <li v-for="preset in customPresets" :key="preset.slug" class="event-item overlay-preset-list-item">
          <div>
            <strong>{{ preset.slug }}</strong>
            <span class="monitor-mono">{{ preset.sizeBytes }} B</span>
          </div>
          <button type="button" class="icon-button" @click="removePreset(preset.slug)">
            {{ t("overlayPresets.delete") }}
          </button>
        </li>
      </ul>
      <p v-else role="status">{{ t("overlayPresets.empty") }}</p>
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

.overlay-preset-list-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
</style>
