<script setup lang="ts">
import { HubConnectionState } from "@microsoft/signalr";
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{
  state: HubConnectionState;
  lastEventAt?: number | null;
  error?: string | null;
}>();

const { t } = useI18n();

const variant = computed(() => {
  if (props.error) return "error";
  switch (props.state) {
    case HubConnectionState.Connected:
      return "ok";
    case HubConnectionState.Connecting:
    case HubConnectionState.Reconnecting:
      return "warn";
    default:
      return "idle";
  }
});

const stateLabel = computed(() => {
  if (props.error) return t("hub.state.error");
  switch (props.state) {
    case HubConnectionState.Connected:
      return t("hub.state.connected");
    case HubConnectionState.Connecting:
      return t("hub.state.connecting");
    case HubConnectionState.Reconnecting:
      return t("hub.state.reconnecting");
    case HubConnectionState.Disconnecting:
      return t("hub.state.disconnecting");
    default:
      return t("hub.state.disconnected");
  }
});

const lastEventLabel = computed(() => {
  if (!props.lastEventAt) return t("hub.lastEvent.never");
  const seconds = Math.max(0, Math.round((Date.now() - props.lastEventAt) / 1000));
  return t("hub.lastEvent.secondsAgo", { seconds });
});
</script>

<template>
  <span class="hub-chip" :data-variant="variant" role="status" :aria-label="stateLabel">
    <span class="hub-dot" aria-hidden="true" />
    <span class="hub-state">{{ stateLabel }}</span>
    <span class="hub-divider" aria-hidden="true">·</span>
    <span class="hub-last-event" data-testid="hub-last-event">{{ lastEventLabel }}</span>
    <span v-if="error" class="hub-error" data-testid="hub-error">{{ error }}</span>
  </span>
</template>
