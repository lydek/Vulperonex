# Phase 7F 應用程式主題系統計畫 (App Theme System Plan)

> 父計畫：`tasks/plan.md`
> 父待辦清單：`tasks/todo.md`
> SPEC 章節：`docs/SPEC.md` 第 4.24 節

## 目標

為 Vue 管理端外殼及共享前端元件建置一個全域主題系統。目前應用程式的顏色定義分散且硬編碼，應用程式外殼僅支援淺色，且僅有監控專用的 Token 鷹架。Phase 7F 旨在實作前明確化主題行為：Token 來源、持久化、切換 UI、遷移順序以及逐頁驗證。

## 現有發現

- `src/frontend/src/styles/app.css` 僅支援淺色，且硬編碼了外殼、卡片、表單、表格、徽章、警報和按鈕的顏色。
- `src/frontend/src/styles/monitor-tokens.css` 包含監控本機 Token 和一個深色區塊，但註釋指出它並非完整且全域的主題。
- `src/frontend/src/main.ts` 使用 PrimeVue 且 `unstyled: true`；因此應用程式擁有所有的視覺 Token。
- 多個管理頁面和編輯器元件在 SFC 樣式中定義了本機的十六進位或 rgba 值。
- Overlay 路由和靜態 OBS 資產是顯示預設檔，並非管理端應用程式主題切換的首要目標。除非在管理端外殼中顯示，否則它們可以保留預設檔專用的 Token。

## 設計決策

1. **全域 Token 優先**：在 `app.css` 中引入 `--vp-*` Token，作為管理端 UI 顏色、表面、邊框、焦點、陰影、圓角和狀態顏色的權威來源。
2. **資料屬性切換**：`document.documentElement.dataset.theme` 是執行階段的切換開關。支援的初始值為 `light`、`dark` 和 `system`。
3. **系統偏好支援**：`system` 遵循 `prefers-color-scheme`，並在作業系統偏好變更時同步更新。
4. **透過設定持久化**：在後端連線存在後，使用者偏好會儲存在應用程式設定中。在此之前，僅允許將本機儲存 (Local Storage) 作為前端橋接，且必須隔離在主題服務/Composable 之中。
5. **不新增相依性**：主題切換使用 Vue、CSS 變數和現有的 API。
6. **監控 Token 源自應用程式 Token**：`--monitor-*` 應成為 `--vp-*` 的別名或衍生值，而不是一個獨立的永久調色盤。
7. **頁面遷移是漸進式的**：先遷移共享應用程式外殼和常見基礎元件，然後按風險和顏色數量遷移管理頁面。
8. **Overlay 預設檔樣式保持範圍限制**：`/overlay/*` 和 `public/overlay/**` 保留預設檔專用 Token，除非嵌入在管理端預覽中，此時周圍的鉻框 (Chrome) 遵循 `--vp-*`。

## 實作切片

### 切片 1 - 主題基礎

- 新增 `src/frontend/src/styles/theme.css` 或在 `app.css` 中使用 `--vp-*` Token 進行擴充。
- 新增 `src/frontend/src/composables/useTheme.ts` 來讀取、套用和持久化主題偏好。
- 針對 `light`、`dark`、`system`、本機持久化備用方案以及媒體查詢 (Media Query) 更新新增測試。
- 在 `SettingsView.vue` 中新增一個緊湊的主題控制項。

### 切片 2 - 外殼和共享基礎元件

- 將 `app.css` 中硬編碼的應用程式外殼顏色替換為 `--vp-*`。
- 轉換共享類別：按鈕、卡片、表單欄位、晶片、強制回應視窗、表格、警報和程式碼區塊。
- 保持版面配置不變。

### 切片 3 - 監控 Token 整合

- 重新連線 `monitor-tokens.css` 以衍生自 `--vp-*`。
- 在 UI 可切換主題後，移除說明深色僅為鷹架的過時註釋。
- 驗證 `/monitor` 在兩種主題與預覽背景控制項下的運作。

### 切片 4 - 多顏色管理頁面

按顏色數量和使用者接觸頻率排序：

1. `MembersView.vue`
2. `OverlayEditorModal.vue`
3. `SimulateControlsPanel.vue`
4. `MonitorOverlayPanel.vue`
5. `ChatStreamPanel.vue`
6. `TwitchAuthView.vue`
7. `SettingsView.vue`
8. `RuleEditorView.vue`
9. `ChatOutboxView.vue`
10. 工作流編輯器元件

### 切片 5 - 驗證與清理

- 為所有路由產生主題稽核矩陣。
- 執行前端型別檢查、Vitest、建置和 Linter。
- 針對桌上型和行動裝置寬度，在淺色和深色主題下進行瀏覽器冒煙測試。
- 確認管理端沒有文字變成低對比度或隱形。

## 驗收標準

- 應用程式支援 `light`、`dark` 和 `system` 偏好。
- 主題偏好可以從「設定」中變更且無需重新載入。
- 管理端外殼、共享控制項和已遷移的頁面使用 `--vp-*` Token 來設定表面、文字、邊框、焦點和狀態顏色。
- 監控頁面遵循應用程式主題，不再擁有斷開的調色盤。
- 靜態 Overlay 預設檔樣式被明確記錄為預設檔作用域，而非應用程式主題作用域。
- 主題稽核將每個路由列為 `done`、`partial`、`preset-scoped` 或 `deferred`。
- 不新增新的軟體包相依性。

## 驗證指令

除非另有說明，否則自 `D:\code\Vulperonex-theme` 執行：

```powershell
rtk corepack pnpm@9.15.4 --dir src/frontend exec vue-tsc --noEmit
rtk corepack pnpm@9.15.4 --dir src/frontend exec vitest run
rtk corepack pnpm@9.15.4 --dir src/frontend build
rtk corepack pnpm@9.15.4 --dir src/frontend lint
```

如果預設的 Vite 輸出路徑被鎖定，請使用：

```powershell
rtk corepack pnpm@9.15.4 --dir src/frontend exec vite build --outDir C:\tmp\vulperonex-frontend-build
```

## 風險

| 風險 | 影響 | 緩解措施 |
| --- | --- | --- |
| 大規模顏色遷移導致視覺退化 | 高 | 按切片提交，並使用路由稽核螢幕截圖/手動檢查 |
| 深色主題對比度缺陷 | 高 | 使用語意化 Token 並檢查文字、控制項、邊框、焦點環 |
| Overlay 預設檔混淆 | 中 | 在文件和程式碼註釋中保持應用程式主題與 OBS 預設檔主題分離 |
| 設定持久化後端範圍擴大 | 中 | 先從隔離的前端主題服務開始，然後在單獨的切片中連線持久化設定 |
