# Phase 7F 手動驗證 (Manual Verification)

## 靜態稽核 - 2026-05-27

主題執行階段搜尋：

- `src/frontend/src` 中目前不存在作用中的 `data-theme` 設定器、`dataset.theme` 指派、`prefers-color-scheme` 監聽器或主題持久化邏輯。
- `src/frontend/src/styles/app.css` 設定了 `color-scheme: light`。
- `src/frontend/src/styles/monitor-tokens.css` 包含 `[data-theme="dark"]`，但註釋指出目前僅渲染淺色主題。
- `OverlayEditorModal.vue` 在本機設定了 Monaco `theme: "vs-dark"`；這是編輯器鉻框，而非應用程式主題系統。

硬編碼顏色數量（不含測試）：

| 檔案 | 次數 | 分類 |
| --- | ---: | --- |
| `src/frontend/src/views/admin/MembersView.vue` | 0 | 應用程式作用域，已遷移 |
| `src/frontend/src/styles/app.css` | 0 (Token 定義之外) | 應用程式作用域基礎，已遷移 |
| `src/frontend/src/components/admin/OverlayEditorModal.vue` | 75 | 應用程式作用域，強制回應視窗/編輯器 |
| `src/frontend/src/components/admin/SimulateControlsPanel.vue` | 65 | 應用程式作用域，監控/模擬控制面板 |
| `src/frontend/src/components/admin/MonitorOverlayPanel.vue` | 50 | 應用程式作用域，監控面板鉻框 |
| `src/frontend/src/styles/monitor-tokens.css` | 43 | Token 橋接器 |
| `src/frontend/src/components/admin/ChatStreamPanel.vue` | 40 | 應用程式作用域，監控面板鉻框 |
| `src/frontend/src/views/overlay/MemberOverlayView.vue` | 24 | 預設檔作用域 Overlay |
| `src/frontend/src/views/admin/ChatOutboxView.vue` | 0 | 應用程式作用域狀態顏色，已遷移 |
| `src/frontend/src/views/admin/TwitchAuthView.vue` | 14 | 應用程式作用域，帶有 Twitch 品牌例外 |
| `src/frontend/src/components/admin/VariablePicker.vue` | 0 | 共享編輯器組件，已遷移 |
| `src/frontend/src/components/admin/ConditionExpressionInput.vue` | 0 | 共享編輯器組件，已遷移 |
| `src/frontend/src/views/admin/SettingsView.vue` | 10 | 應用程式作用域設定頁面 |
| `src/frontend/src/views/admin/RuleEditorView.vue` | 0 | 應用程式作用域編輯器鉻框，已遷移 |
| 工作流編輯器組件 | 0 | 共享編輯器組件，已遷移 |
| Overlay 預設組件 | 共 22 次 | 預設檔作用域 Overlay |

## 路由主題矩陣 (Route Theme Matrix)

| 路由 / 頁面 | 作用域 | 淺色 | 深色 | 行動裝置 | 備註 |
| --- | --- | --- | --- | --- | --- |
| `/monitor` | 管理端路由 | 部分完成 | 部分完成 | 待辦 (TODO) | `monitor-tokens.css` 現已衍生自 `--vp-*`；組件級別的硬編碼文字字面值仍需清理。 |
| `/settings` | 管理端路由 | 部分完成 | 缺失 | 待辦 (TODO) | 需要主題控制項和 Token 化的設定卡片。 |
| `/simulate` | 管理端路由 | 部分完成 | 缺失 | 待辦 (TODO) | `SimulateControlsPanel` 有 65 個硬編碼顏色值。 |
| `/events` | 管理端路由 | 部分完成 | 缺失 | 待辦 (TODO) | 大部分為共享的 `app.css` 表格/晶片樣式。 |
| `/members` | 管理端路由 | 部分完成 | 缺失 | 待辦 (TODO) | 本地硬編碼顏色次數最高。 |
| `/overlay-presets`| 管理端路由 | 部分完成 | 缺失 | 待辦 (TODO) | 管理端鉻框及編輯器強制回應視窗啟動。 |
| `/rules` | 管理端路由 | 部分完成 | 部分完成 | 待辦 (TODO) | 共享的應用程式樣式和粘性動作現已使用 `--vp-*`；瀏覽器冒煙測試待定。 |
| `/rules/:id` | 管理端路由 | 部分完成 | 部分完成 | 待辦 (TODO) | 工作流編輯器共享組件現已使用 `--vp-*`；瀏覽器冒煙測試待定。 |
| `/timers` | 管理端路由 | 部分完成 | 缺失 | 待辦 (TODO) | 大部分為共享的管理端基礎元件。 |
| `/chat-outbox` | 管理端路由 | 部分完成 | 部分完成 | 待辦 (TODO) | 狀態徽章現已使用語意化 `--vp-*`；瀏覽器冒煙測試待定。 |
| `/twitch` | 管理端路由 | 部分完成 | 缺失 | 待辦 (TODO) | Twitch 品牌點綴色可保留為語意化例外。 |
| `/overlay/chat` | 預設檔作用域 | 不適用 | 不適用 | 不適用 | OBS 預設樣式，而非應用程式主題。 |
| `/overlay/alerts` | 預設檔作用域 | 不適用 | 不適用 | 不適用 | OBS 預設樣式，而非應用程式主題。 |
| `/overlay/member` | 預設檔作用域 | 不適用 | 不適用 | 不適用 | OBS 預設樣式，而非應用程式主題。 |
| `public/overlay/**`| 靜態預設資產 | 不適用 | 不適用 | 不適用 | 僅限預設檔專用 Token。 |

## 必要的瀏覽器檢查

- 主題切換應在不重新載入頁面的情況下更新可見外殼。
- `system` 遵循 `prefers-color-scheme`。
- 焦點環在兩種主題中均保持可見。
- 停用狀態保持可讀。
- 在涉及行動的重要地方，危險、警告、成功和資訊狀態不能僅以顏色區分。
- 表格、強制回應視窗、抽屜、面板和編輯器表面保持對比度。
- Overlay 預覽鉻框遵循應用程式主題，而 iframe 預設內容保持預設檔作用域。

## 證據記錄 (Evidence Log)

| 日期 | Commit | 檢查項目 | 結果 | 備註 |
| --- | --- | --- | --- | --- |
| 2026-05-27 | 待定 | `vue-tsc --noEmit` | 通過 | 透過本機 `.\\node_modules\\.bin\\vue-tsc.cmd` 於 `src/frontend` 執行。 |
| 2026-05-27 | 待定 | `vitest run` | 通過 | 37 個檔案 / 201 個測試通過。無關測試中先前已有的 Vue 警告仍保持非失敗狀態。 |
| 2026-05-27 | 待定 | `vite build --outDir C:\\tmp\\vulperonex-theme-frontend-build` | 通過 | 初始沙盒執行時在建立 `C:\\tmp` 時遇到 EPERM 權限錯誤；提高權限後重新執行通過。 |
| 2026-05-27 | 待定 | `oxlint --config oxlint.json` | 通過 | 0 個警告 / 0 個錯誤。 |
| 2026-05-27 | 待定 | 在 `app.css` 中 `rg` 硬編碼顏色 | 通過 | 僅保留 `--vp-*` 淺色/深色 Token 定義。 |
| 2026-05-27 | 待定 | `vue-tsc --noEmit` | 通過 | 在共享 `app.css` Token 遷移後重新執行。 |
| 2026-05-27 | 待定 | `vitest run src/composables/useTheme.test.ts src/views/admin/SettingsView.test.ts` | 通過 | 在共享 `app.css` Token 遷移後，2 個檔案 / 6 個測試通過。 |
| 2026-05-27 | 待定 | `oxlint --config oxlint.json` | 通過 | 在共享 `app.css` Token 遷移後重新執行；0 個警告 / 0 個錯誤。 |
| 2026-05-27 | 待定 | 在 `monitor-tokens.css` 中 `rg` 硬編碼顏色 | 通過 | 無殘留硬編碼顏色；監控 Token 橋接至 `--vp-*`。 |
| 2026-05-27 | 待定 | `vue-tsc --noEmit` | 通過 | 在監控 Token 橋接後重新執行。 |
| 2026-05-27 | 待定 | `vitest run src/views/admin/MonitorDashboardView.test.ts src/composables/useTheme.test.ts` | 通過 | 在監控 Token 橋接後，2 個檔案 / 13 個測試通過。 |
| 2026-05-27 | 待定 | 在共享編輯器/規則/聊天發送信箱切片中 `rg` 硬編碼顏色 | 通過 | 修改的 8 個 Vue 檔案現在有 0 個硬編碼顏色。 |
| 2026-05-27 | 待定 | 針對性共享編輯器/規則/聊天發送信箱 vitest 套件 | 通過 | Token 遷移後，6 個檔案 / 15 個測試通過。 |
| 2026-05-27 | 待定 | `vue-tsc --noEmit` | 通過 | 在共享編輯器/規則/聊天發送信箱 Token 遷移後重新執行。 |
| 2026-05-27 | 待定 | `oxlint --config oxlint.json` | 通過 | 在共享編輯器/規則/聊天發送信箱 Token 遷移後重新執行；0 個警告 / 0 個錯誤。 |
| 2026-05-27 | 待定 | 在 `MembersView.vue` 中 `rg` 硬編碼顏色 | 通過 | `MembersView.vue` 中無殘留硬編碼顏色；樣式現已使用 `--vp-*`。 |
| 2026-05-27 | 待定 | `vitest run src/views/admin/MembersView.test.ts` | 通過 | 在 `MembersView.vue` Token 遷移後，1 個檔案 / 4 個測試通過。 |
| 2026-05-27 | 待定 | `vue-tsc --noEmit` | 通過 | 在 `MembersView.vue` Token 遷移後重新執行。 |
| 2026-05-27 | 待定 | `oxlint --config oxlint.json` | 通過 | 在 `MembersView.vue` Token 遷移後重新執行；0 個警告 / 0 個錯誤。 |
