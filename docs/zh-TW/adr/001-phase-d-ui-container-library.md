# ADR 001: Phase D UI 容器庫 (Phase D UI Container Library)

## 狀態

已接受，2026-05-29。

## 上下文 (Context)

工作流規則編輯器的 Phase 8 Phase D 需要一個具備無障礙支援的抽屜 (Drawer)、分頁 (Tabs) 和表單外殼。現有的前端使用 Vue 3、自訂 CSS 和少量的 PrimeVue，但 Phase D 計畫選擇了無周邊 (Headless) 的基礎元件庫 (Primitive Library)，以便視覺樣式可以保留在 Vulperonex CSS 系統中。

## 決策

使用 `reka-ui` 作為 Phase D 的容器基礎元件。

- 安裝版本：`reka-ui@2.9.8`。
- 在 `RuleEditorDrawer.vue` 成為正式路由實作後，原有的 PoC 元件與測試已被停用；下方的歷史驗證證據記錄了 PoC 門檻。

此 PoC 使用：

- `DialogRoot`、`DialogTrigger`、`DialogPortal`、`DialogOverlay`、`DialogContent`、`DialogTitle`、`DialogDescription` 和 `DialogClose` 作為右側抽屜外殼。
- `TabsRoot`、`TabsList`、`TabsTrigger` 和 `TabsContent` 用於 Basic / Actions / Errors 分頁。
- Reka `Label` 搭配原生表單控制項用於第一個表單路徑。

## Token 與樣式檢查

Reka 發出穩定的資料屬性，可透過現有的 CSS 進行樣式設定：

- 抽屜：`.reka-poc-drawer[data-state="open"]`。
- 分頁：`.reka-poc-tab[data-state="active"]`。
- 對話方塊遮罩/內容：一般類別選取器疊加在未設定樣式的插槽 (Slots) 上。

此 PoC 刻意使用現有的本機按鈕/表單類別（`primary-button`、`icon-button`、`form-field`、`form-label`）與直接的 CSS 選取器，而不是庫內置的主題。這確認了 Phase D 可以使用現有的 CSS 變數/類別，而無須替換應用程式的 Token 系統。

## 軟體包與建置證據 (Bundle and Build Evidence)

`reka-ui` 已新增至 `src/frontend/package.json` 與 `src/frontend/pnpm-lock.yaml`。

驗證執行結果：

- `vitest run src/components/admin/RekaPhaseDPoc.test.ts`：1 個測試通過。
- `vue-tsc --noEmit`：通過。
- `vite build --outDir ../../artifacts/vulperonex-phase8-reka-build --emptyOutDir`：通過。

安裝後且 PoC 元件尚未進入正式路由時的建置輸出：

- CSS 軟體包：`225.64 kB`，gzip `39.45 kB`。
- 主要 JS 軟體包：`4,229.49 kB`，gzip `1,117.17 kB`。

`RuleEditorDrawer.vue` 被 `RulesView` 引入後的 Phase D.1 路由建置輸出：

- CSS 軟體包：`227.48 kB`，gzip `39.75 kB`。
- 主要 JS 軟體包：`4,278.37 kB`，gzip `1,132.42 kB`。
- 與未路由的 PoC 建置相比的路由增量：CSS gzip `+0.30 kB`，主要 JS gzip `+15.25 kB`。

路由增量在 Phase D 的預算範圍內。

`TriggerEditor.vue` 切換為由元資料驅動的強型別欄位後的 Phase D.2 路由建置輸出：

- CSS 軟體包：`227.53 kB`，gzip `39.78 kB`。
- 主要 JS 軟體包：`4,280.65 kB`，gzip `1,133.06 kB`。
- 與 Phase D.1 路由建置相比的增量：CSS gzip `+0.03 kB`，主要 JS gzip `+0.64 kB`。

D.2 增量仍在 Phase D 的預算範圍內。

`VariablePicker.vue` 透過觸發器元資料篩選後的 Phase D.3 路由建置輸出：

- CSS 軟體包：`227.53 kB`，gzip `39.78 kB`。
- 主要 JS 軟體包：`4,281.45 kB`，gzip `1,133.23 kB`。
- 與 Phase D.2 路由建置相比的增量：CSS gzip `+0.00 kB`，主要 JS gzip `+0.17 kB`。

D.3 增量仍在 Phase D 的預算範圍內。

動作編輯器元資料從前端硬編碼定義移至 `/api/metadata/actions` 後的 Phase D.4 路由建置輸出：

- CSS 軟體包：`227.53 kB`，gzip `39.77 kB`。
- 主要 JS 軟體包：`4,278.75 kB`，gzip `1,132.63 kB`。
- 與 Phase D.3 路由建置相比的增量：CSS gzip `-0.01 kB`，主要 JS gzip `-0.60 kB`。

D.4 增量仍在 Phase D 的預算範圍內。

新增 Basic 分頁的角色晶片選取器後的 Phase E.1 路由建置輸出：

- CSS 軟體包：`227.96 kB`，gzip `39.85 kB`。
- 主要 JS 軟體包：`4,281.80 kB`，gzip `1,133.51 kB`。
- 與 Phase D.4 路由建置相比的增量：CSS gzip `+0.08 kB`，主要 JS gzip `+0.88 kB`。

E.1 增量仍在 Phase D/E UI 的預算範圍內。

## 後續影響 (Consequences)

- 繼續使用 `reka-ui` 進行 Phase D.1，而非 PrimeVue Dialog/Tabs。
- 保留舊有的全頁面 `RuleEditorView` 作為進階備用方案。
- 在後續 Phase D 任務新增元資料驅動欄位與動作元資料存放區時，繼續測量 gzip 增量。
