# ADR 001: 前端技術棧選擇 (Frontend Stack Selection)

## 狀態

已批准用於 Phase 6 規劃。

## 上下文 (Context)

Phase 6 將已驗證的本機 Web API、SignalR Overlay、CLI 手動測試流程以及 Twitch OAuth 狀態移入本機 Web UI，隨後將該 UI 封裝在 Photino 桌面外殼中。第一個畫面必須是操作控制台，而不是登陸頁面。

該技術棧必須支援：

- 使用 TypeScript 的 Vue 單一檔案組件 (SFC)。
- 用於頻繁實況主操作的緊湊本機管理 UI。
- 與管理狀態隔離的 Overlay 路由。
- 具有確定性測試的 SignalR 狀態處理。
- 支持 `zh-TW` 和 `en-US` 語系。
- 由 `src/Hosts/Vulperonex.Web/wwwroot` 提供的靜態建置輸出。
- 安裝相依性前需詢問核准。

## 決策

使用以下前端技術棧：

- **Vue 3.5**：搭配 Composition API 和 SFC。
- **Vite 7.3**：用於開發伺服器和生產建置。
- **PrimeVue 4 Unstyled**：用於具備無障礙支援的組件基礎元件，而不強制使用視覺主題。
- **UnoCSS Preset Wind 4**：用於排版與公用樣式設定。
- **Pinia Setup Stores**：用於管理狀態，透過 Actions 暴露唯讀狀態和變更 (Mutations)。
- **vue-i18n**：以 `zh-TW` 為預設，`en-US` 作為第二個必要語系。
- **Vitest + Vue Test Utils**：用於組件和 Composable 測試。
- **vue-tsc**：用於 Vue SFC 型別檢查。
- **oxlint**：作為配置的 Linter；除非計畫有明確修改，否則不新增 ESLint。
- **pnpm 9.15.4**：透過 `packageManager` 固定版本。

## 限制條件

- 首次安裝相依性仍需要事先詢問並取得核准。
- pnpm 9.15.4 不支援 `pnpm install --dry-run`；在安裝技術棧前，先執行 `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts` 以捕獲版本衝突，避免執行生命週期指令碼。
- API 基礎 URL 預設必須使用同源相對路徑，並支援在開發時使用 `VITE_API_URL` 覆寫。
- Overlay 頁面不得共享管理端的 Pinia 狀態；它們直接連線到其專用的 Overlay Hub。
- Overlay 渲染必須僅使用文字繫結；對於外部事件內容，不得使用 `v-html`。
- OAuth `code` 由後端回呼端點使用，不得暴露給 Web UI 路由。

## 後續影響 (Consequences)

- 任務 19 負責前端骨架、建置輸出、i18n 清單、API 用戶端、Stores、SignalR Composables 和 Overlay 路由骨架。
- 任務 20 可以跑在該基礎上建置管理面板，而無須新增第二種狀態模式。
- 任務 21 可以透過桌面外殼載入建置好的 UI，而無須更改前端架構。
