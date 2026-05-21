<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { useOverlayHub } from "@/composables/useOverlayHub";

const { t } = useI18n();
const { events, start, state, lastEventAt, error, clear } = useOverlayHub("member");
const confirmOpen = ref(false);
const clearing = ref(false);

onMounted(() => {
  void start();
});

async function onConfirm(): Promise<void> {
  clearing.value = true;
  try {
    await clear();
  } finally {
    clearing.value = false;
    confirmOpen.value = false;
  }
}
</script>

<template>
  <section class="overlay-panel" aria-labelledby="member-overlay-title">
    <header class="page-header">
      <h1 id="member-overlay-title" class="page-title">{{ t("overlay.member.title") }}</h1>
      <div class="overlay-toolbar">
        <HubStatusChip :state="state" :last-event-at="lastEventAt" :error="error" />
        <button
          type="button"
          class="icon-button"
          :aria-label="t('overlay.clearAriaLabel', { hub: t('overlay.member.title') })"
          @click="confirmOpen = true"
        >
          {{ t("overlay.clear") }}
        </button>
      </div>
    </header>
    <p role="status">{{ t("overlay.memberSkeleton") }}</p>
    <p class="status-label">{{ t("status.eventCount") }}: {{ events.length }}</p>

    <ConfirmDialog
      :open="confirmOpen"
      :title="t('overlay.clearConfirmTitle')"
      :message="t('overlay.clearConfirmMessage', { hub: t('overlay.member.title') })"
      :confirm-label="t('overlay.clearConfirmAction')"
      :cancel-label="t('common.cancel')"
      :busy="clearing"
      @confirm="onConfirm"
      @cancel="confirmOpen = false"
    />
  </section>
</template>
