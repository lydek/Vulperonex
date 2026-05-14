# 第三階段待辦清單：Simulation Adapter + WorkflowEngine

> 詳細計畫：`docs/phases/phase-3-workflow/plan.md`
> 父待辦清單：`tasks/todo.md`

---

## 任務 9：Simulation Adapter + EventTypeRegistry

- [ ] 任務 9a：建立 `IStreamEventTypeRegistry` contract 與 first-wins registry 行為
- [ ] 任務 9b：實作 `SimulationAdapter` / `ISimulationAdapter`，可 publish 七個 MVP events
- [ ] 任務 9c：新增 SimulationAdapter isolation 與 SC-3 架構守門

## 任務 10：WorkflowEngine

- [ ] 任務 10a：建立 WorkflowRule Application contracts、write/read ports 與 action/condition DTO
- [ ] 任務 10b：實作 condition evaluator（UserRole、MessageContent、Cooldown）
- [ ] 任務 10c：實作 `SendChatMessageAction` executor、platform routing 與 SC-9
- [ ] 任務 10d：實作 WorkflowEngine 訂閱、rule matching、priority ordering 與 serial execution
- [ ] 任務 10e：補齊 timeout、retry/backoff、parallel mode 與 ErrorBehavior
- [ ] 任務 10f：實作 `InvokeSubWorkflowAction` executor 與 `InvocationId` dedup

## 任務 11：Plugin System

- [ ] 任務 11a：建立 `IVulperonexPlugin`、`IPluginContext`、`IPluginActionContext` contracts 與最小 `IPluginEventTypeRegistrar`
- [ ] 任務 11b：實作 static plugin registry 與 `InvokePluginAction` executor
- [ ] 任務 11c：完成 plugin publish custom event -> WorkflowRule -> SendChatMessage scenario（SC-10）

## 第三階段檢查點

- [ ] 全方案編譯通過，0 warnings
- [ ] 全方案測試通過
- [ ] SC-2：WorkflowEngine 執行 matching rule 通過
- [ ] SC-3：SimulationAdapter 無 Twitch references 通過
- [ ] SC-4：Domain/Application 無 Twitch symbols 持續通過
- [ ] SC-9：SendChatMessage platform routing 通過
- [ ] SC-10：Plugin 發布事件觸發 WorkflowRule 通過
- [ ] SimulationAdapter -> Bus -> WorkflowEngine -> `IPlatformChatSender` 端到端通過
- [ ] `InvokeSubWorkflowAction` 使用穩定 `InvocationId` dedup，TDQ replay 不重複執行子工作流
- [ ] `IStreamEventTypeRegistry.IsKnownForWorkflow` 排除 system events 且允許 plugin custom events
- [ ] Plugin context reflection test 確認不暴露 `IServiceProvider`
- [ ] Domain coverage gate >90% 通過
- [ ] Application coverage gate >80% 通過
- [ ] Git 狀態乾淨（忽略的本地檔案除外）
- [ ] 第四階段開始前完成第三階段審查
