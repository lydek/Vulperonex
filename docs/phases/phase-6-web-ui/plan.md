# 第 6 階段實作計畫：Web UI + 日誌 + Desktop Shell

> 父計畫：`tasks/plan.md`
> 父待辦清單：`tasks/todo.md`
> 任務範圍：Task 18-21
> 前置條件：Phase 5 CLI / Web API / SignalR / Twitch OAuth manual verification 已完成並記錄於 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`

---

## 目標

Phase 6 把 Phase 5 已由 CLI 驗證過的 loopback Web API 能力搬到 Vue Web UI，形成可長時間操作的本機控制台。第一個畫面必須是可用的操作介面，而不是 landing page：使用者可以確認 API/Twitch 狀態、模擬事件、查看成員、管理 WorkflowRule、監看 SignalR 事件，最後由 Photino Desktop Shell 包裝成桌面入口。

---

## 設計原則

- Web UI 是 CLI manual smoke 的視覺化延伸；不得重新發明後端流程或直接讀寫 SQLite。
- 前端所有後端錯誤只依 error code 呈現，由 vue-i18n 轉譯；後端仍保持 machine-readable error。
- API base URL 不硬編本機 port：瀏覽器由相對路徑呼叫同源 API，Vite dev 可用 `VITE_API_URL` 覆寫。
- UI 走操作型工具風格：資訊密度高、清楚、穩定，不做行銷 hero 或裝飾性視覺。
- 規則編輯先提供可靠的 CLI-equivalent JSON path 與基本表單；完整視覺化 builder 可在後續切片擴充。
- Overlay route 與管理 console route 分離；overlay route 只處理顯示，不包含管理控制。
- 前端 i18n 檔案與 CLI i18n 一樣採外部可擴充思路：語系清單與語系檔名稱對應，缺字串顯示 key，不讓 UI crash。
- 新增 npm/NuGet dependency 前遵守 ask-first；已存在的工具可直接用於驗證。

---

## 依賴圖

```text
Task 18 Serilog/AppLogs
    -> Dashboard log/status widgets can consume later

Task 19 Frontend foundation
    -> Task 20 Management UI
    -> Task 21 Desktop Shell

Task 20 Management UI
    -> Rule/member/simulate/Twitch flows verified in browser

Task 21 Desktop Shell
    -> Ships Web UI through local desktop entry
```

Task 18 可與 Task 19 分開實作，但 Phase 6 的 Web UI 工作應先完成 Task 19，否則 Task 20/21 會缺少共用前端基礎。

---

## Task 18 - Serilog 三 Sink + AppLogs 清理 worker

**描述：** 設定 Console、Rolling File、SQLite AppLogs 三個 sink，加入結構化欄位 enricher，並實作 `log.db_retention_days` / `log.db_max_size_mb` 清理 worker。`log.min_level` 必須透過 SystemSettings 熱重載。

**驗收標準：**
- [ ] Console、rolling file、SQLite AppLogs 均可寫入。
- [ ] AppLogs rows 含 EventTypeKey、Platform、MemberId、WorkflowRuleId、ActionType 等結構化欄位。
- [ ] `config set log.min_level Warning` 後 Debug/Information 不再寫入，無需重啟。
- [ ] size-based cleanup 使用 `PRAGMA page_count * page_size` 判斷，清理後明確執行 `VACUUM`。
- [ ] retention cleanup 與 size cleanup 可單次觸發測試，不依賴 background timing。

**驗證：**
- [ ] Integration test：publish event 後 AppLogs 可查到結構化欄位。
- [ ] Integration test：修改 `log.min_level` 後低等級 log 被抑制。
- [ ] Integration test：超過 `log.db_max_size_mb` 後清理並 vacuum，DB page size 低於門檻容差。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`

**依賴：** Task 5, Task 8

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Web/Logging/`
- `src/Vulperonex.Infrastructure/Logging/`
- `tests/Vulperonex.Tests.Integration/Logging/`

**規模：** M

---

## Task 19 - Vue 前端骨架 + SignalR composable

**描述：** 建立可執行的 `src/frontend` Vite/Vue 應用，包含 routing、Pinia、vue-i18n、API client、SignalR composable、overlay route skeleton、管理 console layout。若缺少必要 npm package，先停下來取得同意再安裝。

**驗收標準：**
- [ ] `src/frontend/package.json` 釘定 `packageManager`，並提供 `dev`、`test`、`build`、`lint` scripts。
- [ ] Vite build 輸出至 `src/Hosts/Vulperonex.Web/wwwroot`，生成檔維持不提交。
- [ ] API client 支援 relative base URL 與 `VITE_API_URL` override。
- [ ] `useStreamEvents` 連線 `/hubs/events`，收到 envelope 後更新 reactive state。
- [ ] `/overlay/chat`、`/overlay/alerts`、`/overlay/member` route 可掛載；member overlay 顯示空狀態。
- [ ] 管理 console 首頁顯示 API health、Twitch auth status、no-Twitch mode 狀態。
- [ ] UI text 走 vue-i18n；至少提供 `zh-TW` 與 `en-US` 語系檔，manifest 控制可用語系。
- [ ] XSS 邊界：overlay 顯示使用 text binding，不使用 `v-html` 渲染外部事件內容。

**驗證：**
- [ ] `cd src/frontend; pnpm test`
- [ ] `cd src/frontend; pnpm build`
- [ ] `cd src/frontend; pnpm lint`（若 linter dependency 已安裝）
- [ ] Browser manual：開啟 Web host 首頁，console layout 可載入且 API status 正確。
- [ ] Browser manual：開啟 `/overlay/chat` 後執行 CLI `simulate chat hello from ui smoke`，畫面收到事件。

**依賴：** Task 15

**預計觸及檔案：**
- `src/frontend/package.json`
- `src/frontend/vite.config.ts`
- `src/frontend/src/main.ts`
- `src/frontend/src/router/`
- `src/frontend/src/api/`
- `src/frontend/src/composables/useStreamEvents.ts`
- `src/frontend/src/i18n/`
- `src/frontend/src/views/overlay/`
- `src/frontend/src/views/dashboard/`
- `src/frontend/tests/`

**規模：** M

---

## Task 20 - 管理 Console：simulate / member / rule / Twitch auth

**描述：** 實作瀏覽器內可操作的核心工作流。這個任務的目標是讓使用者不用 CLI 也能完成 Phase 5 manual verification 的主要步驟：模擬事件、確認 member side effect、建立/啟用/停用/刪除 rule、重置/啟動 Twitch OAuth。

**驗收標準：**
- [ ] Simulate panel 支援 chat/follow/sub，送出後顯示 ack：accepted、eventTypeKey、eventId、platformUserId、displayName。
- [ ] Event monitor 顯示 `/hubs/events` envelope，至少包含 type、eventId、platform、occurredAt。
- [ ] Member panel 支援 list/show，並提供 manual seed/delete 測試操作；成功與錯誤都有明確 toast/status。
- [ ] Rule panel 支援 list/show/create/update/enable/disable/delete。
- [ ] Rule create/update 支援 JSON file/manual JSON textarea；API validation error 顯示在欄位附近或 summary 區。
- [ ] Rule enable/disable/delete 成功後畫面立即反映狀態，不靜默。
- [ ] Twitch panel 顯示 `clientIdConfigured` / `hasRefreshToken`；缺 ClientId 顯示 no-Twitch mode，不產生 authorize URL。
- [ ] Twitch panel 支援 auth start/reset；reset 只清 refresh token。
- [ ] 409 `WORKFLOW_RULE_CONFLICT`、400/403/404 error codes 均以 i18n 顯示並保留原始 code。
- [ ] 所有 destructive 操作使用確認 dialog；dialog 不包在卡片內。

**驗證：**
- [ ] Vitest：simulate form success/error rendering。
- [ ] Vitest：member list empty/success/error states。
- [ ] Vitest：rule enable/disable/delete 成功後更新 local state。
- [ ] Vitest：error code i18n coverage，確保 MVP error codes 有翻譯字串。
- [ ] Browser manual：以 Web UI 完成 `simulate chat` -> member appears -> rule create -> disable -> enable -> delete。
- [ ] Browser manual：缺 Twitch ClientId 時 UI 顯示 no-Twitch mode；有 ClientId 時 auth start 開啟 Twitch URL。

**依賴：** Task 19, Task 14a, Task 14b, Task 16f, Task 16g

**預計觸及檔案：**
- `src/frontend/src/views/dashboard/`
- `src/frontend/src/views/simulate/`
- `src/frontend/src/views/members/`
- `src/frontend/src/views/rules/`
- `src/frontend/src/views/twitch/`
- `src/frontend/src/components/`
- `src/frontend/src/api/`
- `src/frontend/src/i18n/`
- `src/frontend/tests/`

**規模：** L，實作時應拆成多個小 commit。

---

## Task 21 - Photino Desktop Shell + 靜態 fallback

**描述：** 讓 `Vulperonex.Desktop` 啟動 Web host 並載入 Vue UI。埠衝突、WebView2 缺失、migration 失敗、Web host crash 都要有清楚且可操作的 fallback。

**驗收標準：**
- [ ] `dotnet run --project src/Hosts/Vulperonex.Desktop` 開啟桌面視窗並載入 Web UI。
- [ ] API/overlay port pair 必須同時可用才使用；任一 port 被占用時切到下一組 pair。
- [ ] 5000/5001 到 5008/5009 全耗盡時顯示 dialog，不留下半啟動 process。
- [ ] WebView2 缺失時顯示下載連結與退出選項。
- [ ] migration 失敗時顯示 Open log folder / Exit。
- [ ] Web host crash 時顯示內嵌 fallback HTML 與 Restart 按鈕。
- [ ] Desktop host 不改變 Web API loopback-only 安全邊界。

**驗證：**
- [ ] Unit test：mock WebView2 detector 回報缺失，dialog callback 被觸發。
- [ ] Unit test：mock migration failure，dialog 包含 Open log folder / Exit。
- [ ] Unit test：mock Web host crash，fallback HTML 顯示 Restart。
- [ ] Manual：占用 5000 或 5001 時 app 切到 5002/5003。
- [ ] Manual：占用全部 port pair 時看到清楚錯誤。
- [ ] Manual：Desktop shell 中完成 simulate chat -> overlay event 顯示。

**依賴：** Task 19, Task 20

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Desktop/`
- `src/Hosts/Vulperonex.Desktop/Resources/fallback.html`
- `tests/Vulperonex.Tests.Unit/Desktop/`

**規模：** M

---

## Checkpoint：Phase 6

- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm test`
- [ ] `cd src/frontend; pnpm build`
- [ ] `cd src/frontend; pnpm lint`（dependency 已存在時）
- [ ] Browser manual：Web UI 首頁載入，API/Twitch status 正確。
- [ ] Browser manual：Web UI simulate chat/follow/sub 顯示 ack 並推送 SignalR event。
- [ ] Browser manual：Web UI member list/show/seed/delete 可驗證。
- [ ] Browser manual：Web UI rule create/show/enable/disable/delete 可驗證。
- [ ] Browser manual：Twitch auth no-Twitch mode、reset、start 行為可驗證。
- [ ] Desktop manual：Photino shell 載入 Web UI 並完成核心 smoke。
- [ ] 文件：新增或更新 `docs/phases/phase-6-web-ui/manual-verification.md` 記錄人工驗證結果。

---

## 風險與緩解

| 風險 | 影響 | 緩解 |
|------|------|------|
| npm dependencies 尚未安裝或版本不符 | 高 | Task 19 開始前檢查 `package.json`/lockfile；需要新增套件時先取得同意 |
| Rule visual builder 範圍過大 | 中 | 先交付 JSON editor + 基本表單，確保與 CLI/API contract 一致 |
| Twitch OAuth 從 Web UI 啟動與 CLI loopback callback 語意混淆 | 中 | Web UI 只透過 Web API Twitch auth endpoints，不直接保存 token；缺 ClientId 時 fail closed |
| SignalR 測試在 CI flake | 中 | 自動化測試驗 contract/state；時序只在 manual verification 補充 |
| Desktop/Photino 問題遮蔽 Web UI 問題 | 中 | 先用瀏覽器驗證 Web host，再進 Task 21 Desktop shell |
| 目前 Phase 5.5 CLI id resolution worktree 尚未收斂 | 低 | Phase 6 docs 不依賴該 dirty diff；實作 Task 20 前再確認 API/CLI 最終命令語意 |

---

## 建議實作順序

1. Task 19a：前端 package/build/test skeleton。
2. Task 19b：API client + i18n manifest + dashboard shell。
3. Task 19c：SignalR composable + overlay route skeleton。
4. Task 20a：simulate panel + event monitor。
5. Task 20b：member panel。
6. Task 20c：rule list/show + enable/disable/delete。
7. Task 20d：rule create/update JSON editor + validation display。
8. Task 20e：Twitch auth panel。
9. Task 18：Serilog/AppLogs。
10. Task 21：Photino Desktop shell。

Task 20 建議拆成多個 commit；每個 panel 完成後都可用瀏覽器手動驗證。
