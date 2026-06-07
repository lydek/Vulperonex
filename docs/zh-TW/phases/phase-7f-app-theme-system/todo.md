# Phase 7F 待辦清單 (TODO)

## 文件 (Docs)

- [x] 將全域主題系統範圍新增至 `docs/SPEC.md`。
- [x] 新增 Phase 7F 計畫。
- [x] 新增 Phase 7F 待辦清單。
- [x] 新增 Phase 7F 手動驗證範本。
- [x] 從 `tasks/plan.md` 和 `tasks/todo.md` 連結 Phase 7F。

## 稽核 (Audit)

- [x] 盤點所有管理端路由、共享組件、Overlay 預覽和靜態 Overlay 資產。
- [x] 按檔案計算硬編碼顏色的使用次數，並將每個路由分類為 `done`、`partial`、`preset-scoped` 或 `deferred`。
- [x] 在 `manual-verification.md` 中記錄遷移優先級和阻礙因素。

## 主題基礎 (Theme Foundation)

- [x] 為淺色和深色主題定義權威的 `--vp-*` Token。
- [x] 新增執行階段主題偏好模型：`light`、`dark`、`system`。
- [x] 新增一個具有本機備用持久化的主題 Composable/服務。
- [x] 針對主題偏好套用和系統偏好變更新增單元測試。
- [x] 新增用於主題選擇的設定 UI 控制項。

## 共享外殼遷移 (Shared Shell Migration)

- [x] 將 `app.css` 外殼、卡片、表單、表格、按鈕、晶片、強制回應視窗、警報和焦點樣式轉換為 `--vp-*`。
- [ ] 驗證預設淺色主題在視覺上是否與目前的應用程式緊密匹配。
- [ ] 驗證深色主題是否具有可用的對比度。

## 監控整合 (Monitor Integration)

- [x] 重新連線 `monitor-tokens.css` 以衍生自 `--vp-*`。
- [ ] 轉換仍然硬編碼應用程式外殼顏色的監控相關組件。
- [/] 在淺色和深色模式下驗證 `/monitor`。

## 頁面遷移（phase8-theme 分支狀態）

> 此分支整合詳見 `phase8-integration.md`。狀態以 `codex/phase8-theme` 為準。

- [x] 工作流編輯器組件（VariableFieldInput / VariablePicker / ConditionExpressionInput / WorkflowActionsEditor / WorkflowConditionsEditor / StepListShell / RoleChipSelector）
- [x] `ChatOutboxView.vue`
- [x] `MonitorDashboardView.vue`（殘留 rgba 收斂）
- [x] `RuleEditorDrawer.vue`（phase8 以 drawer 取代 RuleEditorView）
- [x] `TwitchAuthView.vue`（保留 Twitch 品牌紫例外）
- [x] `SettingsView.vue`
- [ ] `MembersView.vue`（292 色 premium 卡片，需 light/dark 雙 Token，延後）
- [ ] `OverlayEditorModal.vue`
- [ ] `SimulateControlsPanel.vue`
- [ ] `MonitorOverlayPanel.vue`
- [ ] `ChatStreamPanel.vue`

## 自訂主題（後續）

- [ ] useTheme 擴充支援使用者自訂 Token 覆寫。
- [ ] 自訂主題最小 UI。

## 檢查點 (Checkpoint)

- [x] `rtk corepack pnpm@9.15.4 --dir src/frontend exec vue-tsc --noEmit` - 2026-05-27 透過本機 `.\\node_modules\\.bin\\vue-tsc.cmd --noEmit`，通過 (PASS)
- [x] `rtk corepack pnpm@9.15.4 --dir src/frontend exec vitest run` - 2026-05-27 透過本機 `.\\node_modules\\.bin\\vitest.cmd run`，201/201 通過 (PASS)
- [x] `rtk corepack pnpm@9.15.4 --dir src/frontend build` - 2026-05-27 透過本機 `.\\node_modules\\.bin\\vite.cmd build --outDir C:\\tmp\\vulperonex-theme-frontend-build`，通過 (PASS)
- [x] `rtk corepack pnpm@9.15.4 --dir src/frontend lint` - 2026-05-27 透過本機 `.\\node_modules\\.bin\\oxlint.cmd --config oxlint.json`，0 個警告 / 0 個錯誤
- [ ] 瀏覽器冒煙測試：在 320px、768px、1024px、1440px 下的淺色主題。
- [ ] 瀏覽器冒煙測試：在 320px、768px、1024px、1440px 下的深色主題。
- [ ] `manual-verification.md` 包含路由矩陣和通過/失敗的證據。
