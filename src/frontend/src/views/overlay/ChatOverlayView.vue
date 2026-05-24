<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { useStreamEvents } from "@/composables/useStreamEvents";
import { useOverlayHub } from "@/composables/useOverlayHub";
import { ApiError, getConfigValue } from "@/api/client";
import {
  chatOverlayPresets,
  defaultChatOverlayPresetId,
  findChatOverlayPreset
} from "@/views/overlay/chatPresets";

const { t } = useI18n();
const route = useRoute();
const { events: systemEvents, start: startSystemEvents } = useStreamEvents();
const { events, start, state, lastEventAt, error, clear } = useOverlayHub("chat");
const confirmOpen = ref(false);
const clearing = ref(false);
const presetId = ref<string>(defaultChatOverlayPresetId);
const showMemberCard = ref(false);
const presetError = ref<string | null>(null);

const activePreset = computed(() => findChatOverlayPreset(presetId.value));

onMounted(async () => {
  await resolvePreset();
  void startSystemEvents();
  void start();
});

watch(
  () => systemEvents.value[0]?.eventId,
  () => {
    const latest = systemEvents.value[0];
    if (!latest || latest.type !== "system.config_changed") {
      return;
    }

    if (latest.key === "overlay.chat.preset" || latest.key === "overlay.chat.show_member_card") {
      void resolvePreset();
    }
  }
);

async function resolvePreset(): Promise<void> {
  const queryPreset = route.query.preset;
  if (typeof queryPreset === "string" && queryPreset.length > 0) {
    presetId.value = queryPreset;
  }

  try {
    const presetConfig = await getConfigValue("overlay.chat.preset");
    if (presetConfig.value && !(typeof queryPreset === "string" && queryPreset.length > 0)) {
      presetId.value = presetConfig.value;
    }

    const memberCardConfig = await getConfigValue("overlay.chat.show_member_card");
    showMemberCard.value = memberCardConfig.value === "true";
  } catch (caught) {
    if (caught instanceof ApiError) {
      presetError.value = caught.errorCode ?? `HTTP_${caught.status}`;
    } else if (caught instanceof Error) {
      presetError.value = caught.message;
    } else {
      presetError.value = String(caught);
    }
  }
}

async function onConfirm(): Promise<void> {
  clearing.value = true;
  try {
    await clear();
  } finally {
    clearing.value = false;
    confirmOpen.value = false;
  }
}

function onPresetChange(value: string): void {
  presetId.value = value;
}
</script>

<template>
  <section class="overlay-panel" aria-labelledby="chat-overlay-title">
    <header class="page-header">
      <h1 id="chat-overlay-title" class="page-title">{{ t("overlay.chat.title") }}</h1>
      <div class="overlay-toolbar">
        <HubStatusChip :state="state" :last-event-at="lastEventAt" :error="error" />
        <label class="form-field-inline">
          <span class="visually-hidden">{{ t("overlay.chat.preset") }}</span>
          <select
            data-testid="chat-overlay-preset-select"
            :value="presetId"
            @change="onPresetChange(($event.target as HTMLSelectElement).value)"
          >
            <option v-for="preset in chatOverlayPresets" :key="preset.id" :value="preset.id">
              {{ preset.label }}
            </option>
          </select>
        </label>
        <button
          type="button"
          class="icon-button"
          :aria-label="t('overlay.clearAriaLabel', { hub: t('overlay.chat.title') })"
          @click="confirmOpen = true"
        >
          {{ t("overlay.clear") }}
        </button>
      </div>
    </header>

    <p
      v-if="presetError"
      class="ack-error-code"
      role="alert"
      data-testid="chat-overlay-preset-error"
    >
      {{ presetError }}
    </p>

    <component
      :is="activePreset.component"
      :events="events"
      :empty-label="t('overlay.empty')"
      :show-member-card="showMemberCard"
    />

    <ConfirmDialog
      :open="confirmOpen"
      :title="t('overlay.clearConfirmTitle')"
      :message="t('overlay.clearConfirmMessage', { hub: t('overlay.chat.title') })"
      :confirm-label="t('overlay.clearConfirmAction')"
      :cancel-label="t('common.cancel')"
      :busy="clearing"
      @confirm="onConfirm"
      @cancel="confirmOpen = false"
    />
  </section>
</template>

<style scoped>
.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>
