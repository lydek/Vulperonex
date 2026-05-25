<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import {
  getOverlayPresetCatalog,
  type OverlayPresetDescriptor
} from "@/api/client";

const activeHub = ref<"chat" | "member" | "alerts">("chat");
const presets = ref<OverlayPresetDescriptor[]>([]);
const selectedPresetKey = ref<string>("");
const currentEnv = ref<"draft" | "production">("production");
const timestamp = ref<number>(Date.now());

const bgType = ref<"transparent" | "green" | "pink" | "color" | "url">("transparent");
const customColor = ref<string>("#f4f6f8");
const customUrl = ref<string>("");

onMounted(async () => {
  try {
    presets.value = await getOverlayPresetCatalog();
    selectDefaultPreset();
  } catch (err) {
    console.error("Failed to load presets catalog", err);
  }
});

watch(activeHub, () => {
  selectDefaultPreset();
});

function selectDefaultPreset(): void {
  const filtered = filteredPresets.value;
  selectedPresetKey.value = filtered.length > 0 ? filtered[0].key : "";
}

const filteredPresets = computed(() => presets.value.filter((p) => p.hubName === activeHub.value));
const selectedPreset = computed(() => presets.value.find((p) => p.key === selectedPresetKey.value));
const isCustomPreset = computed(() => selectedPreset.value?.kind === "custom");

const iframeSrc = computed(() => {
  const preset = selectedPreset.value;
  if (!preset) return "";

  let baseUri = preset.relativeUrl;
  if (preset.kind === "custom") {
    const slug = preset.label;
    baseUri = currentEnv.value === "draft"
      ? `/overlay/custom/${slug}/draft/index.html`
      : `/overlay/custom/${slug}/production/index.html`;
  }

  const params = new URLSearchParams({
    preset: preset.key,
    t: String(timestamp.value)
  });

  if (preset.kind === "builtin") {
    params.set("preview", "1");
  }

  const separator = baseUri.includes("?") ? "&" : "?";
  return `${baseUri}${separator}${params.toString()}`;
});

const containerStyle = computed(() => {
  switch (bgType.value) {
    case "green":
      return { backgroundColor: "#00ff00" };
    case "pink":
      return { backgroundColor: "#ff007f" };
    case "color":
      return { backgroundColor: customColor.value };
    case "url":
      if (customUrl.value.trim()) {
        const sanitized = sanitizeUrl(customUrl.value.trim());
        if (sanitized) {
          return {
            backgroundImage: `url('${sanitized}')`,
            backgroundSize: "cover",
            backgroundPosition: "center"
          };
        }
      }
      return { backgroundColor: "transparent" };
    default:
      return { backgroundColor: "transparent" };
  }
});

function sanitizeUrl(val: string): string {
  try {
    const url = new URL(val);
    if (url.protocol === "http:" || url.protocol === "https:") {
      return url.href;
    }
  } catch {
  }
  return "";
}

function reloadIframe(): void {
  timestamp.value = Date.now();
}
</script>

<template>
  <div class="overlay-monitor-card" data-testid="monitor-overlay-panel">
    <header class="monitor-controls-header">
      <div class="hub-tabs">
        <button
          v-for="hub in (['chat', 'member', 'alerts'] as const)"
          :key="hub"
          type="button"
          class="hub-tab-btn"
          :class="{ active: activeHub === hub }"
          @click="activeHub = hub"
        >
          {{ hub.toUpperCase() }}
        </button>
      </div>

      <div class="controls-right">
        <div class="selector-wrapper">
          <span class="selector-label">Preset</span>
          <select v-model="selectedPresetKey" class="preset-select">
            <option v-for="p in filteredPresets" :key="p.key" :value="p.key">
              {{ p.label }} ({{ p.kind }})
            </option>
          </select>
        </div>

        <div v-if="isCustomPreset" class="env-toggle">
          <button type="button" class="env-btn" :class="{ active: currentEnv === 'production' }" @click="currentEnv = 'production'">
            Prod
          </button>
          <button type="button" class="env-btn" :class="{ active: currentEnv === 'draft' }" @click="currentEnv = 'draft'">
            Draft
          </button>
        </div>

        <button type="button" class="reload-btn" title="Reload Overlay" @click="reloadIframe">
          Reload
        </button>
      </div>
    </header>

    <section class="bg-settings-row">
      <span class="tool-title">Preview Background</span>
      <div class="bg-options">
        <label class="bg-radio-label"><input v-model="bgType" type="radio" value="transparent" /><span>Transparent</span></label>
        <label class="bg-radio-label"><input v-model="bgType" type="radio" value="green" /><span class="dot green"></span> Green</label>
        <label class="bg-radio-label"><input v-model="bgType" type="radio" value="pink" /><span class="dot pink"></span> Pink</label>
        <label class="bg-radio-label"><input v-model="bgType" type="radio" value="color" /><span>Color Picker</span></label>
        <label class="bg-radio-label"><input v-model="bgType" type="radio" value="url" /><span>Image URL</span></label>
      </div>

      <input v-if="bgType === 'color'" v-model="customColor" type="color" class="color-picker-input" />
      <input
        v-if="bgType === 'url'"
        v-model="customUrl"
        type="text"
        placeholder="https://example.com/bg.png"
        class="url-input"
      />
    </section>

    <main class="iframe-container-outer">
      <div class="iframe-canvas" :style="containerStyle">
        <iframe
          v-if="iframeSrc"
          :src="iframeSrc"
          sandbox="allow-scripts allow-same-origin"
          class="preview-iframe"
          title="Overlay Preview"
        ></iframe>
        <div v-else class="no-preview">
          <p>No preset available for this hub.</p>
        </div>
      </div>
    </main>
  </div>
</template>

<style scoped>
.overlay-monitor-card {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  border: 1px solid #d6dde5;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 12px 28px rgba(15, 23, 32, 0.08);
}

.monitor-controls-header {
  height: 52px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 0 16px;
  border-bottom: 1px solid #d6dde5;
  background: #ffffff;
}

.hub-tabs,
.env-toggle {
  display: flex;
  gap: 4px;
  padding: 3px;
  border-radius: 8px;
  border: 1px solid #d6dde5;
  background: #f4f6f8;
}

.hub-tab-btn,
.env-btn {
  border: none;
  background: transparent;
  color: #5f6f80;
  padding: 5px 12px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
}

.hub-tab-btn.active,
.env-btn.active {
  background: #ffffff;
  color: #164f48;
  box-shadow: 0 1px 3px rgba(15, 23, 32, 0.12);
}

.controls-right,
.selector-wrapper {
  display: flex;
  align-items: center;
  gap: 8px;
}

.selector-label,
.tool-title {
  color: #5f6f80;
  font-size: 12px;
  font-weight: 600;
}

.preset-select,
.url-input {
  border: 1px solid #d6dde5;
  border-radius: 6px;
  background: #ffffff;
  color: #18202a;
  font-size: 12px;
  padding: 4px 8px;
}

.reload-btn {
  border: 1px solid #d6dde5;
  border-radius: 6px;
  background: #ffffff;
  color: #394756;
  font-size: 12px;
  font-weight: 700;
  padding: 5px 10px;
  cursor: pointer;
}

.bg-settings-row {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  padding: 8px 16px;
  border-bottom: 1px solid #d6dde5;
  background: #f8fafb;
  font-size: 12px;
}

.bg-options {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.bg-radio-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #394756;
}

.bg-radio-label input {
  accent-color: #1f6f64;
}

.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

.dot.green { background: #00ff00; }
.dot.pink { background: #ff007f; }

.color-picker-input {
  width: 24px;
  height: 24px;
  border: none;
  background: transparent;
  padding: 0;
}

.url-input {
  flex: 1;
  min-width: 180px;
}

.iframe-container-outer {
  flex: 1;
  background: #edf2f7;
  overflow: hidden;
}

.iframe-canvas {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.preview-iframe {
  width: 100%;
  height: 100%;
  border: none;
  background: transparent;
}

.no-preview {
  color: #5f6f80;
  font-size: 13px;
}
</style>
