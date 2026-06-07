<script setup lang="ts">
import { onMounted, ref, watch } from "vue";

const props = defineProps<{
  modelValue: string;
  placeholder?: string;
  multiline?: boolean;
  dataTestId?: string;
}>();

const emit = defineEmits<{ (event: "update:modelValue", value: string): void }>();

const root = ref<HTMLDivElement | null>(null);
// Guards re-rendering the DOM while the user is actively editing it, which would
// otherwise reset the caret on every keystroke.
let suppressRender = false;
let draggedToken: HTMLElement | null = null;
let dropMarker: HTMLElement | null = null;
// True once a drop landed inside this editor, so dragend knows the native
// "move out" behaviour did not silently delete the source chip.
let droppedInternally = false;

interface Segment {
  type: "text" | "token";
  value: string;
}

const TOKEN_PATTERN = /\{[^{}]+\}/g;

export interface VariableTokenInputExpose {
  insertToken: (token: string) => void;
}

function parseSegments(value: string): Segment[] {
  const segments: Segment[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;
  TOKEN_PATTERN.lastIndex = 0;
  while ((match = TOKEN_PATTERN.exec(value)) !== null) {
    if (match.index > lastIndex) {
      segments.push({ type: "text", value: value.slice(lastIndex, match.index) });
    }
    segments.push({ type: "token", value: match[0] });
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < value.length) {
    segments.push({ type: "text", value: value.slice(lastIndex) });
  }
  return segments;
}

function tokenLabel(token: string): string {
  return token.replace(/^\{|\}$/g, "");
}

function createChip(token: string): HTMLElement {
  const chip = document.createElement("span");
  chip.className = "token-chip";
  chip.setAttribute("contenteditable", "false");
  chip.setAttribute("draggable", "true");
  chip.dataset.token = token;

  const label = document.createElement("span");
  label.className = "token-chip__label";
  label.textContent = tokenLabel(token);
  chip.appendChild(label);

  return chip;
}

function render(value: string): void {
  const el = root.value;
  if (!el) {
    return;
  }
  el.replaceChildren();
  for (const segment of parseSegments(value)) {
    if (segment.type === "text") {
      el.appendChild(document.createTextNode(segment.value));
    } else {
      el.appendChild(createChip(segment.value));
    }
  }
}

function serialize(): string {
  const el = root.value;
  if (!el) {
    return "";
  }
  let output = "";
  el.childNodes.forEach((node) => {
    if (node.nodeType === Node.TEXT_NODE) {
      output += node.textContent ?? "";
    } else if (node instanceof HTMLElement) {
      if (node.classList.contains("token-drop-marker")) {
        // Transient drag indicator — never part of the value.
      } else if (node.classList.contains("token-chip") && node.dataset.token) {
        output += node.dataset.token;
      } else if (node.tagName === "BR") {
        output += "\n";
      } else {
        output += node.textContent ?? "";
      }
    }
  });
  return output;
}

// Self-managed undo/redo, because programmatic edits to a contenteditable do not
// participate in the browser's native ctrl+z history.
const undoStack: string[] = [];
const redoStack: string[] = [];
const MAX_HISTORY = 100;

function emitFromDom(): void {
  const value = serialize();
  if (value !== props.modelValue) {
    undoStack.push(props.modelValue);
    if (undoStack.length > MAX_HISTORY) {
      undoStack.shift();
    }
    redoStack.length = 0;
    suppressRender = true;
    emit("update:modelValue", value);
  }
}

function applyValue(value: string): void {
  render(value);
  suppressRender = true;
  emit("update:modelValue", value);
}

function isChip(node: Node | null): node is HTMLElement {
  return node instanceof HTMLElement && node.classList.contains("token-chip");
}

// The chip immediately adjacent to a collapsed caret, in the given direction.
function chipAtCaret(direction: "before" | "after"): HTMLElement | null {
  const selection = window.getSelection();
  if (!selection || selection.rangeCount === 0 || !selection.isCollapsed || !root.value) {
    return null;
  }
  const range = selection.getRangeAt(0);
  const node = range.startContainer;
  const offset = range.startOffset;

  if (node === root.value) {
    const index = direction === "before" ? offset - 1 : offset;
    const child = root.value.childNodes[index] ?? null;
    return isChip(child) ? child : null;
  }
  if (node.nodeType === Node.TEXT_NODE && node.parentNode === root.value) {
    if (direction === "before" && offset === 0) {
      return isChip(node.previousSibling) ? node.previousSibling : null;
    }
    if (direction === "after" && offset === (node.textContent?.length ?? 0)) {
      return isChip(node.nextSibling) ? node.nextSibling : null;
    }
  }
  return null;
}

function onKeydown(event: KeyboardEvent): void {
  const mod = event.ctrlKey || event.metaKey;
  if (mod && event.key.toLowerCase() === "z") {
    const redo = event.shiftKey;
    event.preventDefault();
    if (redo) {
      const next = redoStack.pop();
      if (next === undefined) {
        return;
      }
      undoStack.push(props.modelValue);
      applyValue(next);
    } else {
      const previous = undoStack.pop();
      if (previous === undefined) {
        return;
      }
      redoStack.push(props.modelValue);
      applyValue(previous);
    }
    return;
  }

  // Delete a chip like an atomic character when the caret sits next to it.
  if (event.key === "Backspace" || event.key === "Delete") {
    const chip = chipAtCaret(event.key === "Backspace" ? "before" : "after");
    if (chip) {
      event.preventDefault();
      chip.remove();
      emitFromDom();
    }
  }
}

function onInput(): void {
  emitFromDom();
}

function ensureMarker(): HTMLElement {
  if (!dropMarker) {
    dropMarker = document.createElement("span");
    dropMarker.className = "token-drop-marker";
    dropMarker.setAttribute("contenteditable", "false");
  }
  return dropMarker;
}

function removeMarker(): void {
  dropMarker?.remove();
}

function clearDrag(): void {
  removeMarker();
  draggedToken?.classList.remove("token-chip--dragging");
  draggedToken = null;
}

function onDragStart(event: DragEvent): void {
  const chip = (event.target as HTMLElement).closest(".token-chip");
  if (!chip || !(chip instanceof HTMLElement)) {
    return;
  }
  draggedToken = chip;
  chip.classList.add("token-chip--dragging");
  event.dataTransfer?.setData("text/plain", chip.dataset.token ?? "");
  event.dataTransfer!.effectAllowed = "move";
}

function onDragOver(event: DragEvent): void {
  if (!draggedToken || !root.value) {
    return;
  }
  event.preventDefault();
  event.dataTransfer!.dropEffect = "move";

  // Show an insertion bar at the caret position the drop would land on.
  const marker = ensureMarker();
  marker.remove();
  const caretRange = typeof document.caretRangeFromPoint === "function"
    ? document.caretRangeFromPoint(event.clientX, event.clientY)
    : null;
  if (caretRange && root.value.contains(caretRange.startContainer)) {
    caretRange.insertNode(marker);
  } else {
    root.value.appendChild(marker);
  }
}

function onDrop(event: DragEvent): void {
  if (!draggedToken || !root.value) {
    return;
  }
  event.preventDefault();
  droppedInternally = true;

  // No real move: dropped without a marker, or the marker sits right next to the
  // dragged chip. Leave the chip exactly where it was — never destroy it.
  if (!dropMarker || !dropMarker.parentNode
      || dropMarker.nextSibling === draggedToken
      || dropMarker.previousSibling === draggedToken) {
    clearDrag();
    return;
  }

  const chip = createChip(draggedToken.dataset.token ?? "");
  dropMarker.parentNode.replaceChild(chip, dropMarker);
  draggedToken.remove();
  clearDrag();
  emitFromDom();
}

function onDragEnd(): void {
  // Dropped outside the editor (or cancelled): the browser may have ripped the
  // dragged chip out of the contenteditable. Rebuild from the committed value so
  // nothing is lost.
  const restore = !droppedInternally;
  droppedInternally = false;
  clearDrag();
  if (restore) {
    render(props.modelValue);
  }
}

function insertToken(token: string): void {
  const el = root.value;
  if (!el) {
    return;
  }
  el.focus();

  const selection = window.getSelection();
  const chip = createChip(token);
  const spacer = document.createTextNode(" ");

  if (selection && selection.rangeCount > 0 && el.contains(selection.anchorNode)) {
    const range = selection.getRangeAt(0);
    range.deleteContents();
    range.insertNode(spacer);
    range.insertNode(chip);
    range.setStartAfter(spacer);
    range.collapse(true);
    selection.removeAllRanges();
    selection.addRange(range);
  } else {
    el.appendChild(chip);
    el.appendChild(spacer);
  }
  emitFromDom();
}

defineExpose<VariableTokenInputExpose>({ insertToken });

watch(
  () => props.modelValue,
  (next) => {
    if (suppressRender) {
      suppressRender = false;
      return;
    }
    if (next !== serialize()) {
      render(next);
    }
  }
);

onMounted(() => {
  render(props.modelValue);
});
</script>

<template>
  <div
    ref="root"
    class="token-input"
    :class="{ 'token-input--multiline': multiline }"
    :data-testid="dataTestId"
    :data-placeholder="placeholder"
    contenteditable="true"
    role="textbox"
    :aria-multiline="multiline ? 'true' : 'false'"
    @input="onInput"
    @keydown="onKeydown"
    @dragstart="onDragStart"
    @dragover="onDragOver"
    @drop="onDrop"
    @dragend="onDragEnd"
  ></div>
</template>

<style scoped>
.token-input {
  width: 100%;
  box-sizing: border-box;
  min-height: 38px;
  padding: 8px 44px 8px 12px;
  border: 1px solid var(--vp-border-default);
  border-radius: 6px;
  background: var(--vp-bg-surface);
  color: var(--vp-text-primary);
  font-size: 13px;
  line-height: 1.8;
  white-space: pre-wrap;
  word-break: break-word;
  cursor: text;
}

.token-input--multiline {
  min-height: 88px;
}

.token-input:focus {
  outline: none;
  border-color: var(--vp-accent);
}

.token-input:empty::before {
  content: attr(data-placeholder);
  color: var(--vp-text-muted);
}

.token-input :deep(.token-chip) {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin: 0 2px;
  padding: 1px 8px;
  border: 1px solid var(--vp-accent);
  border-radius: 999px;
  background: var(--vp-bg-selected);
  color: var(--vp-text-accent);
  font-family: Consolas, "Courier New", monospace;
  font-size: 12px;
  cursor: grab;
  user-select: none;
  white-space: nowrap;
}

.token-input :deep(.token-chip:active) {
  cursor: grabbing;
}

.token-input :deep(.token-chip--dragging) {
  opacity: 0.4;
}

/* Insertion bar showing where a dragged chip will drop. */
.token-input :deep(.token-drop-marker) {
  display: inline-block;
  width: 2px;
  height: 1.1em;
  margin: 0 1px;
  vertical-align: text-bottom;
  background: var(--vp-accent);
  border-radius: 1px;
  animation: token-drop-marker-blink 0.7s step-start infinite;
}

@keyframes token-drop-marker-blink {
  50% {
    opacity: 0.3;
  }
}

.token-input :deep(.token-chip__label) {
  pointer-events: none;
}
</style>
