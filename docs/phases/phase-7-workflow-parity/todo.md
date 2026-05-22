# Phase 7 待辦清單：Workflow Parity with Omni-Commander

> 詳細計畫：`docs/phases/phase-7-workflow-parity/plan.md`
> 對照來源：`ref/Omni-Commander/OmniCommander.Domain/Workflows/` + `ref/Omni-Commander/OmniCommander.Application/Workflows/`
> 父待辦清單：`tasks/todo.md`
> **實作順序**：23 → 24 → 25 → 26 → 28 → 29 → 27 → 30a-l → 32 → 31 → 33 → 34 → 35

---

## Task 23 - Variable / Expression Substrate

- [ ] Task 23a：加 NuGet `NCalcSync`（ask-first）；中央 `Directory.Packages.props` 釘版本。
- [ ] Task 23b：宣告 `Vulperonex.Application/Expressions/`：`ITemplateResolver` / `IExpressionEvaluator` / `ExpressionContext`（含 Trigger/Steps/Args/Member namespace）。
- [ ] Task 23c：實作 `Infrastructure/Expressions/TemplateResolver.cs`（regex 解析 `{Trigger.X}` placeholder，ZeroCopy fast-path）。
- [ ] Task 23d：實作 `Infrastructure/Expressions/NCalcExpressionEvaluator.cs`（包 NCalc，注入 ExpressionContext 為 functions）。
- [ ] Task 23e：DI 註冊 + SystemSettings key `workflow.template.strict_missing` (default false)。
- [ ] Task 23f：Unit test：placeholder 正向 / 缺失 fail-soft / strict-mode 拋例外 / NCalc bool / NCalc with member context。

## Task 24 - Step ExecutionCondition + OutputVariable

- [ ] Task 24a：`WorkflowAction` 基底加 `ExecutionCondition: string?` + `OutputVariable: string?`。
- [ ] Task 24b：`IWorkflowActionExecutor.ExecuteAsync` 回傳改 `Task<ActionExecutionResult>`（含 `OutputValues: IReadOnlyDictionary<string, object?>?` + `IsSkipped: bool`）。
- [ ] Task 24c：Engine 評估 ExecutionCondition → false → 標 Skipped。
- [ ] Task 24d：Engine 把 OutputValues 寫入 ExpressionContext.Steps[OutputVariable]。
- [ ] Task 24e：`ActionExecutionStatus` 加 `Skipped`；replay 視為 terminal。
- [ ] Task 24f：Unit test：ExecutionCondition false skip；step OutputVariable 鏈式解析。

## Task 25 - Rule-level Throttle + Timeout

- [ ] Task 25a：`WorkflowThrottlePolicy` value record (`MaxConcurrent`, `CooldownSeconds`, `PerUserCooldown`, `PerUserCooldownSeconds`)。
- [ ] Task 25b：`IWorkflowThrottleService` 介面 + in-memory impl（global last-fire + per-user TTL map）。
- [ ] Task 25c：EF migration `WorkflowRules` 加 `ThrottleJson: string`、`TimeoutSeconds: int default 30`。Backward compat：缺欄位用 default。
- [ ] Task 25d：Engine ExecuteRuleAsync 包 linked CTS by `TimeoutSeconds`；timeout → 標 Abandoned。
- [ ] Task 25e：Engine 在執行 rule 前呼叫 throttle service；reject 時 graceful skip。
- [ ] Task 25f：Unit test：global cooldown / per-user cooldown / rule timeout < action timeout。

## Task 26 - OnFailureSteps

- [ ] Task 26a：`WorkflowRule` 加 `OnFailureSteps: IReadOnlyList<WorkflowAction>?`。
- [ ] Task 26b：EF schema 儲入既有 StepsJson 同 row 或新欄位 `OnFailureStepsJson`；ask-first 決定。
- [ ] Task 26c：Engine 主鏈失敗 / timeout → 注入 `{Failure.StepIndex}` + `{Failure.ErrorMessage}` → 跑 OnFailureSteps；OnFailureSteps 內不再支援 OnFailure。
- [ ] Task 26d：`ActionExecutionKey` 加 `phase: Main|OnFailure` 區分 replay。
- [ ] Task 26e：Unit test：main fail → onFailure 跑 + failure context 解析；onFailure 自己失敗 → 不二次補救。

## Task 27 - Sub-process Flag + InvokeRule Polish

- [ ] Task 27a：EF migration 加 `IsSubWorkflow: bool default false`。
- [ ] Task 27b：Engine HandleEventAsync 過濾 `IsSubWorkflow=true`。
- [ ] Task 27c：`InvokeSubWorkflowAction` 加 `Args: Dictionary<string, string>` template；engine resolve 後注入 child ExpressionContext.Args。
- [ ] Task 27d：Unit test：sub-workflow 不被事件觸發；parent step Output → child Args。

## Task 28 - Hot Reload Snapshot Cache

- [ ] Task 28a：`IRuleSnapshotCache` 介面 (`GetByEventTypeAsync`, `GetByIdAsync`)；deep copy WorkflowRule + sub-collections。
- [ ] Task 28b：Cache invalidation 事件源：WorkflowRuleRepository CRUD 後 publish 進 `IStreamEventBus` 內部頻道 OR 直接觸發 cache.Invalidate(ruleId)。
- [ ] Task 28c：Engine 換 cache 取代直接 query；執行中 rule 用 snapshot 跑完。
- [ ] Task 28d：Unit test：rule update inflight execution 不受影響；cache size 受控。

## Task 29 - Trigger Filter + MatchCondition

- [ ] Task 29a：`WorkflowTrigger` value record (`EventTypeKey`, `Filter: Dictionary<string,string>`, `MatchCondition: string?`)。
- [ ] Task 29b：`WorkflowRule.EventTypeKey` 保留為頂層字串（SQL 過濾）；trigger 詳細存 `TriggerJson`。
- [ ] Task 29c：EF migration 加 `TriggerJson: string?`、`MatchCondition: string?`。
- [ ] Task 29d：Engine 先 SQL 過濾 EventTypeKey → 再 in-memory Filter equality → 再 NCalc MatchCondition。
- [ ] Task 29e：Unit test：Filter equality (case-insensitive)；MatchCondition NCalc 解析事件屬性。

## Task 30 - Executor Expansion

- [ ] Task 30a：`DelayActionExecutor` (`delayMs: 100-30000`)；timeoutMs 默認比 delay 大。
- [ ] Task 30b：`StopIfActionExecutor` (`condition: NCalc`)；true 時拋 `WorkflowGracefulStopException`；engine 視為正常結束。
- [ ] Task 30c：`RandomPickerActionExecutor` (`choices: string[], weights?: int[]`)；OutputVariable 寫 Picked。
- [ ] Task 30d：新 `Counter` entity + EF migration + repository；`UpdateCounterActionExecutor` (`key, delta`) → OutputVariable 寫新值。
- [ ] Task 30e：`LookupTwitchUserActionExecutor` (`login? userId?`) → output `DisplayName/Avatar/Description/IsAffiliate`；需 `ITwitchHelixClient`（既有 token 流程）。
- [ ] Task 30f：`ShoutoutActionExecutor` (`targetLogin`) → Helix chat/shoutouts。
- [ ] Task 30g：`RefundTwitchRedemptionActionExecutor` (`rewardId, redemptionId`)。
- [ ] Task 30h：`EmitOverlayWidgetActionExecutor` (`hub, payload`) → 走 OverlayEventForwarder 等價路徑。
- [ ] Task 30i：`EmitSystemEventActionExecutor` (`eventTypeKey, payload`) → publish 進 IStreamEventBus；engine 加 depth cap 5 防循環。
- [ ] Task 30j：`TriggerEffectActionExecutor` (`effectId, durationMs?`) → 廣播 alerts hub with `effect: true` flag。
- [ ] Task 30k：`TriggerCheckInActionExecutor` (`userId`) → MemberStreamStateRepository.IncrementCheckInAsync。
- [ ] Task 30l：`AddLotteryTicketsActionExecutor` (`userId, amount`) → 走 Counter `lottery.tickets.<userId>`（正式 LotteryTicket 表延後 Phase 8）。

## Task 31 - WorkflowTimer Scheduler

- [ ] Task 31a：`WorkflowTimer` entity (`Id, RuleId, IntervalSeconds, IsEnabled, NextFireAt`) + EF migration。
- [ ] Task 31b：`WorkflowTimerHostedService` 每 5 秒 tick；到期者觸發對應 rule（合成 StreamEvent）。
- [ ] Task 31c：Web API `GET/POST/PUT/DELETE /api/timers` + DTO。
- [ ] Task 31d：CLI `timer list/show/create/delete`。
- [ ] Task 31e：Integration test：30s interval × 60s 模擬 → 觸發 2 次；disabled 不前進 NextFireAt。

## Task 32 - ChatOutboxService

- [ ] Task 32a：`IChatOutbox.EnqueueAsync(platform, channel, message, dedupKey?)`。
- [ ] Task 32b：`ChatOutboxDispatcher` background service 按 `chat.outbox.per_second`（default 5）出貨。
- [ ] Task 32c：DedupKey 24h TTL 表（in-memory + SystemSettings persist）。
- [ ] Task 32d：`SendChatMessageActionExecutor` 改寫進 outbox（非阻塞）。
- [ ] Task 32e：Unit test：burst 100 限速；同 dedupKey 24h 內丟棄。

## Task 33 - Web UI Builder Upgrade

- [ ] Task 33a：`TriggerEditor.vue`（EventTypeKey dropdown + Filter key/value 表格 + MatchCondition input）。
- [ ] Task 33b：`ThrottleEditor.vue`（MaxConcurrent / Cooldown / PerUserCooldown）。
- [ ] Task 33c：`OnFailureEditor.vue`（OnFailureSteps 編輯 tab，沿用 RuleJsonEditor 或 simple form）。
- [ ] Task 33d：`StepConditionInput.vue`（per-step ExecutionCondition + OutputVariable inline 編輯）。
- [ ] Task 33e：`IsSubWorkflow` toggle + 隱藏 trigger 區（sub-workflow 不需 trigger）。
- [ ] Task 33f：新路由 `/timers` 含 list/show/create/edit/delete。
- [ ] Task 33g：i18n keys 雙語覆蓋。
- [ ] Task 33h：Vitest per 新 component。

## Task 34 - Plugin Action Variable Surface

- [ ] Task 34a：Plugin SDK `IActionExecutionContext` 加 `Args: IReadOnlyDictionary<string, string>`（default 空）。
- [ ] Task 34b：`InvokePluginAction` 加 `Args: Dictionary<string,string>` template；engine 於 invoke 前 resolve。
- [ ] Task 34c：既有 plugin 不傳 Args 仍 work（regression test）。

## Task 35 - Manual Verification & Parity Sign-off

- [ ] Task 35a：建立 12-15 個典型 rule 配置 JSON 放 `docs/phases/phase-7-workflow-parity/samples/`。
- [ ] Task 35b：Web UI + CLI 雙路徑跑完，PASS/FAIL 寫入 `manual-verification.md`。
- [ ] Task 35c：對照 OC walkthrough.md 標出 N/A 項目 + cross-ref Phase 8 backlog。

## Phase 7 Checkpoint

- [ ] **自檢卡關**：Task 23-35 全部 sub-task `[x]`。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：5 個典型 rule 配置（trigger filter / cooldown / counter / sub-workflow / timer）全綠。
- [ ] Browser manual：rule 編輯介面新欄位（throttle/onFailure/executionCondition/outputVariable）可操作 + 儲存 + 重載一致。
- [ ] DTO whitelist 測試：rule schema 新欄位全部入 whitelist；無 raw JSON 漏網。
- [ ] Audit：所有新 executor 走 strong-typed `WorkflowAction` 多型，未走 raw JSON dictionary 規避。
