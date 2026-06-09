# 功能規格書：模擬副作用政策

> [← Back to Master Specification](../../SPEC.md)

### 4.27 模擬事件副作用政策 Simulation Side-Effect Policy (Phase 9)

**背景與動機：**

操作者透過模擬面板（`/api/simulate/*`，`Platform == "simulation"`）送出假事件以驗證工作流規則與 overlay。問題：部分動作執行器（action executor）會對**真實世界**產生副作用，而模擬事件不應觸發這些副作用。已知缺陷：

- `ShoutoutAction` 直接呼叫 Helix `chat/shoutouts` — 模擬會對真實 Twitch 發出 shoutout（甚至對不相干的真實使用者）。
- `RefundRewardRedemptionAction` 呼叫 Helix refund — 模擬會發出**真實退款**。
- `TriggerCheckInAction` / `AddLotteryTicketsAction` / `UpdateCounterAction` 寫入**真實資料庫**（會員忠誠、打卡、稽核、計數器、抽獎券）。
- `LookupPlatformUserAction` 對真實 Helix 發查詢（唯讀，無破壞，但多餘）。

核心原則：**模擬唯一允許的可見輸出為 overlay 預覽（SignalR）；不得外洩到真實外部服務，預設不得寫入真實持久化狀態。**

---

**動作分類與模擬下行為（總表）：**

| 類別 | 動作 | 副作用 | 模擬下處理 |
|---|---|---|---|
| A. Overlay 預覽（保留） | sendChatMessage、triggerEffect、emitOverlayWidget | SignalR → overlay | 照常（這是模擬的可見輸出）。註：sendChatMessage 的真實平台傳送由 `SimulationPlatformChatSender`（no-op）隔離，**不進真實 Twitch chat**，僅上 overlay。 |
| B. 內部控制流（安全） | delay、stopIf、randomPicker、emitSystemEvent、invokeSubWorkflow | 純計算 / in-memory bus / 遞迴 | 照常。`emitSystemEvent` 與 `invokeSubWorkflow` 會傳遞同一事件之 `Platform`，故下游 leaf 動作各自守門即可，串聯自動安全。 |
| C. 外部 Twitch API（**一律 skip**） | shoutout、refundRewardRedemption、lookupPlatformUser | 真實 Helix 呼叫 | **無條件** skip 真實呼叫，回 synthetic output 讓 pipeline 續跑。**無 toggle** — 對真實 Twitch / 他人之不可逆副作用永遠不在模擬中執行。 |
| D. 真實 DB 寫入（**toggle 控制**） | triggerCheckIn、addLotteryTickets、updateCounter | 寫真實會員/計數/稽核 | 由系統設定 `simulation.allow_persistent_writes`（**預設 false**）控制：false → skip 寫入、回 synthetic output（並照常 emit overlay 事件供預覽）；true → 照常寫入（完整持久化測試路徑）。 |
| E. 外掛（作者責任） | invokePlugin | 取決於外掛 | 引擎無法保證；外掛作者須自行依 `IPluginActionContext` 之事件 `Platform` 守門。SPEC 之外掛契約文件須註記此責任。 |

---

**設計與規格：**

1. **守門點：leaf executor**（非 dispatcher）。每個 C/D 類執行器在 `context.StreamEvent.Platform == "simulation"`（字串比對，`OrdinalIgnoreCase`；沿用 `InMemoryWorkflowThrottleService` 與 `WorkflowConditionEvaluator` 既有慣例）時套用對應策略。理由：`emitSystemEvent`/`invokeSubWorkflow` 傳遞同一 `Platform`，leaf 守門即可涵蓋串聯。

2. **C 類（外部 API，一律 skip）** — synthetic output 採「happy-path」語意，使下游步驟（如後續聊天訊息）照常執行：
   - `ShoutoutActionExecutor`：回 `IsSent=true`，`TargetLogin/TargetDisplayName` 取自解析後 target login，`TargetUserId` 為空（不呼叫 Helix 時無法取得真實 user id）。
   - `RefundRewardRedemptionActionExecutor`：回 `IsRefunded=true` + echo `RewardId/RedemptionId`，不呼叫 Helix。
   - `LookupPlatformUserActionExecutor`：回 `IsFound=true`、`Login/UserId/DisplayName` 取自輸入，`Avatar/Description` 空、`IsAffiliate=false`，不呼叫 Helix。
   - 三者皆 `catch (OperationCanceledException) { throw; }` 不適用（直接短路，未進 try）；新增可選 `ILogger<T>? logger = null` 記錄 info（DI 自動注入；保留既有雙參數測試建構式）。

3. **D 類（DB 寫入，toggle）** — 新增系統設定鍵 `SystemSettingKey.SimulationAllowPersistentWrites = "simulation.allow_persistent_writes"`，預設 `false`：
   - `Platform == "simulation"` 且設定為 `false` → **skip 真實寫入**，回 synthetic output；對 `triggerCheckIn` 仍 emit `MemberCheckedInEvent`（overlay 預覽），synthetic count = `(現有 CheckInCount ?? 0) + 1`（沿用 `SimulateEndpoints` checkin `isTest` 之唯讀語意，且**不**因會員不存在而拋例外）。
   - `addLotteryTickets` / `updateCounter`：skip `counterRepository.IncrementAsync`，回 synthetic `TicketCount/Value`（以 amount/delta 為合成值）。
   - 設定為 `true` → 照常寫入（完整持久化路徑）。
   - 執行器相依：`triggerCheckIn` 已注入 `ISystemSettingsService`；`addLotteryTickets` / `updateCounter` 新增**可選** `ISystemSettingsService? settings = null`（DI 自動注入；保留既有測試建構式。`settings == null` 時於模擬下採安全預設 = 視為 false → skip）。

4. **Toggle UI（後續）** — 模擬面板新增「允許寫入持久化（測試模式）」開關，透過設定 API 寫 `simulation.allow_persistent_writes`。MVP 後端預設 `false` 已滿足「模擬不汙染真實資料」之核心需求；UI 開關為進階測試用增強，可後續補。

---

**驗收（BDD）：**

- Given 模擬 raid/訊息事件，When 規則含 shoutout / refund / lookup 動作，Then 不發生真實 Helix 呼叫，且該步驟回 synthetic 成功，後續步驟照常執行。
- Given `simulation.allow_persistent_writes = false`（預設），When 模擬事件觸發 triggerCheckIn / addLotteryTickets / updateCounter，Then **不寫入** DB（無 IncrementCheckIn、無 counter increment、無 audit log），但 triggerCheckIn 仍 emit overlay 卡片事件，且各步驟回 synthetic output。
- Given `simulation.allow_persistent_writes = true`，When 同上，Then 照常寫入（完整持久化）。
- Given 真實 twitch 事件（`Platform == "twitch"`），When 任一動作，Then 行為完全不變（守門不觸發）。
- 單元測試：各 C/D 類執行器之模擬守門測試（assert 無真實呼叫 / 無 repository 寫入 + synthetic output 正確）。

---

**邊界：**

- overlay 預覽（chat/effect/widget/member card）為模擬的**預期**輸出，不在「應抑制」之列。
- C 類無 toggle（外部不可逆副作用永遠不在模擬執行）；僅 D 類（本地可逆資料）提供 toggle。
- 守門以 `Platform == "simulation"` 字串比對為準（沿用既有慣例，不另抽象常數）。
- 外掛之模擬安全為外掛作者責任，引擎不強制攔截。
- 本政策不改變真實事件路徑之任何行為。
