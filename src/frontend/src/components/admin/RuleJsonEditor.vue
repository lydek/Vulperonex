<script setup lang="ts">
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";

const MAX_BYTES = 1_048_576; // 1 MB cap shared by file upload + paste + textarea.
const PASTE_DEBOUNCE_MS = 300;

const props = defineProps<{
  modelValue: string;
  ariaLabel?: string;
  disabled?: boolean;
}>();

const emit = defineEmits<{
  (event: "update:modelValue", value: string): void;
  (event: "parsed", payload: unknown): void;
  (event: "parse-error", message: string): void;
}>();

const { t } = useI18n();
const textareaRef = ref<HTMLTextAreaElement | null>(null);
const fileInputRef = ref<HTMLInputElement | null>(null);
const toast = ref<string | null>(null);
const parseError = ref<string | null>(null);

let pasteDebounce: ReturnType<typeof setTimeout> | null = null;
let lastValidatedValue = "";

defineExpose({ focus });

function focus(): void {
  textareaRef.value?.focus({ preventScroll: false });
}

function onInput(event: Event): void {
  const next = (event.target as HTMLTextAreaElement).value;
  emit("update:modelValue", next);
  schedulePasteValidation(next);
}

function schedulePasteValidation(next: string): void {
  if (pasteDebounce) {
    clearTimeout(pasteDebounce);
  }
  pasteDebounce = setTimeout(() => {
    pasteDebounce = null;
    validate(next);
  }, PASTE_DEBOUNCE_MS);
}

function validate(value: string): void {
  if (value === lastValidatedValue) return;
  lastValidatedValue = value;

  if (value.length > MAX_BYTES) {
    parseError.value = t("ruleEditor.tooLarge");
    emit("parse-error", parseError.value);
    return;
  }

  if (value.trim().length === 0) {
    parseError.value = null;
    return;
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    parseError.value = null;
    emit("parsed", parsed);
  } catch (error) {
    parseError.value = error instanceof Error ? error.message : String(error);
    emit("parse-error", parseError.value);
  }
}

function onPaste(event: ClipboardEvent): void {
  // Inspect pasted payload in a *plain* local variable, not the Vue ref, so the
  // reactivity system never has to scan an oversized string for changes.
  const pastedRaw = event.clipboardData?.getData("text/plain") ?? "";
  const projectedLength = (props.modelValue.length - selectionLength()) + pastedRaw.length;
  if (projectedLength > MAX_BYTES) {
    event.preventDefault();
    toast.value = t("ruleEditor.tooLarge");
    return;
  }
}

function selectionLength(): number {
  const element = textareaRef.value;
  if (!element) return 0;
  return Math.max(0, element.selectionEnd - element.selectionStart);
}

async function onFileChange(event: Event): Promise<void> {
  toast.value = null;
  parseError.value = null;
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;

  if (!/\.json$/i.test(file.name)) {
    toast.value = t("ruleEditor.fileExtension");
    resetFileInput();
    return;
  }

  if (file.type && file.type !== "application/json" && file.type !== "text/json") {
    toast.value = t("ruleEditor.fileMime");
    resetFileInput();
    return;
  }

  if (file.size > MAX_BYTES) {
    toast.value = t("ruleEditor.tooLarge");
    resetFileInput();
    return;
  }

  const text = await file.text();
  if (text.length > MAX_BYTES) {
    toast.value = t("ruleEditor.tooLarge");
    resetFileInput();
    return;
  }

  try {
    JSON.parse(text);
  } catch (error) {
    toast.value = error instanceof Error ? error.message : String(error);
    resetFileInput();
    return;
  }

  emit("update:modelValue", text);
  lastValidatedValue = "";
  schedulePasteValidation(text);
  resetFileInput();
}

function resetFileInput(): void {
  if (fileInputRef.value) {
    fileInputRef.value.value = "";
  }
}

watch(toast, (value) => {
  if (!value) return;
  window.setTimeout(() => {
    if (toast.value === value) {
      toast.value = null;
    }
  }, 4000);
});
</script>

<template>
  <div class="rule-editor">
    <div class="rule-editor-toolbar">
      <label class="rule-editor-file">
        <input
          ref="fileInputRef"
          type="file"
          accept=".json,application/json"
          :disabled="disabled"
          data-testid="rule-editor-file"
          @change="onFileChange"
        />
        {{ t("ruleEditor.upload") }}
      </label>
      <span class="rule-editor-hint">{{ t("ruleEditor.hint") }}</span>
    </div>

    <textarea
      ref="textareaRef"
      class="rule-editor-textarea monitor-mono"
      :value="modelValue"
      :aria-label="ariaLabel ?? t('ruleEditor.ariaLabel')"
      :maxlength="MAX_BYTES"
      :disabled="disabled"
      data-testid="rule-editor-textarea"
      spellcheck="false"
      rows="14"
      @input="onInput"
      @paste="onPaste"
    />

    <p
      v-if="parseError"
      class="ack-error-code"
      role="alert"
      data-testid="rule-editor-parse-error"
    >
      {{ parseError }}
    </p>

    <p
      v-if="toast"
      class="rule-editor-toast"
      role="alert"
      data-testid="rule-editor-toast"
    >
      {{ toast }}
    </p>
  </div>
</template>
