# Phase 7F 主題系統 — Phase 8 分支整合紀錄

> 本檔記錄 `codex/theme-design` 主題系統移植到 `codex/phase8-workflow-ui`
> 後續分支 `codex/phase8-theme` 的整合過程與決策。
> 設計規格見同目錄 `plan.md` 與 `docs/SPEC.md` §4.24，逐頁稽核見 `manual-verification.md`。

## 背景

- `codex/theme-design` 已完成主題系統設計與多數遷移（基於共用 base `6af10e8`）。
- `codex/phase8-workflow-ui` 自同一 base 分岔，包含 workflow / Twitch / overlay 等
  50 個 commit，但**完全沒有主題系統**（`app.css` 全硬編碼、無 `useTheme`、
  `main.ts` 未初始化主題）。
- 兩分支已 diverge，故在新分支 `codex/phase8-theme` 整合。

## 整合策略

1. 自 `codex/phase8-workflow-ui` 開 `codex/phase8-theme`。
2. phase8 的 `app.css` / `monitor-tokens.css` 與共用 base **完全一致**，
   且 theme-design 的 token 遷移也從同一 base 出發，故可直接沿用
   theme-design 既有成果，無衝突。
3. `SettingsView.vue` / `SettingsView.test.ts` 在 phase8 亦與 base 一致，
   直接套用 theme-design 版本（含主題切換 UI）。
4. i18n 兩語系在 phase8 已分歧，手動補入 `settings.theme.*` 鍵。

## 已完成內容

### Slice 1 — 主題基礎（commit `c3ce96a`）

- `composables/useTheme.ts`：`light` / `dark` / `system`，localStorage 持久化，
  `prefers-color-scheme` 監聽，套用 `data-theme` + `color-scheme`。
- `main.ts` 啟動時呼叫 `initializeTheme()`。
- `styles/app.css` 改用 `--vp-*` 語意 token，新增 `[data-theme="dark"]` 深色配色。
- `styles/monitor-tokens.css` 改為衍生自 `--vp-*`，移除獨立硬編碼 palette。
- `SettingsView.vue` 主題切換控制 + i18n（en/zh）。
- `useTheme.test.ts` / `SettingsView.test.ts` 共 6 測試。

### Slice 2 — workflow editor + 共用組件（commit `f7a52ff`）

scoped CSS 改用 `--vp-*` token：

- `VariableFieldInput` / `VariablePicker` / `ConditionExpressionInput`
- `WorkflowActionsEditor` / `WorkflowConditionsEditor` / `StepListShell`
- `RoleChipSelector` / `ChatOutboxView` 狀態徽章
- `MonitorDashboardView` 殘留 rgba 收斂為 token

### Slice 3 — rule drawer + Twitch auth（commit `8ea08c7`）

- `RuleEditorDrawer`：overlay / drawer / tabs / footer / skeleton 全遷移。
- `TwitchAuthView`：description / input / success / device hint 遷移。

## 刻意例外

- `TwitchAuthView` 的 `#6441a5`（Twitch 品牌紫）保留為品牌例外，不套主題 token。
- ChatOutbox `processing` / `skipped` 狀態無對應 info token，改用中性 surface token。
- overlay preset（`/overlay/*`、`public/overlay/**`）維持 preset scope，非 app 主題範圍。

## 驗證

- `vue-tsc --noEmit`：PASS（每個 slice 後重跑）。
- `vitest run`：210 passed / 4 failed。
  - 4 個失敗皆為 `MembersView` 既有問題（在主題前 baseline `9c8b8db` 同樣失敗），
    與本次主題遷移無關。

## 待辦（後續 slice）

- 高色彩組件遷移：`MembersView`（292 色 premium card 設計，需 light/dark
  雙 token 以保留現有淺色外觀）、`OverlayEditorModal`、`SimulateControlsPanel`、
  `MonitorOverlayPanel`、`ChatStreamPanel`。
- 自訂主題（custom theme）基礎：useTheme 擴充支援使用者 token 覆寫。
- 瀏覽器 light/dark 各斷點 smoke test。
