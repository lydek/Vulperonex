# 第 6 階段實作計畫：Web UI + 日誌 + Desktop Shell

> 父計畫：`tasks/plan.md`
> 父待辦清單：`tasks/todo.md`
> 任務範圍：Task 18-21
> 前置條件：Phase 5 CLI / Web API / SignalR / Twitch OAuth manual verification 已完成並記錄於 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`
> [!IMPORTANT]
> **前置條件 Gate**：父計畫中 Phase 5 Checkpoint 的三項手動驗收（包含 CLI E2E 收尾、Twitch OAuth 真實瀏覽器授權、以及 REPL 手動驗收）必須確認已勾選完成，此 Phase 6 方可開工實作。
> **⚠ OAuth Gate 注意**：「真實瀏覽器授權」需包含完整 code exchange + refresh_token 加密保存，不僅是 `auth start` 開啟瀏覽器。若 Phase 5 manual-verification.md 僅記錄開啟授權 URL，須在有效 `Twitch:ClientId` 環境補充完整 OAuth round-trip 驗收後方可通過此 Gate。

---

## 時程預估與 Onboarding 說明 (II18, II21)

- **Phase 5.5 收斂與 CLI 整合整理**：**0.5 天** (II21)（專注於 CLI id resolution 隔離與 dirty diff 的 git cleanup，以及補齊 Phase 5 的真實 OAuth round-trip 授權測試紀錄，確保 master branch 乾淨與 audit trail 完整）。
- **Phase 6 實作時程**：**7 天** (II18)
  - Task 19 前端骨架建置與 SignalR/Polling 整合：**2 天**
  - Task 20 五大管理面板 (含 1MB JSON 貼入防護、LWW 去重、指數退避)：**3 天**
  - Task 18 Serilog 三 Sink 與 AppLogs 清理機制：**1 天**
  - Task 21 Photino 桌面外殼 (含單實例偵測、IPC Bridge、崩潰重啟上限)：**1 天**
- **Onboarding 指引**：開發人員啟動專案前，優先執行 `corepack enable` 釘定 pnpm@9.15.4 版本；若 Windows 權限阻止建立全域 pnpm shim，改用 `corepack pnpm@9.15.4 <command>` 執行所有前端命令。執行 `pnpm dev` 或 `corepack pnpm@9.15.4 dev` 啟動 Vite dev server 進行 smoke 測試時，stdout 出現 `VITE ... ready in` 字樣即可視為成功，手動 Ctrl+C 後繼續執行後續步驟。

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
- Overlay route 與管理 admin route 分離；overlay route 只處理顯示，不包含管理控制。
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

**描述：** 設定 Console、Rolling File + SQLite AppLogs 三個 sink，加入結構化欄位 enricher，並實作 `log.db_retention_days` / `log.db_max_size_mb` 清理 worker。`log.min_level` 必須透過 SystemSettings 熱重載。`log.db_max_size_mb` 預設值為 `50` (50MB) (II23, HH30)，`log.db_retention_days` 預設值為 `30` (30天)。

**驗收標準：**
- [ ] Console、rolling file、SQLite AppLogs 均可寫入，且不重複設定 `PRAGMA auto_vacuum`（HH11）。
- [ ] AppLogs rows 含 EventTypeKey、Platform、MemberId、WorkflowRuleId、ActionType 等結構化欄位。
- [ ] **去識別化與隱私合規 (II24, HH29)**：`MemberId` 欄位僅記錄已去識別化（Pseudonymized）的 ULID，屬於 pseudonymous，程式碼與日誌中必須有明確的非 PII (Non-PII) 註記，嚴格禁止記錄 any PII (如真實姓名、E-mail 或 platform 帳號原始 ID)。
- [ ] `config set log.min_level Warning` 後 Debug/Information 不再寫入，無需重啟。
- [ ] size-based cleanup 與 retention cleanup 整合於單一背景 worker 中，**以先觸發者為準** (HH18)。size-based cleanup 使用 `PRAGMA page_count * page_size` 判斷，清理後明確執行 `VACUUM`。
- [ ] retention cleanup 與 size cleanup 可單次透過呼叫 `AppLogsCleanupWorker.ExecuteOnce()` 觸發測試，不依賴 background timing。

**驗證：**
- [ ] **dotnet Integration test (後端) (II3)**：
  - `Given_AppLogs_When_PublishedEvent_Then_ContainsPseudonymizedMemberId` 驗證日誌欄位，包含 `MemberId` 屬 pseudonymous 的 Non-PII 註記斷言。
  - `Given_LogSettings_When_MinLevelWarning_Then_SuppressDebugAndInfo` 驗證熱重載 log level。
  - `Given_AppLogs_When_SizeThresholdExceeded_Then_TriggerCleanupAndVacuum` 驗證以先觸發者為準的 cleanup 策略與 vacuum 後 DB page size 降低。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`

**依賴：** Task 5, Task 8

**預計觸及檔案：**
- `src/Hosts/Vulperonex.Web/Logging/`
- `src/Vulperonex.Infrastructure/Logging/`
- `tests/Vulperonex.Tests.Integration/Logging/`

**規模：** M

---

## Task 19 - Vue 前端骨架 + SignalR composable

**描述：** 建立可執行的 `src/frontend` Vite/Vue 應用，包含 routing、Pinia、vue-i18n、API client、SignalR composable、overlay route skeleton、管理 admin layout。
- **Task 19a 啟動與 Lockfile-only 預檢 (II14)**：本任務啟動前，開發人員必須在 `src/frontend` 中執行 `corepack enable` 以確保釘定版本 pnpm@9.15.4 生效；若 Windows 權限阻止建立全域 pnpm shim，改用 `corepack pnpm@9.15.4 <command>`。pnpm 9.15.4 的 `install` 不支援 `--dry-run`，因此預檢命令固定為 `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts`，用來驗證 Vite 7.3、PrimeVue 4、UnoCSS 等前端技術棧的版本相容性且不執行 lifecycle scripts。
- 全部 stack 套件（Vue 3.5、Vite 7.3、PrimeVue 4 Unstyled、UnoCSS Preset Wind 4、Pinia、vue-i18n、oxlint、vue-tsc 以及所有傳遞依賴）首次安裝前須取得使用者同意（ask-first 協議）。
- **Git Scope Auto Gate (II20)**：在 `src/frontend` 中設定 `simple-git-hooks` 或 `husky` 配合 `commitlint`。設置自動化 Hook 在 `commit-msg` 階段執行 scope check，強制阻斷不符合 Conventional Commits 格式的 git commit 提交，從工具鏈層級提供自動化防護。

**驗收標準：**
- [ ] 本任務啟動前已執行 `corepack enable`，或在 Windows shim 權限受阻時改用 `corepack pnpm@9.15.4 <command>`；且 `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts` 通過，確保無版本相容性衝突 (II14)。
- [ ] `src/frontend/package.json` 釘定 `"packageManager": "pnpm@9.15.4"`（`pnpm --version` 回傳 `9.15.4`；不使用 `9.x.x` 萬用版本），並提供 `dev`、`test`、`build`、`lint` scripts。
- [ ] `package.json` 含 `"lint": "oxlint --config oxlint.json"` script；`oxlint.json` 含 Vue 3 + TypeScript rule set；`pnpm lint` 無錯誤（oxlint 為指定 linter，不使用 ESLint）。
- [ ] Vite build 輸出至 `src/Hosts/Vulperonex.Web/wwwroot`，生成檔維持不提交。
- [ ] API client 支援 relative base URL 與 `VITE_API_URL` override。
- [ ] `useStreamEvents` 連線 `/hubs/events`（管理 Hub），收到 envelope 後更新 reactive state。
- [ ] `/overlay/chat`（連線 `/hubs/overlay/chat` 獨立 Hub）、`/overlay/alerts`（連線 `/hubs/overlay/alerts` 獨立 Hub）可掛載與連線；前端消費後端定義之 DTO contract，重申其為「消費端白名單」，即消費端不解構或傳遞額外欄位，詳細 DTO 欄位精確白名單規範以 cross-ref 指向父計畫 [tasks/plan.md § Task 15 DTO 規格](file:///d:/code/Vulperonex/tasks/plan.md)，避免行號變動失效風險。
- [ ] `/overlay/member`（連線 `/hubs/overlay/member` 獨立 Hub）可掛載；顯示 MVP skeleton 空狀態 UI（Server 端 MVP 階段不發送事件至 `/hubs/overlay/member`，不 crash，確認 post-MVP 頁面不 crash）。
- [ ] **useEventStore Setup Store 設計 (II7, II10)**：
  - 前端與後端管理 Hub `/hubs/events` 的通訊由 `useStreamEvents` composable 處理，但接收到的所有事件信封（Envelope）必須轉推並匯流至 Pinia Setup Store `useEventStore` (位於 `src/frontend/src/stores/eventStore.ts`)。
  - **命名與單向數據流規範 (II10)**：Pinia store 統一採用 Setup Store 語法，命名規則必須為 `use[Feature]Store` (例如 `useAuthStore`、`useWorkflowStore`、`useMonitorStore`)。狀態必須以唯讀屬性 (`readonly(state)`) 露出，且僅能透過 Store Actions 進行修改，維持 unidirectional data flow (單向數據流)。
  - **資料去重與一致性 (Last-write-wins) (II7)**：Store 內以 `eventId` 為 key 進行儲存與維護。由 `useEventStore` 作為 **Last-write-wins reducer** 的實作位置。當 SignalR 與 HTTP Polling fallback 同時傳來相同 `eventId` 的事件時，必須比對 envelope 的 `occurredAt`，採用 `last-write-wins` (LWW) 覆蓋策略，將最新 occurredAt 的內容寫入 store。
  - **Overlay 獨立與解耦設計**：Overlay（`/overlay/chat`, `/overlay/alerts`）不共享 `useEventStore` 的狀態。它們應直接並單獨呼叫各自的 `useOverlayHub(hubName)` composable 連線對應 of `/hubs/overlay/chat` 與 `/hubs/overlay/alerts` 獨立 Hub。這能確保 Overlay 的安全性邊界（僅消費白名單欄位，不將敏感的管理主控台資料帶入 Overlay 頁面，防範 DTO 溢出）。
- [ ] 管理 admin 首頁僅顯示狀態卡片（API health、Twitch auth status、no-Twitch mode），Dashboard Log Widget 標記為 **Defer (非 MVP 範疇)**。
- [ ] UI text 走 vue-i18n；至少提供 `zh-TW` 與 `en-US` 語系檔，並由 manifest 檔 `src/frontend/src/i18n/manifest.json` 控制可用語系，其格式為 `{ "locales": ["zh-TW", "en-US"], "default": "zh-TW" }`。缺字串則顯示 key 名稱而不 crash。
- [ ] XSS 邊界：overlay 顯示使用 text binding，不使用 `v-html` 渲染外部事件內容。

**驗證：**
- [ ] **TypeScript 型別防護 Gate**：執行 `pnpm run build` 前，必須成功通過 `pnpm vue-tsc --noEmit` 型別檢查無 error（Vue SFC 需 vue-tsc，非 tsc）。
- [ ] **Vitest (前端) (II3, II8, II9)**：
  - 所有單元測試符合符合 BDD 規範的 `should [behavior] when [scenario]` 格式 (II8) (例如 `should safe-render chat when XSS payloads injected`)。
  - 測試覆蓋率門檻要求 (II9)：`Branch Coverage ≥ 70%`，`Statement Coverage ≥ 80%`。
  - 核心 Composable 與 Store 的單元測試均成功通過。
- [ ] `cd src/frontend; pnpm test` -> 專屬與 composable 單元測試通過。
- [ ] `cd src/frontend; pnpm build` -> wwwroot 有 index.html + assets。
- [ ] `cd src/frontend; pnpm lint` -> 執行 `oxlint` 語法檢驗，前端 lint 全綠無錯誤。
- [ ] **Browser Manual 驗證**：開啟 Web host 首頁，admin layout 可載入且 API status 正確。
- [ ] **Browser Manual 驗證**：開啟 `/overlay/chat` 後執行 CLI `simulate chat hello from ui smoke`，畫面收到事件。

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
- `src/frontend/src/views/admin/`
- `src/frontend/src/components/admin/`
- `src/frontend/tests/`

**規模：** M

---

## Task 20 - Web 管理主控台 (Web Admin UI)：simulate / member / rule / Twitch auth

**描述：** 實作瀏覽器內可操作的核心工作流。此任務之所有前端主控台 View 與 Component 的路徑均扁平化為 `src/frontend/src/views/admin/` 與 `src/frontend/src/components/admin/`，取代多層 nested 目錄（BB3, CC1, CC2, CC3）。這個任務的目標是讓使用者不用 CLI 也能完成 Phase 5 manual verification 的主要步驟：模擬事件、確認 member side effect、建立/啟用/停用/刪除 rule、重置/啟動 Twitch OAuth，並實作完整的健全性防護機制。

**驗收標準：**
- [ ] Simulate panel 支援 chat/follow/sub，送出後顯示 ack：accepted、eventTypeKey、eventId、platformUserId、displayName。
- [ ] Event monitor 顯示 `/hubs/events` envelope，精確鎖定其 schema 欄位為 `{ type, eventId, platform, occurredAt }`（Phase 5 後端實際 emit 格式，定義於 `src/Hosts/Vulperonex.Web/SignalR/OverlayEventForwarder.cs` 之 `StreamEventEnvelope` record）。`schemaVersion` 與 `data` 欄位之擴充延後至 Phase 7 (非 MVP 範疇)。
- [ ] Member panel 僅支援 list/show 唯讀操作；**不提供 seed/delete 按鈕，不新增 member CRUD 端點**。測試資料建立與清理由 CLI/manual test surface 處理。
- [ ] **成員唯讀負向測試斷言 (Z10)**：所有成員欄位（如姓名、平台識別碼等）在 Web UI 內均為唯讀，不允許在前端直接編輯，且不可出現 seed/delete 操作入口。
- [ ] Rule panel 支援 list/show/create/update/enable/disable/delete。
- [ ] **樂觀鎖支援 (II17)**：前端在更新 Rule 時，必須在 DTO 中攜帶 `version` 欄位以支援後端樂觀鎖驗證。當後端回傳 409 Conflict 時，前端必須捕捉此錯誤並彈出專屬的樂觀鎖衝突提示，引導使用者重新載入或覆蓋。
- [ ] Rule create/update 支援 JSON file/manual JSON textarea；API validation error 顯示在欄位附近或 summary 區。
- [ ] **JSON Textarea 1MB limit 三重 check (II15)**：
  - 實作 textarea `maxlength` 限制。
  - 長度在貼入（paste）時進行 `300ms` 防抖（debounce）檢查，當資料大小超過 1MB 時，拒絕解析並顯示 toast 警告。
  - 將貼入的原始大文字存入非響應式變數（如 plain object 或自訂普通 ref/變數），而非直接賦值給 Vue 的響應式 ref，防範 Vue 反覆偵測屬性變化而造成的主執行緒卡死與 OOM 崩潰。
- [ ] Rule enable/disable/delete 成功後畫面立即反映狀態，不靜默。
- [ ] **a11y ARIA 與 WCAG AA 對比標準 (II16)**：UI 元件與操作均配置 basic a11y ARIA 標籤（如 `aria-label`, `aria-describedby` 等），並符合 WCAG AA 對比標準（前景與背景對比度至少 4.5:1）。
- [ ] Twitch panel 顯示 `clientIdConfigured` / `hasRefreshToken`；缺 ClientId 顯示 no-Twitch mode，不產生 authorize URL。
- [ ] Twitch panel 支援 auth start/reset。**Twitch OAuth 302 重導向回根路徑 (II4, II29)**：Twitch OAuth 授權成功或失敗後，OAuth callback endpoint 必須先在後端消費 `code`、完成 token exchange 與 refresh token 加密保存，再以 `302` 重導向回本機 Web UI 根路徑（`/`）。Web UI 不接收 OAuth `code` 或 raw error；授權結果由 `platform.connection_changed`、`GET /api/twitch/status` 與 toast/status card 呈現。
- [ ] **Twitch Reset 與 emit 變更 (II25)**：執行 Twitch Reset 時，除了清除後端 refresh token，還必須主動斷開與 Twitch 的連線，並向所有訂閱的 Overlay Hubs/Web Clients 發送狀態變更 event 以重置狀態。
- [ ] **Polling fallback 防瞬斷指數退避序列 (II22, II25)**：
  - SignalR 連線瞬斷時觸發 `HubConnection.onclose`，無法重連時啟動 HTTP Polling 作為 fallback。
  - Polling fallback 序列以 `30s` 為 base delay，每次失敗乘上 `2` 倍乘數，最大退避上限為 `300s`。不可在 0s 立即重複呼叫。
  - 當 `onreconnected` 重新連線成功時，必須立即釋放退避定時器（timer），停止 Polling 呼叫。
- [ ] 409 `WORKFLOW_RULE_CONFLICT`、400/403/404 error codes 均以 i18n 顯示並保留原始 code。
- [ ] 所有 destructive 操作使用確認 dialog；dialog 不包在卡片內。

**驗證：**
- [ ] Vitest：simulate form success/error rendering，且符合 basic a11y ARIA 與對比標準。
- [ ] Vitest：member list empty/success/error states，並實作負向測試斷言，確保 Web UI 無 seed/delete 入口且所有成員欄位唯讀 (Z10)。
- [ ] Vitest：rule enable/disable/delete 成功後更新 local state；測試 409 樂觀鎖衝突的捕捉與 UI 提示。
- [ ] Vitest：測試 JSON textarea 1MB 貼入防抖防崩潰機制，確保大於 1MB 的資料會被拒絕且不寫入響應式狀態。
- [ ] Vitest：測試 SignalR 連線中斷後，Polling fallback 序列之指數退避延遲時間計算（30s base, 2x factor, max 300s），並驗證無 0s 立即呼叫，以及連線恢復時 timer 的釋放。
- [ ] Vitest：error code i18n coverage，確保 MVP error codes 有翻譯字串。
- [ ] 【dotnet Integration test】（reset 後端路徑）：`POST /api/twitch/reset` → TwitchAdapter disconnect + `IEventBus.Publish(PlatformConnectionChangedEvent { platform: "twitch", connected: false })`（C# integration test，非 Vitest；確認後端 reset → emit 完整行為鏈）。
- [ ] Browser manual：以 Web UI 完成 `simulate chat` -> member appears -> rule create -> disable -> enable -> delete（依 `manual-verification.md` § Task 20 Browser Manual Checklist）。
- [ ] Browser manual：缺 Twitch ClientId 時 UI 顯示 no-Twitch mode；有 ClientId 時 auth start 開啟 Twitch URL，授權完成後後端完成 code exchange、refresh token 加密保存，並 302 重導向回 `/`，Web UI 透過 status/event 顯示成功狀態（依 `manual-verification.md` § Task 20k）。

**依賴：** Task 19, Task 14a, Task 14b, Phase 5 Task 16f/16g manual gates

**預計觸及檔案：**
- `src/frontend/src/views/admin/`
- `src/frontend/src/components/admin/`
- `src/frontend/src/api/`
- `src/frontend/src/i18n/`
- `src/frontend/tests/`

**規模：** L，實作時應拆成多個小 commit。

---

## Task 21 - Photino Desktop Shell + 靜態 fallback

**描述：** 讓 `Vulperonex.Desktop` 啟動 Web host 並載入 Vue UI。埠衝突、WebView2 缺失、migration 失敗、Web host crash 都要有清楚且可操作的 fallback。因應本專案使用 .NET 10.0 且 Photino 使用 3.x 版本，必須於實作中包含「.NET 10.0 + Photino 3.x 相容性預驗」，並於發生 native runtime 崩潰時提供「切回 WebView2 fallback 或改以獨立 Kestrel 服務運行」之緩解手段 (II30)。

**驗收標準：**
- [ ] `dotnet run --project src/Hosts/Vulperonex.Desktop` 開啟桌面視窗並載入 Web UI。
- [ ] **單實例偵測 (II17)**：本機 Desktop Shell 啟動時必須使用 .NET `NamedMutex`（命名互斥鎖）進行單一實例（Single Instance）偵測。若已存在執行中的實例，則直接退出或彈出錯誤提示，防止 Port 占用與 SQLite locking 衝突。
- [ ] **Photino IPC DTO Schema (II19)**：定義並實作 C# 與 Photino-Vue 前端之間的 IPC 通訊 Bridge，其資料結構精確鎖定為 `{ type: string, payload: any }`。
- [ ] API/overlay port pair 必須同時可用才使用；任一 port 被占用時切到下一組 pair。
- [ ] 5000/5001 到 5008/5009 全耗盡時顯示 dialog，不留下半啟動 process。
- [ ] WebView2 缺失時顯示下載連結與退出選項。
- [ ] migration 失敗時顯示 Open log folder / Exit。
- [ ] Web host crash 時顯示內嵌 fallback HTML 與 Restart 按鈕。
- [ ] **Web Host Crash 重啟上限與 Vitest 斷言 (II13)**：模擬 Web host crash 的重啟行為，前 3 次 crash 會自動 retry 重啟，到第 4 次 crash 時，停止 retry，並在 UI fallback 畫面提示「多次重啟失敗，請手動重啟 Vulperonex 服務」。
- [ ] Desktop host 不改變 Web API loopback-only 安全邊界。

**驗證：**
- [ ] Unit test/Vitest：模擬 Web host crash 重啟，斷言前 3 次自動重啟，第 4 次停止並在 UI fallback 提示「多次重啟失敗，請手動重啟 Vulperonex 服務」之行為符合預期 (II13)。
- [ ] Unit test/Vitest：驗證 Photino IPC Bridge 符合 `{ type: string, payload: any }` 結構。
- [ ] Unit test：mock WebView2 detector 回報缺失，dialog callback 被觸發。
- [ ] Unit test：mock migration failure，dialog 包含 Open log folder / Exit。
- [ ] Unit test：mock Web host crash，fallback HTML 顯示 Restart。
- [ ] Manual：重複開啟 Desktop 實例時，NamedMutex 單實例偵測成功拒絕並提示 (II17)。
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

## Checkpoint：Phase 6 (II2, II6, II26)

- [ ] **自檢卡關 (II6)**：確認 Task 18, 19, 20, 21 之所有 sub-tasks 均已標記為 `[x]`。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm test`
- [ ] `cd src/frontend; pnpm build`
- [ ] `cd src/frontend; pnpm lint`
- [ ] **手動驗證與 Audit Trail 閉環 (II2, II26)**：
  - 確實執行 `docs/phases/phase-6-web-ui/manual-verification.md` 中 Task 20、Task 20k、Task 21 的所有手動驗收步驟。
  - 確認 `docs/phases/phase-6-web-ui/manual-verification.md` 中所有 Dated Entries 均明確記錄為 `Result: PASS` (II2)。
  - 本 Checkpoint 之手動項目一律簡化為：「**依 manual-verification.md § Task 20/21 Browser Manual Checklist 全項目通過**」 (II26)。

---

## 風險與緩解

| 風險 | 影響 | 緩解 |
|------|------|------|
| npm dependencies 尚未安裝或版本不符 | 高 | Task 19 開始前檢查 `package.json`/lockfile；需要新增套件時先取得同意，並嚴格釘定 pnpm 版本且用 `install --lockfile-only --ignore-scripts` 預檢。 |
| Rule visual builder 範圍過大 | 中 | 先交付 JSON editor + 基本表單，確保與 CLI/API contract 一致 |
| Twitch OAuth 從 Web UI 啟動與 CLI loopback callback 語意混淆 | 中 | Web UI 只透過 Web API Twitch auth endpoints，不直接保存 token；缺 ClientId 時 fail closed |
| SignalR 測試在 CI flake | 中 | 自動化測試驗 contract/state；時序只在 manual verification 補充 |
| Desktop/Photino 問題遮蔽 Web UI 問題 | 中 | 先用瀏覽器驗證 Web host，再進 Task 21 Desktop shell |
| 目前 Phase 5.5 CLI id resolution worktree 尚未收斂 | 低 | Phase 6 docs 不依賴該 dirty diff；實作 Task 20 前再確認 API/CLI 最終命令語意 |
| **.NET 10.0 + Photino 3.x 兼容性未經驗證 (II30)** | 中 | Task 19 先以瀏覽器驗證 Web UI；Task 21 負責 .NET 10.0 + Photino 3.x compatibility 預驗。若遭遇 native runtime 崩潰，提供「切回 WebView2 fallback 或改以獨立 Kestrel 服務運行」之緩解手段。 |

---

## 建議實作順序

1. Task 19a：執行 `corepack enable`（若 Windows shim 權限受阻，改用 `corepack pnpm@9.15.4 <command>`）；執行 `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts`（驗 stack 版本兼容性，有錯先解再裝）；確認後安裝全部 stack 套件；建立前端 package/build/test skeleton。
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
