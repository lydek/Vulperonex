# 第三階段待辦清單：Simulation Adapter + WorkflowEngine

> 詳細計畫：`docs/phases/phase-3-workflow/plan.md`
> 父待辦清單：`tasks/todo.md`

---

## 任務 9：Simulation Adapter + EventTypeRegistry

- [x] 任務 9a：建立 `IStreamEventTypeRegistry` contract 與 first-wins registry 行為
- [x] 任務 9b：實作 `IStreamEventSource`、`SimulationAdapter` / `ISimulationAdapter`，可 publish 七個 MVP events
- [x] 任務 9c：新增 SimulationAdapter isolation 與 SC-3 架構守門

## 任務 10：WorkflowEngine

- [x] 任務 10a：建立 WorkflowRule Application contracts、write/read ports 與 action/condition DTO
- [x] 任務 10b：實作 condition evaluator（UserRole flags、MessageContent PrefixMatch/ContainsMatch/FullRegex、Cooldown）
- [x] 任務 10c：實作 `SendChatMessageAction` executor、platform routing 與 SC-9
- [x] 任務 10d：實作 WorkflowEngine 訂閱、rule matching、priority ordering 與 serial execution
- [x] 任務 10e：補齊 timeout、retry/backoff、parallel mode 與 `ContinueOnError` / `StopOnError` / `RetryOnError`
- [x] 任務 10f：實作 `InvokeSubWorkflowAction` executor 與 `InvocationId` dedup

## 任務 11：Plugin System

- [x] 任務 11a：建立 `IVulperonexPlugin`、`IPluginContext`、`IPluginActionContext` contracts 與最小 `IPluginEventTypeRegistrar`
- [x] 任務 11b：實作 static plugin registry 與 `InvokePluginAction` executor
- [x] 任務 11c：完成 plugin publish custom event -> WorkflowRule -> SendChatMessage scenario（SC-10）

## 第三階段檢查點

- [x] 全方案編譯通過，0 warnings
- [x] 全方案測試通過
- [x] SC-2：WorkflowEngine 執行 matching rule 通過
- [x] SC-3：SimulationAdapter 無 Twitch references 通過
- [x] SC-4：Domain/Application 無 Twitch symbols 持續通過
- [x] SC-9：SendChatMessage platform routing 通過
- [x] SC-10：Plugin 發布事件觸發 WorkflowRule 通過
- [x] SimulationAdapter -> Bus -> WorkflowEngine -> `IPlatformChatSender` 端到端通過
- [x] `InvokeSubWorkflowAction` 使用穩定 `InvocationId` dedup，TDQ replay 不重複執行子工作流
- [x] `IStreamEventTypeRegistry.IsKnownForWorkflow` 排除 system events 且允許 plugin custom events
- [x] Plugin context reflection test 確認不暴露 `IServiceProvider`
- [x] Domain coverage gate >90% 通過
- [x] Application coverage gate >80% 通過
- [x] Git 狀態乾淨（忽略的本地檔案除外）
- [x] 第四階段開始前完成第三階段審查
