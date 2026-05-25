# Spec: MonitorDashboardView 排版重設計（仿 Omni-Commander）

## Objective

重構 `src/frontend/src/views/admin/MonitorDashboardView.vue` 的版面與互動結構，仿 `ref/Omni-Commander/OmniCommander.UI/src/components/monitor/MonitorDashboard.vue` 的資訊密度與工作流，但保留淺色主題、PrimeVue UI 庫與現有後端契約。

**使用者**：直播主在開播前/開播中的單一 admin 操作台 — 需同時看 overlay 預覽、聊天室事件流、隨手送模擬事件。

**成功定義**：
- 寬螢幕（≥ 1280px）三欄佈局：可摺疊 Sider + Preview + Chat
- 中螢幕（1024–1279px）：Sider 自動摺疊為 Drawer，Preview + Chat 維持並列
- 窄螢幕（< 1024px）：垂直堆疊
- Header 採 glass 風格，含齒輪 icon、大寫粗體標題、SignalR/Server 狀態 pulse dot
- 引入 CSS variables design token 為未來暗色主題鋪路（本階段僅淺色實作）
- 既有功能、i18n key、a11y 測試零回歸

## Tech Stack

- Vue 3.5 + TypeScript + `<script setup>`
- PrimeVue 4 (Drawer / Button / Tabs / Select)
- vue-i18n 11（雙語 en-US / zh-TW）
- Vite 7 + Vitest 3 + jsdom
- 純 CSS scoped + `:root` CSS variables（不引入 SCSS toolchain）

不引入：
- `naive-ui`（ref 用，跟 PrimeVue 重複）
- `@vueuse/core useBreakpoints`（用 `window.matchMedia` 手刻足夠）
- SCSS / Sass loader

## Commands

```powershell
# 前端 dev
cd src/frontend
pnpm dev

# 型別檢查
pnpm vue-tsc --noEmit

# 測試
pnpm test

# 建置
pnpm build

# Lint
pnpm lint
```

## Project Structure

```
src/frontend/src/
├── views/admin/
│   ├── MonitorDashboardView.vue          ← 本 spec 主檔
│   └── MonitorDashboardView.test.ts      ← 既有測試，需更新
├── components/admin/
│   ├── MonitorOverlayPanel.vue           ← 保留 tabs/preset，外殼樣式調整
│   ├── ChatStreamPanel.vue               ← 列表式維持，header 調整為 LIVE chip
│   └── SimulateControlsPanel.vue         ← 分區強化（測試模式 / 訊息模擬 / 打卡模擬）
├── styles/
│   └── monitor-tokens.css                ← 新增：CSS variables design token
└── i18n/
    ├── en-US.json                        ← 新 key 補強
    └── zh-TW.json
```

## Code Style

### CSS variables design token（新增 `styles/monitor-tokens.css`）

```css
:root {
  /* Surface */
  --monitor-bg-base: linear-gradient(180deg, #f8fafb 0%, #eef3f1 100%);
  --monitor-bg-surface: rgba(255, 255, 255, 0.92);
  --monitor-bg-elevated: #ffffff;

  /* Border + radius */
  --monitor-border: #d6dde5;
  --monitor-radius-card: 12px;
  --monitor-radius-pill: 999px;

  /* Text */
  --monitor-text-primary: #18202a;
  --monitor-text-muted: #5f6f80;
  --monitor-text-accent: #164f48;

  /* Status */
  --monitor-success: #10b981;
  --monitor-success-subtle: #eaf7f1;
  --monitor-danger: #ef4444;
  --monitor-danger-subtle: #fdf0ef;
  --monitor-warning: #f59e0b;
  --monitor-warning-subtle: #fff7e6;

  /* Glass header */
  --monitor-header-height: 4.5rem;
  --monitor-header-blur: 12px;

  /* Sider */
  --monitor-sider-width: 380px;
  --monitor-sider-transition: 0.35s cubic-bezier(0.4, 0, 0.2, 1);

  /* Shadow */
  --monitor-shadow-elevated: 0 10px 30px rgba(15, 23, 32, 0.14);
}

/* 暗色主題鋪路（本階段不啟用）*/
[data-theme="dark"] {
  --monitor-bg-base: linear-gradient(180deg, #0f1419 0%, #1a2026 100%);
  --monitor-bg-surface: rgba(28, 34, 42, 0.92);
  /* ... 後續 phase 補完 */
}
```

### Component shell（MonitorDashboardView.vue 範例片段）

```vue
<template>
  <div class="monitor-dashboard" data-testid="monitor-dashboard">
    <header class="dashboard-header glass">
      <div class="header-left">
        <span class="header-icon" aria-hidden="true">⚙️</span>
        <div>
          <p class="dashboard-eyebrow">{{ t("monitor.dashboard.eyebrow") }}</p>
          <h1 class="dashboard-title">{{ t("monitor.dashboard.title") }}</h1>
        </div>
      </div>

      <div class="header-right">
        <button
          v-if="!isWide"
          type="button"
          class="sider-toggle"
          :aria-expanded="isSiderOpen"
          @click="toggleSider"
        >
          {{ t("monitor.dashboard.simulateEvent") }}
        </button>

        <div class="status-chip" :class="serverHealth" role="status">
          <span class="chip-dot" aria-hidden="true"></span>
          <span class="chip-text">
            {{ t("monitor.dashboard.signalrLabel") }}
            {{ t(`monitor.dashboard.health.${serverHealth}`) }}
          </span>
        </div>
      </div>
    </header>

    <div class="monitor-body">
      <!-- 寬屏 Sider，collapsible -->
      <aside
        v-if="isWide"
        class="controls-sider"
        :class="{ open: isSiderOpen }"
        :aria-hidden="!isSiderOpen"
      >
        <div class="sider-content">
          <SimulateControlsPanel :isEmbedded="true" />
        </div>
      </aside>

      <!-- 窄屏 Drawer（PrimeVue Drawer） -->
      <Drawer
        v-else
        v-model:visible="isSiderOpen"
        position="right"
        :pt="{ root: { style: 'width: 380px' } }"
      >
        <template #header>
          <h3>{{ t("monitor.dashboard.simulateControls") }}</h3>
        </template>
        <SimulateControlsPanel :isEmbedded="true" @simulated="isSiderOpen = false" />
      </Drawer>

      <main class="main-area">
        <section class="preview-panel">
          <MonitorOverlayPanel />
        </section>
        <aside class="chat-panel">
          <ChatStreamPanel />
        </aside>
      </main>
    </div>
  </div>
</template>
```

### 命名約定

- BEM-lite：`.monitor-dashboard__header` 簡寫為 `.dashboard-header`（單檔 scoped 即可）
- 狀態 modifier：`.status-chip.healthy`、`.controls-sider.open`
- data-testid：`monitor-dashboard`、`sider-toggle`、`status-chip`、`preview-panel`、`chat-panel`
- ARIA：`aria-expanded` / `aria-hidden` / `role="status"` 必備

## Testing Strategy

### Vitest 既有測試 (`MonitorDashboardView.test.ts`) 需擴充

| 測試案例 | 目的 |
|---|---|
| 渲染 header glass + icon + 標題 | 視覺結構回歸 |
| 寬螢幕 sider 預設展開 + toggle 摺疊至 0 | Collapsible 行為 |
| 窄螢幕（mock `window.innerWidth < 1280`）改 Drawer | 響應式 |
| Drawer `@simulated` event 自動關閉 | 互動回歸 |
| `serverHealth` 三態 `healthy/unhealthy/checking` chip class 對應 | 狀態渲染 |
| 鍵盤 Tab focus order：header → sider toggle → sider → preview → chat | a11y |
| i18n 切換 en-US / zh-TW key 都解析 | i18n 完整性 |

### a11y test (`src/frontend/src/a11y.test.ts`)

- header chip `role="status"` 存在
- sider 摺疊時 `aria-hidden="true"`
- toggle button `aria-expanded` 正確

### Coverage 要求

- `MonitorDashboardView.vue` lines ≥ 90%（既有 ≈ 75%，提升至 90%）
- 整體 frontend `pnpm test` 維持 167+ 案例 100% 綠燈

## Boundaries

### Always
- 保留所有現有 `monitor.dashboard.*` i18n key（僅新增，不刪除）
- 維持淺色配色 token（hex 值見 Code Style）
- 維持 `MonitorOverlayPanel` / `ChatStreamPanel` / `SimulateControlsPanel` 公開 prop 與 event 契約
- 新增 CSS variables 全寫入 `:root`，未來暗色 token 接 `[data-theme="dark"]`
- 維持 PrimeVue + 純 CSS 技術選型
- 保留 `data-testid="monitor-dashboard"` 給整合測試錨點

### Ask first
- 引入新 dependency（如 `@vueuse/core` motion utilities）
- 修改 SimulateControlsPanel 內部分區結構（目前是平鋪表單，ref 是四個明顯分區）
- 新增 SignalR connection state hook（目前只有 `getHealth` 輪詢）
- breakpoint 數值偏離 1280 / 1024（ref 用 tailwind xl=1280）

### Never
- 引入 SCSS / `@use mixins`
- 引入 `naive-ui`
- 複製 ref 的 BitsWidget / ChannelPointWidget / FollowWidget / RaidWidget / SubWidget（後端無對應事件）
- 複製 ref 的 NativeChat 桌面置頂功能（無 Electron / native bridge）
- 改 backend endpoint 行為（純前端重構）
- 刪除既有測試案例

## Success Criteria

1. **視覺驗證**：
   - 寬螢幕（1920×1080）三欄並列：Sider 380px / Preview flex / Chat 320px
   - Header 高 4.5rem，glass blur 12px，左齒輪 icon + 大寫標題，右健康 chip
   - Sider 展開/摺疊有 0.35s cubic-bezier transition
2. **響應式驗證**：
   - `< 1280px` 時 Sider 改 Drawer
   - `< 1024px` 時 Preview + Chat 垂直堆疊
3. **互動驗證**：
   - Sider toggle 按鈕 `aria-expanded` 同步狀態
   - Drawer 內 SimulateControlsPanel 送出後自動關閉
   - Server health 10 秒輪詢，chip 即時更新
4. **無回歸**：
   - `pnpm test` 167+/167+ 綠燈
   - `pnpm vue-tsc --noEmit` 零錯誤
   - `pnpm build` 成功
   - `pnpm lint` 零警告
5. **暗色鋪路**：
   - `styles/monitor-tokens.css` 含 `:root` + `[data-theme="dark"]` 兩組（dark 可暫留 TODO）
   - 元件全部用 `var(--monitor-*)` 引用，無硬編 hex 漏網

## Open Questions

1. **SignalR 真實狀態 vs 輪詢**：目前用 `getHealth()` 10s 輪詢，ref 用真實 SignalR connection state。本 spec 範圍是否含改為 `@microsoft/signalr` connection state hook？
   - 建議：本 phase 保留輪詢，後續另開 spec 改 hook。
2. **SimulateControlsPanel 分區重構**：ref 分四區（測試模式 switch / Native chat / 訊息模擬 / 打卡模擬），當前是平鋪。要重構嗎？
   - 建議：保留平鋪，僅微調區塊分隔線符合視覺密度。需確認後處理。
3. **MonitorOverlayPanel header 補充**：ref 中欄 header 含 SCENE PREVIEW + DRAFT/LIVE switch + Refresh 按鈕。當前 panel 內無 header bar。是否補？
   - 建議：補一條 mini header（不改 tabs 主體），含當前 hub 名 + Reload icon。
4. **CSS variables 命名前綴**：`--monitor-*` vs `--vp-*` 全域？
   - 建議：先 `--monitor-*` 局部，未來 design system 統一前綴另議。

---

## Implementation Plan（高層級，待 spec 批准後展開 task list）

1. **Phase A — Design tokens 鋪設**
   - 新增 `src/styles/monitor-tokens.css`，註冊於 `main.ts`
   - 替換 `MonitorDashboardView.vue` 硬編 hex 為 `var(--monitor-*)`
2. **Phase B — Header glass + collapsible Sider**
   - 重寫 header（齒輪 icon + 大寫標題 + status chip）
   - 加 `isSiderOpen` ref + PrimeVue Drawer for narrow
   - 寫 transition + aria-expanded/hidden
3. **Phase C — MonitorOverlayPanel mini header**（待 Q3 確認）
4. **Phase D — 響應式 breakpoint**
   - `window.matchMedia('(min-width: 1280px)')` 設 `isWide`
   - `(min-width: 1024px)` 設 `isMedium` 控 chat 並列
5. **Phase E — i18n key 補強**
   - 補 `monitor.dashboard.eyebrow`、`monitor.dashboard.signalrLabel`、`monitor.dashboard.health.{healthy,unhealthy,checking}`
6. **Phase F — 測試擴充**
   - 新增 7 個 Vitest case（見 Testing Strategy）
   - 更新 a11y test
7. **Phase G — Lint + build + manual verify**
   - 四步 checkpoint 全綠

---

## Verification Checkpoint（spec 落地後）

```powershell
cd src/frontend
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
```

Manual：
- 開 dev server，1920×1080 / 1280×800 / 800×600 三尺寸各驗一次
- DevTools `prefers-reduced-motion` 開啟驗 transition 是否略過（後續 phase）
- 鍵盤 Tab 走訪 + screen reader 唸出 status chip
