# Functional Specification: Simulation Side-Effect Policy

> [← Back to Master Specification](../../SPEC.md)

### 4.27 Simulation Side-Effect Policy (Phase 9)

**Background & Motivation:**

When operators send mock events through the simulation panel (`/api/simulate/*`, `Platform == "simulation"`), the workflow engine and overlays are triggered to verify rules. Problem: some action executors produce side effects in the **real world**, which should not be triggered by simulated events. Known flaws:

- `ShoutoutAction` calls the Helix `chat/shoutouts` endpoint directly — simulation triggers a real shoutout on Twitch (even to unrelated real users).
- `RefundRewardRedemptionAction` calls the Helix refund endpoint — simulation triggers a **real refund**.
- `TriggerCheckInAction` / `AddLotteryTicketsAction` / `UpdateCounterAction` write to the **real database** (member loyalty, check-in count, audits, counters, lottery tickets).
- `LookupPlatformUserAction` queries Helix (read-only, non-destructive, but redundant).

Core Principle: **The only allowed visible output for simulation is overlay preview (SignalR); it must not leak to real external services, and by default, it must not write to real persistent state.**

---

**Action Categorization and Behavior under Simulation:**

| Category | Action | Side Effect | Behavior under Simulation |
|---|---|---|---|
| A. Overlay Preview (Retained) | sendChatMessage, triggerEffect, emitOverlayWidget | SignalR → overlay | Normal behavior (this is the expected visible output of simulation). Note: Actual platform delivery of sendChatMessage is isolated by `SimulationPlatformChatSender` (no-op) and **does not enter real Twitch chat**, only the overlay. |
| B. Internal Control Flow (Safe) | delay, stopIf, randomPicker, emitSystemEvent, invokeSubWorkflow | Pure computation / in-memory bus / recursion | Normal behavior. `emitSystemEvent` and `invokeSubWorkflow` propagate the `Platform` of the same event, so downstream leaf actions can guard themselves, making chaining automatically safe. |
| C. External Twitch API (**Always Skip**) | shoutout, refundRewardRedemption, lookupPlatformUser | Real Helix calls | **Unconditionally** skip the real call, and return a synthetic output to let the pipeline continue. **No toggle** — irreversible side effects on real Twitch or other users are never executed in simulation. |
| D. Real DB Writes (**Controlled by Toggle**) | triggerCheckIn, addLotteryTickets, updateCounter | Writes to real member/counter/audit state | Controlled by system setting `simulation.allow_persistent_writes` (**default false**): false → skip writes, return synthetic output (and emit overlay events as normal for preview); true → write as normal (complete persistent test path). |
| E. Plugins (Author's Responsibility) | invokePlugin | Depends on the plugin | The engine cannot guarantee safety; plugin authors must guard themselves using the event `Platform` from `IPluginActionContext`. This responsibility must be noted in the plugin contract spec. |

---

**Design & Specifications:**

1. **Gatekeeping Point: Leaf Executor** (not the dispatcher). Each Category C/D executor applies its corresponding strategy when `context.StreamEvent.Platform == "simulation"` (string comparison, `OrdinalIgnoreCase`; adhering to existing conventions in `InMemoryWorkflowThrottleService` and `WorkflowConditionEvaluator`). Reason: since `emitSystemEvent`/`invokeSubWorkflow` propagate the same `Platform`, leaf gatekeeping naturally covers all chained rules.

2. **Category C (External APIs, Always Skip)** — Synthetic outputs adopt a "happy-path" semantic so that downstream steps (e.g. subsequent chat messages) can run normally:
   - `ShoutoutActionExecutor`: Returns `IsSent=true`, resolves `TargetLogin/TargetDisplayName` from the parsed target login template, and sets `TargetUserId` to empty (since real user ID cannot be resolved without Helix).
   - `RefundRewardRedemptionActionExecutor`: Returns `IsRefunded=true` + echoes `RewardId/RedemptionId`, without calling Helix.
   - `LookupPlatformUserActionExecutor`: Returns `IsFound=true`, resolves `Login/UserId/DisplayName` from the inputs, leaves `Avatar/Description` empty, and sets `IsAffiliate=false` without calling Helix.
   - All three bypass `catch (OperationCanceledException) { throw; }` since they short-circuit before try-catch; optional `ILogger<T>? logger = null` is added for logging info (auto-injected by DI; preserving existing two-argument test constructors).

3. **Category D (DB Writes, Toggle)** — Introduces system setting key `SystemSettingKey.SimulationAllowPersistentWrites = "simulation.allow_persistent_writes"`, defaulting to `false`:
   - If `Platform == "simulation"` and setting is `false` → **skip real writes**, return synthetic output; for `triggerCheckIn`, still emit `MemberCheckedInEvent` (for overlay preview) with synthetic count = `(Current CheckInCount ?? 0) + 1` (following the read-only semantic of checkin `isTest` in `SimulateEndpoints`, and **never** throwing an exception for non-existing members).
   - `addLotteryTickets` / `updateCounter`: Skip `counterRepository.IncrementAsync`, return synthetic `TicketCount/Value` (using amount/delta as the synthetic value).
   - If setting is `true` → write as normal (complete persistence path).
   - Dependency injection: `triggerCheckIn` already has `ISystemSettingsService` injected; `addLotteryTickets` / `updateCounter` add an **optional** `ISystemSettingsService? settings = null` (auto-injected by DI; preserving existing test constructors. If `settings == null` under simulation, it falls back to safe default = false → skip).

4. **Toggle UI (Future)** — The simulation panel can add an "Allow Persistent Writes (Test Mode)" toggle, writing to `simulation.allow_persistent_writes` via the config API. The MVP backend default `false` already satisfies the core requirement of "simulation does not pollute real data"; the UI toggle is an advanced test enhancement that can be added later.

---

**Acceptance Criteria (BDD):**

- Given a simulated raid/message event, When the rule contains shoutout / refund / lookup actions, Then no real Helix calls occur, the step returns synthetic success, and subsequent steps execute normally.
- Given `simulation.allow_persistent_writes = false` (default), When a simulated event triggers triggerCheckIn / addLotteryTickets / updateCounter, Then **no writes** occur in the DB (no IncrementCheckIn, no counter increment, no audit log), but triggerCheckIn still emits the overlay card event and each step returns synthetic output.
- Given `simulation.allow_persistent_writes = true`, When the same trigger occurs, Then writes proceed normally (full persistence).
- Given a real Twitch event (`Platform == "twitch"`), When any action executes, Then the behavior remains unchanged (gatekeeping does not trigger).
- Unit Tests: Gatekeeping tests for Category C/D executors under simulation (asserting no Helix call / no repository write + correct synthetic output).

---

**Boundaries:**

- Overlay preview (chat/effect/widget/member card) is the **expected** output of simulation, and is not suppressed.
- Category C has no toggle (irreversible external side effects are never executed under simulation); only Category D (local reversible data) provides a toggle.
- Gatekeeping is based on string comparison of `Platform == "simulation"` (adhering to existing conventions, without abstracting a new constant).
- Simulation safety of plugins is the plugin author's responsibility; the engine does not intercept plugin calls.
- This policy does not alter any behavior of real event execution paths.
