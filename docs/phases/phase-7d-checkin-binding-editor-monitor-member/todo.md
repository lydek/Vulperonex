# Phase 7D 待辦：CheckIn 綁定、Custom HTML 編輯器、統一監控頁、會員可編輯

> 對應規劃：`docs/phases/phase-7d-checkin-binding-editor-monitor-member/plan.md`
> 父層待辦：`tasks/todo.md`
> SPEC：`docs/SPEC.md` §4.14.2、§4.14.3、§4.18、§4.19

## Track A — CheckIn → MemberOverlay 綁定

### Task 50 - MemberCheckedInEvent 領域事件

- [ ] Task 50a：定義 `MemberCheckedInEvent` record 於 `Vulperonex.Domain.Events`
- [ ] Task 50b：實作 `IStreamEvent`（EventId / OccurredAt / Platform / EventTypeKey）
- [ ] Task 50c：`EventTypeKey = "system.member.checked_in"` 註冊到 `StreamEventTypeRegistry`
- [ ] Task 50d：Unit test 涵蓋構造 + EventTypeKey 一致性

### Task 51 - TriggerCheckInActionExecutor 發 event

- [ ] Task 51a：Executor 注入 `IStreamEventBus`
- [ ] Task 51b：計算 RoundIndex / StampSlotInRound（用 `overlay.member.stamps_per_round` 設定）
- [ ] Task 51c：成功 increment 後 publish event（transaction commit 後）
- [ ] Task 51d：失敗路徑不 publish
- [ ] Task 51e：Unit test mock event bus 驗 publish 呼叫

### Task 52 - OverlayEventForwarder 訂閱 MemberCheckedInEvent

- [ ] Task 52a：注入 `IHubContext<OverlayMemberHub>` + `IOverlayHistoryService<OverlayMemberPayload>`
- [ ] Task 52b：`stream.OfType<MemberCheckedInEvent>().Subscribe(...)` 訂閱
- [ ] Task 52c：實作 `ForwardMemberCheckInEventAsync`，走 `SafeSendAsync` + `TryPersistAsync`
- [ ] Task 52d：Integration test：publish event → hub group 收到 + history 可查

### Task 53 - OverlayMemberPayload 擴充 + 反射測試

- [ ] Task 53a：Payload 加 `RoundIndex` + `StampSlotInRound`
- [ ] Task 53b：`OverlayDtoWhitelistTests` 更新精確 key set
- [ ] Task 53c：維持 `memberId/totalLoyalty/linkedPlatforms` 排除
- [ ] Task 53d：前端 `useOverlayHub.ts` type 同步
- [ ] Task 53e：`MemberOverlayView` 改讀 payload `RoundIndex`

### Task 54 - CLI simulate checkin 走 event publish

- [ ] Task 54a：CLI 子指令改 publish path
- [ ] Task 54b：既有 CLI integration test 重新 green
- [ ] Task 54c：更新 `docs/cli.md`（若存在）

---

## Track B — Custom HTML 編輯器

### Task 55 - Draft/Production 目錄重構

- [ ] Task 55a：`OverlayPresetStore` 支援 `draft/` `production/` `history/` 三子目錄
- [ ] Task 55b：Middleware `RewritePath` 處理 `/overlay/custom/{slug}/*` → `production/`
- [ ] Task 55c：預覽路徑 `/overlay/custom/{slug}/draft/*` 不 rewrite
- [ ] Task 55d：Zip upload 解壓改 `draft/`
- [ ] Task 55e：Startup migration：偵測無 `production/` 的既有 preset，搬移到 `production/`
- [ ] Task 55f：Phase 7C integration test 維持綠

### Task 56 - Files API endpoints

- [ ] Task 56a：`GET /api/overlay/custom-presets/{slug}/files`
- [ ] Task 56b：`GET /files/{path}?env=draft|production`
- [ ] Task 56c：`PUT /files/{path}`（draft only）
- [ ] Task 56d：`DELETE /files/{path}`（draft only）
- [ ] Task 56e：Path sanitize + server-side 二次驗最終絕對路徑
- [ ] Task 56f：單檔 2MB / 整 slug 10MB 上限
- [ ] Task 56g：Binary 上傳擋（回 400）
- [ ] Task 56h：Integration test 全 case

### Task 57 - Validation Gate

- [ ] Task 57a：NuGet add `AngleSharp` / `ExCSS` / `Jint` (ask-first)
- [ ] Task 57b：HTML parse error → issue
- [ ] Task 57c：CSS parse error → issue
- [ ] Task 57d：JS parse error → issue
- [ ] Task 57e：SignalR contract probe（regex）
- [ ] Task 57f：Hub URL 引用檢查
- [ ] Task 57g：外部 URL warning
- [ ] Task 57h：檔案大小 issue
- [ ] Task 57i：Issue DTO 定義（severity/code/message/filePath?/line?）
- [ ] Task 57j：`POST /validate` endpoint
- [ ] Task 57k：Unit test 每種 issue type
- [ ] Task 57l：Integration test 合法 + 各種錯誤 sample

### Task 58 - Deploy / Rollback / History

- [ ] Task 58a：`POST /deploy` 流程：validate → write history → atomic copy
- [ ] Task 58b：Atomic copy（temp dir + rename）
- [ ] Task 58c：History 旋轉（保留最近 10 份）
- [ ] Task 58d：`POST /rollback?to={ts}` 還原
- [ ] Task 58e：`GET /history` 列表
- [ ] Task 58f：同 slug concurrent deploy → 409
- [ ] Task 58g：Integration test 完整 flow

### Task 59 - Admin Overlay Editor UI

- [ ] Task 59a：新路由 `/admin/overlay-editor`
- [ ] Task 59b：NPM add `monaco-editor` (ask-first)
- [ ] Task 59c：左 sider：slug list + file tree
- [ ] Task 59d：中 Monaco editor（語言依副檔名）
- [ ] Task 59e：右 iframe preview + draft/production toggle
- [ ] Task 59f：操作列：Save / Validate / Deploy / Rollback
- [ ] Task 59g：Validate issues panel + 點選跳檔位置
- [ ] Task 59h：Deploy 前自動 validate，error 阻擋
- [ ] Task 59i：Dirty state confirm dialog
- [ ] Task 59j：i18n 雙語
- [ ] Task 59k：Vitest 涵蓋核心 flow

### Task 60 - Zip upload 整合

- [ ] Task 60a：Phase 7C `POST /api/overlay/custom-presets` 解壓改 `draft/`
- [ ] Task 60b：上傳後 server 自動跑 validate
- [ ] Task 60c：回傳 `{ slug, issues }` 給 UI
- [ ] Task 60d：Phase 7C integration test 改寫
- [ ] Task 60e：UI 顯示 issues + 引導到 Overlay Editor

---

## Track C — 統一即時監控頁

### Task 61 - /monitor Dashboard 骨架

- [x] Task 61a：新增 `MonitorDashboardView.vue` + router 註冊
- [x] Task 61b：`/` 預設導 `/monitor`
- [x] Task 61c：寬螢幕三欄 layout（collapsible Sider 380px + 主區 grid 7fr/3fr 或 6fr/4fr）
- [x] Task 61d：窄螢幕 drawer（Escape close + focus trap 簡版 toggle↔close）
- [ ] Task 61e：Header 平台/SignalR 狀態 chip — 目前由 `/api/health` 輪詢驅動，ChatStreamPanel 內額外有 SignalR live 狀態 dot；dashboard chip → SignalR migration 延期（plan task 3.5）
- [x] Task 61f：既有路由保留

### Task 62 - Simulate Controls Panel

- [x] Task 62a：抽出 `SimulateControlsPanel.vue` 共用元件
- [x] Task 62b：既有 `SimulateView` 重構為 wrapper
- [x] Task 62c：批次 checkin 工具（N 次 + PrimeVue ProgressBar）
- [x] Task 62d：失敗 toast 沿用 `ApiError` handling
- [x] Task 62e：Vitest 涵蓋 4 section render / test-mode toggle / batch ProgressBar / alias ack 等

### Task 63 - Overlay Preview iframe

- [x] Task 63a：Iframe `src` 動態組合
- [x] Task 63b：Hub tab 切換（chat / member / alerts）
- [x] Task 63c：背景切換（5 選 1）
- [x] Task 63d：Preset dropdown（讀 `GET /api/overlay/presets`）
- [x] Task 63e：Custom preset draft/production 切換
- [x] Task 63f：Reload button（bump timestamp）
- [x] Task 63g：iframe sandbox attribute 維持安全 — 預覽 iframe 維持 `sandbox="allow-scripts allow-same-origin"` 以保留同源 overlay runtime

### Task 64 - Chat Stream Panel

- [x] Task 64a：新增 `ChatStreamPanel.vue`
- [x] Task 64b：重用 `useOverlayHub("chat")` composable + 新增 `useHubConnectionState` 三層 reconnect pattern
- [x] Task 64c：列最新 50 筆（時間 / displayName / message snippet）
- [x] Task 64d：`memberSnapshot` chip 顯示
- [x] Task 64e：Clear 按鈕（不影響 history）+ Reconnect 按鈕（Disconnected 才顯示）
- [x] Task 64f：Vitest mock hub event + useHubConnectionState 8 case 覆蓋三層 pattern

---

## Track D — 會員可編輯

### Task 65 - MemberAuditLogs migration + repository

- [ ] Task 65a：EF Core migration 新增 table（schema 見 SPEC §4.19）
- [ ] Task 65b：`IMemberAuditLogRepository` 定義 + 實作
- [ ] Task 65c：Append-only（無 update / delete）
- [ ] Task 65d：Index `(MemberId, OccurredAt DESC)`
- [ ] Task 65e：Unit test append + query
- [ ] Task 65f：Cleanup worker（`members.audit_retention_days` 預設 365，沿用 `AppLogsCleanupWorker` pattern）

### Task 66 - Member mutation endpoints

- [ ] Task 66a：`PATCH /api/members/{id}/loyalty`
- [ ] Task 66b：`POST /api/members/{id}/reset`
- [ ] Task 66c：`POST /api/members/{id}/delete-token`
- [ ] Task 66d：`DELETE /api/members/{id}`
- [ ] Task 66e：`GET /api/members/{id}/audit?limit&offset`
- [ ] Task 66f：`If-Match` etag concurrency（基於 `UpdatedAt` ticks hash）
- [ ] Task 66g：`reason` validation（3-500 字元）
- [ ] Task 66h：Loopback-only middleware
- [ ] Task 66i：每個 mutation 寫 audit log
- [ ] Task 66j：Integration test 全 case + concurrency + token expiry
- [ ] Task 66k：OpenAPI doc 更新

### Task 67 - Member Edit UI

- [ ] Task 67a：`AdjustLoyaltyModal.vue`（before/after diff + reason）
- [ ] Task 67b：`ResetModal.vue`（checkboxes + reason）
- [ ] Task 67c：`DeleteConfirmDialog.vue`（兩段確認 + token）
- [ ] Task 67d：`AuditLogDrawer.vue`（timeline + 無限滾動）
- [ ] Task 67e：MembersView 加入操作按鈕
- [ ] Task 67f：409 conflict toast + auto reload
- [ ] Task 67g：i18n 完整雙語
- [ ] Task 67h：a11y（dialog role + focus trap）
- [ ] Task 67i：Vitest 每個 modal flow

### Task 68 - Workflow audit 整合

- [ ] Task 68a：`TriggerCheckInActionExecutor` 注入 `IMemberAuditLogRepository`
- [ ] Task 68b：成功 increment 後寫 audit（ActorKind=workflow / ActorId=ruleId）
- [ ] Task 68c：Unit test mock repo 驗 audit 呼叫
- [ ] Task 68d：失敗路徑不寫 audit

---

## Track E — 模組與外掛程式管理系統

### Task 69 - 模組與外掛程式狀態持久化與 API 整合

- [x] Task 69a：支援以 `modules.enabled.{name}` 設定鍵值儲存開關狀態
- [x] Task 69b：實作 `GET /api/plugins-modules` 端點，回傳模組狀態與相依性
- [x] Task 69c：實作 `POST /api/plugins-modules/{name}/toggle` 端點，更新狀態並傳送 `SettingChangedEvent`
- [x] Task 69d：狀態變更寫入系統 Audit Log（`ActorKind='user'`, `Operation='disable_module'`）
- [x] Task 69e：單元與整合測試覆蓋 API 正確性

### Task 70 - 模組相依性拓撲連鎖反應實作

- [x] Task 70a：後端實作拓撲相依性分析與連鎖狀態更新
- [x] Task 70b：停用 `MemberModule` 時，拓撲判定自動連鎖停用 `CheckInModule` 與 `LotteryModule`
- [x] Task 70c：啟用 `CheckInModule` 時，若 `MemberModule` 關閉，自動開啟相依或提示拒絕
- [x] Task 70d：前端 Switch 控制項與連鎖確認 Dialog 整合，展示受影響模組清單
- [x] Task 70e：測試連鎖啟用與連鎖停用邏輯，防範無效狀態

### Task 71 - Hosted Services 動態啟閉與 No-Op 設計

- [x] Task 71a：Hosted Services 啟動時偵測對應模組開關狀態
- [x] Task 71b：訂閱 `SettingChangedEvent`，在狀態變更為 `false` 時取消 EventBus 訂閱並切換至 No-Op
- [x] Task 71c：`IWorkflowActionExecutor` 執行時若模組關閉則拋出 `DependencyMissingException`
- [x] Task 71d：單元測試 mock 服務狀態驗證 Workflow Action 阻擋

### Task 72 - 模組管理 UI 頁面 (Module Management)

- [x] Task 72a：於 `/admin/settings` 下建立「功能模組與外掛程式」分頁
- [x] Task 72b：卡片化 Grid 展現核心 Hosted Services 與 OneComme 外掛程式
- [x] Task 72c：整合 ON/OFF 開關 Switch 與相依性圖示提示
- [x] Task 72d：Vitest 覆蓋模組卡片渲染與 Toggle 互動

---

## Track F — 事件與忠誠度模擬功能擴充

### Task 73 - 模擬 API 擴充身分組 (StreamRole Flags)

- [x] Task 73a：`SimulateRequest` DTO 之 `Roles` 欄位支援多重陣列解析
- [x] Task 73b：字串陣列成功對應至 `StreamRole` 的 Flags 組合
- [x] Task 73c：帶入模擬事件的 User context 中，保證事件匯流排收到的 flag 完整
- [x] Task 73d：單元測試涵蓋多角色結合與邊界解析

### Task 74 - 打卡與忠誠度模擬端點

- [x] Task 74a：實作 `POST /api/simulate/checkin` 端點
- [x] Task 74b：端點接收 `platformUserId`, `displayName`, `skipCooldown`, `stampCount`
- [x] Task 74c：調用 `IMemberResolver` 與 `IMemberStreamStateRepository` 增量
- [x] Task 74d：成功寫入資料庫後發布 `MemberCheckedInEvent` 事件到 EventBus
- [x] Task 74e：整合測試模擬端點行為與狀態變更

### Task 75 - 監控控制台模擬 UI 擴充 (SimulateControlsPanel)

- [x] Task 75a：`SimulateControlsPanel.vue` 加入身分組多選勾選框 (Checkbox Group)
- [x] Task 75b：新增忠誠度與打卡模擬專用區塊表單
- [x] Task 75c：模擬按鈕點選調用 `POST /api/simulate/checkin` 端點
- [x] Task 75d：Vitest 測試各模擬控制元件之互動

---

## Track G — 視覺化工作流設定 UI

### Task 76 - 條件建構器前端元件 (ConditionBuilder)

- [x] Task 76a：實作 `ConditionBuilder.vue` 以視覺化行編輯取代純文字
- [x] Task 76b：行內支援 `[變數]` `[比較運算子]` `[目標值]` 下拉
- [x] Task 76c：讀取 `StreamEventTypeRegistry` 與 Context 變數進行防呆
- [x] Task 76d：前端自動將視覺化條件組裝成 NCalc 表示式字串
- [x] Task 76e：Vitest 測試條件列增刪與表示式生成

### Task 77 - 強型別動作動態表單 (DynamicActionForm)

- [x] Task 77a：實作 `DynamicActionForm.vue` 元件
- [x] Task 77b：讀取後端 `ActionParameterMetadata` 生成強型別輸入元件
- [x] Task 77c：去除自由文字 JSON，改為 Slider, Toggle, Select 等精緻控制項
- [x] Task 77d：Vitest 測試不同動作參數類型之對應渲染

### Task 78 - 變數選擇器浮動面板 (Variable Picker)

- [x] Task 78a：實作變數 Picker 浮動按鈕或鍵入 `{` 彈出面板
- [x] Task 78b：展示當前事件與全域可用之變數列表
- [x] Task 78c：點選自動以 `{user.name}` 格式將變數插入文字框游標處
- [x] Task 78d：Vitest 測試游標位置定位與字串插入

---

## Checkpoint：Phase 7D

- [ ] 全部 Task 50-78 sub-task `[x]` 完成自檢
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual 全 PASS（見 `plan.md` Checkpoint 區）
- [ ] Security review 全 PASS（見 `plan.md` Checkpoint 區）
- [ ] `manual-verification.md` 記錄 dated entries + evidence commits

---

## 風險追蹤

見 `plan.md` 風險與緩解表。

## Out-of-Scope

見 `plan.md` Out-of-Scope。
