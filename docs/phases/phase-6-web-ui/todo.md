# 第 6 階段待辦清單：Web UI + 日誌 + Desktop Shell

> 詳細計畫：`docs/phases/phase-6-web-ui/plan.md`
> 父待辦清單：`tasks/todo.md`
> **實作順序**：Task 19 → Task 20 → Task 18 → Task 21（依賴圖順序；詳見 plan.md § 建議實作順序）
> [!IMPORTANT]
> **前置條件 Gate**：父計畫中 Phase 5 Checkpoint 的三項手動驗收（包含 CLI E2E 收尾、Twitch OAuth 真實瀏覽器授權含完整 code exchange + refresh_token 保存、以及 REPL 手動驗收）必須確認已勾選完成，此 Phase 6 方可開工實作。

---

## Task 19 - Vue 前端骨架

- [ ] Task 19a：執行 `corepack enable`；建立 `src/frontend` package、Vite 7.3、Vue 3.5、TypeScript，釘定 `"packageManager": "pnpm@9.15.4"`。Task 19 全部新套件（Vue 3.5、Vite 7.3、PrimeVue 4 Unstyled、UnoCSS Preset Wind 4、Pinia、vue-i18n、oxlint、vue-tsc）首次安裝前均須 ask-first。
- [ ] Task 19b：建立 router、Pinia、layout shell、API client 與 `VITE_API_URL` override，區分管理 Hub `/hubs/events` 與 Overlay 專屬 Hubs。
- [ ] Task 19c：建立 vue-i18n manifest `src/frontend/src/i18n/manifest.json`，格式為 `{ "locales": ["zh-TW", "en-US"], "default": "zh-TW" }`，同時提供 `zh-TW` 與 `en-US` 語系檔，缺 key 則顯示 key。
- [ ] Task 19d：建立 dashboard status cards：API health、Twitch auth status、no-Twitch mode (Log/Logs widget 標記 Defer)。
- [ ] Task 19e：建立 `useStreamEvents` composable。
- [ ] Task 19f：建立 `/overlay/chat`、`/overlay/alerts`、`/overlay/member` route skeleton（Server 端 MVP 階段不發送事件至 `/hubs/overlay/member`，Overlay 僅連線 Hub 並呈現空 skeleton）。
- [ ] Task 19g：補齊前端基礎與 XSS text binding 防護單元測試（渲染 `<script>` 或 `displayName` 於 ChatOverlay 與 AlertsOverlay 中均確保為文字節點且無 script 元素），以及 `pnpm dev` 啟動無錯誤驗收（手動驗證）。

## Task 20 - Web 管理主控台 (Web Admin UI)

> 所有前端主控台 View 與 Component 的路徑均扁平化為 `src/frontend/src/views/admin/` 與 `src/frontend/src/components/admin/`，取代多層 nested 目錄（BB3, CC1, CC2, CC3）。
> Vitest 測試命名遵守 `should * when *` 格式（如 `should preserve textarea content when API returns 400`）。

- [ ] Task 20a：Simulate 面板支援 chat/follow/sub 短 alias，成功後顯示 ack 響應與 accepted/eventId/platformUserId 資訊。
- [ ] Task 20b：Event 監看器顯示 SignalR envelope 與最近事件列表。精確鎖定 envelope schema 欄位為 `{ type, eventId, platform, occurredAt }`（對齊 Phase 5 後端 `StreamEventEnvelope` record）。`schemaVersion` 與 `data` 欄位之擴充延後至 Phase 7。
- [ ] Task 20c：Member 面板僅支援 list/show 唯讀操作；不提供 seed/delete 按鈕，不新增 member CRUD 端點。成員唯讀負向測試斷言 (Z10)：所有成員欄位（如姓名、平台識別碼等）在 Web UI 內均為唯讀，不允許直接編輯，且 Vitest 需斷言沒有 seed/delete 操作入口。測試資料建立與清理由 CLI/manual test surface 處理。
- [ ] Task 20d：Rule 面板支援 list/show，顯示 enabled、version、priority、createdAt，刪除等操作導入二次確認對話框。實作樂觀鎖支援 (II17)：前端更新 Rule 時在 DTO 攜帶 `version` 欄位，後端回傳 409 Conflict 時，前端必須捕捉錯誤並彈出專屬樂觀鎖衝突提示，引導重新載入或覆蓋。
- [ ] Task 20e：EventTypeKey Dropdown 實作：**確實排除過濾** `platform.connection_changed` (isSystemEvent: true)，且 Dropdown 以 badge 標示三種 canonical 可模擬 keys，其餘 keys（`user.donated`、`user.gifted_sub`、`channel.raided`、`reward.redeemed`）確實標示為不支援。
- [ ] Task 20f：Rule create/update 支援 JSON file 上傳（限制 1MB / `.json` 副檔名 + MIME + JSON.parse 三重 check）與手動 **JSON Textarea 編輯**，送出失敗保留內容且 refocus 於 textarea（`inputRef.value?.focus({ preventScroll: false })`），行內顯示 API validation error。**實作 JSON Textarea 1MB limit 三重 check (II15)**：實作 textarea `maxlength` 限制；貼入（paste）時進行 `300ms` 防抖檢查，長度超過 1MB 則拒減解析並顯示 toast 警告；貼入的原始大文字存入非響應式變數，而非直接賦值給 Vue 的響應式 ref，防範 Vue 反覆偵測屬性變化而造成主執行緒卡死與 OOM 崩潰。
- [ ] Task 20g：Twitch auth 面板支援 status、start 轉址（系統預設瀏覽器）、reset token。**Twitch OAuth 302 重導向回根路徑 (II4, II29)**：Twitch 授權後由 OAuth callback endpoint 在後端消費 `code`、完成 token exchange 與 refresh token 加密保存，再以 `302` 重導向回本機 Web UI 根路徑（`/`）。Web UI 不接收 OAuth `code` 或 raw error；授權結果由 `platform.connection_changed`、`GET /api/twitch/status` 與 toast/status card 呈現。**Twitch Reset 與 emit 變更 (II25)**：執行 Twitch Reset 時，除了清除後端 refresh token，還必須主動斷開與 Twitch 的連線，並向所有訂閱的 Overlay Hubs/Web Clients 發送狀態變更 event 以重置狀態。缺 ClientId 時呈現 no-Twitch mode。
- [ ] Task 20h：補齊 MVP 錯誤碼於 `zh-TW.ts` 與 `en-US.ts` 之翻譯與覆蓋率單元測試（逐一驗證 errorCodes.ts 常數存在且非空值）；5xx error 顯示 `INTERNAL_ERROR` i18n + `console.error`。
- [ ] Task 20i：完成 browser manual E2E 驗收，覆蓋完整的建立 rule -> 點擊模擬 -> overlay 顯示 -> 狀態更新 -> 刪除流程（依 `docs/phases/phase-6-web-ui/manual-verification.md` § Task 20 Browser Manual Checklist 為驗收唯一來源）。
- [ ] Task 20j：OAuth 閉環：透過 SignalR `platform.connection_changed` 驅動 UI Twitch 狀態卡片自動重新渲染與狀態同步，且在 Vitest 中模擬 `platform.connection_changed` 事件驗證 UI 狀態卡片與 OAuth 狀態的完整更新。**Polling fallback 防瞬斷指數退避序列 (II22, II25)**：SignalR 連線瞬斷時觸發 `HubConnection.onclose`，無法重連時啟動 HTTP Polling 作為 fallback。Polling 序列以 `30s` 為 base delay，每次失敗乘上 `2` 倍乘數，最大退避上限為 `300s`。不可在 0s 立即重複呼叫。當 `onreconnected` 重新連線成功時，必須立即釋放退避定時器（timer），停止 Polling 呼叫。在 Vitest 中對此指數退避序列與定時器釋放進行完整斷言測試。
- [ ] Task 20k：Twitch OAuth E2E 手動人工端到端檢驗，包含 start、status、reset 流程，驗收後將人工測試結果完整寫入 `manual-verification.md`。
- [ ] Task 20l：a11y 與 WCAG AA 支援 (II16)：UI 元件與操作均配置 basic a11y ARIA 標籤（如 `aria-label`, `aria-describedby` 等），並符合 WCAG AA 對比標準（前景與背景對比度至少 4.5:1），並於 Vitest 測試驗證之。

## Task 18 - Serilog + AppLogs

- [ ] Task 18a：設定 Console、rolling file、SQLite AppLogs sink（不重複設定 `PRAGMA auto_vacuum`，已在 Task 5 DB bootstrap）。
- [ ] Task 18b：加入 EventTypeKey、Platform、MemberId、WorkflowRuleId、ActionType 結構化欄位。**去識別化與隱私合規 (II24)**：日誌中的 `MemberId` 欄位僅記錄已去識別化（Pseudonymized）的 ULID，嚴格禁止記錄任何可直接識別使用者的 PII (如真實姓名、E-mail 或平台帳號原始 ID)。
- [ ] Task 18c：實作 `log.min_level` 熱重載。
- [ ] Task 18d：實作 AppLogs retention/size cleanup worker（retention 與 size-based 兩策略以先觸發者為準，其預設值為 `log.db_max_size_mb = 50MB` 與 `log.db_retention_days = 30天`），size cleanup 後執行 `VACUUM`，統一呼叫 `AppLogsCleanupWorker.ExecuteOnce()`。
- [ ] Task 18e：補齊 logging integration tests，並包含 `MemberId` 去識別化合規斷言。

## Task 21 - Photino Desktop Shell

- [ ] Task 21a：Desktop host 啟動 Web host 並載入 Vue UI，設定 `<TargetFramework>net10.0-windows</TargetFramework>` 且支援 Windows 10 1809+。**單實例偵測與 mutex 鎖 (II17)**：啟動時使用 .NET `NamedMutex`（命名互斥鎖）進行單一實例（Single Instance）偵測。若已存在執行中的實例，則直接退出或彈出錯誤提示，防止 Port 占用與 SQLite locking 衝突。
- [ ] Task 21b：整合 port pair allocation，任一 port 被占用時切到下一組 pair（PortPairAllocator 單元測試已於 Task 15 完成，此處僅整合）。
- [ ] Task 21c：WebView2 缺失偵測，顯示包含下載連結（`https://go.microsoft.com/fwlink/p/?LinkId=2124703`）之對話框。
- [ ] Task 21d：Migration 失敗偵測與 dialog 呈現（包含 [Open log folder]（`%LOCALAPPDATA%\Vulperonex\logs`）與 [Exit] 按鈕）。
- [ ] Task 21e：Web host crash 偵測與內嵌 fallback HTML 呈現。**重啟次數上限與 Vitest 斷言 (II13)**：模擬 Web host crash 的重啟行為，前 3 次 crash 會自動 retry 重啟，到第 4 次 crash 時，停止 retry，並在 UI fallback 畫面提示「多次重啟失敗，請手動重啟 Vulperonex 服務」。補齊 mock Web host 單元測試，在單元測試中斷言前 3 次自動重啟、第 4 次停止並提示手動重啟的行為符合預期。
- [ ] Task 21f：補齊 Desktop shell 整合單元測試與手動連線 smoke。定義並實作 C# 與 Photino-Vue 前端之間的 IPC 通訊 Bridge，其資料結構精確鎖定為 `{ type: string, payload: any }`，並於單元測試中驗證此 IPC Bridge 的結構相容性 (II19)。
- [ ] Task 21g：.NET 10.0 + Photino 3.x 相容性預驗 (II30)：執行 compatibility 預驗，若發生 native runtime 崩潰時提供「切回 WebView2 fallback 或改以獨立 Kestrel 服務運行」之緩解手段。

## Phase 6 Checkpoint

- [ ] **前置：Phase 5 Gate 自檢**：父計畫中 Phase 5 Checkpoint 的三項手動驗收（包含 CLI E2E 收尾、Twitch OAuth 真實瀏覽器授權含完整 code exchange + refresh_token 保存、以及 REPL 手動驗收）必須確認已勾選完成，此 Phase 6 方可開工實作。
- [ ] 全部 Task 18-21 sub-task `[x]` 完成自檢。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit` -> TypeScript 型別檢查完全無錯誤（Vue SFC 需 vue-tsc，非 tsc）。
- [ ] `cd src/frontend; pnpm test` -> 所有 Vitest test 通過（涵蓋 composable、overlay XSS、Member negative、JSON textarea、simulate badge、OAuth 閉環、i18n coverage）。
- [ ] `cd src/frontend; pnpm build` -> 建置成功，生成檔無錯誤輸出。
- [ ] `cd src/frontend; pnpm lint` -> 執行 `oxlint` 語法檢驗，前端 lint 全綠無錯誤（若尚未安裝，須先 ask-first 再 npm install；已安裝後直接執行）。
- [ ] `cd src/frontend; pnpm dev` -> 啟動 Vite dev server 進行 smoke 測試（stdout 出現 `VITE ... ready in` 字樣即視為成功；手動 Ctrl+C 後繼續執行後續 checkpoint 步驟）。
- [ ] Browser manual：Web UI 狀態卡片呈現正確，simulate chat/follow/sub 能正確向獨立的 Overlay Hub 推送事件並呈現。
- [ ] Browser manual：Web UI member list/show 唯讀檢視正常，Rule JSON Textarea 編輯與增刪、Twitch OAuth 狀態與啟始正常。
- [ ] Desktop manual：Photino 封裝成功載入 UI，並在埠衝突、WebView2 缺失、migration 失敗等邊界下成功執行對應 fallback/dialog。
- [ ] 文件：於 `docs/phases/phase-6-web-ui/` 目錄下建立並更新 `manual-verification.md` 記錄人工驗收結果，格式與結構完全沿用 Phase 5 之 manual-verification.md 結果格式與 template，保持完整性。
- [ ] Git 暫存集限於 Phase 6 任務範圍；Phase 5.5/CLI resolver 既有 dirty diff 不混入。
- [ ] **人工審查安全性符合標準**：
  - [ ] **Overlay DTO 唯讀安全**：反射驗證 DTO JSON key set 精確符合白名單（詳細欄位精確白名單規範沿用父計畫 `tasks/plan.md` Task 15 之 exact DTO 規格，即 chat/alerts 排除 `memberId`/`platformUserId`；member 排除 `memberId`/`totalLoyalty`/`linkedPlatforms` 且採用 snapshot 結構，不含 `eventId`/`timestamp`）。
  - [ ] **雙埠雙綁定**：API 與 Overlay 雙埠在 Production 下以 Loopback (IPv4/IPv6) 雙綁定。
  - [ ] **OAuth PKCE 安全邊界**：
    - [ ] `state` 參數 CSRF 驗證：state 不符、超過 10 分鐘 TTL 或已使用過 -> 拒絕且不進行 code exchange。
    - [ ] OAuth callback listener：loopback-only (127.0.0.1 / ::1) + Host header allowlist 限制 + 只接受預設 path + 接收後立即關閉 (single-use)。
    - [ ] Logger Scrub 敏感詞：logger scrub 排除 access token、authorization code、code_verifier、raw refresh token。
  - [ ] **設定敏感命名空間防護**：
    - [ ] `/api/config` 讀寫限制：`security.*` / `oauth.*` 封鎖 (回傳 403)；未知 `oauth.*` key 優先封鎖。
    - [ ] CLI 設定防護：`config set security.*`/`config set oauth.*` 拒絕寫入並回傳 403。
  - [ ] **密文加密與生命週期**：
    - [ ] machine.key 不存在時自動生成（首次啟動自動隨機生成），並設定 OS 限制性權限（Windows ACL 目前使用者 FullControl / Unix 0600）；失敗 fail-fast。
    - [ ] AES-256-GCM token 加密：GCM random nonce 強度防篡改校驗，且同一個明文加密兩次所產生的密文不同（驗證隨機 nonce 效應），並與 AAD 繫結設定鍵名，篡改或跨鍵複製拋 `CredentialDecryptionException`。
    - [ ] Refresh token envelope 採用標準 Base64 格式（非 Base64Url），解碼以 `Convert.FromBase64String` 執行。
  - [ ] **Plugin 隔離防護**：
    - [ ] Plugin/Action context 絕不暴露 `System.IServiceProvider` (service locator 反模式，由 PR 代碼審查與架構規範確保，不要求額外寫 ArchUnit/NetArchTest 測試)。
