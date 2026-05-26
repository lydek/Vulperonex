# Phase 3 Todo List: Simulation Adapter + WorkflowEngine

> Detailed Plan: `docs/phases/phase-3-workflow/plan.md`
> Parent Todo List: `tasks/todo.md`

---

## Task 9: Simulation Adapter + EventTypeRegistry

- [x] Task 9a: Establish `IStreamEventTypeRegistry` contract and first-wins registry behavior
- [x] Task 9b: Implement `IStreamEventSource`, `SimulationAdapter` / `ISimulationAdapter`, publishing seven MVP events
- [x] Task 9c: Add SimulationAdapter isolation and SC-3 architectural guardrails

## Task 10: WorkflowEngine

- [x] Task 10a: Establish WorkflowRule Application contracts, write/read ports, and action/condition DTOs
- [x] Task 10b: Implement condition evaluator (UserRole flags, MessageContent PrefixMatch/ContainsMatch/FullRegex, Cooldown)
- [x] Task 10c: Implement `SendChatMessageAction` executor, platform routing, and SC-9
- [x] Task 10d: Implement WorkflowEngine subscription, rule matching, priority ordering, and serial execution
- [x] Task 10e: Complete timeout, retry/backoff, parallel mode, and `ContinueOnError` / `StopOnError` / `RetryOnError` behaviors
- [x] Task 10f: Implement `InvokeSubWorkflowAction` executor and `InvocationId` deduplication

## Task 11: Plugin System

- [x] Task 11a: Establish `IVulperonexPlugin`, `IPluginContext`, `IPluginActionContext` contracts, and minimal `IPluginEventTypeRegistrar`
- [x] Task 11b: Implement static plugin registry and `InvokePluginAction` executor
- [x] Task 11c: Complete plugin publish custom event -> WorkflowRule -> SendChatMessage scenario (SC-10)

## Phase 3 Checkpoint

- [x] Full solution compilation passes with 0 warnings
- [x] Full solution tests pass
- [x] SC-2: WorkflowEngine executing matching rule passes
- [x] SC-3: SimulationAdapter has no Twitch references passes
- [x] SC-4: Domain/Application has no Twitch symbols continues to pass
- [x] SC-9: SendChatMessage platform routing passes
- [x] SC-10: Plugin publishing event triggering WorkflowRule passes
- [x] SimulationAdapter -> Bus -> WorkflowEngine -> `IPlatformChatSender` end-to-end passes
- [x] `InvokeSubWorkflowAction` uses stable `InvocationId` for deduplication; TDQ replay does not re-execute sub-workflows
- [x] `IStreamEventTypeRegistry.IsKnownForWorkflow` excludes system events and permits plugin custom events
- [x] Plugin context reflection test confirms it does not expose `IServiceProvider`
- [x] Domain coverage gate >90% passes
- [x] Application coverage gate >80% passes
- [x] Git status clean (excluding ignored local files)
- [x] Complete Phase 3 review before beginning Phase 4
