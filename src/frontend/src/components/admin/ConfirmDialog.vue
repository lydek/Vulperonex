<script setup lang="ts">
import { onMounted, ref, watch } from "vue";

const props = defineProps<{
  open: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  busy?: boolean;
}>();

const emit = defineEmits<{
  (event: "confirm"): void;
  (event: "cancel"): void;
}>();

const confirmButton = ref<HTMLButtonElement | null>(null);

watch(
  () => props.open,
  (open) => {
    if (open) {
      queueMicrotask(() => confirmButton.value?.focus());
    }
  }
);

onMounted(() => {
  if (props.open) {
    queueMicrotask(() => confirmButton.value?.focus());
  }
});

function onKeydown(event: KeyboardEvent): void {
  if (event.key === "Escape" && !props.busy) {
    emit("cancel");
  }
}
</script>

<template>
  <div
    v-if="open"
    class="confirm-backdrop"
    role="dialog"
    aria-modal="true"
    :aria-labelledby="`confirm-title-${title}`"
    @keydown="onKeydown"
  >
    <div class="confirm-card">
      <h2 :id="`confirm-title-${title}`" class="confirm-title">{{ title }}</h2>
      <p class="confirm-message">{{ message }}</p>
      <div class="confirm-actions">
        <button
          type="button"
          class="secondary-button"
          :disabled="busy"
          @click="emit('cancel')"
        >
          {{ cancelLabel }}
        </button>
        <button
          ref="confirmButton"
          type="button"
          class="danger-button"
          :disabled="busy"
          @click="emit('confirm')"
        >
          {{ confirmLabel }}
        </button>
      </div>
    </div>
  </div>
</template>
