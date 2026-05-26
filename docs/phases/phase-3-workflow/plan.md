# Phase 3 Detailed Plan: Simulation Adapter + WorkflowEngine

> Parent Plan: `tasks/plan.md` Phase 3
> Scope: Tasks 9-11 only
> Goal: Establish a manually and automatically testable event simulation entry point, WorkflowRule evaluation, and built-in Action execution, and wire them up to the MVP static plugin contract. This ensures that Twitch, Web, and CLI hosts all share the same event pipeline in subsequent phases.

---

## Execution Rules

- Develop each slice on a small branch. Commit immediately after verification. Use `git merge --ff-only` when merging back to `main`.
- For each behavioral requirement, write BDD-style Given / When / Then scenarios first, then implement using TDD RED / GREEN / REFACTOR.
- Do not add new NuGet packages in Phase 3. If package addition becomes necessary, inquire and obtain approval per SPEC §8.2.
- The Application boundary must adhere to light CQRS; do not mix WorkflowRule write/read ports.
- SimulationAdapter is a real adapter, not a testing shortcut. The CLI and Web hosts must invoke the adapter/API and not bypass `IStreamEventBus` in subsequent phases.
- The Plugin MVP uses startup-time static registration. Do not perform DLL scanning, runtime hotloading, or utilize AssemblyLoadContext.
- The Plugin context must not expose `IServiceProvider`. Adding plugin-available services must go through explicit interface properties.
- The `--no-build` flag is strictly reserved for commands that immediately follow a successful compilation within the same task.
- Keep `.claude/`, DB files, test outputs, and other local files out of commits.

---

## Dependency Order

```
Task 9a Event type registry contract
    -> Task 9b SimulationAdapter publish all MVP events
    -> Task 9c adapter isolation / SC-3 guard

Task 10a WorkflowRule application contracts and DTOs
    -> Task 10b condition evaluator
    -> Task 10c SendChatMessage action and platform routing
    -> Task 10d WorkflowEngine subscription / priority / serial execution
    -> Task 10e timeout, retry, parallel mode, and error behavior
    -> Task 10f InvokeSubWorkflow action and InvocationId dedup

Task 11a plugin contracts
    -> Task 11b plugin static registry / action executor
    -> Task 11c plugin publish custom event scenario
```

Task 10 depends on Task 9 because workflow integration tests should use SimulationAdapter for acceptance paths. Task 11 depends on Task 10 because plugin action execution is a WorkflowEngine action.

---

## Task 9a: EventTypeRegistry Contract

**Description:** Establish `IStreamEventTypeRegistry` and workflow-visible metadata contracts in the Application layer, allowing adapters to register canonical event keys during `StartAsync` and enabling the `/api/event-types` endpoint in Task 14a to reuse the same contract.

**Acceptance Criteria:**
- [ ] `IStreamEventTypeRegistry` is located in `Vulperonex.Application`.
- [ ] Registry metadata contains at least `Key`, `Description`, and `IsSystemEvent`.
- [ ] `Register(...)` is idempotent for the same key; first-wins.
- [ ] In the event of metadata conflicts, retain the first-registered metadata and do not throw exceptions.
- [ ] `IsKnown(key)` returns true for both registered general events and system events.
- [ ] `IsKnownForWorkflow(key)` excludes keys where `IsSystemEvent=true`, for use in WorkflowRule storage validation.
- [ ] `GetAll()` returns only workflow-visible event keys, excluding keys where `IsSystemEvent=true`.

**Verification:**
- [ ] Unit test: Duplicate registration of the same key retains only one entry.
- [ ] Unit test: Metadata conflicts resolve using first-wins.
- [ ] Unit test: `platform.connection_changed` can be marked as a system event; `IsKnown=true`, `IsKnownForWorkflow=false`, and it does not appear in `GetAll()`.

**Dependencies:** Task 4

**Files Likely Involved:**
- `src/Vulperonex.Application/EventTypes/IStreamEventTypeRegistry.cs`
- `src/Vulperonex.Application/EventTypes/StreamEventTypeMetadata.cs`
- `src/Vulperonex.Infrastructure/EventTypes/InMemoryStreamEventTypeRegistry.cs`
- `tests/Vulperonex.Tests.Unit/Application/EventTypes/`

**Estimated Size:** S

---

## Task 9b: SimulationAdapter Publish MVP Events

**Description:** Implement `IStreamEventSource`, `SimulationAdapter`, and `ISimulationAdapter` to allow tests, the Web API, and the CLI to publish all seven MVP domain events through a unified adapter. Exposing REST/CLI aliases is deferred to Task 14b; this task provides the internal APIs and test entry points.

**Acceptance Criteria:**
- [ ] The `ISimulationAdapter` contract does not reference Twitch or Web/CLI types.
- [ ] `IStreamEventSource` is located in `Vulperonex.Adapters.Abstractions`, serving as a shared contract for adapter lifecycles and event sources.
- [ ] `SimulationAdapter` can publish seven MVP events: message, followed, donated, subscribed, gifted subscription, raided, and reward redeemed.
- [ ] `StartAsync` registers all Simulation-supported event keys to `IStreamEventTypeRegistry`.
- [ ] The publish path goes strictly through `IStreamEventBus.PublishAsync`.
- [ ] Unsupported simulation requests return clear failure results or throw domain-neutral exceptions; do not use human-readable API error codes (API mapping is deferred to Task 14b).

**Verification:**
- [ ] Unit/integration test: After publishing each simulation request, `Subscribe<IStreamEvent>` receives the corresponding concrete event.
- [ ] Unit test: Message simulation preserves `StreamUser` and message text.
- [ ] Unit test: Subscription simulation preserves the tier.
- [ ] Unit test: StartAsync registers all seven MVP keys, and does not register `platform.connection_changed` as a workflow-visible event.

**Dependencies:** Task 9a

**Files Likely Involved:**
- `src/Adapters/Vulperonex.Adapters.Abstractions/IStreamEventSource.cs`
- `src/Adapters/Vulperonex.Adapters.Simulation/ISimulationAdapter.cs`
- `src/Adapters/Vulperonex.Adapters.Simulation/SimulationAdapter.cs`
- `src/Adapters/Vulperonex.Adapters.Simulation/SimulationRequest.cs`
- `tests/Vulperonex.Tests.Unit/Adapters/Simulation/`
- `tests/Vulperonex.Tests.Integration/Adapters/Simulation/`

**Estimated Size:** M

---

## Task 9c: Simulation Isolation and SC-3 Guard

**Description:** Add architectural tests to ensure SimulationAdapter does not reference Twitch adapter, and the Domain/Application layers remain free of platform leaks.

**Acceptance Criteria:**
- [ ] `Vulperonex.Adapters.Simulation` does not reference `Vulperonex.Adapters.Twitch`.
- [ ] No `Twitch*` type dependencies exist in the Simulation project.
- [ ] SC-3 and existing SC-4 architectural tests pass.

**Verification:**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false`

**Dependencies:** Task 9b

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Architecture/Adapters/SimulationAdapterIsolationTests.cs`
- `tests/Vulperonex.Tests.Architecture/Domain/PlatformLeakageTests.cs`

**Estimated Size:** S

---

## Task 10a: WorkflowRule Application Contracts

**Description:** Create WorkflowRule contracts, repositories, read DTOs, and action/condition models in the Application layer. Define a testable workflow contract first, without hooking up Web APIs.

**Acceptance Criteria:**
- [ ] `WorkflowRule`/DTO contracts are located in Application, not exposing EF entities.
- [ ] Write repository ports and query service ports are kept separate.
- [ ] Conditions/Actions utilize explicit type discriminators instead of arbitrary dynamic objects.
- [ ] Action configuration contains MVP fields: `TimeoutMs`, `MaxRetries`, `BackoffMs`, and `ErrorBehavior`; validation endpoints are deferred to Task 14a.
- [ ] `SendChatMessageAction` and `InvokeSubWorkflowAction` serve as built-in action models.

**Verification:**
- [ ] Unit/contract test: Application workflow contracts do not reference Infrastructure/EF.
- [ ] Unit test: Action/condition DTOs can perform round-trips via `System.Text.Json`.
- [ ] Application coverage gate remains >80%.

**Dependencies:** Task 5

**Files Likely Involved:**
- `src/Vulperonex.Application/Workflows/WorkflowRule.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowRuleRepository.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowRuleQueryService.cs`
- `src/Vulperonex.Application/Workflows/Conditions/`
- `src/Vulperonex.Application/Workflows/Actions/`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/`

**Estimated Size:** M

---

## Task 10b: Condition Evaluator

**Description:** Implement the pure logical evaluation of conditions for the WorkflowEngine, supporting MVP evaluators for UserRole, MessageContent, and Cooldown first. This task does not execute actions.

**Acceptance Criteria:**
- [ ] `UserRoleCondition` utilizes `StreamRole` flags: `Subscriber`, `Moderator`, `Vip`, and `Follower`, supporting `HasAny`, `HasAll`, and `NotHave` modes.
- [ ] `MessageContentCondition` supports `PrefixMatch`, `ContainsMatch`, and `FullRegex` MVP semantics.
- [ ] Regex pattern length caps at 512, and validity checks are expressed in the Application validator; API error mapping is deferred to Task 14a.
- [ ] `FullRegex` runtime evaluation timeout is 500ms, preventing ReDoS from locking up handlers.
- [ ] `CooldownCondition` utilizes `IClock` instead of directly reading `DateTime.UtcNow`. The `DurationSeconds` save-time range is `[1, 86400]`, expressed in the Application validator; API error mapping is deferred to Task 14a.
- [ ] Unknown condition types return failure/validation results instead of crashing.

**Verification:**
- [ ] Unit test: UserRole HasAny/HasAll/NotHave matching.
- [ ] Unit test: Message PrefixMatch/ContainsMatch/FullRegex matching.
- [ ] Unit test: Invalid regex and regex exceeding 512 characters are identified.
- [ ] Unit test: FullRegex evaluation exceeding 500ms fails closed.
- [ ] Unit test: Cooldown blocks and permits actions under a fake clock.
- [ ] Unit test: CooldownCondition DurationSeconds range validator.
- [ ] Unit test: Unknown condition types do not trigger rules.

**Dependencies:** Task 10a

**Files Likely Involved:**
- `src/Vulperonex.Application/Workflows/Conditions/WorkflowConditionEvaluator.cs`
- `src/Vulperonex.Application/Workflows/Conditions/`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/Conditions/`

**Estimated Size:** M

---

## Task 10c: SendChatMessage Action and Platform Routing

**Description:** Implement the built-in `SendChatMessageAction` executor and `IPlatformChatSender` routing logic, fulfilling SC-9.

**Acceptance Criteria:**
- [ ] The `IPlatformChatSender` contract is located in Application or adapter abstractions at a position that conforms to SPEC dependency directions.
- [ ] When `TargetPlatform` is not set, use the source event platform.
- [ ] When `TargetPlatform` is set, override the source platform.
- [ ] If a sender is not found, log a warning and skip execution without crashing.
- [ ] Template rendering preserves original text for unknown placeholders; replaces null/empty placeholders with empty strings.

**Verification:**
- [ ] Unit test: `SendChatMessageAction_DefaultsToSourcePlatform`.
- [ ] Unit test: `SendChatMessageAction_RespectsTargetPlatformOverride`.
- [ ] Unit test: Missing sender is skipped.
- [ ] Unit test: Template rendering handles unknown/null placeholders.

**Dependencies:** Task 10a

**Files Likely Involved:**
- `src/Vulperonex.Application/Workflows/Actions/SendChatMessageActionExecutor.cs`
- `src/Vulperonex.Application/Workflows/Actions/TemplateRenderer.cs`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/Actions/`

**Estimated Size:** M

---

## Task 10d: WorkflowEngine Subscription and Serial Execution

**Description:** Implement the `WorkflowEngine` hosted service that subscribes to `IStreamEvent`, loads enabled rules, sorts them by `Priority ASC, CreatedAt ASC, Id ASC`, and executes actions serially first.

**Acceptance Criteria:**
- [ ] WorkflowEngine subscribes to `IStreamEvent` on startup and disposes of the subscription on shutdown.
- [ ] Disabled rules are not executed.
- [ ] Rules with mismatched EventTypeKeys are not executed.
- [ ] Rules are executed sorted by `Priority ASC, CreatedAt ASC, Id ASC`.
- [ ] Serial mode executes actions in the order of their action indices.
- [ ] Serial mode scope applies to a single `WorkflowRule`: events of the same rule are executed one at a time; different rules utilize independent queues so Rule A does not block Rule B.
- [ ] Action execution uses the Task 6 `ActionExecutionLog` deduplication key shape: general actions use `(EventId, WorkflowRuleId, ActionIndex)`, while `InvokeSubWorkflowAction` uses `(EventId, WorkflowRuleId, ActionIndex, InvocationId)`.

**Verification:**
- [ ] Integration test: Publish `UserSentMessageEvent` -> matching rule -> mock sender receives exactly once (SC-2 entry point).
- [ ] Unit/integration test: Disabled rules are skipped.
- [ ] Unit/integration test: Priority ordering.
- [ ] Unit/integration test: Replaying the same event/rule/action is skipped due to deduplication.
- [ ] Unit/integration test: The serial queue for the same rule sorts and executes; different rules do not block one another.

**Dependencies:** Task 9b, Task 10b, Task 10c

**Files Likely Involved:**
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowActionExecutor.cs`
- `tests/Vulperonex.Tests.Integration/Workflows/WorkflowEngineTests.cs`

**Estimated Size:** M

---

## Task 10e: WorkflowEngine Timeout, Retry, Parallel Mode, and Error Behavior

**Description:** Complete Workflow action execution policies: timeout, retry/backoff, Serial/Parallel concurrency, and ErrorBehavior. This task completes the full MVP behavior for Task 10.

**Acceptance Criteria:**
- [ ] `TimeoutMs` is passed to the executor via a cancellation token; handled per `ErrorBehavior` on timeout.
- [ ] `RetryOnError` utilizes `MaxRetries` and `BackoffMs`.
- [ ] `ContinueOnError` logs errors and executes subsequent actions.
- [ ] `StopOnError` terminates subsequent actions for the current rule.
- [ ] Parallel mode respects `MaxParallelism`.
- [ ] Handler exceptions do not bubble up through `WaitForIdleAsync`, adhering to the EventBus contract.

**Verification:**
- [ ] Unit/integration test: Timeout cancels action.
- [ ] Unit/integration test: Retry count and backoff operate under a fake clock or deterministic scheduler.
- [ ] Unit/integration test: Differences between ContinueOnError and StopOnError are verified.
- [ ] Unit/integration test: Parallel mode does not exceed `MaxParallelism`.
- [ ] `dotnet test` passes 100% green.

**Dependencies:** Task 10d

**Files Likely Involved:**
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- `src/Vulperonex.Application/Workflows/WorkflowExecutionOptions.cs`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/`
- `tests/Vulperonex.Tests.Integration/Workflows/`

**Estimated Size:** M

---

## Task 10f: InvokeSubWorkflow Action and InvocationId Deduplication

**Description:** Implement the built-in `InvokeSubWorkflowAction` executor, allowing the WorkflowEngine to call another workflow rule, and ensuring sub-workflow calls use a stable `InvocationId` in deduplication keys.

**Acceptance Criteria:**
- [ ] `InvokeSubWorkflowActionExecutor` loads the target workflow rule by `WorkflowId` and executes its actions.
- [ ] If the target workflow does not exist, log a warning and skip execution without crashing and without applying `ErrorBehavior`.
- [ ] Each `InvokeSubWorkflowAction` generates an `InvocationId` before action execution, persisting it in the TDQ payload or equivalent persistent data so that replays utilize the same `InvocationId`.
- [ ] Sub-workflow action execution keys use `(EventId, WorkflowRuleId, ActionIndex, InvocationId)`.
- [ ] Sub-workflows still respect timeout, retry/backoff, serial/parallel, and ErrorBehavior policies.

**Verification:**
- [ ] Unit/integration test: Missing target workflow produces a warning and is skipped.
- [ ] Unit/integration test: Sub-workflow matching actions are executed.
- [ ] Unit/integration test: The `InvocationId` remains unchanged upon replay, without regenerating a new ULID.
- [ ] Unit/integration test: Sub-workflow deduplication keys contain the `InvocationId`, skipping duplicate executions upon replay.

**Dependencies:** Task 10e

**Files Likely Involved:**
- `src/Vulperonex.Application/Workflows/Actions/InvokeSubWorkflowActionExecutor.cs`
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- `tests/Vulperonex.Tests.Unit/Application/Workflows/Actions/`
- `tests/Vulperonex.Tests.Integration/Workflows/`

**Estimated Size:** M

---

## Task 11a: Plugin Contracts

**Description:** Implement MVP plugin contracts in `Vulperonex.Plugins.Abstractions`: `IVulperonexPlugin`, `IPluginContext`, and `IPluginActionContext`.

**Acceptance Criteria:**
- [ ] `IVulperonexPlugin.Name` is a lowercase-kebab plugin id.
- [ ] `IPluginContext` exposes only explicit services like `IStreamEventBus`, a logger, and `IPluginEventTypeRegistrar`; it must not expose `IServiceProvider` or the full `IStreamEventTypeRegistry`.
- [ ] `IPluginEventTypeRegistrar` is a minimal registration surface for plugins, allowing them to register custom `EventTypeKeys` during `InitializeAsync`, without querying or overriding existing registry metadata.
- [ ] `IPluginActionContext` contains `ActionExecutionKey`, event/rule/action metadata, and `Params: IReadOnlyDictionary<string, JsonElement>`.
- [ ] Contracts do not reference Infrastructure, Hosts, or adapter implementations.

**Verification:**
- [ ] Architectural test: `Plugins.Abstractions` only depends on Domain + Application.
- [ ] Reflection test: Plugin context interface property types do not contain `IServiceProvider` or the full `IStreamEventTypeRegistry`.
- [ ] Unit test: JsonElement parameters can be read via `.GetString()`, `.GetInt32()`, or `.GetBoolean()`.
- [ ] Unit test: Plugins can register custom workflow-visible event types via `IPluginContext.EventTypes` / `IPluginEventTypeRegistrar`.

**Dependencies:** Task 10a, Task 9a

**Files Likely Involved:**
- `src/Vulperonex.Plugins.Abstractions/IVulperonexPlugin.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginContext.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginActionContext.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginEventTypeRegistrar.cs`
- `tests/Vulperonex.Tests.Unit/Plugins/`
- `tests/Vulperonex.Tests.Architecture/Plugins/`

**Estimated Size:** S

---

## Task 11b: Static Plugin Registry and InvokePluginAction Executor

**Description:** Implement the MVP static plugin registry and the `InvokePluginAction` executor, allowing the WorkflowEngine to invoke registered plugins via plugin ID and action ID.

**Acceptance Criteria:**
- [ ] The plugin registry is established via DI startup-time static registration.
- [ ] `InvokePluginAction.PluginId` must equal `IVulperonexPlugin.Name`.
- [ ] A missing plugin logs a warning and is skipped without crashing.
- [ ] `IPluginActionContext.ActionExecutionKey` utilizes the full deduplication key.
- [ ] The executor forwards JsonElement parameters as-is to the plugin context.

**Verification:**
- [ ] Unit/integration test: Registered plugin actions are invoked.
- [ ] Unit/integration test: Missing plugins are skipped.
- [ ] Unit test: ActionExecutionKey contains the event, rule, and action index.
- [ ] Unit test: Reading JsonElement parameter types does not trigger `InvalidCastException`.

**Dependencies:** Task 11a, Task 10d

**Files Likely Involved:**
- `src/Vulperonex.Application/Workflows/Actions/InvokePluginActionExecutor.cs`
- `src/Vulperonex.Application/Plugins/IPluginRegistry.cs`
- `src/Vulperonex.Infrastructure/Plugins/StaticPluginRegistry.cs`
- `tests/Vulperonex.Tests.Unit/Plugins/`
- `tests/Vulperonex.Tests.Integration/Plugins/`

**Estimated Size:** M

---

## Task 11c: Plugin Publishes Custom Event Scenario

**Description:** Complete SC-10: plugins publish custom `IStreamEvents` via `IPluginContext.Events.PublishAsync(customEvent)`, and the WorkflowEngine triggers subsequent actions using matching rules.

**Acceptance Criteria:**
- [ ] Plugins can publish custom `IStreamEvents` during `InitializeAsync` or action execution.
- [ ] Plugins register custom event keys to `IStreamEventTypeRegistry` during `InitializeAsync`, where `IsKnownForWorkflow(customKey)=true`.
- [ ] Custom events can match WorkflowRules via EventTypeKey.
- [ ] Matching custom event rules can trigger `SendChatMessageAction`.
- [ ] The plugin publish path still routes through `IStreamEventBus`, not directly calling the WorkflowEngine.

**Verification:**
- [ ] Integration test: `Plugin_CanPublishCustomEvent_TriggeringWorkflow` passes.
- [ ] Integration test: Custom event keys from plugins can be queried via workflow-visible registry queries after registration.
- [ ] `dotnet test` -> SC-10 passes.
- [ ] Application coverage gate remains >80%.

**Dependencies:** Task 11b, Task 10f

**Files Likely Involved:**
- `tests/Vulperonex.Tests.Integration/Plugins/PluginWorkflowTests.cs`
- `tests/Vulperonex.Tests.Unit/Plugins/`

**Estimated Size:** S

---

## Phase 3 Checkpoint

**Acceptance Criteria:**
- [ ] Tasks 9a-11c are completed and committed in small slices.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] SC-2, SC-3, SC-4, SC-9, SC-10 pass.
- [ ] SimulationAdapter -> Bus -> WorkflowEngine -> `IPlatformChatSender` end-to-end passes.
- [ ] Plugins can publish events and trigger rules.
- [ ] Domain coverage gate >90% passes.
- [ ] Application coverage gate >80% passes.
- [ ] Architectural tests confirm no Infrastructure/EF/service locator leaks in Domain/Application/Plugins.Abstractions.
- [ ] `git status --short --ignored` displays only expected ignored local files.

**Review Threshold:**
- [ ] Manually review WorkflowEngine's retry/timeout/dedup semantics, plugin context surface, and Simulation/Twitch equivalence readiness before beginning Phase 4.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|------|----------|
| WorkflowEngine scope becomes too large | High | Split into five slices: contracts, conditions, SendChatMessage, serial engine, and advanced execution. |
| Action timeout/retry tests become flaky | Medium | Utilize fake clocks and deterministic synchronization; do not rely on fixed sleep times. |
| Plugin context becomes a service locator | High | Design contracts first; reflection architectural tests block `IServiceProvider`. |
| SimulationAdapter is used as a testing shortcut | Medium | Mandate that all acceptance paths publish through `IStreamEventBus`. |
| WorkflowRule JSON shape is bound prematurely to Web APIs | Medium | Define only Application contracts in Phase 3; API validation/error code mapping is deferred to Task 14a. |

## Open Questions

- Should the EF repository implementation for `WorkflowRule` be completed in Task 10a, or should an in-memory fake support the WorkflowEngine first, deferring EF CRUD to Task 14a? Recommendation: Use Application ports + in-memory fakes to verify workflow behaviors in Phase 3; defer EF CRUD to Task 14a to avoid inflating Phase 3 scope.
