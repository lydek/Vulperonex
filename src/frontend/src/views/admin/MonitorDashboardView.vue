<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { getHealth } from "@/api/client";
import SimulateControlsPanel from "@/components/admin/SimulateControlsPanel.vue";
import MonitorOverlayPanel from "@/components/admin/MonitorOverlayPanel.vue";
import ChatStreamPanel from "@/components/admin/ChatStreamPanel.vue";

const { t } = useI18n();

const WIDE_BREAKPOINT = 1280;

const isWide = ref(true);
const isSiderOpen = ref(true);
const showDrawer = ref(false);
const serverHealth = ref<"healthy" | "unhealthy" | "checking">("checking");
const toggleBtnRef = ref<HTMLButtonElement | null>(null);
const drawerCloseBtnRef = ref<HTMLButtonElement | null>(null);

// rAF-debounced layout sync — avoids flap during continuous resize.
let resizeFrame: number | null = null;
function scheduleLayoutUpdate(): void {
  if (resizeFrame !== null) return;
  resizeFrame = window.requestAnimationFrame(() => {
    resizeFrame = null;
    applyLayoutForCurrentWidth();
  });
}

function applyLayoutForCurrentWidth(): void {
  const nextIsWide = window.innerWidth >= WIDE_BREAKPOINT;
  const wasWide = isWide.value;
  isWide.value = nextIsWide;

  // Only reset open-state on actual breakpoint transition — not on every resize tick.
  if (nextIsWide && !wasWide) {
    showDrawer.value = false; // entering wide: close drawer
  } else if (!nextIsWide && wasWide) {
    isSiderOpen.value = false; // entering narrow: collapse rail
  }
}

// Deep server-health polling — visibility-aware to skip wasted requests when
// dashboard tab is backgrounded. SignalR connection state lives in the chat
// panel (see useHubConnectionState); dashboard chip intentionally reflects
// DEEP service health (DB / module wiring) via /api/health, not hub liveness.
const HEALTH_POLL_INTERVAL_MS = 30_000;

async function checkHealth(): Promise<void> {
  try {
    const res = await getHealth();
    serverHealth.value = res.status === "Healthy" || res.status === "healthy" || res.status === "ok" ? "healthy" : "unhealthy";
  } catch {
    serverHealth.value = "unhealthy";
  }
}

let healthInterval: ReturnType<typeof setInterval> | null = null;

function startHealthPolling(): void {
  if (healthInterval !== null) return;
  healthInterval = setInterval(checkHealth, HEALTH_POLL_INTERVAL_MS);
}

function stopHealthPolling(): void {
  if (healthInterval !== null) {
    clearInterval(healthInterval);
    healthInterval = null;
  }
}

function onVisibilityChange(): void {
  if (document.hidden) {
    stopHealthPolling();
  } else {
    void checkHealth(); // immediate refresh on tab focus
    startHealthPolling();
  }
}

onMounted(() => {
  applyLayoutForCurrentWidth();
  isSiderOpen.value = isWide.value;
  window.addEventListener("resize", scheduleLayoutUpdate);
  document.addEventListener("keydown", onKeydown);
  document.addEventListener("visibilitychange", onVisibilityChange);
  void checkHealth();
  if (typeof document === "undefined" || !document.hidden) {
    startHealthPolling();
  }
});

onUnmounted(() => {
  window.removeEventListener("resize", scheduleLayoutUpdate);
  document.removeEventListener("keydown", onKeydown);
  document.removeEventListener("visibilitychange", onVisibilityChange);
  if (resizeFrame !== null) {
    window.cancelAnimationFrame(resizeFrame);
    resizeFrame = null;
  }
  stopHealthPolling();
});

function toggleSider(): void {
  if (isWide.value) {
    isSiderOpen.value = !isSiderOpen.value;
  } else {
    showDrawer.value = !showDrawer.value;
  }
}

function closeDrawer(): void {
  showDrawer.value = false;
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === "Escape" && showDrawer.value) {
    closeDrawer();
  }
}

// Basic focus management: open drawer → focus close button; close → return focus to toggle.
watch(showDrawer, async (open) => {
  await nextTick();
  if (open) {
    drawerCloseBtnRef.value?.focus();
  } else {
    toggleBtnRef.value?.focus();
  }
});
</script>

<template>
  <div class="monitor-dashboard" data-testid="monitor-dashboard">
    <header class="dashboard-header glass">
      <div class="header-left">
        <span class="header-icon" aria-hidden="true">⚙️</span>
        <div class="header-titles">
          <p class="dashboard-eyebrow">{{ t("monitor.dashboard.eyebrow") }}</p>
          <h1 class="dashboard-title">{{ t("monitor.dashboard.title") }}</h1>
        </div>
      </div>

      <div class="header-right">
        <button
          ref="toggleBtnRef"
          type="button"
          class="toggle-sim-btn sider-toggle"
          :aria-expanded="isWide ? isSiderOpen : showDrawer"
          :aria-controls="isWide ? 'monitor-sider' : 'monitor-drawer'"
          @click="toggleSider"
        >
          {{ t("monitor.dashboard.simulateEvent") }}
        </button>

        <div class="status-chip" :class="serverHealth" role="status" data-testid="status-chip">
          <span class="chip-dot" aria-hidden="true"></span>
          <span class="chip-text">
            {{ t("monitor.dashboard.signalrLabel") }}{{ t(`monitor.dashboard.health.${serverHealth}`) }}
          </span>
        </div>
      </div>
    </header>

    <div class="monitor-body">
      <aside
        v-if="isWide"
        id="monitor-sider"
        class="controls-sider"
        :class="{ open: isSiderOpen }"
        :aria-hidden="!isSiderOpen"
        data-testid="controls-sider"
      >
        <div class="sider-content">
          <SimulateControlsPanel :isEmbedded="true" />
        </div>
      </aside>

      <main class="main-area" data-testid="main-area">
        <section class="preview-panel" data-testid="preview-panel">
          <MonitorOverlayPanel />
        </section>
        <aside class="chat-panel" data-testid="chat-panel">
          <ChatStreamPanel />
        </aside>
      </main>
    </div>

    <div
      v-if="!isWide && showDrawer"
      class="drawer-backdrop"
      role="presentation"
      @click="closeDrawer"
    >
      <div
        id="monitor-drawer"
        class="drawer-content"
        role="dialog"
        :aria-label="t('monitor.dashboard.simulateControls')"
        @click.stop
      >
        <header class="drawer-header">
          <h3>{{ t("monitor.dashboard.simulateControls") }}</h3>
          <button
            ref="drawerCloseBtnRef"
            type="button"
            class="drawer-close"
            :aria-label="t('common.close')"
            @click="closeDrawer"
          >×</button>
        </header>
        <div class="drawer-body">
          <SimulateControlsPanel :isEmbedded="true" @simulated="closeDrawer" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.monitor-dashboard {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 48px);
  margin: -24px;
  overflow: hidden;
  background: var(--monitor-bg-base);
  color: var(--monitor-text-primary);
}

.dashboard-header {
  height: var(--monitor-header-height);
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 1.5rem;
  border-bottom: 1px solid var(--monitor-border);
  z-index: 10;
}

.dashboard-header.glass {
  background: var(--monitor-bg-surface);
  backdrop-filter: blur(var(--monitor-header-blur));
  -webkit-backdrop-filter: blur(var(--monitor-header-blur));
}

.header-left,
.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-icon {
  font-size: 24px;
  line-height: 1;
  filter: drop-shadow(0 1px 2px rgba(31, 111, 100, 0.25));
}

.header-titles {
  display: flex;
  flex-direction: column;
  line-height: 1.15;
}

.dashboard-eyebrow {
  margin: 0;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--monitor-text-muted);
}

.dashboard-title {
  margin: 0;
  font-size: 18px;
  font-weight: 700;
  letter-spacing: 0.04em;
  color: var(--monitor-text-accent);
}

.toggle-sim-btn {
  border: none;
  border-radius: var(--monitor-radius-button);
  padding: 7px 14px;
  background: var(--monitor-accent-gradient);
  color: var(--monitor-text-inverse);
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  box-shadow: var(--monitor-shadow-accent);
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}

.toggle-sim-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(31, 111, 100, 0.22);
}

.toggle-sim-btn:focus-visible {
  outline: 2px solid var(--monitor-accent);
  outline-offset: 2px;
}

.status-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 5px 12px;
  border-radius: var(--monitor-radius-pill);
  border: 1px solid var(--monitor-border);
  background: var(--monitor-bg-elevated);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.chip-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: currentColor;
  animation: monitor-pulse-dot 1.8s ease-in-out infinite;
}

.status-chip.healthy {
  color: var(--monitor-success);
  border-color: var(--monitor-success-border);
  background: var(--monitor-success-subtle);
}

.status-chip.unhealthy {
  color: var(--monitor-danger);
  border-color: var(--monitor-danger-border);
  background: var(--monitor-danger-subtle);
}

.status-chip.checking {
  color: var(--monitor-warning);
  border-color: var(--monitor-warning-border);
  background: var(--monitor-warning-subtle);
}

.monitor-body {
  flex: 1;
  display: flex;
  min-height: 0;
  position: relative;
}

/* Collapsible sider — width animates 0 → 380px */
.controls-sider {
  width: 0;
  background: var(--monitor-bg-surface);
  border-right: 0 solid var(--monitor-border);
  transition: width var(--monitor-sider-transition), border-right-width var(--monitor-sider-transition);
  overflow: hidden;
  flex-shrink: 0;
}

.controls-sider.open {
  width: var(--monitor-sider-width);
  border-right-width: 1px;
}

.sider-content {
  width: var(--monitor-sider-width);
  height: 100%;
  overflow-y: auto;
  padding: 16px;
  box-sizing: border-box;
}

.main-area {
  flex: 1;
  display: grid;
  grid-template-columns: 7fr 3fr;
  gap: 1px;
  background: var(--monitor-border-subtle);
  min-width: 0;
}

.preview-panel,
.chat-panel {
  display: flex;
  flex-direction: column;
  min-width: 0;
  height: 100%;
  background: var(--monitor-bg-elevated);
  overflow: hidden;
}

.chat-panel {
  border-left: 1px solid var(--monitor-border);
}

/* Medium screens — tighten chat column */
@media (max-width: 1440px) {
  .main-area {
    grid-template-columns: 6fr 4fr;
  }
}

/* Narrow — stack preview above chat */
@media (max-width: 1023px) {
  .main-area {
    grid-template-columns: 1fr;
    grid-template-rows: auto 1fr;
    overflow-y: auto;
  }

  .preview-panel {
    height: 400px;
    flex-shrink: 0;
  }

  .chat-panel {
    border-left: none;
    border-top: 1px solid var(--monitor-border);
    min-height: 500px;
  }
}

/* Drawer (narrow only) */
.drawer-backdrop {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  justify-content: flex-end;
  background: var(--monitor-bg-overlay);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
}

.drawer-content {
  width: 340px;
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--monitor-bg-elevated);
  border-left: 1px solid var(--monitor-border);
  box-shadow: var(--monitor-shadow-elevated);
  animation: monitor-drawer-slide 0.25s ease-out;
}

@keyframes monitor-drawer-slide {
  from { transform: translateX(100%); }
  to { transform: translateX(0); }
}

.drawer-header {
  height: 60px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 20px;
  border-bottom: 1px solid var(--monitor-border);
}

.drawer-header h3 {
  margin: 0;
  font-size: 15px;
  color: var(--monitor-text-accent);
}

.drawer-close {
  border: none;
  background: transparent;
  color: var(--monitor-text-muted);
  font-size: 20px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: var(--monitor-radius-button);
}

.drawer-close:hover {
  background: rgba(0, 0, 0, 0.04);
  color: var(--monitor-text-primary);
}

.drawer-body {
  flex: 1;
  padding: 16px;
  overflow-y: auto;
}

@media (prefers-reduced-motion: reduce) {
  .controls-sider,
  .toggle-sim-btn,
  .chip-dot,
  .drawer-content {
    transition: none !important;
    animation: none !important;
  }
}
</style>
