<script setup lang="ts">
import { onMounted } from "vue";
import { useI18n } from "vue-i18n";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { useOverlayHub } from "@/composables/useOverlayHub";

const { t } = useI18n();
const { events, start, state, lastEventAt, error } = useOverlayHub("member");

onMounted(() => {
  void start();
});
</script>

<template>
  <section class="overlay-panel" aria-labelledby="member-overlay-title">
    <header class="page-header">
      <h1 id="member-overlay-title" class="page-title">{{ t("overlay.member.title") }}</h1>
      <HubStatusChip :state="state" :last-event-at="lastEventAt" :error="error" />
    </header>
    <p role="status">{{ t("overlay.memberSkeleton") }}</p>
    <p class="status-label">{{ t("status.eventCount") }}: {{ events.length }}</p>
  </section>
</template>
