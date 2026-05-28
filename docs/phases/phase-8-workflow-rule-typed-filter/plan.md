# Phase 8 · Workflow Rule Typed Filter & Observability — Implementation Plan

> **Spec Baseline**: [`docs/specs/workflow-rule-design-comparison.md`](../../specs/workflow-rule-design-comparison.md)
>
> **Status**: Draft · **Created Date**: 2026-05-28 · **Author**: Codex
>
> **Output Scope**: All implementation slices, dependencies, verifications, and checkpoints from Phase A to Phase E in Section 6 of the Spec.

---

## Overview

Resolve the **systemic root causes** of the Twitch chat `!checkin` incident (lack of typed semantics in trigger filters, silent NCalc fallback, FE/BE double-writes of metadata) and complete the cleanup of Vulperonex's own internal schema redundancies. The final goals are:

1. NCalc and filter failures are recorded in structured logs, enabling operators to trace them directly to `RuleId`.
2. `EventTypeKey` and `MatchCondition` each appear exactly once in the schema.
3. The backend serves as the single source of truth for trigger and action metadata, dynamically pulled by the frontend.
4. Trigger filtering adopts a per-event-type typed matcher (OC-style) to eliminate misconfigurations of generic dictionaries.
5. The Editor UX is upgraded to a Drawer + Tabs layout, and `TriggerEditor` becomes schema-driven.
6. Enhance the role-gating UX (leveraging `UserRoleCondition` without altering the schema).

---

## Architecture Decisions (Adopting Resolved Items in Spec §8)

| # | Decision | Reason |
|---|---|---|
| AD-1 | Place metadata in the backend, generated via reflection using attributes on action records | Single source of truth, eliminating double-maintenance. |
| AD-2 | Add `RuleId/RuleName` properties to `ExpressionContext` (rather than changing the `IExpressionEvaluator` signature) | Stable interfaces to protect existing tests. |
| AD-3 | Ship Phase A.5 §5b.1/§5b.2/§5b.3 in a single merged release | Share the same DB migration window, eliminating compound risks. |
| AD-4 | Register engine-internal events via an independent `WorkflowInternalEventTypeBootstrapper IHostedService` | Symmetrical to the adapter registration flow. |
| AD-5 | Directly run `DELETE FROM workflow_rules` and let `DefaultWorkflowRuleSeedService` reseed typed examples in Phase B | Development stage, no real operator data; the previous `legacy_filter_blob` design is reverted. |
| AD-6 | Adopt **Reka UI** (headless) + existing custom CSS for Phase D containers | Minimal bundle footprint (~30 KB gzip), optimal accessibility, zero styling conflict. |

**Fallback Order** (if the AD-6 PoC exceeds budget limits): Naive UI tree-shake ＞ pure hand-crafting.

---

## Dependency Graph

```
Phase A (observability)
    │
    └── Phase A.5 (schema cleanup §5b.1/5b.2/5b.3)
            │
            ├── Phase B (metadata provider + legacy scrub)
            │       │
            │       ├── Phase C (typed matcher registry)
            │       │       │
            │       │       └── §1 sample rule end-to-end trigger
            │       │
            │       └── Phase D (UI: Drawer + schema-driven TriggerEditor)
            │
            └── Phase E (role gating UX — parallelizable with D)

Phase §5b.5 (workflow.timer registry) — Independent branch, parallelizable with A
```

---

## Task List

### Phase 0 · Prerequisites (Parallelizable with Phase A)

#### Task 0.1: Register `workflow.timer` into Event Type Registry

**Description**: Resolves Spec §5b.5. Introduce a `WorkflowInternalEventTypeBootstrapper` `IHostedService` to register engine-internal event keys (starting with `workflow.timer`) via `IStreamEventTypeRegistry.Register()` on startup. Add a corresponding entry in `StreamEventDescriptions` synchronously (for consistency, not as the "fix").

**Acceptance criteria**:
- [ ] `curl /api/event-types` response contains `{ "key": "workflow.timer", "isSimulatable": false, ... }`.
- [ ] `POST /api/rules` with `eventTypeKey="workflow.timer"` no longer returns 400 `UnknownEventTypeKey`.
- [ ] UI dropdown displays the timer option.
- [ ] Existing `WorkflowTimerHostedService.InvokeAsync` operates unchanged.

**Verification**:
- [ ] Run `dotnet test --filter Category=EventTypeRegistry`.
- [ ] Manual: `curl http://localhost:5000/api/event-types | jq '.[] | select(.key=="workflow.timer")'`.

**Dependencies**: None (Independent branch).

**Files**:
- `src/Vulperonex.Application/Workflows/Timers/WorkflowInternalEventTypeBootstrapper.cs` (New)
- `src/Hosts/Vulperonex.Web/DependencyInjection.cs`
- `src/Vulperonex.Domain/Events/StreamEventDescriptions.cs`
- `tests/Vulperonex.Tests.Integration/Web/EventTypeEndpointTests.cs`

**Scope**: S

---

### Phase A · Immediate Stop-Bleeding (Observability, No Schema Changes)

#### Task A.1: Add `RuleId` / `RuleName` to `ExpressionContext` (Resolving §5b.4)

**Description**: Add two optional string properties to the record; populate them with current rule data inside `WorkflowEngine.BuildExpressionContext`. The signature of `IExpressionEvaluator.Evaluate` remains unchanged (AD-2).

**Acceptance criteria**:
- [ ] `ExpressionContext` features new properties `RuleId` / `RuleName` (optional; defaults to null for backward compatibility).
- [ ] All `new ExpressionContext(...)` instantiation sites inject rule data (required in engine paths, optional in test paths).
- [ ] Existing unit tests pass successfully without any changes.

**Verification**:
- [ ] Run `dotnet test`.
- [ ] Grep: inspect all invocation sites containing `new ExpressionContext`.

**Dependencies**: None.

**Files**:
- `src/Vulperonex.Application/Expressions/ExpressionContext.cs`
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- Tests: Existing `ExpressionContext` / `WorkflowEngine` test suites.

**Scope**: S

---

#### Task A.2: Inject `ILogger` into `NCalcExpressionEvaluator` + Emit Structured Failure Logs

**Description**: Resolves the three silent paths described in Spec §1. For evaluation throws or `HasErrors()`, log `LogWarning(... {RuleId} {RuleName} {ExpressionHash} {ErrorClass})`. Let `ExpressionHash` = first 8 characters of SHA1. **Do not log the full expression body** (for PII protection).

**Acceptance criteria**:
- [ ] `null/whitespace` path: does not log (remains a valid short-circuit).
- [ ] `HasErrors()`: emits `LogWarning` carrying `ErrorClass=ParseError`.
- [ ] `catch`: emits `LogWarning` carrying `ErrorClass=EvalError` along with the exception type.
- [ ] Logs contain no raw expression texts (enforced by grep tests).
- [ ] Triggering an event on a rule with an intentional typo logs `RuleId=... ExpressionHash=... ErrorClass=...`.

**Verification**:
- [ ] Run `dotnet test --filter NCalcExpressionEvaluator`.
- [ ] New unit test: `Given_InvalidExpression_When_Evaluate_Then_LogsWarningWithoutBody`.
- [ ] Manual: Run a rule with a typo in staging and inspect the logs.

**Dependencies**: A.1

**Files**:
- `src/Vulperonex.Infrastructure/Expressions/NCalcExpressionEvaluator.cs`
- `src/Vulperonex.Infrastructure/DependencyInjection.cs` (Inject logger)
- `tests/Vulperonex.Tests.Unit/Expressions/NCalcExpressionEvaluatorTests.cs`

**Scope**: S

---

#### Task A.3: Log Classification in `WorkflowEngine` + Structured Event Logs

**Description**: Classify logs according to the log noise principles in Spec Phase A:

| Event | Log Level |
|---|---|
| Expression parse/eval throw | Warning (Provided by A.2) |
| Filter key not in valid metadata list | Warning |
| Action executor throw | Warning |
| Filter value mismatch (normal no-match) | Debug |
| `MatchCondition` false (normal no-match) | Debug |
| Throttle deny | Debug |
| `EventTypeKey` mismatch | No Log |

Structured log event: `workflow_rule_skipped` carrying fields `RuleId / Reason / EventTypeKey`.

**Acceptance criteria**:
- [ ] `MatchesTriggerFilter` logs `Warning` for unknown keys (temporarily using the "known valid filter keys" list prior to Phase B) and `Debug` for value mismatches.
- [ ] `MatchesTrigger` logs `Debug` on a `MatchCondition` false.
- [ ] Throttle deny logs `Debug`.
- [ ] Normal chat traffic does not emit no-match log noise under the `Information` log level.

**Verification**:
- [ ] Run `dotnet test --filter WorkflowEngine`.
- [ ] New unit test: `tests/Vulperonex.Tests.Unit/Workflows/WorkflowEngineLogTests.cs` (New).
- [ ] Manual: Send 1000 `user.message` events; verify that `Information` logs do not contain `rule_skipped`.
- [ ] Verify that complete fan-out traces are visible under the `Debug` level.

**Dependencies**: A.2

**Files**:
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- Tests: `tests/Vulperonex.Tests.Unit/Workflows/WorkflowEngineLogTests.cs` (New)

**Scope**: M

---

#### Task A.4: Document "Known Valid Filter Keys" Table

**Description**: A manually maintained reference table prior to the Phase B metadata endpoints going live. Placed in `docs/phases/phase-8-workflow-rule-typed-filter/known-filter-keys.md` and archived after Phase B is completed.

**Acceptance criteria**:
- [ ] Aligned with existing event types: `user.message` / `user.donated` / `user.subscribed` / `user.gifted_sub` / `channel.raided` / `reward.redeemed` / `workflow.timer`.
- [ ] Lists valid filter keys, types, and examples for each event type.

**Verification**:
- [ ] Code review.

**Dependencies**: None.

**Files**:
- `docs/phases/phase-8-workflow-rule-typed-filter/known-filter-keys.md`

**Scope**: XS

---

### Checkpoint α · Phase A Wrap-Up

- [ ] All unit and integration tests pass successfully.
- [ ] No compiler warnings.
- [ ] Manual: Restarting the web application and triggering an event on a rule with an intentional typo logs a trace that instantly identifies `RuleId`.
- [ ] Manual: Normal chat traffic produces no log noise.
- [ ] Review Phase A results with human operators before proceeding to Phase A.5.

---

### Phase A.5 · Schema Cleanup (§5b.1 / §5b.2 / §5b.3, AD-3 in Same Release)

> **Overall Risk**: DB migration. *Mitigation*: Run inside independent transactions, support dry-run modes, log JSON diffs, and keep backup tables for one release cycle.
>
> **⚠ SQLite Limitations (Applies to all Phase A.5 tasks)**: The project uses SQLite (`vulperonex.db`). SQLite imposes strict limitations on `ALTER TABLE` operations (especially when converting between NULL and NOT NULL on indexed columns, which triggers table-rebuild SQL generated by EF Core). **All A.5 migration integration tests must run against a real SQLite provider** (not InMemory or SQLite in-memory modes) to ensure generated SQL executes successfully under a production SQLite engine.

#### Task A5.1: §5b.1 Abolish `WorkflowTrigger.EventTypeKey`

**Description**: Remove the property from the domain model; allow the `JsonConstructor` to accept the old field but ignore it; DB migration lifts `trigger_json.eventTypeKey` to the outer layer (in case of a mismatch, prioritize the outer level and emit a warning log).

**Acceptance criteria**:
- [ ] `WorkflowTrigger` does not contain the `EventTypeKey` property.
- [ ] DB rows no longer contain the inner `eventTypeKey` in `trigger_json` after migration.
- [ ] Deserializing old JSON formats does not throw exceptions (backward compatibility).
- [ ] Simplify `WorkflowRuleJsonMapper.NormalizeTrigger` (eliminating double writes).

**Verification**:
- [ ] Run `dotnet test`.
- [ ] Migration dry-run: run against staging DB and produce a diff log.
- [ ] Round-trip test: `Given_LegacyRuleWithInnerEventTypeKey_When_LoadAndSave_Then_OuterPreservedInnerDropped`.

**Dependencies**: Checkpoint α

**Files**:
- `src/Vulperonex.Application/Workflows/WorkflowTrigger.cs`
- `src/Hosts/Vulperonex.Web/Workflows/WorkflowRuleJsonMapper.cs`
- `src/Vulperonex.Infrastructure/Persistence/Migrations/[N]_StripInnerEventTypeKey.cs` (New)
- `src/frontend/src/views/admin/RuleEditorView.vue` (Remove double writes)

**Scope**: M

---

#### Task A5.2: §5b.2 Abolish `WorkflowTrigger.MatchCondition`

**Description**: Remove the inner field; DB migration lifts the inner value to the outer layer (prioritizing the outer value and logging warnings on conflicts); remove `??` fallback in `WorkflowEngine.MatchesTrigger`.

**Acceptance criteria**:
- [ ] `WorkflowTrigger` does not contain the `MatchCondition` property.
- [ ] `WorkflowEngine.MatchesTrigger` reads exclusively from `rule.MatchCondition`.
- [ ] Existing rules consolidate their `MatchCondition` in the outer layer post-migration.
- [ ] Remove double writes in `RuleEditorView` frontend.

**Verification**:
- [ ] Run `dotnet test`.
- [ ] Migration dry-run + diff logs.
- [ ] Integration test: `Given_LegacyRuleWithInnerMatchCondition_When_Migrated_Then_OuterCarriesValue`.

**Dependencies**: A5.1 (Shared migration window)

**Files**:
- `src/Vulperonex.Application/Workflows/WorkflowTrigger.cs`
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs:237`
- `src/Vulperonex.Infrastructure/Persistence/Migrations/[N+1]_LiftInnerMatchCondition.cs` (New)
- `src/frontend/src/views/admin/RuleEditorView.vue`

**Scope**: M

---

#### Task A5.3: §5b.3 Change `WorkflowRule.EventTypeKey` to `string?` (Strict 3-Step Sequence)

**Description**: Backend first, frontend second.

**[1] Validator Conditional Branches**:
- `IsSubWorkflow == true` ⇒ `EventTypeKey is null && Trigger is null`.
- `IsSubWorkflow == false` ⇒ `EventTypeKey is not null and not whitespace`.

**[2] DB Migration**:
- **Pre-check**: Verify that EF Core entity mappings and column constraints are adjusted in sync.
- **SQLite Table Lock Risk**: Changing SQLite NOT NULL to NULL triggers an EF Core table rebuild (`CREATE new → INSERT SELECT → DROP old → RENAME`). Integration tests must run against a **real SQLite file provider** (disabling InMemory or SQLite in-memory modes) to verify that generated SQL executes successfully.
- **Lock Mitigation**: Run a single migration in development since data volume is small. For production environments with large tables, split it into two releases (drop NOT NULL first, then backfill in a later release).
- Backfill: Convert `event_type_key = ''` to `NULL` for sub-workflow rules.

**[3] Frontend Payload**: Omit `eventTypeKey` in payloads when in sub-workflow mode.

**Acceptance criteria**:
- [ ] `WorkflowRule.EventTypeKey` is typed as `string?`.
- [ ] Web API: creating a sub-workflow rule (payload omitting `eventTypeKey`) returns 200.
- [ ] Web API: creating a non-sub-workflow rule missing `eventTypeKey` returns 400 `UnknownEventTypeKey`.
- [ ] The §5b.6 short-circuit remains but only rejects when "not a sub-workflow and null/whitespace".
- [ ] Existing sub-workflow rules in the DB have `event_type_key` set to `NULL`.

**Verification**:
- [ ] Run `dotnet test --filter WorkflowRuleValidator`.
- [ ] New integration tests (**using a real SQLite file provider**):
  - `Given_SubWorkflowRule_WithoutEventTypeKey_When_Create_Then_Returns200`
  - `Given_NonSubWorkflowRule_WithoutEventTypeKey_When_Create_Then_Returns400`
  - `Given_LegacyRows_When_RunMigration_Then_SqliteTableRebuildSucceeds`
- [ ] Migration dry-run.

**Dependencies**: A5.1 / A5.2 (Same release window)

**Files**:
- `src/Vulperonex.Application/Workflows/WorkflowRule.cs`
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Workflows/WorkflowRuleDto.cs`
- `src/Vulperonex.Infrastructure/Persistence/Migrations/[N+2]_NullableEventTypeKey.cs` (New)
- `src/frontend/src/views/admin/RuleEditorView.vue`

**Scope**: M

---

### Checkpoint β · Phase A.5 Wrap-Up (Pre-release DB Migration Gate)

- [ ] All three migrations pass dry-runs successfully, and diff logs are reviewed.
- [ ] Staging DB migrations complete, and existing rules support 100% round-trips.
- [ ] Backup tables are retained until the next release.
- [ ] Creating sub-workflow rules does not require filling in triggers.
- [ ] `EventTypeKey` and `MatchCondition` each appear exactly once in the schema.
- [ ] Review with human operators and confirm readiness for the release window.

---

### Phase B · Metadata Service Layer + Legacy Scrub

#### Task B.1: `ITriggerMetadataProvider` + Endpoints

**Description**: Introduce a provider returning:
- `AvailableEventTypes`: `[{ key, displayName, description }]`
- `FilterFieldsFor(eventTypeKey)`: `[{ key, label, type, options?, help, required? }]`
- `ValidVariablesFor(eventTypeKey)`: `string[]`

Endpoint: `GET /api/metadata/triggers`.

**Acceptance criteria**:
- [ ] The provider covers 7 event types (matching the Task 0.1 registry list).
- [ ] `FilterFieldsFor("user.message")` returns at least `CommandName` and `Prefix`.
- [ ] `ValidVariablesFor("user.message")` includes `MessageText`, `UserLogin`, etc.
- [ ] The endpoint returns a 200 JSON response.

**Verification**:
- [ ] Run `dotnet test --filter TriggerMetadataProvider`.
- [ ] Verify structure via `curl /api/metadata/triggers | jq`.

**Dependencies**: Checkpoint β

**Files**:
- `src/Vulperonex.Application/Workflows/Metadata/ITriggerMetadataProvider.cs` (New)
- `src/Vulperonex.Infrastructure/Workflows/Metadata/TriggerMetadataProvider.cs` (New)
- `src/Hosts/Vulperonex.Web/Endpoints/MetadataEndpoints.cs` (New)
- `tests/Vulperonex.Tests.Unit/Workflows/Metadata/TriggerMetadataProviderTests.cs`

**Scope**: M

---

#### Task B.2: `IActionMetadataProvider` + Attributes (Reflection via AD-1)

**Description**: Annotate every `WorkflowAction` record with an attribute (e.g., `[ActionMetadata(DisplayName=..., Description=...)]` and property attributes like `[ActionParam(IsRequired=..., DefaultValue=...)]`). The provider reflects these attributes to assemble metadata. Endpoint: `GET /api/metadata/actions`.

**Acceptance criteria**:
- [ ] Annotate all 15 action records with metadata attributes.
- [ ] The provider dynamically harvests 15 entries, each containing `parameters[]`.
- [ ] The endpoint returns a 200 JSON response.
- [ ] Unit test: fails when a new action is added without metadata attributes.

**Verification**:
- [ ] Run `dotnet test --filter ActionMetadata`.
- [ ] `curl /api/metadata/actions | jq 'length'` equals 15.

**Dependencies**: B.1 (Parallelizable, but logically part of Phase B)

**Files**:
- `src/Vulperonex.Application/Workflows/Metadata/ActionMetadataAttribute.cs` (New)
- `src/Vulperonex.Application/Workflows/Actions/*.cs` (Add attributes to 15 records)
- `src/Vulperonex.Infrastructure/Workflows/Metadata/ActionMetadataProvider.cs` (New)
- `tests/Vulperonex.Tests.Unit/Workflows/Metadata/ActionMetadataProviderTests.cs`

**Scope**: M

---

#### Task B.3: DB Wipe + Reseed Typed Sample Rules (Replacing legacy_filter_blob Design)

**Description**: AD-5. Directly wipe existing rules in the development stage, letting `DefaultWorkflowRuleSeedService` populate typical typed sample rules.

**One-time Migration**:
```sql
DELETE FROM workflow_rules;
-- Or retain column definitions and clear data; the next startup will reseed them automatically.
```

**`DefaultWorkflowRuleSeedService` Expansion** (Seeding executes only when the DB has no rules to protect custom rules created by operators):

| Sample Rule | Trigger | Filter (Typed) | Conditions | Actions |
|---|---|---|---|---|
| `!checkin` Check-In | `user.message` | `CommandName: "!checkin"` | – | `triggerCheckIn` + `sendChatMessage` |
| `!so` Shoutout (mod-only) | `user.message` | `CommandName: "!so"` | `userRole: [broadcaster, moderator]` | `shoutout` + `sendChatMessage` |
| Bits 100+ Special Effect | `user.donated` | `MinAmount: 100` | – | `triggerEffect` + `emitOverlayWidget` |
| New Subscription Welcome | `user.subscribed` | – | – | `sendChatMessage` |
| 50+ Gifted Subs Alert | `user.gifted_sub` | `MinGiftCount: 50` | – | `emitOverlayWidget(severity=warning)` |
| Raid Welcome | `channel.raided` | `MinViewers: 5` | – | `shoutout` + `sendChatMessage` |
| Raffle Points Redemption | `reward.redeemed` | `RewardName: "Lottery Ticket"` | – | `addLotteryTickets` |

**Acceptance criteria**:
- [ ] The migration `DELETE FROM workflow_rules` runs successfully.
- [ ] The seed service populates 7 typed sample rules on startup.
- [ ] Every seeded rule passes Phase B.4 strict validation.
- [ ] Consolidate legacy seeds (original boot-seed `!checkin`) into the new version to avoid duplicates.
- [ ] Subsequent startups are idempotent (bypassing seed logic when the DB contains rules).

**Verification**:
- [ ] Run `dotnet test --filter DefaultWorkflowRuleSeed`.
- [ ] Integration test: `Given_EmptyDb_When_AppStart_Then_SevenTypedRulesSeeded`.
- [ ] Integration test: `Given_DbHasRules_When_AppStart_Then_SeedSkipped`.
- [ ] Manual: Clear the DB → start the application → verify that `curl /api/rules | jq 'length'` equals 7.

**Dependencies**: B.1 (Requires metadata list to validate typed filters) + B.4 (The strict validator must be live for seeds to pass validation).

**Files**:
- `src/Vulperonex.Infrastructure/Persistence/Migrations/[N+3]_WipeWorkflowRules.cs` (New)
- `src/Hosts/Vulperonex.Web/DefaultWorkflowRuleSeedService.cs` (Expanded)
- `tests/Vulperonex.Tests.Integration/Web/DefaultWorkflowRuleSeedTests.cs` (Expanded)

**Scope**: M

---

#### Task B.4: Enforce Strict Validation Everywhere (No Lenient Paths)

**Description**: The development DB has been wiped and legacy non-compliant rules are discarded, meaning no lenient reading paths are required in the validator. Add metadata validation checks to creation and editing paths in `WorkflowRuleValidator`; reading paths remain unchanged.

**Acceptance criteria**:
- [ ] POST `/api/rules` with an invalid filter key returns 400 `InvalidFilterKey`.
- [ ] PUT `/api/rules/{id}` behaves identically.
- [ ] GET paths do not include `migrationWarnings` fields (completely avoiding legacy compatibility designs).
- [ ] Do not introduce the `legacy_filter_blob` field.

**Verification**:
- [ ] Run `dotnet test --filter WorkflowRuleValidator`.
- [ ] Integration test: `Given_RuleWithUnknownFilterKey_When_Post_Then_Returns400`.

**Dependencies**: B.1

**Files**:
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Errors/ErrorCodes.cs` (New `InvalidFilterKey`)

---

### Checkpoint γ · Phase B Wrap-Up

- [ ] `/api/metadata/triggers` and `/api/metadata/actions` are live.
- [ ] DB wipe + reseed completed, and `/api/rules` returns 7 typed sample rules.
- [ ] Enforcing strict validation blocks invalid filter keys with 400 responses.
- [ ] Seeding is idempotent on subsequent startups.
- [ ] Review with human operators.

---

### Phase C · Filter Typed Dispatch (Backend)

#### Task C.1: `TriggerFilterMatcherRegistry` + Built-in Matchers

**Description**: Register matchers by `EventTypeKey` and replace `WorkflowEngine.MatchesTriggerFilter`.

**Architectural Constraints (Thread Safety)**:
- Register `TriggerFilterMatcherRegistry` as a **Singleton**.
- Implement each `ITriggerFilterMatcher` as a **Stateless / Immutable Singleton**.
- Freeze the internal dispatch dictionary (`Dictionary<string, ITriggerFilterMatcher>`) as a `FrozenDictionary` or `IReadOnlyDictionary` after reflection loads it during DI bootstrapping.
- **Runtime modifications are strictly prohibited** (do not expose a public `Register()` method at runtime).
- High-frequency concurrent chat paths remain lock-free, reading exclusively from the frozen dictionary.

*Reasoning*: Chat events fan out under high concurrency, making the matcher registry a hot path. Any `ConcurrentDictionary` writes or `lock` operations will become severe bottlenecks during traffic spikes.

| EventTypeKey | Matcher |
|---|---|
| `user.message` | `MatchChatMessage` (CommandName / Prefix + **Boundary Checks** preventing `!so` from matching `!sorry`). |
| `user.donated` | `MatchMinThreshold(MinAmount)`. |
| `user.subscribed` | `MatchSubFilter(Tier, IsGift)`. |
| `user.gifted_sub` | `MatchSubFilter(Tier) + MatchMinThreshold(MinGiftCount)`. |
| `channel.raided` | `MatchMinThreshold(MinViewers)`. |
| `reward.redeemed` | `MatchExactString(RewardName)`. |
| `workflow.timer` | `MatchExactString(TimerName)`. |
| Others | Fallback to generic dict + emit warning log (for backward compatibility). |

**Acceptance criteria**:
- [ ] Registry provides `TryMatch(eventTypeKey, filter, triggerValues) → bool`.
- [ ] `WorkflowEngine.MatchesTrigger` invokes the registry instead of performing exact dictionary matches.
- [ ] Registry and matchers are registered as Singletons; the dispatch dictionary is a `FrozenDictionary` / `IReadOnlyDictionary`.
- [ ] No public `Register()` method is exposed at runtime (only loaded during DI bootstrapping).
- [ ] Boundary check unit tests: verify `!so` does not match `!sorry`, and `MinAmount: 100` does not match `Bits=50`.
- [ ] Concurrency tests: running 1000 threads × 100 invocations of `TryMatch` shows no race conditions or deadlocks.

**Verification**:
- [ ] Run `dotnet test --filter TriggerFilterMatcher`.
- [ ] Integration test: verify that the seeded sample `!checkin` rule triggers `triggerCheckIn` from Twitch chat (end-to-end).
- [ ] Integration test: verify that the seeded `!so` mod-only rule triggers for moderators but is skipped for regular users.
- [ ] Integration test: verify that the Bits 100+ effect triggers for Bits=100 and is skipped for Bits=50.

**Dependencies**: Checkpoint γ

**Files**:
- `src/Vulperonex.Application/Workflows/Filters/ITriggerFilterMatcher.cs` (New)
- `src/Vulperonex.Application/Workflows/Filters/TriggerFilterMatcherRegistry.cs` (New)
- `src/Vulperonex.Application/Workflows/Filters/Matchers/*.cs` (7 matchers + fallback)
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs:242` (Refactored)
- `tests/Vulperonex.Tests.Unit/Workflows/Filters/*.cs`
- `tests/Vulperonex.Tests.Integration/Workflows/CheckInEndToEndTests.cs` (New)

**Scope**: L → Split into two PRs: (C.1a) registry + 4 matchers (`user.message`, `donated`, `subscribed`, `reward.redeemed`); (C.1b) remaining 3 matchers + fallback + integration tests for seeded rules.

---

### Checkpoint δ · Phase C Wrap-Up

- [ ] Verify that all 7 seeded sample rules (e.g., `!checkin`, `!so`, Bits 100+ effect) trigger successfully end-to-end.
- [ ] Boundary checks (`!so` ≠ `!sorry`, `MinAmount: 100` ≠ `Bits=50`) pass successfully.
- [ ] All unit and integration tests are green.
- [ ] Review with human operators.

---

### Phase D · WebUI UX Enhancement (Post-Gate)

#### Gate D · Pre-execution Checklist

- [x] **AQ-1 Resolved**: Adopt Reka UI (headless) + existing custom CSS (AD-6).
- [ ] **Reka UI PoC**: Run `pnpm add reka-ui` and build a minimal working example of a Drawer, Tabs, and Form.
  - Measure bundle size (before/after gzip, target +<200 KB, estimate ~30 KB).
  - **Design Tokens Integration Verification**: Verify that Reka primitive unstyled slots / data-attributes (`data-state`, `data-orientation`, etc.) can be hooked by existing CSS variable selectors, ensuring theme colors (dark/light, accent colors) are inherited completely without rewriting the token system.
  - List styling integration samples for at least 3 critical components (Drawer / Tabs / Dialog).
- [ ] Log PoC results in ADR `docs/zh-TW/adr/[N]-phase-d-ui-container-library.md`.
- [ ] If PoC bundle size exceeds limits → Fall back to Naive UI tree-shake → If still exceeding, hand-craft the components.
- [ ] If PoC token integration friction exceeds expectations → Evaluate Inspira UI styled recipes as a baseline.
- [ ] Phase B endpoints are live (D depends on B).

#### Task D.1: Introduce `RuleEditorDrawer.vue` Container (Replacing Full-Page View)

**Description**: Introduce a Drawer layout featuring three tabs (Basic / Action Steps / Error Handling), embedding the existing `WorkflowActionsEditor` and `WorkflowConditionsEditor`.

**Acceptance criteria**:
- [ ] Add an "Edit (New)" button to the list to open the Drawer.
- [ ] Retain the old "Edit" button to route to `RuleEditorView` (Advanced JSON Mode fallback).
- [ ] Switching between the three tabs inside the Drawer preserves edited data.

**Verification**:
- [ ] Run `pnpm test` (frontend).
- [ ] Manual: open Drawer → edit fields → save → reopen and verify persistence.

**Dependencies**: Gate D

**Files**:
- `src/frontend/src/components/admin/RuleEditorDrawer.vue` (New)
- `src/frontend/src/views/admin/RuleListView.vue`

**Scope**: M

---

#### Task D.2: Upgrade `TriggerEditor` to be Schema-Driven

**Description**: Pull `FilterFieldsFor(eventTypeKey)` from `/api/metadata/triggers` to render typed fields dynamically, replacing the generic key/value rows.

**Acceptance criteria**:
- [ ] Switching to `user.message` displays `CommandName` and `Prefix` typed inputs.
- [ ] Switching to `user.donated` displays a `MinAmount` number input.
- [ ] The editor renders only metadata-defined typed fields and does not depend on `migrationWarnings` or any legacy-filter cleanup path.

**Verification**:
- [ ] Frontend unit tests.
- [ ] Manual: Switch through event types and verify fields.

**Dependencies**: D.1 + Phase B.1

**Files**:
- `src/frontend/src/components/admin/TriggerEditor.vue`
- `src/frontend/src/stores/triggerMetadata.ts` (New)

**Scope**: M

---

#### Task D.3: Filter `VariablePicker` by Event Type

**Description**: Dynamically filter and display valid variables by pulling `ValidVariablesFor(eventTypeKey)` based on the active `eventTypeKey`.

**Acceptance criteria**:
- [ ] Under `user.message`, the picker only lists variables valid for this event type.
- [ ] Switching event types instantly updates the variable list.

**Verification**:
- [ ] Frontend unit tests.
- [ ] Manual: Switch event types and verify the filtered lists.

**Dependencies**: D.2

**Files**:
- `src/frontend/src/components/admin/VariablePicker.vue`
- `src/frontend/src/stores/triggerMetadata.ts`

**Scope**: S

---

#### Task D.4: Dynamic Action & Condition Loading from Backend

**Description**: Remove hardcoded definitions in `workflowEditor.ts`, pulling instead from `/api/metadata/actions` on startup. Retain a minimal hardcoded fallback to prevent empty interfaces during API failures.

**Acceptance criteria**:
- [ ] `workflowEditor.ts` contains no hardcoded definitions for the 15 actions.
- [ ] Adding a new action only requires modifying the BE record + metadata attribute; the FE harvests it automatically.
- [ ] The UI remains openable on API failures (displaying a fallback warning).

**Verification**:
- [ ] Frontend unit tests.
- [ ] Manual: mock API failures and verify fallback displays.

**Dependencies**: D.1 + Phase B.2

**Files**:
- `src/frontend/src/lib/workflowEditor.ts`
- `src/frontend/src/stores/actionMetadata.ts` (New)
- `src/frontend/src/components/admin/WorkflowActionsEditor.vue`
- `src/frontend/src/components/admin/WorkflowConditionsEditor.vue`

**Scope**: M

---

### Checkpoint ε · Phase D Wrap-Up

- [ ] Streamers can create the §1 sample rule in the new Drawer without writing JSON.
- [ ] Adding a new action to the backend automatically propagates to the frontend (no FE changes required).
- [ ] Bundle size remains within budget.
- [ ] The legacy `RuleEditorView` remains accessible as a fallback.
- [ ] Review with human operators.

---

### Phase E · Role Gating UX Enhancement (Parallelizable with Phase D)

#### Task E.1: Add "Common Role Restrictions" Shortcut to Basic Tab

**Description**: Add a role-chip selector (Broadcaster / Moderator / Subscriber / VIP / Everyone) to the Drawer's Basic tab. Checking a role directly pushes a `userRole` Condition to `Conditions[]` behind the scenes.

**Acceptance criteria**:
- [ ] The role-chip selector appears on the Basic tab.
- [ ] Checking a role pushes a corresponding `userRole` Condition to `Conditions[]`.
- [ ] Unchecking the role removes the Condition.

**Verification**:
- [ ] Frontend unit tests.
- [ ] Manual verification.

**Dependencies**: Phase D.1 (Embedded in the Drawer's Basic tab)

**Files**:
- `src/frontend/src/components/admin/RuleEditorDrawer.vue`
- `src/frontend/src/components/admin/RoleChipSelector.vue` (New)

**Scope**: S

---

#### Task E.2: Pin `userRole` to Top of Conditions Tab + Display Migration Chips

**Description**: Render the `userRole` Condition at the very top of the list in the Conditions tab. Scan `Conditions[]` and `MatchCondition` of existing rules; if they use `Member.IsModerator` or `Member.IsSubscriber` NCalc expressions, display a "Can be converted to UserRoleCondition" chip (guiding manual conversion, no automation).

**Acceptance criteria**:
- [ ] The `userRole` Condition renders at the first position of the Conditions list.
- [ ] Rules utilizing `Member.IsModerator` NCalc expressions display an orange migration chip.
- [ ] Clicking the chip opens a suggestion dialog without executing automatic changes.

**Verification**:
- [ ] Frontend unit tests.
- [ ] Manual: create a rule with a `Member.IsModerator` expression and verify that the chip is displayed.

**Dependencies**: E.1

**Files**:
- `src/frontend/src/components/admin/WorkflowConditionsEditor.vue`
- `src/frontend/src/lib/legacyRoleExpressionDetector.ts` (New)

**Scope**: S

---

#### Task E.3: Document Migration Mapping

**Description**: Write a short guide illustrating common NCalc role expressions and their corresponding `UserRoleCondition` configurations.

**Acceptance criteria**:
- [ ] Includes at least 4 common mappings (Broadcaster / Mod / Sub / VIP).

**Verification**:
- [ ] Code review.

**Dependencies**: None.

**Files**:
- `docs/phases/phase-8-workflow-rule-typed-filter/role-condition-migration.md` (New)

**Scope**: XS

---

### Checkpoint ζ · Phase E Wrap-Up

- [ ] Role chip selector UX operates smoothly.
- [ ] Rules using NCalc role expressions successfully display suggestion chips.
- [ ] The mapping documentation is live.
- [ ] Review with human operators.

---

## Final Checkpoint · All Systems Go

- [ ] Seeded sample `!checkin` rule triggers successfully end-to-end (the original §1 bug pattern is eradicated).
- [ ] `EventTypeKey` and `MatchCondition` each appear exactly once in the schema.
- [ ] The backend acts as the single source of truth for metadata, dynamically pulled by the frontend.
- [ ] Typed trigger filtering dispatch completely replaces generic dictionary matching.
- [ ] Drawer + Tabs editor UX is live.
- [ ] Role selector chips operate successfully.
- [ ] All unit and integration tests are green, and bundle size is within budget.
- [ ] Flag breaking changes in CHANGELOG / release notes (removal of inner fields and database wipe).
- [ ] Seeding is verified to be idempotent (skipped when the DB contains rules).

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Phase A.5 DB migration fails or corrupts rules | High | Run inside independent transactions, support dry-run modes, log JSON diffs, and keep backup tables for one release cycle. |
| Phase A.5 breaks existing API clients (CLI/plugins referencing inner fields) | Med | Allow the `JsonConstructor` to accept old fields but ignore them and emit warnings; flag breaking changes in release notes. |
| Phase C typed matcher dispatch breaks custom-crafted rules | Med | Fall back to generic matching and log warnings during the compatibility window. |
| Phase D Reka UI PoC exceeds bundle budget / styling efforts | Low | Fallback order: Naive UI tree-shake ＞ pure hand-crafting; Inspira UI styled recipes can significantly reduce styling work. |
| Metadata schema drifts (new actions missing attributes) | Low | Unit tests verify "reflection-derived count == validator allow-list count". |
| §5b.3 ALTER COLUMN table-lock on SQLite | Med | Split into two migrations (drop NOT NULL first, then backfill). |
| **SQLite ALTER TABLE limitations**: table rebuild succeeds in InMemory provider but fails on production SQLite engines | High | Force all A.5 migration tests to run against a real SQLite file provider; add an e2e SQLite migration gate to the CI pipeline. |
| **Matcher Registry runtime lock contention** | Med | Implement as Stateless Singleton matchers and a Singleton Registry utilizing `FrozenDictionary` without public runtime registration APIs; validated by concurrency tests. |
| **Reka UI design tokens integration friction** | Low-Med | Validate Gate D PoC with styling samples for at least 3 components; fall back to Inspira UI styled recipes. |
| Interaction between boot-seeding and Phase B/C | Low | Validate final checkpoint ensures seeding is idempotent. |

---

## Parallelization Map

| Parallelizable? | Targets |
|---|---|
| ✅ | Task 0.1 and all Phase A tasks. |
| ✅ | A5.1 / A5.2 / A5.3 (technically independent, but grouped under the same migration window). |
| ✅ | B.1 and B.2 (different providers). |
| ✅ | Phase D and Phase E (E can begin as soon as D.1 is completed). |
| ❌ | Phase B → Phase C (C depends on metadata whitelists). |
| ❌ | Phase B must not begin before Checkpoint β is cleared (DB migration window isolation). |
