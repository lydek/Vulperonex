<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from "vue";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import * as monaco from "monaco-editor";
import editorWorker from "monaco-editor/esm/vs/editor/editor.worker?worker";
import jsonWorker from "monaco-editor/esm/vs/language/json/json.worker?worker";
import cssWorker from "monaco-editor/esm/vs/language/css/css.worker?worker";
import htmlWorker from "monaco-editor/esm/vs/language/html/html.worker?worker";
import tsWorker from "monaco-editor/esm/vs/language/typescript/ts.worker?worker";
import {
  getOverlayCustomPresetFiles,
  readOverlayCustomPresetFile,
  writeOverlayCustomPresetFile,
  deleteOverlayCustomPresetFile,
  deployOverlayCustomPreset,
  validateOverlayCustomPreset,
  getOverlayCustomPresetHistory,
  rollbackOverlayCustomPreset,
  type OverlayFileDescriptor,
  type OverlayHistoryVersion,
  type OverlayValidationIssue
} from "@/api/client";

self.MonacoEnvironment = {
  getWorker(_, label) {
    if (label === "json") {
      return new jsonWorker();
    }
    if (label === "css" || label === "less" || label === "scss") {
      return new cssWorker();
    }
    if (label === "html" || label === "handlebars" || label === "razor") {
      return new htmlWorker();
    }
    if (label === "typescript" || label === "javascript") {
      return new tsWorker();
    }
    return new editorWorker();
  }
};

interface Props {
  slug: string;
  visible: boolean;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "close"): void;
}>();

const files = ref<OverlayFileDescriptor[]>([]);
const history = ref<OverlayHistoryVersion[]>([]);
const currentFile = ref<string | null>(null);
const currentContent = ref("");
const originalContent = ref("");
const isModified = ref(false);
const activeTab = ref<"editor" | "history">("editor");
const validationIssues = ref<OverlayValidationIssue[]>([]);

const newFileName = ref("");
const showNewFile = ref(false);
const editorError = ref<string | null>(null);
const statusMessage = ref<string | null>(null);

const loading = ref(false);
const saving = ref(false);
const deploying = ref(false);

const container = ref<HTMLElement | null>(null);
let monacoEditor: any = null;

// ConfirmDialog state
const confirmDialog = ref<{
  open: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  onConfirm: (() => void) | (() => Promise<void>);
}>({
  open: false,
  title: "",
  message: "",
  confirmLabel: "",
  cancelLabel: "",
  onConfirm: () => {}
});

function openConfirm(
  title: string,
  message: string,
  confirmLabel: string,
  cancelLabel: string,
  onConfirm: (() => void) | (() => Promise<void>)
) {
  confirmDialog.value = {
    open: true,
    title,
    message,
    confirmLabel,
    cancelLabel,
    onConfirm
  };
}

function handleConfirm() {
  confirmDialog.value.open = false;
  void confirmDialog.value.onConfirm();
}

onMounted(() => {
  if (props.visible && props.slug) {
    void init();
  }
});

onUnmounted(() => {
  destroyEditor();
});

watch(
  () => props.slug,
  (newSlug) => {
    if (props.visible && newSlug) {
      void init();
    }
  }
);

watch(currentContent, (newVal) => {
  isModified.value = newVal !== originalContent.value;
});

async function init(): Promise<void> {
  loading.value = true;
  editorError.value = null;
  statusMessage.value = null;
  validationIssues.value = [];
  currentFile.value = null;
  currentContent.value = "";
  originalContent.value = "";
  destroyEditor();

  try {
    await loadFiles();
    await loadHistory();
    
    // Open index.html by default
    const indexFile = files.value.find(f => f.relativePath.toLowerCase() === "index.html");
    if (indexFile) {
      await doSelectFile(indexFile.relativePath);
    } else if (files.value.length > 0) {
      await doSelectFile(files.value[0].relativePath);
    }
  } catch (err: any) {
    editorError.value = err.message || "Initialization failed";
  } finally {
    loading.value = false;
  }
}

async function loadFiles(): Promise<void> {
  files.value = await getOverlayCustomPresetFiles(props.slug);
}

async function loadHistory(): Promise<void> {
  history.value = await getOverlayCustomPresetHistory(props.slug);
}

function destroyEditor(): void {
  if (monacoEditor) {
    monacoEditor.dispose();
    monacoEditor = null;
  }
}

function getLanguage(path: string): string {
  const ext = path.split(".").pop()?.toLowerCase();
  if (ext === "html" || ext === "htm") return "html";
  if (ext === "css") return "css";
  if (ext === "js" || ext === "json") return "javascript";
  return "plaintext";
}

function selectFile(path: string): void {
  if (isModified.value) {
    openConfirm(
      "Unsaved Changes",
      "Discard unsaved changes to this file?",
      "Discard",
      "Cancel",
      () => void doSelectFile(path)
    );
  } else {
    void doSelectFile(path);
  }
}

async function doSelectFile(path: string): Promise<void> {
  editorError.value = null;
  statusMessage.value = null;

  try {
    loading.value = true;
    currentFile.value = path;
    const content = await readOverlayCustomPresetFile(props.slug, path);
    currentContent.value = content;
    originalContent.value = content;
    isModified.value = false;

    if (!container.value) return;
    
    destroyEditor();

    monacoEditor = monaco.editor.create(container.value, {
      value: content,
      language: getLanguage(path),
      theme: "vs-dark",
      automaticLayout: true,
      fontSize: 14,
      minimap: { enabled: false },
      tabSize: 2
    });

    monacoEditor.onDidChangeModelContent(() => {
      currentContent.value = monacoEditor.getValue();
    });
  } catch (err: any) {
    editorError.value = err.message || "Failed to load file";
  } finally {
    loading.value = false;
  }
}

async function saveFile(): Promise<void> {
  if (!currentFile.value) return;
  saving.value = true;
  editorError.value = null;
  statusMessage.value = null;

  try {
    await writeOverlayCustomPresetFile(props.slug, currentFile.value, currentContent.value);
    originalContent.value = currentContent.value;
    isModified.value = false;
    statusMessage.value = "Draft file saved successfully!";
    await loadFiles();
  } catch (err: any) {
    // Extract syntax validation errors thrown by the API
    if (err.body) {
      try {
        const parsed = JSON.parse(err.body);
        editorError.value = parsed.error || err.message;
      } catch {
        editorError.value = err.body || err.message;
      }
    } else {
      editorError.value = err.message || "Save failed";
    }
  } finally {
    saving.value = false;
  }
}

async function createFile(): Promise<void> {
  const name = newFileName.value.trim();
  if (!name) return;

  saving.value = true;
  editorError.value = null;
  statusMessage.value = null;

  try {
    let initialVal = "";
    const ext = name.split(".").pop()?.toLowerCase();
    if (ext === "html") initialVal = "<!DOCTYPE html>\n<html>\n<head>\n  <meta charset=\"utf-8\">\n  <title>Custom Overlay</title>\n</head>\n<body>\n  <h1>My Custom Overlay</h1>\n</body>\n</html>";
    else if (ext === "css") initialVal = "body {\n  margin: 0;\n  background: transparent;\n}";
    else if (ext === "js") initialVal = "// Custom overlay script\nconsole.log('Overlay loaded');";

    await writeOverlayCustomPresetFile(props.slug, name, initialVal);
    newFileName.value = "";
    showNewFile.value = false;
    await loadFiles();
    await doSelectFile(name);
  } catch (err: any) {
    editorError.value = err.message || "Create failed";
  } finally {
    saving.value = false;
  }
}

function deleteFile(path: string): void {
  openConfirm(
    "Delete File",
    `Are you sure you want to delete ${path}?`,
    "Delete",
    "Cancel",
    () => void doDeleteFile(path)
  );
}

async function doDeleteFile(path: string): Promise<void> {
  loading.value = true;
  editorError.value = null;
  statusMessage.value = null;

  try {
    await deleteOverlayCustomPresetFile(props.slug, path);
    if (currentFile.value === path) {
      currentFile.value = null;
      currentContent.value = "";
      originalContent.value = "";
      destroyEditor();
    }
    await loadFiles();
  } catch (err: any) {
    editorError.value = err.message || "Delete failed";
  } finally {
    loading.value = false;
  }
}

function deployPreset(): void {
  if (isModified.value) {
    openConfirm(
      "Unsaved Changes",
      "You have unsaved changes in the editor. Deploy anyway?",
      "Deploy",
      "Cancel",
      () => void doDeployPreset()
    );
  } else {
    void doDeployPreset();
  }
}

async function doDeployPreset(): Promise<void> {
  deploying.value = true;
  editorError.value = null;
  statusMessage.value = null;

  try {
    await deployOverlayCustomPreset(props.slug);
    statusMessage.value = "Preset deployed to production successfully!";
    validationIssues.value = [];
    await loadHistory();
  } catch (err: any) {
    editorError.value = err.message || "Deploy failed";
  } finally {
    deploying.value = false;
  }
}

async function validatePreset(): Promise<void> {
  loading.value = true;
  editorError.value = null;
  statusMessage.value = null;

  try {
    validationIssues.value = await validateOverlayCustomPreset(props.slug);
    statusMessage.value = validationIssues.value.length === 0
      ? "Draft validation passed."
      : `Validation found ${validationIssues.value.length} issue(s).`;
  } catch (err: any) {
    editorError.value = err.message || "Validation failed";
  } finally {
    loading.value = false;
  }
}

function rollback(versionStamp: string): void {
  openConfirm(
    "Rollback Version",
    `Rollback to version ${versionStamp}? All current drafts will be overwritten.`,
    "Rollback",
    "Cancel",
    () => void doRollback(versionStamp)
  );
}

async function doRollback(versionStamp: string): Promise<void> {
  loading.value = true;
  editorError.value = null;
  statusMessage.value = null;

  try {
    await rollbackOverlayCustomPreset(props.slug, versionStamp);
    statusMessage.value = `Successfully rolled back to version ${versionStamp}!`;
    await init();
  } catch (err: any) {
    editorError.value = err.message || "Rollback failed";
  } finally {
    loading.value = false;
  }
}

function formatStamp(stamp: string): string {
  if (stamp.length !== 14) return stamp;
  const y = stamp.slice(0, 4);
  const m = stamp.slice(4, 6);
  const d = stamp.slice(6, 8);
  const h = stamp.slice(8, 10);
  const min = stamp.slice(10, 12);
  const s = stamp.slice(12, 14);
  return `${y}-${m}-${d} ${h}:${min}:${s}`;
}
</script>

<template>
  <div v-if="visible" class="editor-backdrop">
    <div class="editor-window">
      <!-- Top Header Section -->
      <header class="editor-header">
        <div class="header-left">
          <span class="editor-icon">⚡</span>
          <h2 class="editor-title">Custom Overlay IDE ({{ props.slug }})</h2>
        </div>
        
        <div class="header-right">
          <!-- Tab Switching -->
          <div class="tab-buttons">
            <button 
              type="button" 
              class="tab-btn" 
              :class="{ active: activeTab === 'editor' }" 
              @click="activeTab = 'editor'"
            >
              🛠️ Editor
            </button>
            <button 
              type="button" 
              class="tab-btn" 
              :class="{ active: activeTab === 'history' }" 
              @click="activeTab = 'history'"
            >
              🕒 History ({{ history.length }})
            </button>
          </div>

          <a 
            :href="`/overlay/custom/${props.slug}/index.html`" 
            target="_blank" 
            class="action-btn preview-btn"
          >
            👁️ Open Live
          </a>

          <button
            type="button"
            class="action-btn preview-btn"
            :disabled="loading || deploying"
            @click="validatePreset"
          >
            Validate
          </button>

          <button 
            type="button" 
            class="action-btn deploy-btn" 
            :disabled="deploying || loading"
            @click="deployPreset"
          >
            🚀 Deploy
          </button>
          
          <button type="button" class="close-btn" @click="emit('close')">✖</button>
        </div>
      </header>

      <!-- Main Content Area -->
      <div class="editor-content-body">
        
        <!-- EDITOR TAB -->
        <div v-show="activeTab === 'editor'" class="tab-panel editor-panel">
            <!-- Left File Management Tree -->
          <aside class="sidebar">
            <div class="sidebar-header">
              <h3>Files</h3>
              <button type="button" class="new-file-icon" title="New file" @click="showNewFile = !showNewFile">+</button>
            </div>

            <!-- Create New File Dialog -->
            <div v-if="showNewFile" class="new-file-form">
              <input 
                v-model="newFileName" 
                type="text" 
                placeholder="name.html" 
                @keyup.enter="createFile"
              />
              <div class="form-actions">
                <button type="button" class="sm-btn confirm" @click="createFile">Add</button>
                <button type="button" class="sm-btn cancel" @click="showNewFile = false">Cancel</button>
              </div>
            </div>

            <!-- File List -->
            <ul class="file-list">
              <li 
                v-for="file in files" 
                :key="file.relativePath"
                class="file-item"
                :class="{ active: currentFile === file.relativePath }"
                @click="selectFile(file.relativePath)"
              >
                <span class="file-label">📄 {{ file.relativePath }}</span>
                <button 
                  v-if="file.relativePath.toLowerCase() !== 'index.html'"
                  type="button" 
                  class="delete-file-btn" 
                  title="Delete file"
                  @click.stop="deleteFile(file.relativePath)"
                >
                  🗑️
                </button>
              </li>
            </ul>
          </aside>

          <!-- Right Monaco Editor -->
          <main class="editor-area">
            <div v-if="currentFile" class="editor-area-header">
              <span class="current-file-name">Editing: <code>{{ currentFile }}</code> <span v-if="isModified" class="mod-dot" title="Modified">*</span></span>
              <button 
                type="button" 
                class="action-btn save-btn" 
                :disabled="saving || !isModified" 
                @click="saveFile"
              >
                💾 Save Draft
              </button>
            </div>

            <!-- Error and Status Banner -->
            <div v-if="editorError" role="alert" class="msg-banner error-banner">
              ⚠️ <strong>Error:</strong> {{ editorError }}
            </div>
            <div v-if="statusMessage" role="status" class="msg-banner success-banner">
              ✅ {{ statusMessage }}
            </div>

            <ul v-if="validationIssues.length > 0" class="validation-issues">
              <li v-for="issue in validationIssues" :key="`${issue.code}-${issue.filePath ?? 'draft'}-${issue.message}`">
                <strong>{{ issue.severity.toUpperCase() }}</strong>
                <span>{{ issue.code }}</span>
                <span>{{ issue.filePath ?? "draft" }}</span>
                <span>{{ issue.message }}</span>
              </li>
            </ul>

            <div v-show="currentFile" ref="container" class="monaco-container"></div>
            
            <div v-if="!currentFile" class="empty-editor">
              <p>Select a file from the sidebar to start editing.</p>
            </div>
          </main>
        </div>

        <!-- HISTORY TAB -->
        <div v-show="activeTab === 'history'" class="tab-panel history-panel">
          <div class="history-content">
            <h3 class="section-title">Deployment History</h3>
            <p class="section-desc">Each deployment generates a version snapshot. You can roll back the current draft and production overlays to any of these snapshots.</p>

            <ul v-if="history.length > 0" class="history-list">
              <li v-for="version in history" :key="version.versionStamp" class="history-item">
                <div class="version-info">
                  <span class="version-badge">🏷️ {{ version.versionStamp }}</span>
                  <span class="version-date">{{ formatStamp(version.versionStamp) }}</span>
                </div>
                <button 
                  type="button" 
                  class="action-btn rollback-btn"
                  :disabled="loading"
                  @click="rollback(version.versionStamp)"
                >
                  ↩️ Rollback
                </button>
              </li>
            </ul>
            <p v-else class="empty-history">No deployment history found for this preset.</p>
          </div>
        </div>

      </div>

      <!-- ConfirmDialog -->
      <ConfirmDialog
        :open="confirmDialog.open"
        :title="confirmDialog.title"
        :message="confirmDialog.message"
        :confirm-label="confirmDialog.confirmLabel"
        :cancel-label="confirmDialog.cancelLabel"
        @confirm="handleConfirm"
        @cancel="confirmDialog.open = false"
      />
    </div>
  </div>
</template>

<style scoped>
.editor-backdrop {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(10, 10, 15, 0.85);
  backdrop-filter: blur(8px);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 10000;
}

.editor-window {
  width: 90vw;
  height: 85vh;
  background: #14141d;
  border: 1px solid #2d2d3d;
  border-radius: 12px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.6);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  font-family: system-ui, -apple-system, sans-serif;
  color: #e2e2e9;
}

/* Header */
.editor-header {
  height: 60px;
  background: #1a1a26;
  border-bottom: 1px solid #2d2d3d;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 20px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.editor-icon {
  font-size: 20px;
}

.editor-title {
  font-size: 16px;
  font-weight: 600;
  margin: 0;
  background: linear-gradient(135deg, #a78bfa 0%, #ec4899 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.tab-buttons {
  display: flex;
  background: #0f0f15;
  padding: 4px;
  border-radius: 8px;
  border: 1px solid #252535;
  margin-right: 12px;
}

.tab-btn {
  background: transparent;
  border: none;
  color: #8b8b9f;
  padding: 6px 12px;
  font-size: 13px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tab-btn.active {
  background: #252535;
  color: #f3f4f6;
  font-weight: 500;
}

.close-btn {
  background: transparent;
  border: none;
  color: #8b8b9f;
  font-size: 16px;
  cursor: pointer;
  padding: 6px;
  transition: color 0.2s;
}

.close-btn:hover {
  color: #ef4444;
}

/* Action buttons */
.action-btn {
  padding: 8px 14px;
  font-size: 13px;
  border-radius: 8px;
  font-weight: 500;
  cursor: pointer;
  border: none;
  transition: all 0.2s ease;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.preview-btn {
  background: #27273a;
  color: #cbd5e1;
  border: 1px solid #3f3f56;
}

.preview-btn:hover {
  background: #35354e;
  color: #f1f5f9;
}

.deploy-btn {
  background: linear-gradient(135deg, #8b5cf6 0%, #d946ef 100%);
  color: #ffffff;
  box-shadow: 0 4px 12px rgba(139, 92, 246, 0.3);
}

.deploy-btn:hover:not(:disabled) {
  opacity: 0.9;
  transform: translateY(-1px);
}

.deploy-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.save-btn {
  background: #10b981;
  color: #ffffff;
}

.save-btn:hover:not(:disabled) {
  background: #059669;
}

.save-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Content Body */
.editor-content-body {
  flex: 1;
  display: flex;
  overflow: hidden;
  background: #0f0f15;
}

.tab-panel {
  width: 100%;
  height: 100%;
  display: flex;
  overflow: hidden;
}

/* Editor Panel */
.editor-panel {
  display: flex;
}

/* Sidebar */
.sidebar {
  width: 250px;
  background: #151522;
  border-right: 1px solid #232333;
  display: flex;
  flex-direction: column;
}

.sidebar-header {
  padding: 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #232333;
}

.sidebar-header h3 {
  font-size: 13px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #8b8b9f;
  margin: 0;
}

.new-file-icon {
  background: transparent;
  border: none;
  color: #8b8b9f;
  font-size: 18px;
  cursor: pointer;
}

.new-file-icon:hover {
  color: #a78bfa;
}

.new-file-form {
  padding: 12px;
  background: #1d1d2f;
  border-bottom: 1px solid #2d2d44;
}

.new-file-form input {
  width: 100%;
  padding: 6px 8px;
  background: #0f0f15;
  border: 1px solid #3f3f56;
  border-radius: 4px;
  color: #e2e2e9;
  font-size: 12px;
  margin-bottom: 8px;
  box-sizing: border-box;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 6px;
}

.sm-btn {
  padding: 4px 8px;
  font-size: 11px;
  border-radius: 4px;
  cursor: pointer;
  border: none;
}

.sm-btn.confirm {
  background: #8b5cf6;
  color: white;
}

.sm-btn.cancel {
  background: transparent;
  color: #8b8b9f;
}

.file-list {
  list-style: none;
  padding: 8px 0;
  margin: 0;
  overflow-y: auto;
  flex: 1;
}

.file-item {
  padding: 10px 16px;
  font-size: 13px;
  cursor: pointer;
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: #a1a1b5;
  transition: all 0.15s ease;
}

.file-item:hover {
  background: #1b1b2f;
  color: #e2e2e9;
}

.file-item.active {
  background: #252538;
  color: #a78bfa;
  font-weight: 500;
  border-left: 3px solid #8b5cf6;
}

.delete-file-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.15s;
}

.file-item:hover .delete-file-btn {
  opacity: 0.6;
}

.file-item:hover .delete-file-btn:hover {
  opacity: 1;
}

/* Editor Area */
.editor-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: #0f0f15;
}

.editor-area-header {
  height: 44px;
  background: #13131c;
  border-bottom: 1px solid #232333;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 16px;
}

.current-file-name {
  font-size: 13px;
  color: #8b8b9f;
}

.current-file-name code {
  color: #e2e2e9;
  font-weight: 500;
}

.mod-dot {
  color: #ec4899;
  margin-left: 4px;
}

.monaco-container {
  flex: 1;
  width: 100%;
  background: #1e1e1e;
}

.empty-editor {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
  color: #52526b;
  font-size: 14px;
}

/* Messages Banner */
.msg-banner {
  padding: 10px 16px;
  font-size: 13px;
  line-height: 1.4;
}

.error-banner {
  background: rgba(239, 68, 68, 0.15);
  color: #fca5a5;
  border-bottom: 1px solid rgba(239, 68, 68, 0.3);
}

.success-banner {
  background: rgba(16, 185, 129, 0.15);
  color: #a7f3d0;
  border-bottom: 1px solid rgba(16, 185, 129, 0.3);
}

.validation-issues {
  margin: 0;
  padding: 10px 16px;
  list-style: none;
  display: grid;
  gap: 6px;
  background: rgba(245, 158, 11, 0.12);
  border-bottom: 1px solid rgba(245, 158, 11, 0.25);
  color: #fde68a;
  font-size: 12px;
}

/* History Panel */
.history-panel {
  padding: 24px;
  overflow-y: auto;
  box-sizing: border-box;
}

.history-content {
  max-width: 800px;
  margin: 0 auto;
  width: 100%;
}

.section-title {
  font-size: 18px;
  font-weight: 600;
  margin-top: 0;
  margin-bottom: 8px;
}

.section-desc {
  font-size: 13px;
  color: #8b8b9f;
  margin-bottom: 24px;
  line-height: 1.5;
}

.history-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.history-item {
  background: #151522;
  border: 1px solid #232333;
  border-radius: 8px;
  padding: 14px 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  transition: border-color 0.2s;
}

.history-item:hover {
  border-color: #35354e;
}

.version-info {
  display: flex;
  align-items: center;
  gap: 16px;
}

.version-badge {
  font-family: monospace;
  font-size: 13px;
  color: #a78bfa;
  background: #251b3d;
  padding: 4px 8px;
  border-radius: 4px;
}

.version-date {
  font-size: 13px;
  color: #8b8b9f;
}

.rollback-btn {
  background: #3b82f6;
  color: white;
}

.rollback-btn:hover {
  background: #2563eb;
}

.empty-history {
  color: #52526b;
  text-align: center;
  padding: 40px 0;
}
</style>
