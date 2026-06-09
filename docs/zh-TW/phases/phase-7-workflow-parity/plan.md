# Phase 7 實作計畫：Workflow Parity with Omni-Commander

> 父計畫：`tasks/plan.md`
> 父待辦清單：`tasks/todo.md`
> 對照來源：`ref/Omni-Commander/OmniCommander.Domain/Workflows/` + `ref/Omni-Commander/OmniCommander.Application/Workflows/` + `ref/Omni-Commander/OmniCommander.Application/Workflows/Executors/` + `ref/Omni-Commander/OmniCommander.Tests/Workflows/` + `ref/Omni-Commander/walkthrough.md`
> 優先順序：Phase 7 先行；Phase 6 未完成的 Photino/manual verification 等非 workflow parity 項目延後處理。
> 前置條件：Phase 5 runtime + Phase 6 已完成的 Web UI/rule JSON editor/overlay history 基線可用；不再等待完整 Phase 6 Checkpoint。
> 父層同步：Phase 7 已在 `tasks/plan.md` / `tasks/todo.md` 建立父層指標，為目前優先 active implementation slice。
> 進度來源：本文件中的 checkbox 僅作設計/驗收草案；實際完成狀態以 `docs/phases/phase-7-workflow-parity/todo.md` 與 `tasks/todo.md` 為準。

---

## 目標

把 Vulperonex 的 workflow 引擎能力對齊 Omni-Commander 的 DSL-style executor 集 + 變數插值 + Rule-level 控制平面，**保留** Vulperonex 已建立的優勢（強型別 Action / 樂觀鎖 / idempotent replay / Plugin 機制 / TDQ），把兩者的長處合併成 unified workflow runtime。

---

## 設計原則

- **加法為主，不破壞**：既有 `WorkflowAction` 強型別多型保留並擴充；不退化為純字串 DSL。
- **變數插值層獨立**：所有 Action 共用 `IExpressionContext` + `{Trigger.*}` / `{Step.*}` / `{Args.*}` 解析器；強型別 Action 可選擇性吃 template 字串。
- **NCalc 為單一 expression 引擎**：條件式（`ExecutionCondition`、`MatchCondition`）統一走 NCalc，避免再造輪子；Phase 7 只提供資料 namespace / scalar helpers，不開放任意自訂 function 註冊 API。
- **Rule-level 控制 > Action-level**：rule-level timeout / throttle / OnFailure 為 first-class，與既有 Action-level timeout / retry 並存（rule 為外層 envelope）。
- **Sub-process pattern**：`EventTypeKey` 仍為主要觸發鍵；新增 `IsSubWorkflow: bool` 旗標標示「只能被 invoke」的 rule，等同 OC `Trigger == null`。
- **熱重載快取**：rule cache 改 immutable snapshot + version-keyed swap，仿 OC `DeepCopy`，避免執行中 rule 被 EF tracker 改動。
- **Executor 集分批引入**：每個新 executor 一個 commit + integration test，可獨立驗收。
- **Plugin 不變**：新 built-in executor 與 plugin executor 共用 `IWorkflowActionExecutor`，不分流。
- **DTO/JSON 契約穩定**：所有 schema 變更走 EF migration + DTO whitelist 測試。

---

## 相依圖

```text
Task 23 Variable / Expression substrate
    -> Task 24 Step ExecutionCondition + OutputVariable
    -> Task 25 Rule-level Throttle + Timeout
    -> Task 26 OnFailureSteps
    -> Task 28 Hot reload snapshot cache
    -> Task 29 Trigger filter + MatchCondition
    -> Task 27 Sub-process flag + InvokeRule polish
    -> Task 30 Executor expansion (split into 30a-30l)
    -> Task 32 ChatOutboxService
    -> Task 31 WorkflowTimer scheduler
    -> Task 33 Web UI builder upgrade for new schema
    -> Task 34 Plugin Action plumbing for new variable surface
    -> Task 35 Manual verification + parity sign-off
```

---

## Task 23 - Variable / Expression Substrate

**描述：** 抽出獨立的 `IExpressionContext` + `ITemplateResolver`，可解析 `{Trigger.*}` / `{Step.*}` / `{Args.*}` / `{Member.*}` placeholder。NCalc 為條件式單一引擎，置於 `Vulperonex.Application/Expressions/` 命名空間。

**驗收標準：**
- [ ] `ITemplateResolver.Resolve(string template, ExpressionContext context)` 字串插值；無 placeholder 時 ZeroCopy 返回原字串。
- [ ] `IExpressionEvaluator.Evaluate(string expression, ExpressionContext context)` 走 NCalc，回傳 bool / object；變數只來自 `ExpressionContext` namespace，禁止直接暴露 arbitrary CLR object / reflection。
- [ ] `ExpressionContext` 暴露 `Trigger`、`Steps`、`Args`、`Member` 四個 namespace；read-only。
- [ ] 缺失 placeholder（如 `{Trigger.Missing}`）依設定回傳空字串或 `null`（fail-soft，不丟例外）；模式可由 SystemSettings `workflow.template.strict_missing` 切換。
- [ ] DI 註冊：`AddSingleton<ITemplateResolver, TemplateResolver>()`, `AddSingleton<IExpressionEvaluator, NCalcExpressionEvaluator>()`.
- [ ] 加 NuGet `NCalcSync`（ask-first；Phase 7 實作時已先取得批准。若後續需改用其他 NCalc package，先更新本文件與 lockfile 預檢再安裝）。

**驗證：**
- [ ] Unit test：placeholder 解析正向 + 缺失 fail-soft；NCalc 運算式以 namespace scalar value 驗證（例如 `Member.IsBroadcaster == true && Counter > 5`）；template + expression 巢狀。
- [ ] Unit test：strict mode 切換時 missing placeholder 拋 `TemplateMissingPlaceholderException`。

**預計觸及檔案：**
- `src/Vulperonex.Application/Expressions/ITemplateResolver.cs`（新）
- `src/Vulperonex.Application/Expressions/IExpressionEvaluator.cs`（新）
- `src/Vulperonex.Application/Expressions/ExpressionContext.cs`（新）
- `src/Vulperonex.Infrastructure/Expressions/TemplateResolver.cs`（新）
- `src/Vulperonex.Infrastructure/Expressions/NCalcExpressionEvaluator.cs`（新）

**規模：** M

---

## Task 24 - Step ExecutionCondition + OutputVariable

**描述：** `WorkflowAction` 共用基底加 `ExecutionCondition: string?`（NCalc）+ `OutputVariable: string?`（寫入 `{Step.<name>.*}`）。Engine 在執行前評估 condition，略過時記錄為 Skipped。

**驗收標準：**
- [ ] `WorkflowAction` 基底加兩屬性。
- [ ] `IWorkflowActionExecutor.ExecuteAsync` 介面回傳改為 `Task<ActionExecutionResult>`（包含 `OutputValues: IReadOnlyDictionary<string, object?>?`）。
- [ ] Engine 評估 `ExecutionCondition` (false → skip + 記 Skipped)；執行後把 OutputValues 寫入下個 step 可見的 `{Step.<OutputVariable>.*}`。
- [ ] `InMemoryWorkflowActionExecutionStore` 多 `Skipped` 終端機狀態，replay 時 skip。

**驗證：**
- [ ] Unit test：ExecutionCondition false → 後續 step 仍能跑（除非 condition 是 stop）。
- [ ] Unit test：step1 OutputVariable=`Lookup` → step2 `{Step.Lookup.Avatar}` 解析成功。

**規模：** M

---

## Task 25 - Rule-level Throttle + Timeout

**描述：** `WorkflowRule` 加 `Throttle: WorkflowThrottlePolicy { MaxConcurrent, CooldownSeconds, PerUserCooldown, PerUserCooldownSeconds }` + `TimeoutSeconds: int (default 30)`。Engine ExecuteRuleAsync 包 linked CTS（rule timeout）+ 走 `IWorkflowThrottleService` 取得 permit。

**驗收標準：**
- [ ] `WorkflowThrottlePolicy` value record；JSON migration 從舊 schema fallback (MaxConcurrent ← rule.MaxParallelism)。
- [ ] `IWorkflowThrottleService` 介面 + in-memory impl：global cooldown last-fire 紀錄 / per-user cooldown map（key=userId, TTL）。
- [ ] Engine `ExecuteRuleAsync` 加 linked CTS by TimeoutSeconds；timeout 觸發 → 標 `Abandoned`。
- [ ] EF migration：`WorkflowRules` 加 `ThrottleJson: string`、`TimeoutSeconds: int`（default 30）。
- [ ] Backward compat：舊 rule 自動套用 default throttle（max_concurrent=1, cooldown=0）。

**驗證：**
- [ ] Unit test：cooldown 期間 reject second fire；TTL 過後 accept。
- [ ] Unit test：PerUserCooldown，user A 冷卻 user B 仍可觸發。
- [ ] Unit test：Rule timeout < Action timeout → rule timeout wins，actions 被取消標 Abandoned。

**規模：** L

---

## Task 26 - OnFailureSteps

**描述：** `WorkflowRule` 加 `OnFailureSteps: IReadOnlyList<WorkflowAction>?`，當主流程因 `StopOnError` 中止或 timeout 時，engine 跑 OnFailureSteps 作為補救（不再有第二層 OnFailure）。

**驗收標準：**
- [ ] Rule schema 加欄位 + EF 持久；優先新增 `OnFailureActionsJson`（或等價新欄位），不混入既有 `ActionsJson`，避免主鏈與補救鏈序列化/遷移互相汙染。
- [ ] Engine：主鏈失敗 → 創新 ExecutionContext（注入 `{Trigger.*}` + `{Failure.StepIndex}` + `{Failure.ErrorMessage}`） → 跑 OnFailureSteps 鏈，**OnFailureSteps 本身不再支援 OnFailureSteps**（避免遞迴）。
- [ ] Idempotent replay：OnFailure 鏈 ActionExecutionKey 加 `phase=OnFailure` 區分。

**驗證：**
- [ ] Unit test：main step 拋例外 → OnFailureStep 執行 + Failure context 變數可解。
- [ ] Unit test：OnFailureStep 自己失敗 → 不再二次補救，標 Failed。

**規模：** M

---

## Task 27 - Sub-process Flag + InvokeRule Polish

**描述：** `WorkflowRule` 加 `IsSubWorkflow: bool` 旗標。`true` 時 engine 不會由 event bus 觸發，只能由 `InvokeSubWorkflowAction` 呼叫。修整 `InvokeSubWorkflowActionExecutor` 把 parent rule 的 `{Step.*}` 變數透傳給 child（透過 `Args` namespace）。

**驗收標準：**
- [ ] EF migration 加 `IsSubWorkflow: bool` (default false)。
- [ ] Engine HandleEventAsync 過濾 `IsSubWorkflow=true` 之 rule。
- [ ] InvokeSubWorkflow 接受 `Args: Dictionary<string,string>` 範本字串；child 端透過 `{Args.<key>}` 取得。
- [ ] `InvocationId` 穩定性不回退：Args plumbing 不得改變既有 `InvokeSubWorkflowAction` 的 stable invocation identity；TDQ replay 必須使用同一 dedup key。
- [ ] 既有 cycle check 不變。

**驗證：**
- [ ] Unit test：sub-workflow rule 不被 event 觸發。
- [ ] Unit test：parent step Output → child Args → child 內可解。
- [ ] Unit test：同一 parent event/rule/action replay 時 child invocation id 與 `ActionExecutionKey` 維持穩定。

**規模：** S

---

## Task 28 - Hot Reload Snapshot Cache

**描述：** Engine 內部維 `IRuleSnapshotCache`：load on first use + on settings change `workflow.cache.invalidate`，每次取 rule 拿 immutable snapshot copy（仿 OC DeepCopy）。執行中 rule 與 EF tracker 完全隔離。

**驗收標準：**
- [ ] `IRuleSnapshotCache.GetByEventTypeAsync` / `GetByIdAsync` 回傳 deep-copied `WorkflowRule`。
- [ ] Cache invalidation：rule CRUD 後 push invalidation event；engine 訂閱後重 build snapshot。
- [ ] 執行中 rule 被改 → 用 snapshot 跑完不受影響。

**驗證：**
- [ ] Unit test：rule update 中途，inflight execution 仍用舊 snapshot。
- [ ] Unit test：cache size 不無限增長（snapshot eviction by rule id）。

**規模：** M

---

## Task 29 - Trigger Filter + MatchCondition

**描述：** `WorkflowRule` 加 `Trigger: WorkflowTrigger { EventTypeKey, Filter: Dictionary<string,string>, MatchCondition: string? }`（NCalc）。`EventTypeKey` 維持頂層欄位以便 SQL 過濾；Filter + MatchCondition 於 in-memory 評估。

**驗收標準：**
- [ ] Schema 擴充 + EF migration（FilterJson, MatchCondition）。
- [ ] Engine HandleEventAsync：先 SQL `EventTypeKey == ?` → 再 in-memory 跑 Filter equality（不分大小寫）→ 再跑 MatchCondition NCalc。
- [ ] Filter key 命名：`Property.SubProperty` 對應 event payload；缺欄位視為不匹配。

**驗證：**
- [ ] Unit test：Filter `{ "Tier": "1000" }` → 只有 Tier=1000 訂閱事件命中。
- [ ] Unit test：MatchCondition `Member.IsBroadcaster == true || Member.Role == "VIP"` 解析；不相依自訂 NCalc function 註冊。

**規模：** M

---

## Task 30 - Executor Expansion

每個子任務一個 executor，獨立 commit + test。

- [ ] **Task 30a - DelayActionExecutor**：`{ delayMs: 100-30000 }`；engine 內 await Task.Delay。
- [ ] **Task 30b - StopIfActionExecutor**：`{ condition: string }`；NCalc true → 拋 `WorkflowGracefulStopException`，engine 視為正常結束（不跑後續 step、不觸發 OnFailure）。
- [ ] **Task 30c - RandomPickerActionExecutor**：`{ choices: string[], weights?: int[] }`；OutputVariable 寫入 `Picked`。
- [ ] **Task 30d - UpdateCounterActionExecutor** + Counter 持久層（新 entity `Counter { Key, Value, UpdatedAt }`）：`{ key, delta }`；OutputVariable 寫入新值。
- [ ] **Task 30e - LookupTwitchUserActionExecutor**：`{ login? userId? }` → Output `{ Step.X.DisplayName, Avatar, Description, IsAffiliate }`。需 `ITwitchHelixClient`（後端 Phase 4 已建立的 token 流程）。
- [ ] **Task 30f - ShoutoutActionExecutor**：`{ targetLogin }` → 透過 Helix `chat/shoutouts`（需 broadcaster scope）。
- [ ] **Task 30g - RefundTwitchRedemptionActionExecutor**：`{ rewardId, redemptionId }` → Helix update redemption status=CANCELED。
- [ ] **Task 30h - EmitOverlayWidgetActionExecutor**：使用 strong-typed overlay action 與 DTO whitelist（例如 `OverlayTarget`, `DisplayText`, `Severity`, `DurationMs` 等明確欄位），再投影至 `OverlayEventForwarder` 等價路徑進 history + broadcast；禁止接受任意 `payload: object` 直接穿透到 SignalR。
- [ ] **Task 30i - EmitSystemEventActionExecutor**：`{ eventTypeKey, payload }` → 內部 publish 進 `IStreamEventBus`，可被其他 rule 接收（不可循環，加 depth 上限 5）。
- [ ] **Task 30j - TriggerEffectActionExecutor**：`{ effectId, durationMs? }` → 廣播至 alerts hub 的 strong-typed effect DTO（固定 schema + whitelist test），前端 overlay 解析觸發動畫 hook；音效素材由 Phase 8 補。不得以 ad hoc `payload.effect = true` 擴充既有 alert payload。
- [ ] **Task 30k - TriggerCheckInActionExecutor**：`{ userId }` → 走 `MemberStreamStateRepository.IncrementCheckInAsync`。
- [ ] **Task 30l - AddLotteryTicketsActionExecutor**：`{ userId, amount }` → 需 Counter 系統（Task 30d）+ 新 `LotteryTicket` entity；MVP 可先寫進 Counter `lottery.tickets.<userId>`，正式表延後。

**規模：** 每子任務 S；整體 XL

---

## Task 31 - WorkflowTimer Scheduler

**描述：** 加 `WorkflowTimer { Id, RuleId, IntervalSeconds, IsEnabled, NextFireAt }` entity + EF migration + `WorkflowTimerHostedService` 每 5 秒 tick 一次，到期者觸發對應 rule（以合成 `StreamEvent` 注入 `{Trigger.IsTimer: true}`）。

**驗收標準：**
- [ ] Schema + migration。
- [ ] Web API `GET/POST/PUT/DELETE /api/timers` CRUD。
- [ ] Timer 觸發後 NextFireAt = now + Interval；rule disabled 時 timer skip 不前進。
- [ ] CLI command `timer list/show/create/delete`。

**驗證：**
- [ ] Integration test：timer 30s interval，advanced 60s → 觸發 2 次。
- [ ] Idempotent：單一 Web host 重新啟動不造成重複觸發；兩個 host 同時跑屬 out-of-scope，Phase 7 不相依尚未收斂的 Desktop/NamedMutex gate。

**規模：** M

---

## Task 32 - ChatOutboxService

**描述：** `SendChatMessageAction` 改寫進 `IChatOutbox` queue（in-memory + SQLite-backed），背景 worker 按 rate limit 出貨。直接 Send fallback 模式可保留。目的：避免多 rule 並行寫聊天時觸發 Twitch rate limit。

**驗收標準：**
- [x] `IChatOutbox.EnqueueAsync(platform, channel, message, dedupKey?)`。
- [x] Background worker `ChatOutboxDispatcher`：每秒 dispatch 至多 N（依 SystemSetting `chat.outbox.per_second`）。
- [x] 缺 `IPlatformChatSender` 註冊時不得 silent no-op；outbox item 標為 `Skipped` 或 `Failed`，記錄 structured warning，並在測試中驗證可觀測。
- [x] DedupKey 24h TTL 重複抑制。

**驗證：**
- [ ] Unit test：burst 100 enqueue → 限速送出。
- [ ] Unit test：相同 dedupKey 24h 內被丟棄。

**規模：** M

---

## Task 33 - Web UI Builder Upgrade

**描述：** `RuleEditorView` 擴充支援新 schema：trigger filter / matchCondition 編輯區、OnFailureSteps tab、throttle/timeout 區段、step ExecutionCondition + OutputVariable inline 編輯、IsSubWorkflow toggle、Timer CRUD 新路由 `/timers`。

**驗收標準：**
- [ ] 新 sub-components：`TriggerEditor.vue`、`ThrottleEditor.vue`、`OnFailureEditor.vue`、`StepConditionInput.vue`。
- [ ] 1MB JSON 編輯仍可用（fallback）；表單模式為預設。
- [ ] i18n keys + 雙語覆蓋。
- [ ] Vitest 對每個新 sub-component。

**規模：** L

---

## Task 34 - Plugin Action Variable Surface

**描述：** `InvokePluginAction` 接收 `Args: Dictionary<string, string>`（template strings），執行前 resolve；plugin 端從 `IActionExecutionContext.Args` 拿解析後值。Plugin SDK 不破壞既有 contract（加 optional Args）。

**驗收標準：**
- [ ] Plugin SDK 加 `IReadOnlyDictionary<string, string> Args { get; }` 至 context。
- [ ] 既有 plugin 不傳 Args 仍 work。

**規模：** S

---

## Task 35 - Manual Verification & Parity Sign-off

**描述：** 對照 Omni-Commander 的 walkthrough，建立 12-15 個典型 rule 配置（lottery / shoutout / cooldown chat / counter increment / timer broadcast / sub-workflow chain 等）並在 Web UI + CLI 雙路徑跑完。

**驗收標準：**
- [ ] `docs/phases/phase-7-workflow-parity/manual-verification.md` 包含 OC 對照矩陣 + 每項 rule 配置 + PASS/FAIL。
- [ ] 缺 executor / 功能在 PASS 表中明確標 N/A 並 cross-ref Phase 8 backlog。

**規模：** M

---

## Checkpoint：Phase 7

- [ ] **自檢卡關**：Task 23-35 全部 sub-task `[x]`。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm test` / `pnpm build` / `pnpm lint`
- [ ] Browser manual：建立 5 個典型 rule 配置覆蓋 trigger filter / cooldown / counter / sub-workflow / timer，全部依預期執行。
- [ ] Browser manual：rule 編輯介面新欄位（throttle / onFailure / executionCondition / outputVariable）皆可操作 + 儲存 + 重載一致。
- [ ] Audit：所有新 executor 走 strong-typed `WorkflowAction` 多型，不偷 raw JSON dictionary 規避型別檢查；overlay/effect executor 另需 exact DTO whitelist + SignalR JSON contract 測試。

---

## 風險與緩解

| 風險 | 影響 | 緩解 |
|------|------|------|
| NCalc 與 strong-typed Action 共存使設計複雜 | 中 | NCalc 僅用於 condition expression；strong-typed 仍是 action 主路徑。Template resolver 只處理字串欄位。 |
| Rule schema 多次 migration 可能造成資料遷移痛點 | 中 | 所有 column 為 nullable / default 值；migration only-additive；DTO whitelist 測試守住契約。 |
| Executor 一次擴 12 個，工作量大 | 高 | Task 30 拆 12 個獨立子任務，可由不同 PR 並行；MVP 可選擇先做 30a/30b/30c/30d/30k 五個核心，其餘進 backlog。 |
| Hot reload snapshot cache 與 idempotent replay 兩個 store 互動 | 中 | snapshot cache 僅複製 rule 結構；replay store 仍以 (eventId, ruleId, actionIndex, invocationId, phase) 為 key，與 cache 解耦。 |
| WorkflowTimer 在多 host 場景重複觸發 | 中 | Phase 7 維持單一 Web host 假設；本階段只驗證單 host 重新啟動 idempotency，多實例 leader election 與 Desktop/NamedMutex gate 延後。 |
| OnFailureSteps 遞迴爆炸 | 中 | OnFailureSteps 本身不再支援 OnFailure；最大深度=1。 |
| Plugin SDK 變更可能破壞既有 plugin | 低 | Args 為新 optional 屬性，default 空 dictionary。 |

---

## 建議實作順序

1. Task 23 Expression substrate（其餘多項相依）。
2. Task 24 Step ExecutionCondition + OutputVariable。
3. Task 25 Rule throttle + timeout。
4. Task 26 OnFailureSteps。
5. Task 28 Hot reload snapshot（在執行多 rule 前隔離）。
6. Task 29 Trigger filter + MatchCondition。
7. Task 27 Sub-workflow flag。
8. Task 30a-30c（最低投入 executor：Delay / StopIf / RandomPicker）。
9. Task 30d Counter（基礎 dependency for 30l Lottery）。
10. Task 30e-30g Twitch helix executors。
11. Task 30h-30j overlay / system event / effect。
12. Task 30k-30l member / lottery。
13. Task 32 ChatOutboxService。
14. Task 31 WorkflowTimer。
15. Task 33 Web UI builder upgrade。
16. Task 34 Plugin Args。
17. Task 35 Manual verification + sign-off。

---

## Out-of-Scope（明確排除）

- OmniCommander 之 `CommandRoleLevel` 完整權限階層 — Vulperonex 已用 `UserRoleCondition`，足夠 MVP；完整 role 階層延後。
- WorkflowTimer 多實例 leader election — 單實例假設。
- 視覺化 builder（drag-and-drop graph）— JSON Textarea + 表單即可。
- Plugin sandbox 加強 — 沿用 Phase 1 plugin 隔離規範。
- NCalc 任意自定 function 註冊 API — 以後再開；Phase 7 僅允許內建 namespace/scalar helper。
