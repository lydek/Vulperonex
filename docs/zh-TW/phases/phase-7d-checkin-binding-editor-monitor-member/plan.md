# Phase 7D 實作計畫：CheckIn→Member 綁定、Custom HTML 編輯器、統一監控頁、會員可編輯、模組與外掛程式管理、模擬事件擴充、直覺工作流 UI

> 父層計畫：`tasks/plan.md`
> 父層待辦：`tasks/todo.md`
> SPEC 對應：`docs/SPEC.md` §4.14.2、§4.14.3、§4.18、§4.19、§4.20、§4.21、§4.22
> 對照來源：`ref/Omni-Commander/OmniCommander.UI/src/components/monitor/MonitorDashboard.vue`、`MonitorControls.vue`、`MonitorOverlay.vue`、`members/MemberDashboard.vue`、`editor/EditorDashboard.vue`、`ModuleManagementTab.vue`、`DynamicActionForm.vue`、`ConditionBuilder.vue`
> 前置條件：Phase 7C 已完成（member overlay preset、URL sanitize、custom preset zip upload、cross-hub member snapshot、OneComme bridge contract）。
> 目標：補齊 Phase 7C 體驗缺口與使用者痛點 — checkin 真實 push 到 member overlay、custom HTML 可線上編輯與驗證、模擬與預覽合併單頁、會員可編輯帶 audit、核心服務與外部外掛程式狀態集中開關控制（帶拓撲連鎖反應）、模擬事件擴充（身分組與打卡/忠誠度模擬）、工作流條件與動作視覺化表單編輯。
> 邊界：OneComme bridge plugin 實作仍延後到 Phase 7E（contract 已於 7C 完成）。

---

## 目標

Phase 7C 補完 overlay 基礎設施，但留下使用者報告的關鍵缺口：

1. **CheckIn 與 member overlay 未綁定** — `TriggerCheckInActionExecutor` 只寫 DB，從未 publish `MemberCheckedInEvent`；`OverlayEventForwarder` 也無 forward 到 `OverlayMemberHub`。結果 `/overlay/member` 在實際 workflow 觸發時收不到任何事件。
2. **Custom HTML upload 無法驗證合法性** — Phase 7C zip upload 落地後沒有任何 syntax / SignalR contract 檢查，使用者只能跑 OBS 才知道是否能掛 hub。
3. **Simulate / overlay / chat 跨頁切換成本高** — 實況主 debug 流程要在多個 route 間跳轉，無法在一頁同時模擬事件 + 看 overlay + 看 chat 資料層。
4. **Member 頁面唯讀不合理** — 真實情境需要手動調 loyalty / 補簽到次數 / 重設 / 刪測試會員，目前只能走 CLI。
5. **模組/外掛程式缺乏集中開關與依賴連鎖** — 無法設定是否開啟特定模組。且模組之間相依性未處理（如會員核心關閉，打卡/抽獎模組仍盲目運行導致狀態漂移）。
6. **事件模擬功能不完整** — 缺少身分組的模擬、打卡及忠誠點數的模擬。
7. **工作流規則設定不直覺** — 使用者必須手動編寫 JSON 或複雜的 NCalc 文字，缺乏防呆與視覺化引導。

Phase 7D 落地七條線：

- **Binding 線**：`MemberCheckedInEvent` + forwarder + DTO 擴充
- **Editor 線**：Monaco editor + draft/production + validation gate（取代純 zip）
- **Monitor 線**：`/monitor` 統一頁，simulate + preview + chat 同框
- **Member edit 線**：adjust / reset / delete + audit log
- **Module Mgmt 線**：功能模組與外部外掛程式集中控制頁、相依性拓撲連鎖關閉閘門與自動啟用
- **Simulation Ext 線**：身分組（StreamRoleFlags）多選模擬、打卡與忠誠點數 API/UI 模擬
- **Visual Rule UI 線**：條件建構器（Condition Builder）、強型別動態動作表單（Dynamic Action Form）與變數選擇器

---

## 範圍

### 內含

- `MemberCheckedInEvent` 領域事件 + publish from `TriggerCheckInActionExecutor`
- `OverlayEventForwarder` 訂閱新事件並 forward `OverlayMemberHub`
- `OverlayMemberPayload` 加 `RoundIndex` / `StampSlotInRound` 欄位 + 反射測試擴充
- CLI `simulate checkin` 改 publish event（不直接 repository）
- `/admin/overlay-editor` Monaco-based 線上編輯頁
- Draft/production 目錄結構 + history 旋轉
- Validation gate：AngleSharp / ExCSS / Jint parse + SignalR contract probe + 檔案大小檢查
- 8 個新 API endpoint (`/api/overlay/custom-presets/{slug}/files`、`validate`、`deploy`、`rollback`、`history`)
- `/monitor` 統一頁 + Simulate controls + Overlay preview iframe + Chat stream
- Member admin 可編輯：AdjustLoyaltyModal、ResetModal、DeleteConfirmDialog、AuditLogDrawer
- `MemberAuditLogs` SQLite table + migration
- `PATCH/POST/DELETE /api/members/{id}/*` 四個新 endpoint + `If-Match` concurrency
- 反射白名單測試擴充（member endpoint 不曝光 internal PK）
- 模組/外掛程式狀態變更 API (`GET/POST /api/plugins-modules/*`) 與 `ISystemSettingsService` 整合
- 模組相依性關閉之拓撲排序連鎖邏輯，以及 Hosted Services 動態狀態熱載入
- `/admin/settings` 下的功能模組管理 UI 分頁
- 模擬端點擴充身分組 flag，以及新增 `POST /api/simulate/checkin` 模擬打卡/忠誠度功能
- `/monitor` 模擬控制台支援身份組多重勾選、打卡與忠誠點數模擬
- `ConditionBuilder.vue` 視覺化條件編輯器與 NCalc 自動輸出
- `DynamicActionForm.vue` 強型別動作參數編輯與變數選擇器浮動面板

### 不含

- OneComme bridge plugin 完整 importer 實作（Phase 7E）
- Workflow `TriggerAdjustLoyaltyAction` 等新 action 類型（單獨 slice）
- Member 大量批次操作 / 匯入 / 匯出（暫無需求）
- /monitor 上 OBS scene preview 整合（純 overlay iframe）
- Overlay editor 多人協作 / 鎖檔機制
- Audit log 加密 / digital signature

---

## 任務分解

### Track A — CheckIn → MemberOverlay 綁定（SPEC §4.14.2）

## Task 50 - MemberCheckedInEvent 領域事件

**描述：** 新增 `MemberCheckedInEvent` 於 `Vulperonex.Domain.Events`，作為 checkin 動作的可訂閱事件。

**驗收標準：**
- [ ] `MemberCheckedInEvent` record 定義（欄位見 SPEC §4.14.2）
- [ ] 實作 `IStreamEvent`（EventId、OccurredAt、Platform、EventTypeKey="system.member.checked_in"）
- [ ] `EventTypeKey` 註冊到 `StreamEventTypeRegistry`
- [ ] Unit test 涵蓋構造 + EventTypeKey 一致性

## Task 51 - TriggerCheckInActionExecutor 發 event

**描述：** Increment 後 publish `MemberCheckedInEvent` 到 `IStreamEventBus`，附帶 displayName / avatarUrl / loyalty snapshot。

**驗收標準：**
- [ ] Executor 注入 `IStreamEventBus`
- [ ] 成功 increment 後計算 RoundIndex / StampSlotInRound
- [ ] Publish event 於 transaction commit 後（避失敗還原留下虛假事件）
- [ ] 既有 `ActionExecutionResult` 不變
- [ ] Unit test：mock `IStreamEventBus` 驗證 publish 呼叫含正確欄位
- [ ] 失敗路徑：increment 失敗不 publish

## Task 52 - OverlayEventForwarder 訂閱 MemberCheckedInEvent

**描述：** `OverlayEventForwarder.StartAsync` 加入 `stream.OfType<MemberCheckedInEvent>().Subscribe(...)`，映射為 `OverlayMemberPayload` 推 `OverlayMemberHub` + 寫 history。

**驗收標準：**
- [ ] 新增 `ForwardMemberCheckInEventAsync` private 方法
- [ ] 注入 `IHubContext<OverlayMemberHub>` + `IOverlayHistoryService<OverlayMemberPayload>`
- [ ] 推送與寫 history 走 `SafeSendAsync` / `TryPersistAsync` 既有 helper
- [ ] Integration test：publish event → hub group 收到 + history endpoint 可查
- [ ] Logger warning 路徑同 chat hub（avatar/cache 失敗不阻斷推送）

## Task 53 - OverlayMemberPayload 欄位擴充 + 反射測試

**描述：** Payload 加 `RoundIndex`、`StampSlotInRound`，更新前端 type 與反射白名單測試。

**驗收標準：**
- [ ] `OverlayMemberPayload` 加兩欄位
- [ ] `OverlayDtoWhitelistTests` 同步更新精確 key set
- [ ] 維持 `memberId` / `totalLoyalty` / `linkedPlatforms` 排除
- [ ] 前端 `useOverlayHub.ts` type 同步
- [ ] `MemberOverlayView` 改用 payload 的 `RoundIndex`，不再前端自己算

## Task 54 - CLI simulate checkin 走 event publish

**描述：** CLI `simulate checkin` 子指令從直接呼叫 repository 改為 publish `MemberCheckedInEvent`（與真實 workflow 路徑一致）。

**驗收標準：**
- [ ] CLI 子指令呼叫 publish path
- [ ] 既有 CLI Integration test 重新 green
- [ ] 文件 `docs/cli.md` 同步更新（若存在）

---

### Track B — Custom HTML 編輯器（SPEC §4.14.3）

## Task 55 - Draft/Production 目錄結構重構

**描述：** 將既有 `wwwroot/overlay/custom/{slug}/` 拆 `production/` + `draft/` + `history/`。Zip upload 解壓目標改 `draft/`。OBS 載入路徑 `/overlay/custom/{slug}/index.html` 透過 middleware rewrite 到 `production/index.html`。

**驗收標準：**
- [ ] `OverlayPresetStore` 支援三子目錄
- [ ] Middleware `RewritePath` 處理 `/overlay/custom/{slug}/*` → `/overlay/custom/{slug}/production/*`
- [ ] 預覽路徑 `/overlay/custom/{slug}/draft/*` 不 rewrite
- [ ] 既有 zip upload 落 `draft/` 而非根目錄
- [ ] Phase 7C 既有 integration test 維持綠
- [ ] 既有上傳的 preset 自動 migrate（startup 偵測「無 production/ 子目錄」者搬到 production/）

## Task 56 - Files API endpoints (list / read / write / delete)

**描述：** 4 個 file CRUD endpoint，支援 Monaco editor 載入與儲存 draft 內容。

**驗收標準：**
- [ ] `GET /api/overlay/custom-presets/{slug}/files` 列檔（含 draft/production diff）
- [ ] `GET /files/{path}?env=draft|production` 讀檔（UTF-8 text，binary 回 400）
- [ ] `PUT /files/{path}` 寫 draft（不可寫 production）
- [ ] `DELETE /files/{path}` 刪 draft 單檔
- [ ] Path sanitize：禁 `..`、絕對路徑、控制字元；server-side 二次驗最終絕對路徑必在 `draft/` 內
- [ ] 單檔大小上限 2MB 寫入時擋
- [ ] 整 slug 大小上限 10MB（draft + production + history）；超過 PUT 回 413
- [ ] Integration test：path traversal、size limit、binary 上傳、不存在的 slug 全 case

## Task 57 - Validation Gate

**描述：** `POST /validate` 對 draft 跑 syntax + contract probe，回 issues list。

**驗收標準：**
- [ ] 引入 NuGet：`AngleSharp`、`ExCSS`、`Jint`（ask-first）
- [ ] HTML parse error 列 issue（severity=error）
- [ ] CSS parse error 列 issue（severity=error）
- [ ] JS parse error 列 issue（severity=error）
- [ ] SignalR contract probe（regex）：缺 `OverlayCommon.initSignalRConnection(` 或 `signalR.HubConnectionBuilder` → warning
- [ ] `/hubs/overlay/{chat|alerts|member}` URL 引用缺失 → warning
- [ ] 外部 URL 引用 → warning（含位置）
- [ ] 整檔 / 個別檔過大 → error
- [ ] Issue DTO 含 `severity`, `code`, `message`, `filePath?`, `line?`
- [ ] Unit test 涵蓋每種 issue type 至少一個 case
- [ ] Integration test：合法 sample passes、各種錯誤 sample 報對應 issue

## Task 58 - Deploy / Rollback / History endpoints

**描述：** Deploy 為原子目錄複製，舊 production 寫 history。Rollback 從 history 還原。

**驗收標準：**
- [ ] `POST /deploy`：先呼叫 validate，error 阻擋；通過後寫 history、複製 draft→production
- [ ] 複製為原子（temp dir + rename）
- [ ] History 旋轉：保留最近 10 份（按 timestamp 排序），舊 prune
- [ ] `POST /rollback?to={ts}`：對應 history → production，現有 production 進新 history entry
- [ ] `GET /history` 列 history timestamp + size
- [ ] Integration test：deploy 後 OBS URL 反映新內容、history 正確生成、rollback 還原
- [ ] Concurrency：同 slug 同時 deploy → 第二個 409（簡易 in-memory lock）

## Task 59 - Admin Overlay Editor UI

**描述：** `/admin/overlay-editor` 新頁，Monaco editor + file tree + draft/production toggle + iframe preview + validate/deploy/rollback buttons。

**驗收標準：**
- [ ] 新增路由 `/admin/overlay-editor`
- [ ] 引入 NPM：`monaco-editor`（ask-first）
- [ ] 左側 sider：slug list + 選定 slug 的 file tree
- [ ] 中間：Monaco editor，語言依副檔名（html / css / js / json）
- [ ] 右側：iframe live preview，draft / production 切換
- [ ] 操作列：Save draft / Validate / Deploy / Rollback dropdown
- [ ] Validate issues panel：error 紅、warning 黃，點選跳至對應檔位置
- [ ] Deploy 前自動跑 validate，error 不讓 deploy
- [ ] Dirty state：未存的 draft 切檔顯示 confirm dialog
- [ ] i18n 雙語
- [ ] Vitest 涵蓋：file tree render、validate issue render、deploy confirm flow

## Task 60 - Zip upload 整合 + 既有 endpoint 行為調整

**描述：** Phase 7C 既有 `POST /api/overlay/custom-presets` 解壓改 `draft/`，回傳新 endpoint 設計的 validate 結果。

**驗收標準：**
- [ ] Zip 解壓目標改 `draft/`
- [ ] 上傳成功後 server 自動跑 validate，回 `{ slug, issues }` 給 UI
- [ ] UI 顯示 issues 並提示「請於 Overlay Editor 修正後 deploy」
- [ ] 既有 Phase 7C integration test 改寫對應新行為

---

### Track C — 統一即時監控頁（SPEC §4.18）

## Task 61 - /monitor 路由 + Dashboard 骨架

**描述：** 新增 `/monitor` 為新預設 landing。三欄寬螢幕、上下堆疊窄螢幕。

**驗收標準：**
- [ ] 新增 `MonitorDashboardView.vue` + router 註冊
- [ ] `/` 預設導 `/monitor`
- [ ] 寬螢幕 sider + main + aside 三欄 layout
- [ ] 窄螢幕：sider 改 drawer（沿用 ConfirmDialog focus trap）
- [ ] Header：平台連線狀態 chip + SignalR 狀態 chip + Live/Settings 切換
- [ ] 既有 `/simulate`、`/overlay/*`、`/admin/members` 路由保留

## Task 62 - Simulate Controls (sider)

**描述：** 將既有 `SimulateView` 的事件模擬功能元件化，置入 `/monitor` sider。

**驗收標準：**
- [ ] 抽出 `SimulateControlsPanel.vue` 共用元件（既有 `SimulateView` 重構為 wrapper）
- [ ] 涵蓋：chat / follow / sub / giftsub / raid / bits / redeem / checkin
- [ ] 新增「批次 checkin」工具（N 次傳送 + 進度條）
- [ ] 失敗訊息 toast（沿用既有 ApiError handling）
- [ ] Vitest：每種 simulate 觸發 emit + ack 路徑

## Task 63 - Overlay Preview iframe (main)

**描述：** 中間預覽區，iframe 顯示 chat / member / alerts overlay；可切背景、preset、draft/production；Reload button。

**驗收標準：**
- [ ] Iframe `src` 動態：`/overlay/{hub}?preset={key}&t={ts}`
- [ ] Hub 切換 tab（chat / member / alerts）
- [ ] 背景切換：transparent / green / pink / 自訂色（color picker）/ 自訂圖 URL（sanitize 同 §4.14.1）
- [ ] Preset 切換 dropdown（讀 `GET /api/overlay/presets`）
- [ ] Custom preset 時可切 draft / production
- [ ] Reload button（bump timestamp）
- [ ] iframe sandbox attribute 維持安全邊界（`allow-scripts allow-same-origin` 必要）

## Task 64 - Chat Stream Panel (aside)

**描述：** 右側 chat stream panel 訂閱 `/hubs/overlay/chat`，純表格樣式列訊息 + member chip preview（不渲染 preset CSS）。

**驗收標準：**
- [ ] 新增 `ChatStreamPanel.vue`
- [ ] 重用既有 `useOverlayHub("chat")` composable
- [ ] 列最新 50 筆訊息（時間 / displayName / message snippet）
- [ ] 若 payload 帶 `memberSnapshot` → 顯示 chip（頭像 + checkInCount）
- [ ] Clear 按鈕清空（不影響後端 history）
- [ ] Vitest：mock hub event → 表格正確 render

---

### Track D — 會員可編輯介面（SPEC §4.19）

## Task 65 - MemberAuditLogs migration + repository

**描述：** 新增 `MemberAuditLogs` table、entity、repository。

**驗收標準：**
- [ ] EF Core migration 新增 table（schema 見 SPEC §4.19）
- [ ] `IMemberAuditLogRepository` interface + 實作
- [ ] Append-only：repository 只暴露 `AppendAsync` + `QueryAsync`，無 update/delete
- [ ] Index：`(MemberId, OccurredAt DESC)`
- [ ] Unit test：append + query 順序
- [ ] Cleanup worker：定期刪超過 `members.audit_retention_days`（預設 365）的記錄，沿用 `AppLogsCleanupWorker` pattern

## Task 66 - Member mutation endpoints

**描述：** `PATCH /loyalty`、`POST /reset`、`DELETE /`、`GET /audit` 四個 endpoint。

**驗收標準：**
- [ ] 全部走 `If-Match` etag concurrency；版本不符 409
- [ ] `reason` required，3-500 字元
- [ ] `PATCH /loyalty`：寫 audit before/after JSON
- [ ] `POST /reset`：選擇性歸零 loyalty/checkIn；寫 audit
- [ ] `DELETE`：需先 `POST /delete-token` 拿 30s 拋棄式 token；body 帶 token
- [ ] `GET /audit?limit&offset` 分頁
- [ ] Loopback-only（既有 middleware）
- [ ] Integration test 全 case + concurrency + delete token expiry
- [ ] OpenAPI doc 更新

## Task 67 - Member Edit UI (modals + drawer)

**描述：** Admin members 頁加入可編輯模態。

**驗收標準：**
- [ ] `AdjustLoyaltyModal.vue`：表單 + before/after diff 預覽 + reason
- [ ] `ResetModal.vue`：兩個 checkbox + reason
- [ ] `DeleteConfirmDialog.vue`：兩段確認（拿 token → 確認執行）
- [ ] `AuditLogDrawer.vue`：右側 drawer，timeline 列 audit；無限滾動載更多
- [ ] MembersView 從唯讀升為含三個操作按鈕 + 一個 audit 按鈕
- [ ] 409 conflict 處理：顯示 toast + 自動 reload
- [ ] i18n 完整雙語
- [ ] a11y：dialog role + focus trap（沿用 ConfirmDialog）
- [ ] Vitest：每個 modal flow + concurrency error 處理

## Task 68 - Workflow audit 整合

**描述：** `TriggerCheckInActionExecutor` 寫 audit log（ActorKind=workflow, ActorId=ruleId）。

**驗收標準：**
- [ ] Executor 注入 `IMemberAuditLogRepository`
- [ ] 成功 increment 後寫 audit
- [ ] Unit test：mock repo 驗 audit 呼叫
- [ ] 失敗路徑：increment 失敗不寫 audit

---

## Checkpoint：Phase 7D

- [ ] 全部 Task 50-68 sub-task 達成驗收標準
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：
  - [ ] simulate checkin → `/overlay/member` 5s 內顯示卡片
  - [ ] `/admin/overlay-editor` 新建 slug → 寫檔 → validate → deploy → OBS 載入 production 反映
  - [ ] Validate 故意餵 broken HTML → error 阻擋 deploy
  - [ ] `/monitor` 寬螢幕三欄 + 窄螢幕 drawer + simulate → 預覽 + chat stream 即時反應
  - [ ] AdjustLoyaltyModal 調 loyalty → audit drawer 看到記錄
  - [ ] DeleteConfirmDialog 兩段確認 → 會員消失
  - [ ] 同一會員兩個分頁同時編輯 → 第二個 409 提示 reload
- [ ] Security review：
  - [ ] Custom preset path traversal（draft + production + history 三層）
  - [ ] Editor PUT/DELETE 不可寫 production
  - [ ] Delete token expiry 嚴守 30s
  - [ ] Audit log append-only
  - [ ] `OverlayMemberPayload` 反射白名單擴充正確
  - [ ] Member mutation endpoint loopback-only
- [ ] `manual-verification.md` 紀錄 dated entries + evidence commits

---

## 風險與緩解

| 風險 | 影響 | 緩解 |
| --- | --- | --- |
| Monaco editor bundle 過大拖累 admin 載入 | 中 | Lazy-load `/admin/overlay-editor` route，使用 dynamic import |
| Draft/production middleware rewrite 與 SPA route 衝突 | 高 | 限定 rewrite 範圍 `/overlay/custom/*`，OBS path 與 SPA route 互不重疊；加 integration test |
| MemberCheckedInEvent 太頻繁衝爆 OverlayHub | 中 | 沿用既有 `IOverlayHistoryService` 容量限制（20）；burst test 1000 events 驗 latency |
| Audit log 寫入失敗阻斷 mutation 主路徑 | 中 | Audit append 在 transaction 內必成功；失敗則 mutation 也還原，避狀態漂移 |
| Delete confirm token 被前端 cache 重複使用 | 中 | Token 為拋棄式 + 30s TTL，server 端 dictionary 用後即刪 |
| Validation gate 引入新相依套件（AngleSharp/ExCSS/Jint）破壞既有 build | 中 | Ask-first NuGet add；隔離於新 module；既有 build pipeline 不受影響 |
| Zip upload migration 對既有已上傳 preset 破壞 | 中 | Startup 自動偵測無 `production/` 子目錄者，搬移現存內容到 `production/`；migration 寫單獨 unit test |

---

## Out-of-Scope

- OneComme bridge plugin importer 實作（留 Phase 7E）
- 多 streamer / 多 channel 會員資料隔離
- 雲端 audit log 同步
- 跨會員批次操作（merge / split / 匯入）
- Monaco editor 多人協作 / 鎖檔
- Overlay editor preview 整合 OBS scene
- Workflow `TriggerAdjustLoyalty` / `TriggerDecreaseLoyalty` 新 action 類型

---

## 對 SPEC 章節對應

| SPEC | Task |
| --- | --- |
| §4.14.2 CheckIn → MemberOverlay 綁定 | Task 50-54 |
| §4.14.3 Custom HTML 編輯 pipeline | Task 55-60 |
| §4.18 Unified Monitor Page | Task 61-64 |
| §4.19 Member editable surface | Task 65-68 |
