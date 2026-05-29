<script setup lang="ts">
import { ref } from "vue";
import {
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
  DialogTrigger,
  Label,
  TabsContent,
  TabsList,
  TabsRoot,
  TabsTrigger
} from "reka-ui";

const activeTab = ref("basic");
const ruleName = ref("Timer check-in");
const commandName = ref("!checkin");
const enabled = ref(true);
</script>

<template>
  <DialogRoot>
    <DialogTrigger as-child>
      <button type="button" class="primary-button" data-testid="reka-poc-open">
        Open PoC
      </button>
    </DialogTrigger>
    <DialogPortal>
      <DialogOverlay class="reka-poc-overlay" />
      <DialogContent class="reka-poc-drawer" data-testid="reka-poc-drawer">
        <header class="reka-poc-header">
          <div>
            <DialogTitle class="section-title">Rule drawer PoC</DialogTitle>
            <DialogDescription class="status-label">
              Reka primitives styled only through local CSS and data attributes.
            </DialogDescription>
          </div>
          <DialogClose as-child>
            <button type="button" class="icon-button" data-testid="reka-poc-close">
              Close
            </button>
          </DialogClose>
        </header>

        <TabsRoot v-model="activeTab" class="reka-poc-tabs">
          <TabsList class="reka-poc-tab-list" aria-label="Rule sections">
            <TabsTrigger class="reka-poc-tab" value="basic" data-testid="reka-poc-tab-basic">
              Basic
            </TabsTrigger>
            <TabsTrigger class="reka-poc-tab" value="actions" data-testid="reka-poc-tab-actions">
              Actions
            </TabsTrigger>
            <TabsTrigger class="reka-poc-tab" value="errors" data-testid="reka-poc-tab-errors">
              Errors
            </TabsTrigger>
          </TabsList>

          <form class="reka-poc-form" data-testid="reka-poc-form">
            <TabsContent class="reka-poc-tab-panel" value="basic">
              <div class="form-field">
                <Label class="form-label" for="reka-poc-name">Rule name</Label>
                <input id="reka-poc-name" v-model="ruleName" data-testid="reka-poc-name" />
              </div>
              <label class="form-field form-field-inline">
                <input v-model="enabled" type="checkbox" data-testid="reka-poc-enabled" />
                <span class="form-label">Enabled</span>
              </label>
            </TabsContent>

            <TabsContent class="reka-poc-tab-panel" value="actions">
              <div class="form-field">
                <Label class="form-label" for="reka-poc-command">Command</Label>
                <input id="reka-poc-command" v-model="commandName" data-testid="reka-poc-command" />
              </div>
            </TabsContent>

            <TabsContent class="reka-poc-tab-panel" value="errors">
              <p class="status-label">Error handling tab shell preserves state across tab switches.</p>
            </TabsContent>
          </form>
        </TabsRoot>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>

<style scoped>
.reka-poc-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 32, 0.45);
  z-index: 100;
}

.reka-poc-drawer {
  position: fixed;
  inset: 0 0 0 auto;
  display: grid;
  grid-template-rows: auto 1fr;
  width: min(520px, 100vw);
  border-left: 1px solid #d6dde5;
  background: #ffffff;
  padding: 18px;
  box-shadow: 0 10px 30px rgba(15, 23, 32, 0.18);
  z-index: 101;
}

.reka-poc-drawer[data-state="open"] {
  animation: reka-poc-slide-in 0.18s ease-out;
}

.reka-poc-header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.reka-poc-tabs {
  display: grid;
  grid-template-rows: auto 1fr;
  gap: 14px;
  min-height: 0;
}

.reka-poc-tab-list {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid #d6dde5;
}

.reka-poc-tab {
  border: 0;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: #394756;
  cursor: pointer;
  font-weight: 700;
  padding: 8px 10px;
}

.reka-poc-tab[data-state="active"] {
  border-color: #1f6f64;
  color: #164f48;
}

.reka-poc-form,
.reka-poc-tab-panel {
  display: grid;
  gap: 12px;
  align-content: start;
}

@keyframes reka-poc-slide-in {
  from {
    transform: translateX(24px);
    opacity: 0.75;
  }

  to {
    transform: translateX(0);
    opacity: 1;
  }
}
</style>
