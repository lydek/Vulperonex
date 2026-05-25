<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { getHealth } from "@/api/client";
import SimulateControlsPanel from "@/components/admin/SimulateControlsPanel.vue";
import MonitorOverlayPanel from "@/components/admin/MonitorOverlayPanel.vue";
import ChatStreamPanel from "@/components/admin/ChatStreamPanel.vue";

const { t } = useI18n();

const showDrawer = ref(false);
const isDesktop = ref(true);
const serverHealth = ref<"healthy" | "unhealthy" | "checking">("checking");

function updateWidth(): void {
  isDesktop.value = window.innerWidth >= 1024;
  if (isDesktop.value) {
    showDrawer.value = false;
  }
}

async function checkHealth(): Promise<void> {
  try {
    const res = await getHealth();
    serverHealth.value = res.status === "Healthy" || res.status === "healthy" ? "healthy" : "unhealthy";
  } catch {
    serverHealth.value = "unhealthy";
  }
}

let healthInterval: ReturnType<typeof setInterval> | null = null;

onMounted(() => {
  isDesktop.value = window.innerWidth >= 1024;
  window.addEventListener("resize", updateWidth);
  void checkHealth();
  healthInterval = setInterval(checkHealth, 10000);
});

onUnmounted(() => {
  window.removeEventListener("resize", updateWidth);
  if (healthInterval) clearInterval(healthInterval);
});

function toggleDrawer(): void {
  showDrawer.value = !showDrawer.value;
}
</script>

<template>
  <div class="monitor-dashboard" data-testid="monitor-dashboard">
    <header class="dashboard-header">
      <div class="header-left">
        <span class="logo-icon" aria-hidden="true">V</span>
        <div>
          <p class="dashboard-eyebrow">Live Preview</p>
          <h1 class="dashboard-title">{{ t("monitor.dashboard.title") }}</h1>
        </div>
      </div>

      <div class="header-right">
        <button
          v-if="!isDesktop"
          type="button"
          class="toggle-sim-btn"
          @click="toggleDrawer"
        >
          {{ t("monitor.dashboard.simulateEvent") }}
        </button>

        <div class="status-chip" :class="serverHealth">
          <span class="chip-dot"></span>
          <span class="chip-text">{{ t("monitor.dashboard.server") }}{{ serverHealth.toUpperCase() }}</span>
        </div>
      </div>
    </header>

    <div class="dashboard-workspace">
      <aside v-if="isDesktop" class="workspace-aside simulate-column">
        <SimulateControlsPanel :isEmbedded="true" />
      </aside>

      <main class="workspace-main preview-column">
        <MonitorOverlayPanel />
      </main>

      <aside class="workspace-aside chat-column">
        <ChatStreamPanel />
      </aside>
    </div>

    <div
      v-if="!isDesktop && showDrawer"
      class="drawer-backdrop"
      @click="showDrawer = false"
    >
      <div class="drawer-content" @click.stop>
        <header class="drawer-header">
          <h3>{{ t("monitor.dashboard.simulateControls") }}</h3>
          <button type="button" class="drawer-close" @click="showDrawer = false">×</button>
        </header>
        <div class="drawer-body">
          <SimulateControlsPanel :isEmbedded="true" @simulated="showDrawer = false" />
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
  background: linear-gradient(180deg, #f8fafb 0%, #eef3f1 100%);
  color: #18202a;
}

.dashboard-header {
  height: 64px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 20px;
  border-bottom: 1px solid #d6dde5;
  background: rgba(255, 255, 255, 0.92);
  backdrop-filter: blur(12px);
}

.header-left,
.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.logo-icon {
  width: 34px;
  height: 34px;
  display: inline-grid;
  place-items: center;
  border-radius: 10px;
  background: linear-gradient(135deg, #1f6f64 0%, #2f8a78 100%);
  color: #ffffff;
  font-weight: 800;
}

.dashboard-eyebrow {
  margin: 0;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #5f6f80;
}

.dashboard-title {
  margin: 0;
  font-size: 18px;
  font-weight: 700;
  color: #164f48;
}

.toggle-sim-btn {
  border: none;
  border-radius: 8px;
  padding: 7px 14px;
  background: linear-gradient(135deg, #1f6f64 0%, #164f48 100%);
  color: #ffffff;
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(31, 111, 100, 0.18);
}

.status-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 5px 10px;
  border-radius: 999px;
  border: 1px solid #d6dde5;
  background: #ffffff;
  font-size: 11px;
  font-weight: 700;
}

.chip-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
  box-shadow: 0 0 6px currentColor;
}

.status-chip.healthy {
  color: #10b981;
  border-color: rgba(16, 185, 129, 0.22);
  background: #eaf7f1;
}

.status-chip.unhealthy {
  color: #ef4444;
  border-color: rgba(239, 68, 68, 0.22);
  background: #fdf0ef;
}

.status-chip.checking {
  color: #f59e0b;
  border-color: rgba(245, 158, 11, 0.22);
  background: #fff7e6;
}

.dashboard-workspace {
  flex: 1;
  display: flex;
  gap: 16px;
  padding: 16px;
  box-sizing: border-box;
  overflow: hidden;
}

.workspace-aside {
  width: 320px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
}

.workspace-main {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

@media (max-width: 1023px) {
  .dashboard-workspace {
    flex-direction: column;
    overflow-y: auto;
  }

  .workspace-aside.chat-column {
    width: 100%;
    height: 400px;
  }

  .preview-column {
    height: 500px;
    flex-shrink: 0;
  }
}

.drawer-backdrop {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  justify-content: flex-end;
  background: rgba(15, 23, 32, 0.4);
  backdrop-filter: blur(4px);
}

.drawer-content {
  width: 340px;
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #ffffff;
  border-left: 1px solid #d6dde5;
  box-shadow: -10px 0 30px rgba(15, 23, 32, 0.14);
  animation: slideIn 0.25s ease-out;
}

@keyframes slideIn {
  from { transform: translateX(100%); }
  to { transform: translateX(0); }
}

.drawer-header {
  height: 60px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 20px;
  border-bottom: 1px solid #d6dde5;
}

.drawer-header h3 {
  margin: 0;
  font-size: 15px;
  color: #164f48;
}

.drawer-close {
  border: none;
  background: transparent;
  color: #5f6f80;
  font-size: 20px;
  cursor: pointer;
}

.drawer-body {
  flex: 1;
  padding: 16px;
  overflow-y: auto;
}
</style>
