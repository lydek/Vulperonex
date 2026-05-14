# 第三階段詳細計畫：Simulation Adapter + WorkflowEngine

> 父計畫：`tasks/plan.md` 第三階段
> 範圍：任務 9-11
> 目標：建立可手動與自動測試的事件模擬入口、WorkflowRule 評估與內建 Action 執行，再接上 MVP 靜態 plugin contract，讓後續 Twitch / Web / CLI 都能共用同一條事件路徑。

---

## 執行規則

- 每個切片使用一個小分支開發，驗證後立即提交，合併回 `main` 時使用 `git merge --ff-only`。
- 每個行為需求先寫 BDD-style Given / When / Then scenario，再以 TDD RED / GREEN / REFACTOR 實作。
- Phase 3 不新增 NuGet 套件；若發現必須新增套件，先依 SPEC §8.2 詢問批准。
- Application 邊界維持 light CQRS；WorkflowRule 的 write/read port 不混用。
- SimulationAdapter 是真實 adapter，不是測試捷徑；CLI/Web 之後只能呼叫 adapter/API，不繞過 `IStreamEventBus`。
- Plugin MVP 為 startup-time static registration，不做 DLL 掃描、runtime hotload 或 AssemblyLoadContext。
- Plugin context 不暴露 `IServiceProvider`；新增 plugin 可用服務時必須透過明確 interface 屬性。
- `--no-build` 只可緊接在同一任務中成功編譯後使用。
- 保持 `.claude/`、DB 檔、測試輸出與其他本地檔案不進入提交。

---

## 依賴順序

```
Task 9a Event type registry contract
    -> Task 9b SimulationAdapter publish all MVP events
    -> Task 9c adapter isolation / SC-3 guard

Task 10a WorkflowRule application contracts and DTOs
    -> Task 10b condition evaluator
    -> Task 10c SendChatMessage action and platform routing
    -> Task 10d WorkflowEngine subscription / priority / serial execution
    -> Task 10e timeout, retry, parallel mode, and error behavior

Task 11a plugin contracts
    -> Task 11b plugin static registry / action executor
    -> Task 11c plugin publish custom event scenario
```

Task 10 depends on Task 9 because workflow integration tests should use SimulationAdapter for acceptance paths. Task 11 depends on Task 10 because plugin action execution is a WorkflowEngine action.

---

## Task 9a：EventTypeRegistry contract

**描述：** 在 Application 層建立 `IStreamEventTypeRegistry` 與 workflow-visible metadata contract，供 adapters 在 `StartAsync` 時註冊 canonical event keys，並讓 Task 14a 的 `/api/event-types` 可重用同一 contract。

**驗收準則：**
- [ ] `IStreamEventTypeRegistry` 位於 `Vulperonex.Application`。
- [ ] registry metadata 至少包含 `Key`、`Description`、`IsSystemEvent`。
- [ ] `Register(...)` 對同一 key idempotent；first-wins。
- [ ] metadata 衝突時保留先到者，不拋例外。
- [ ] `GetAll()` 排除 `IsSystemEvent=true` key，或提供明確 API 讓呼叫端排除 system events。

**驗證：**
- [ ] Unit test：重複註冊同一 key 只保留一筆。
- [ ] Unit test：metadata 衝突時 first-wins。
- [ ] Unit test：`platform.connection_changed` 可標為 system event 並被 workflow-visible query 排除。

**依賴：** Task 4

**可能涉及的檔案：**
- `src/Vulperonex.Application/EventTypes/IStreamEventTypeRegistry.cs`
- `src/Vulperonex.Application/EventTypes/StreamEventTypeMetadata.cs`
- `src/Vulperonex.Infrastructure/EventTypes/InMemoryStreamEventTypeRegistry.cs`
- `tests/Vulperonex.Tests.Unit/Application/EventTypes/`

**預估規模：** S

---

## Task 9b：SimulationAdapter publish MVP events

**描述：** 實作 `SimulationAdapter` 與 `ISimulationAdapter`，讓測試與後續 Web/CLI 可以透過同一 adapter 發布七個 MVP domain events。REST/CLI 公開 alias 留到 Task 14b；本 task 提供內部 API 與測試入口。

**驗收準則：**
- [ ] `ISimulationAdapter` contract 不引用 Twitch 或 Web/CLI 型別。
- [ ] `SimulationAdapter` 可 publish 七個 MVP events：message、followed、donated、subscribed、gifted subscription、raided、reward redeemed。
- [ ] `StartAsync` 註冊所有 Simulation-supported event keys 到 `IStreamEventTypeRegistry`。
- [ ] publish 路徑只透過 `IStreamEventBus.PublishAsync`。
- [ ] unsupported simulation request 回傳明確失敗結果或拋 domain-neutral exception，不使用 human-readable API error code（API mapping 留到 Task 14b）。

**驗證：**
- [ ] Unit/integration test：每個 simulation request publish 後，`Subscribe<IStreamEvent>` 可收到對應 concrete event。
- [ ] Unit test：message simulation 保留 `StreamUser` 與 message text。
- [ ] Unit test：sub simulation 保留 tier。
- [ ] Unit test：StartAsync 註冊所有 seven MVP keys，且不註冊 `platform.connection_changed` 為 workflow-visible event。

**依賴：** Task 9a

**可能涉及的檔案：**
- `src/Adapters/Vulperonex.Adapters.Simulation/ISimulationAdapter.cs`
- `src/Adapters/Vulperonex.Adapters.Simulation/SimulationAdapter.cs`
- `src/Adapters/Vulperonex.Adapters.Simulation/SimulationRequest.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Simulation/`
- `tests/Vulperonex.Tests.Integration/Adapters/Simulation/`

**預估規模：** M

---

## Task 9c：Simulation isolation and SC-3 guard

**描述：** 補齊架構測試，確保 SimulationAdapter 不引用 Twitch adapter，且 Domain/Application 仍無平台洩漏。

**驗收準則：**
- [ ] `Vulperonex.Adapters.Simulation` 不引用 `Vulperonex.Adapters.Twitch`。
- [ ] Simulation 專案中沒有 `Twitch*` 型別依賴。
- [ ] SC-3 與既有 SC-4 架構測試通過。

**驗證：**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false`

**依賴：** Task 9b

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Architecture/Adapters/SimulationAdapterIsolationTests.cs`
- `tests/Vulperonex.Tests.Architecture/Domain/PlatformLeakageTests.cs`

**預估規模：** S

---

## Task 10a：WorkflowRule application contracts

**描述：** 在 Application 層建立 WorkflowRule contracts、repositories、read DTOs 與 action/condition models，先定義可測試的 workflow contract，不接 Web API。

**驗收準則：**
- [ ] `WorkflowRule`/DTO contract 位於 Application，不暴露 EF entity。
- [ ] write repository port 與 query service port 分離。
- [ ] Conditions/Actions 使用 explicit type discriminator，不使用任意 dynamic object。
- [ ] Action config 包含 `TimeoutMs`、`MaxRetries`、`BackoffMs`、`ErrorBehavior` 的 MVP 欄位，但驗證端點留到 Task 14a。
- [ ] `SendChatMessageAction` 與 `InvokeSubWorkflowAction` 為內建 action model。

**驗證：**
- [ ] Unit/contract test：Application workflow contracts 不引用 Infrastructure/EF。
- [ ] Unit test：action/condition DTO 可用 `System.Text.Json` round-trip。
- [ ] Application coverage gate 維持 >80%。

**依賴：** Task 5

**可能涉及的檔案：**
- `src/Vulperonex.Application/Workflows/WorkflowRule.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowRuleRepository.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowRuleQueryService.cs`
- `src/Vulperonex.Application/Workflows/Conditions/`
- `src/Vulperonex.Application/Workflows/Actions/`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/`

**預估規模：** M

---

## Task 10b：Condition evaluator

**描述：** 實作 WorkflowEngine 的 condition evaluation 純邏輯，先支援 UserRole、MessageContent、Cooldown 的 MVP evaluator。此 task 不執行 actions。

**驗收準則：**
- [ ] `MessageContentCondition` 支援 contains / exact / regex MVP semantics。
- [ ] regex pattern 長度與合法性檢查以 Application validator 表達；API error mapping 留到 Task 14a。
- [ ] `CooldownCondition` 使用 `IClock`，不直接讀 `DateTime.UtcNow`。
- [ ] unknown condition type 回傳 fail/validation result，不 crash。

**驗證：**
- [ ] Unit test：message contains/exact/regex matching。
- [ ] Unit test：invalid regex 被辨識。
- [ ] Unit test：cooldown 在 fake clock 下阻擋與放行。
- [ ] Unit test：unknown condition type 不觸發 rule。

**依賴：** Task 10a

**可能涉及的檔案：**
- `src/Vulperonex.Application/Workflows/Conditions/WorkflowConditionEvaluator.cs`
- `src/Vulperonex.Application/Workflows/Conditions/`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/Conditions/`

**預估規模：** M

---

## Task 10c：SendChatMessage action and platform routing

**描述：** 實作內建 `SendChatMessageAction` executor 與 `IPlatformChatSender` 選擇邏輯，完成 SC-9。

**驗收準則：**
- [ ] `IPlatformChatSender` contract 位於 Application 或 adapter abstraction 中符合 SPEC dependency direction 的位置。
- [ ] 未設定 `TargetPlatform` 時使用來源 event platform。
- [ ] 設定 `TargetPlatform` 時覆寫來源 platform。
- [ ] 找不到 sender 時記錄 warning 並跳過，不 crash。
- [ ] template rendering 未知 placeholder 保留原文；null/empty placeholder 替換為空字串。

**驗證：**
- [ ] Unit test：`SendChatMessageAction_DefaultsToSourcePlatform`。
- [ ] Unit test：`SendChatMessageAction_RespectsTargetPlatformOverride`。
- [ ] Unit test：missing sender skip。
- [ ] Unit test：template rendering unknown placeholder/null placeholder。

**依賴：** Task 10a

**可能涉及的檔案：**
- `src/Vulperonex.Application/Workflows/Actions/SendChatMessageActionExecutor.cs`
- `src/Vulperonex.Application/Workflows/Actions/TemplateRenderer.cs`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/Actions/`

**預估規模：** M

---

## Task 10d：WorkflowEngine subscription and serial execution

**描述：** 實作 `WorkflowEngine` hosted service，透過 `Subscribe<IStreamEvent>` 接收事件，載入 enabled rules，依 `Priority ASC, CreatedAt ASC, Id ASC` 排序，先完成 serial action execution。

**驗收準則：**
- [ ] WorkflowEngine 啟動時訂閱 `IStreamEvent`，停止時 dispose subscription。
- [ ] disabled rules 不執行。
- [ ] EventTypeKey 不匹配的 rules 不執行。
- [ ] rules 依 `Priority ASC, CreatedAt ASC, Id ASC` 執行。
- [ ] serial mode 依 action index 順序執行。
- [ ] action execution 使用 Task 6 的 `ActionExecutionLog` dedup key shape `(EventId, WorkflowRuleId, ActionIndex)`。

**驗證：**
- [ ] Integration test：publish `UserSentMessageEvent` -> matching rule -> mock sender 收到一次（SC-2 起點）。
- [ ] Unit/integration test：disabled rule skip。
- [ ] Unit/integration test：priority ordering。
- [ ] Unit/integration test：同一 event/rule/action 重播被 dedup skip。

**依賴：** Task 9b, Task 10b, Task 10c

**可能涉及的檔案：**
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowActionExecutor.cs`
- `tests/Vulperonex.Tests.Integration/Workflows/WorkflowEngineTests.cs`

**預估規模：** M

---

## Task 10e：WorkflowEngine timeout, retry, parallel mode, and error behavior

**描述：** 補齊 Workflow action 執行策略：timeout、retry/backoff、Serial/Parallel concurrency、ErrorBehavior。此 task 完成 Task 10 的完整 MVP behavior。

**驗收準則：**
- [ ] `TimeoutMs` 透過 cancellation token 傳入 executor；timeout 後依 `ErrorBehavior` 處理。
- [ ] `RetryOnError` 使用 `MaxRetries` 與 `BackoffMs`。
- [ ] `ContinueOnError` 記錄錯誤後執行後續 action。
- [ ] `StopRuleOnError` 停止目前 rule 的後續 actions。
- [ ] Parallel mode 尊重 `MaxParallelism`。
- [ ] handler exception 不透過 `WaitForIdleAsync` 外拋，仍符合 EventBus contract。

**驗證：**
- [ ] Unit/integration test：timeout cancels action。
- [ ] Unit/integration test：retry count 與 backoff 使用 fake clock 或 deterministic scheduler。
- [ ] Unit/integration test：ContinueOnError / StopRuleOnError 差異。
- [ ] Unit/integration test：parallel mode 不超過 MaxParallelism。
- [ ] `dotnet test` 全綠。

**依賴：** Task 10d

**可能涉及的檔案：**
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- `src/Vulperonex.Application/Workflows/WorkflowExecutionOptions.cs`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/`
- `tests/Vulperonex.Tests.Integration/Workflows/`

**預估規模：** M

---

## Task 11a：Plugin contracts

**描述：** 在 `Vulperonex.Plugins.Abstractions` 實作 MVP plugin contracts：`IVulperonexPlugin`、`IPluginContext`、`IPluginActionContext`。

**驗收準則：**
- [ ] `IVulperonexPlugin.Name` 為 lowercase-kebab plugin id。
- [ ] `IPluginContext` 只暴露 `IStreamEventBus` 與 logger 等明確服務，不暴露 `IServiceProvider`。
- [ ] `IPluginActionContext` 包含 `ActionExecutionKey`、event/rule/action metadata、`Params: IReadOnlyDictionary<string, JsonElement>`。
- [ ] contracts 不引用 Infrastructure、Hosts 或 adapter implementations。

**驗證：**
- [ ] Architecture test：`Plugins.Abstractions` 只依賴 Domain + Application。
- [ ] Reflection test：plugin context interfaces property types 不含 `IServiceProvider`。
- [ ] Unit test：JsonElement params 可用 `.GetString()` / `.GetInt32()` / `.GetBoolean()` 讀取。

**依賴：** Task 10a

**可能涉及的檔案：**
- `src/Vulperonex.Plugins.Abstractions/IVulperonexPlugin.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginContext.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginActionContext.cs`
- `tests/Vulperonex.Tests.Unit/Plugins/`
- `tests/Vulperonex.Tests.Architecture/Plugins/`

**預估規模：** S

---

## Task 11b：Static plugin registry and InvokePluginAction executor

**描述：** 實作 MVP 靜態 plugin registry 與 `InvokePluginAction` executor，讓 WorkflowEngine 可透過 plugin id/action id 呼叫已註冊 plugin。

**驗收準則：**
- [ ] plugin registry 由 DI startup-time static registration 建立。
- [ ] `InvokePluginAction.PluginId` 必須等於 `IVulperonexPlugin.Name`。
- [ ] plugin 缺失時 warning + skip，不 crash。
- [ ] `IPluginActionContext.ActionExecutionKey` 使用完整 dedup key。
- [ ] executor 把 JsonElement params 原樣傳給 plugin context。

**驗證：**
- [ ] Unit/integration test：registered plugin action 被呼叫。
- [ ] Unit/integration test：missing plugin skip。
- [ ] Unit test：ActionExecutionKey 包含 event/rule/action index。
- [ ] Unit test：JsonElement params 型別讀取不發生 `InvalidCastException`。

**依賴：** Task 11a, Task 10d

**可能涉及的檔案：**
- `src/Vulperonex.Application/Workflows/Actions/InvokePluginActionExecutor.cs`
- `src/Vulperonex.Application/Plugins/IPluginRegistry.cs`
- `src/Vulperonex.Infrastructure/Plugins/StaticPluginRegistry.cs`
- `tests/Vulperonex.Tests.Unit/Plugins/`
- `tests/Vulperonex.Tests.Integration/Plugins/`

**預估規模：** M

---

## Task 11c：Plugin publishes custom event scenario

**描述：** 完成 SC-10：plugin 透過 `IPluginContext.Events.PublishAsync(customEvent)` 發布事件，WorkflowEngine 以 matching rule 觸發後續 action。

**驗收準則：**
- [ ] plugin 可在 `InitializeAsync` 或 action execution 中 publish custom `IStreamEvent`。
- [ ] custom event 可透過 EventTypeKey match WorkflowRule。
- [ ] matching custom event rule 可觸發 `SendChatMessageAction`。
- [ ] plugin publish 路徑仍通過 `IStreamEventBus`，不直接呼叫 WorkflowEngine。

**驗證：**
- [ ] Integration test：`Plugin_CanPublishCustomEvent_TriggeringWorkflow` 通過。
- [ ] `dotnet test` → SC-10 通過。
- [ ] Application coverage gate 維持 >80%。

**依賴：** Task 11b, Task 10e

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Integration/Plugins/PluginWorkflowTests.cs`
- `tests/Vulperonex.Tests.Unit/Plugins/`

**預估規模：** S

---

## 第三階段檢查點

**驗收準則：**
- [ ] 任務 9a-11c 已完成並以小切片形式提交。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 通過，0 warnings。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [ ] SC-2, SC-3, SC-4, SC-9, SC-10 通過。
- [ ] SimulationAdapter -> Bus -> WorkflowEngine -> `IPlatformChatSender` 端到端通過。
- [ ] Plugin 可發布事件並觸發 rule。
- [ ] Domain coverage gate >90% 通過。
- [ ] Application coverage gate >80% 通過。
- [ ] 架構測試確認 Domain/Application/Plugins.Abstractions 無 Infrastructure/EF/service locator 洩漏。
- [ ] `git status --short --ignored` 僅顯示預期忽略的本地檔案。

**審查門檻：**
- [ ] 開始 Phase 4 前人工 review WorkflowEngine 的 retry/timeout/dedup 語意、plugin context surface、Simulation/Twitch 等效性準備狀態。

---

## 風險與緩解

| 風險 | 影響 | 緩解措施 |
|------|------|----------|
| WorkflowEngine 範圍過大 | 高 | 拆成 contracts、conditions、SendChatMessage、serial engine、advanced execution 五個切片。 |
| Action timeout/retry 測試 flaky | 中 | 使用 fake clock、deterministic synchronization，不依賴固定 sleep。 |
| Plugin context 變成 service locator | 高 | contract 先行，reflection architecture test 阻擋 `IServiceProvider`。 |
| SimulationAdapter 成為測試捷徑 | 中 | 所有 acceptance path 都要求 publish through `IStreamEventBus`。 |
| WorkflowRule JSON shape 過早綁死 Web API | 中 | Phase 3 只定義 Application contract；API validation/error code mapping 留到 Task 14a。 |

## 開放問題

- `WorkflowRule` 的 EF repository 實作是否應在 Task 10a 同步完成，或先用 in-memory fake 支撐 WorkflowEngine，再於 Task 14a 接 EF CRUD。建議：Phase 3 使用 Application port + in-memory fake 驗 workflow 行為；EF CRUD 留在 Task 14a，避免 Phase 3 膨脹。
