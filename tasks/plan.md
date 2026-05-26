# Implementation Plan: Vulperonex MVP

> Based on docs/SPEC.md v0.3
> Created: 2026-05-11 / Last Updated: 2026-05-13

---

## Overview

Vulperonex is a stream automation tool designed to be platform-neutral. The MVP scope includes: Twitch events reception → Event Bus → WorkflowEngine executing rules → Overlay pushing SignalR updates → Photino desktop shell. The CLI can simulate events, manage configurations, manage rules (list/show/enable/disable/delete), and query members.

---

## Architectural Decision Records Summary

| # | Decision |
|---|---|
| A1 | Clean Architecture + tactical DDD: Domain → Application → Infrastructure / Adapters → Hosts; Domain owns entities, Value Objects, Domain Events, and invariants. |
| A2 | Event Bus: in-memory `Channel<IStreamEvent>`, 10,000 slots, overflowing into Transient Delivery Queue (TDQ, SQLite). |
| A3 | Member ID: ULID, composite unique key `PlatformIdentity (Platform, PlatformUserId)`. |
| A4 | WorkflowRule: JSON columns to store Conditions / Actions, EF Core 10 JSON mapping. |
| A5 | Dual ports are always loopback-only (`IPAddress.Loopback` + `IPAddress.IPv6Loopback`): API Port 5000, Overlay Port 5001. Neither requires authentication; Kestrel bind address serves as the safety boundary. |
| A6 | Testing approach: BDD scenarios define behavior, TDD red/green/refactor for implementation; Domain >90%, Application >80%. |
| A7 | Plugins: MVP uses static DI registration, without dynamic DLL scanning. |

---

## Global Implementation Rules

- Every behavioral requirement must first be written as a BDD-style Given / When / Then scenario.
- Scenarios must map directly to automated tests. The implementation workflow follows TDD: write a failing test, write the minimal implementation to pass, and refactor under green lights.
- Domain rules are implemented via tactical DDD: invariants reside in Domain/Application, repositories serve as Application ports, and EF/Core SDK payloads do not leak into the Domain.
- Application boundaries use light CQRS: commands change state via write repository ports, and queries return read DTOs via query service ports. The MVP does not introduce a command bus, event sourcing, or a separate read database.
- Photino, OBS, and browser runtime manual verification can be added, but they do not replace automated acceptance tests.
- Every automated test name must comply with `Given_<State>_When_<Action>_Then_<Expected>` (C#) or `should <expected> when <condition>` (Vitest), or contain a `// Given / When / Then` block. This rule is verified during each Checkpoint's code review.
- **DCI-inspired Role/Behavior Guidelines (SPEC §4.1b)**: When an Aggregate or Domain service accumulates multiple context-specific behaviors, they can be decoupled using Role/Behavior objects. Role/Behavior objects must contain pure Domain logic and must not depend on `DbContext`, EF Core, or any Infrastructure types. Context/Interaction resides in the Application use case (`*Context` / `*UseCase` types must not be defined in the Domain). The MVP does not implement runtime dynamic roles, reflection, or mixins. Architecture tests (`DciRoleIsolationTests`) verify that Role objects contain no Infrastructure references. Context/Interaction location rules are enforced via PR code reviews.

---

## Dependency Diagram

```
Vulperonex.Domain
    └── Vulperonex.Application
            ├── Vulperonex.Infrastructure               (Implements Application ports)
            ├── Vulperonex.Plugins.Abstractions         (Depends on Domain + Application)
            ├── Vulperonex.Adapters.Simulation          (Depends on Domain + Application + Adapters.Abstractions)
            ├── Vulperonex.Adapters.Twitch              (Depends on Domain + Application + Adapters.Abstractions)
            └── Hosts
                ├── Vulperonex.Web                      (Depends on all)
                ├── Vulperonex.Cli                      (Depends on all)
                └── Vulperonex.Desktop                  (Wraps Web)
Vulperonex.Adapters.Abstractions                        (IStreamEventSource, IPlatformUserInfoCache, etc.)
    ├── Vulperonex.Adapters.Simulation                  (Same as above, also depends on Application)
    ├── Vulperonex.Adapters.Twitch                      (Same as above, also depends on Application)
    └── (Future platform Adapters depend on Domain + Adapters.Abstractions + Application; IStreamEventBus is defined in Application, required to publish events)
frontend (Vue SPA)
    └── Served by Web/wwwroot, communicates with backend via SignalR + REST
```

---

## Task List

### Phase 1: Solution Skeleton + Domain Foundation

> Detailed Slice Plan: `docs/phases/phase-1-foundation/plan.md`

#### Task 1: Create Solution Structure and Project Skeleton

**Description:** Create the .NET Solution and all csproj files, configure project references, and verify that `dotnet build` compiles clean. No business logic included.

**Acceptance Criteria:**
- [ ] `dotnet build Vulperonex.sln` has no errors.
- [ ] Each csproj's `<ProjectReference>` aligns with the dependency diagram (Domain does not reference any other Vulperonex project).
- [ ] Architecture test project exists (NetArchTest configured).

**Verification Steps:**
- [ ] `dotnet build` → 0 errors, 0 warnings (except nullable warnings).
- [ ] `dotnet list reference` confirms no circular dependencies.

**Dependencies:** None

**Target Files:**
- `Vulperonex.sln`
- `src/Vulperonex.Domain/Vulperonex.Domain.csproj`
- `src/Vulperonex.Application/Vulperonex.Application.csproj`
- `src/Vulperonex.Infrastructure/Vulperonex.Infrastructure.csproj`
- `src/Vulperonex.Plugins.Abstractions/Vulperonex.Plugins.Abstractions.csproj`
- `src/Adapters/Vulperonex.Adapters.Abstractions/...csproj`
- `src/Adapters/Vulperonex.Adapters.Twitch/...csproj`
- `src/Adapters/Vulperonex.Adapters.Simulation/...csproj`
- `src/Hosts/Vulperonex.Web/...csproj`
- `src/Hosts/Vulperonex.Cli/...csproj`
- `src/Hosts/Vulperonex.Desktop/...csproj`
- `tests/Vulperonex.Tests.Unit/...csproj`
- `tests/Vulperonex.Tests.Integration/...csproj`
- `tests/Vulperonex.Tests.Architecture/...csproj`

**Scale:** L (Structural, but each file is small)

---

#### Task 2: Domain Core - IStreamEvent, Domain Events, StreamUser

**Description:** Implement all Domain-layer types: `IStreamEvent` interface, 7 MVP event records, `StreamUser` value object, `StreamEventKeys` constants, and `PlatformConnectionChangedEvent`. All are immutable records.

**Acceptance Criteria:**
- [ ] All 7 MVP events + `PlatformConnectionChangedEvent` implement `IStreamEvent`.
- [ ] `StreamEventKeys` includes all 7 canonical key constants + `PlatformConnectionChanged = "platform.connection_changed"`.
- [ ] `StreamUser` contains `Platform`, `UserId`, and `DisplayName`.
- [ ] Domain layer contains no references to Infrastructure / Adapters (verified by Architecture tests).
- [ ] All `EventId` defaults to `Ulid.NewUlid().ToString()`.

**Verification Steps:**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → `Domain_HasNoReferenceToTwitchSymbols` passes.
- [ ] `dotnet test tests/Vulperonex.Tests.Unit` → Domain event unit tests pass.

**Dependencies:** Task 1

**Target Files:**
- `src/Vulperonex.Domain/Events/IStreamEvent.cs`
- `src/Vulperonex.Domain/Events/StreamEventKeys.cs`
- `src/Vulperonex.Domain/Events/StreamEventDescriptions.cs`
- `src/Vulperonex.Domain/Events/UserSentMessageEvent.cs`
- `src/Vulperonex.Domain/Events/UserFollowedEvent.cs`
- `src/Vulperonex.Domain/Events/UserDonatedEvent.cs`
- `src/Vulperonex.Domain/Events/UserSubscribedEvent.cs`
- `src/Vulperonex.Domain/Events/UserGiftedSubscriptionEvent.cs`
- `src/Vulperonex.Domain/Events/ChannelRaidedEvent.cs`
- `src/Vulperonex.Domain/Events/RewardRedeemedEvent.cs`
- `src/Vulperonex.Domain/Events/PlatformConnectionChangedEvent.cs`
- `src/Vulperonex.Domain/StreamUser.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/`

**Scale:** M

---

#### Task 3: Domain - Member Entity and Value Objects

**Description:** Implement `MemberRecord`, `PlatformIdentity`, `LoyaltyInfo` domain entities. Define `IMemberRepository` (commands) and `IMemberQueryService` (queries) interfaces in the Application layer (light CQRS).

**Acceptance Criteria:**
- [ ] `MemberRecord` contains `MemberId` (ULID string), `Identities: List<PlatformIdentity>`.
- [ ] `IMemberRepository` resides in the Application layer (not in Domain).
- [ ] `IMemberQueryService` is independent of the Repository (CQRS separation).
- [ ] Architecture tests verify Application does not reference Infrastructure.
- [ ] Architecture test `DciRoleIsolationTests` verifies types in `Vulperonex.Domain` ending with `Role` or `Behavior` do not reference `Vulperonex.Infrastructure`, `Microsoft.EntityFrameworkCore`, or any `*.Infrastructure.*` namespace (SPEC §4.1b).

**Verification Steps:**
- [ ] `dotnet test tests/Vulperonex.Tests.Unit` → Member domain tests pass.
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → Layer dependencies + DciRoleIsolationTests pass.

**Dependencies:** Task 2

**Target Files:**
- `src/Vulperonex.Domain/Members/MemberRecord.cs`
- `src/Vulperonex.Domain/Members/PlatformIdentity.cs`
- `src/Vulperonex.Domain/Members/LoyaltyInfo.cs`
- `src/Vulperonex.Application/Members/IMemberRepository.cs`
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Members/`
- `tests/Vulperonex.Tests.Architecture/Domain/DciRoleIsolationTests.cs`

**Scale:** S

---

### Checkpoint: Phase 1

- [ ] `dotnet build` compiles clean.
- [ ] Architecture tests pass: Domain has no Infrastructure / Platform references.
- [ ] Domain unit test coverage > 90%.

---

### Phase 2: Event Bus + Infrastructure

> Detailed Slice Plan: `docs/phases/phase-2-infrastructure/plan.md`

#### Task 4: IStreamEventBus + In-Memory Implementation

**Description:** Define the `IStreamEventBus` interface in the Application layer and implement the `InMemoryStreamEventBus` based on `Channel<IStreamEvent>` in the Infrastructure layer. Features: handler exception isolation (try/catch per handler), `WaitForIdleAsync` for testing, and fire-and-forget semantics. The TDQ overflow mechanism exists as a stub (completed in Task 6).

**Acceptance Criteria:**
- [ ] `PublishAsync` is fire-and-forget, not blocking the caller.
- [ ] A single handler failure does not affect other handlers.
- [ ] `Subscribe<T>` uses **assignable match**: `Subscribe<IStreamEvent>` receives all events; `Subscribe<UserSentMessageEvent>` receives only that concrete type. `WorkflowModule`, `OverlayModule`, and `MemberModule` subscribe via `Subscribe<IStreamEvent>`.
- [ ] Modules ignore unconcerned event types gracefully with no-op, no exceptions, and no error logs (modules pattern match each event and fallback to a quiet default).
- [ ] `WaitForIdleAsync` resolves when the queue is empty and all handlers have finished; exceptions are caught and logged, not thrown through `WaitForIdleAsync`; callers cannot know if handlers erred from `WaitForIdleAsync`.
- [ ] Channel capacity defaults to 10,000 slots (hardcoded constant, overridden by `ISystemSettingsService` in Task 8).

**Verification Steps:**
- [ ] `dotnet test` → Event bus unit tests pass.
- [ ] Test: publish 5 events, one handler throws, the remaining 4 still receive their events.
- [ ] Test (assignable match): `Subscribe<IStreamEvent>` handler → publish `UserSentMessageEvent` → handler receives it.
- [ ] Test: `Subscribe<UserSentMessageEvent>` handler → publish `UserFollowedEvent` → handler is **not** called.
- [ ] Test (fire-and-forget timing): handler has `await Task.Delay(100ms)`; publishing `PublishAsync` returns in < 10ms (caller does not wait); `WaitForIdleAsync` resolves after completion.

**Dependencies:** Task 2

**Target Files:**
- `src/Vulperonex.Application/EventBus/IStreamEventBus.cs`
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/EventBus/`

**Scale:** M

---

#### Task 5: EF Core + SQLite + DB Migration Infrastructure

**Description:** Configure `VulperonexDbContext`, add EF Core 10 SQLite provider, and create the initial migrations (MemberRecord, PlatformIdentity, WorkflowRules, SystemSettings, AppLogs, PlatformUserDisplayInfo tables). Implement automatic additive migrations on startup.

**Acceptance Criteria:**
- [ ] `dotnet ef migrations add InitialSchema` succeeds.
- [ ] `MigrateAsync()` runs successfully in in-memory SQLite (or temp file SQLite) in tests.
- [ ] `SystemSettings` table contains Key, Value, Category, and UpdatedAt columns.
- [ ] `WorkflowRules` table contains Text columns `ConditionsJson` and `ActionsJson`.
- [ ] Database bootstrap runs `PRAGMA auto_vacuum = FULL` (before `MigrateAsync()`).
- [ ] Architecture test `MigrationClassifier` correctly identifies destructive migrations: raw SQL migrations using `migrationBuilder.Sql(...)` containing `DROP`, `DELETE`, `TRUNCATE`, `ALTER`, or `RENAME` (regex `\b(DROP|DELETE|TRUNCATE|ALTER|RENAME)\b`) must be classified as destructive/review-required; any `ALTER` is treated as review-required.

**Verification Steps:**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → MigrationClassifier tests pass.
- [ ] Unit test: `MigrationClassifier` flags raw SQL containing `DROP TABLE` as destructive.
- [ ] Unit test: `MigrationClassifier` flags raw SQL containing `RENAME TABLE` as destructive.
- [ ] Unit test: `MigrationClassifier` flags raw SQL containing `ALTER TABLE AddColumn` as review-required.
- [ ] Unit test: `MigrationClassifier` flags raw SQL containing `DELETE FROM` as destructive.
- [ ] Unit test: `MigrationClassifier` flags raw SQL containing `TRUNCATE` as destructive.
- [ ] Integration test: `VulperonexDbContext` can build and migrate.
- [ ] Integration test: bootstrap executes `PRAGMA auto_vacuum` and returns `2` (FULL).

**Dependencies:** Task 3

**Target Files:**
- `src/Vulperonex.Infrastructure/Data/VulperonexDbContext.cs`
- `src/Vulperonex.Infrastructure/Migrations/` (Auto-generated)
- `src/Vulperonex.Infrastructure/Data/Configurations/` (EF config per entity)
- `tests/Vulperonex.Tests.Architecture/Migrations/MigrationClassifierTests.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/`

**Scale:** M

---

#### Task 6: TDQ (Transient Delivery Queue) + At-Least-Once Guarantee

**Description:** Implement overflow handling for the event bus: write events to `TransientDeliveryQueue` table in SQLite when the Channel is full; replay pending items on startup; implement `ActionExecutionLog` table and a robust deduplication protocol (including `Failed` status, `AttemptCount`, permanent failure semantics, and `IClock` abstraction).

**Acceptance Criteria:**
- [ ] Events are written to TDQ instead of discarded when the Channel is full.
- [ ] Unprocessed events in TDQ are replayed on startup.
- [ ] Deduplication key: standard actions = `(EventId, WorkflowRuleId, ActionIndex)`; `InvokeSubWorkflowAction` = `(EventId, WorkflowRuleId, ActionIndex, InvocationId)` — **`InvocationId` must be generated and stored in the TDQ payload prior to action execution** (not generated dynamically as a new ULID on every retry) to ensure replay deduplication correctness.
- [ ] `ActionExecutionLog` schema contains `Status` (Pending/Completed/Failed) and `AttemptCount`.
- [ ] Stale Pending entries (> 30s, threshold injected via `IClock`) are retried, incrementing `AttemptCount`; `AttemptCount >= MaxRetries+1` → `Status=Failed` (permanent failure, skipped in subsequent replays).
- [ ] Log entries with `Status=Completed` or `Status=Failed` are skipped during replays.

**Verification Steps:**
- [ ] Integration test: force Channel full → events go to TDQ → restart → events are replayed.
- [ ] Integration test: executing the same key repeatedly → skipped on subsequent attempts (deduped, `Status=Completed`).
- [ ] Integration test (fake clock): stale Pending exceeding 30s → retry triggered (`AttemptCount` increments).
- [ ] Integration test: `AttemptCount` reaches `MaxRetries+1` → `Status=Failed` → not retried again.
- [ ] Unit test: `InvocationId` pre-persisted in TDQ payload is read back as the same ID on replay.

**Dependencies:** Task 4, Task 5

**Target Files:**
- `src/Vulperonex.Infrastructure/EventBus/TransientDeliveryQueue.cs`
- `src/Vulperonex.Infrastructure/EventBus/ActionExecutionLog.cs`
- `src/Vulperonex.Infrastructure/Migrations/` (TDQ + ActionExecutionLog migrations)
- `tests/Vulperonex.Tests.Integration/EventBus/`

**Scale:** M

---

#### Task 7: MemberResolver + PlatformUserDisplayCache (Infrastructure-only)

**Description:** Implement `MemberResolver` (`INSERT OR IGNORE + SELECT` atomic GetOrCreate) and `PlatformUserDisplayCache` (L1 in-memory LRU + L2 SQLite, `IPlatformUserInfoCache` interface). Includes `UserDisplayInfo` records and a TTL cleanup background worker. **Note: `PlatformUserDisplayCache` belongs strictly to the Adapter Infrastructure layer; Application/Domain are unaware of its existence. The adapter updates caches directly during event callbacks.**

**Acceptance Criteria:**
- [ ] Concurrent calls to `MemberResolver` do not create duplicate `MemberRecord` entries (ULID unique).
- [ ] L1 miss → L2 check → Platform API fetch works as expected.
- [ ] L1 capacity defaults to 500 slots (hardcoded constant, not depending on Task 8).
- [ ] TTL defaults to 24 hours, expired entries cleared by background worker.

**Verification Steps:**
- [ ] `dotnet test tests/Vulperonex.Tests.Integration` → SC-8 passes (ULID format for `MemberId`).
- [ ] Concurrent test: 10 Tasks concurrently resolving the same PlatformUser → only 1 `MemberRecord` created.
- [ ] Unit test (`UpdateAsync` cache miss): updating non-existent user → creates default cache row (`Badges` empty, all nullable fields null, `FetchedAt = UtcNow`) without throwing.

**Dependencies:** Task 5

**Target Files:**
- `src/Vulperonex.Application/Members/IMemberResolver.cs` (Port interface, defines `ResolveAsync`)
- `src/Vulperonex.Infrastructure/Members/MemberResolver.cs` (EF Core + raw SQL implementation in Infrastructure)
- `src/Vulperonex.Infrastructure/Cache/PlatformUserDisplayCache.cs`
- `src/Vulperonex.Infrastructure/Cache/LruCache.cs`
- `tests/Vulperonex.Tests.Integration/Members/`

**Scale:** M

---

#### Task 8: SystemSettings Service + Configuration Hot Reload

**Description:** Implement `ISystemSettingsService`: SQLite-backed Get/Set, `IObservable<SettingChangedEvent>` change notifications, and a three-tier configuration structure (appsettings.json / SystemSettings table / encrypted OAuth tokens).

**Acceptance Criteria:**
- [ ] `Get<T>(key, default)` deserializes correctly.
- [ ] `SetAsync` writes to DB and triggers the `Changes` observable.
- [ ] Subscribers receive notifications immediately upon setting changes without restarting.
- [ ] OAuth refresh token encrypted using **AES-256-GCM** in SQLite: versioned envelope `"v1:" + Base64(nonce(12B) || ciphertext || tag(16B))` stored in `SystemSettings.Value TEXT`; raw key is machine.key 32 bytes (no KDF); setting key is `SystemSettingKey.OAuthTwitchRefreshToken` (`"oauth.twitch.refresh_token"`); **AAD is the setting key UTF-8 bytes** (`"oauth.twitch.refresh_token"`), passed to `AesGcm.Encrypt()`, verified on decryption. AAD is not stored in the envelope, binding ciphertext to the key name to prevent cross-key copying.
- [ ] Startup automatically generates a `machine.key` (cryptographically random 32 bytes) under the OS app-data path if missing, applying restrictive permissions immediately (Windows: Current user FullControl, inheritance disabled; Unix: chmod 0600); fail-fast with `IOException` on chmod/ACL failures. Path is fixed to OS app-data root (Windows: `%AppData%\Vulperonex\`; macOS: `~/Library/Application Support/Vulperonex/`; Linux: `~/.local/share/Vulperonex/`), not following `Database:Path` configurations.
- [ ] If `machine.key` is missing (e.g., migration to new device) → AES decryption fails → throw `CredentialDecryptionException` (prompting user to re-authorize, no crash).

**Verification Steps:**
- [ ] Unit test: reading a set key immediately returns the new value.
- [ ] Unit test: subscribing to Changes → SetAsync → subscriber receives `SettingChangedEvent`.
- [ ] Integration test (temp dir): missing `machine.key` → `MachineKeyProvider` creates a 32-byte key with restricted permissions (Windows ACL / Unix 0600).
- [ ] Integration test (chmod failure simulation): ACL/chmod failure → `MachineKeyProvider` throws `IOException`.
- [ ] Unit test (AAD cross-key copy attack): direct encryption helper call: encrypt token with AAD=`"oauth.twitch.refresh_token"` → attempt decryption with AAD=`"oauth.unknown.refresh_token"` → throws `CredentialDecryptionException`.
- [ ] Unit test: decrypting with a wrong key → throws `CredentialDecryptionException`.
- [ ] Unit test: `StoreRefreshTokenAsync("twitch", "raw-token")` → `GetRefreshTokenAsync("twitch")` returns `"raw-token"`.
- [ ] Unit test: `StoreRefreshTokenAsync` writes to the correct key, with category set to `"oauth"`.
- [ ] Unit test: verifying database value is encrypted (does not equal raw token).
- [ ] Unit test: GCM authentication tag validation fails if ciphertext bytes are altered → throws `CredentialDecryptionException`.
- [ ] Unit test: consecutive `StoreRefreshTokenAsync` calls produce different values (verifying random nonce uniqueness).
- [ ] Unit test: `StoreRefreshTokenAsync` with an unknown platform → throws `ArgumentException` (MVP only allows `"twitch"`).

**Dependencies:** Task 5

**Target Files:**
- `src/Vulperonex.Application/Settings/ISystemSettingsService.cs`
- `src/Vulperonex.Application/Settings/SystemSettingKey.cs` (Includes key constants like `OAuthTwitchRefreshToken`, `StreamingPlatform`, `LogMinLevel`, `LogDbRetentionDays`, etc.)
- `src/Vulperonex.Application/Auth/IOAuthTokenStore.cs` (Interface: `StoreRefreshTokenAsync(platform, rawToken)`, `GetRefreshTokenAsync(platform)`)
- `src/Vulperonex.Infrastructure/Settings/SystemSettingsService.cs`
- `src/Vulperonex.Infrastructure/Security/MachineKeyProvider.cs`
- `src/Vulperonex.Infrastructure/Auth/OAuthTokenStore.cs`
- `tests/Vulperonex.Tests.Unit/Settings/`
- `tests/Vulperonex.Tests.Unit/Auth/`

**Scale:** M

---

### Checkpoint: Phase 2

- [ ] `dotnet test` passes.
- [ ] Integration test: publishing event → bus → handler receives.
- [ ] DB migration succeeds in temporary SQLite.
- [ ] MemberResolver concurrent resolution tests pass.

---

### Phase 3: Simulation Adapter + WorkflowEngine (Vertical Slice 1)

#### Task 9: Simulation Adapter + IStreamEventTypeRegistry

**Description:** Implement `SimulationAdapter` (`IStreamEventSource`) — publishes `IStreamEvent` upon receiving CLI / test requests. Implement `IStreamEventTypeRegistry` (all Adapters call `Register` for their EventTypeKeys during `StartAsync`). **Note:** `SimulationAdapter` supports publishing all 7 MVP events (for tests); REST/CLI `POST /api/simulate/{alias}` only exposes `chat/follow/sub` aliases (Task 14b). Other events are only tested via direct mock simulation calls.

**Acceptance Criteria:**
- [ ] `SimulationAdapter` can publish all 7 MVP events.
- [ ] `SimulationAdapter` contains no references to `Vulperonex.Adapters.Twitch` types (SC-3).
- [ ] `IStreamEventTypeRegistry.IsKnown(key)` returns true after Adapter `StartAsync`.
- [ ] Duplicate registrations are idempotent (first wins, log warning on different descriptions).
- [ ] `Register("platform.connection_changed", ..., isSystemEvent: true)` is excluded from `GetAll()` (system events filtered).
- [ ] Architecture test SC-4 passes.

**Verification Steps:**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture` → SC-3, SC-4 pass.
- [ ] Integration test: simulating `UserSentMessageEvent` → bus handler receives it.
- [ ] Unit test (idempotency first-wins): register `user.message` with descA, then descB → description remains descA, warning logged, only one entry in `GetAll()`.
- [ ] Unit test (idempotent same key + same description): same parameters → no warnings, only one entry.

**Dependencies:** Task 4

**Target Files:**
- `src/Adapters/Vulperonex.Adapters.Abstractions/IStreamEventSource.cs` (New)
- `src/Adapters/Vulperonex.Adapters.Simulation/SimulationAdapter.cs`
- `src/Vulperonex.Application/EventTypes/IStreamEventTypeRegistry.cs`
- `src/Vulperonex.Application/EventTypes/StreamEventTypeRegistry.cs`
- `tests/Vulperonex.Tests.Architecture/Adapters/SimulationAdapterIsolationTests.cs`
- `tests/Vulperonex.Tests.Integration/Adapters/`

**Scale:** M

---

#### Task 10: WorkflowEngine - Condition Evaluation + Basic Actions

**Description:** Implement `WorkflowEngine` (`IHostedService`, subscribing to bus): load `WorkflowRule` entries, evaluate triggers, evaluate conditions (`UserRole`, `MessageContent`, `Cooldown`), and execute actions (`SendChatMessage`, `InvokeSubWorkflow`). Features priority ordering, Serial/Parallel execution modes, and per-action ErrorBehavior/Timeout.

**Acceptance Criteria:**
- [ ] `UserSentMessageEvent` matching a rule invokes `IPlatformChatSender.SendAsync` (SC-2).
- [ ] Short-circuiting conditions: first failed condition stops evaluation.
- [ ] `CooldownCondition` global and per-user timers are tracked correctly.
- [ ] `StopOnError` halts subsequent action executions.
- [ ] `RetryOnError` + backoff retries up to `MaxRetries` times.
- [ ] Action timeout uses `CancellationToken` signaling instead of thread abortion.

**Verification Steps:**
- [ ] `dotnet test` → SC-2 passes.
- [ ] Unit test: SC-9 (TargetPlatform override / default source platform).
- [ ] Unit test: `SendChatMessageAction` with an unregistered platform target → skip action, log warning, does not throw.
- [ ] Integration test: publish event → WorkflowEngine → IPlatformChatSender mock receives (using `InMemoryWorkflowRuleRepository` fake, not depending on Task 14a's EF Core implementation).

**Dependencies:** Task 4, Task 5, Task 9

**Target Files:**
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- `src/Vulperonex.Application/Workflows/Conditions/`
- `src/Vulperonex.Application/Workflows/Actions/SendChatMessageAction.cs`
- `src/Vulperonex.Application/Workflows/Actions/InvokeSubWorkflowAction.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowRuleRepository.cs`
- `src/Vulperonex.Application/Workflows/IWorkflowRuleQueryService.cs`
- `src/Vulperonex.Application/Workflows/Dtos/WorkflowRuleSummaryDto.cs`
- `tests/Vulperonex.Tests.Unit/Workflows/`
- `tests/Vulperonex.Tests.Integration/Workflows/`

**Scale:** L

---

#### Task 11: Plugin System - IVulperonexPlugin + InvokePluginAction

**Description:** Implement `IVulperonexPlugin`, `IPluginContext`, and `IPluginActionContext` contracts (`Plugins.Abstractions`), and the executor for `InvokePluginAction` in the `WorkflowEngine`. Handles: missing plugins (warning + skip, no crash), passing `ActionExecutionKey` to plugins, and static DI registration.

**Acceptance Criteria:**
- [ ] Plugins can publish custom events via `IPluginContext.Events.PublishAsync`.
- [ ] `InvokePluginAction` inside a `WorkflowRule` triggers `ExecuteActionAsync` of the registered plugin.
- [ ] Missing plugins trigger warning logs and skip execution (no crash).
- [ ] SC-10 passes (plugin publishes event → triggers rule → `SendAsync` receives).
- [ ] `IPluginActionContext.Params` type is `IReadOnlyDictionary<string, JsonElement>`; parameters are read safely via `.GetString()`, `.GetInt32()`, `.GetBoolean()` without throwing `InvalidCastException`.

**Verification Steps:**
- [ ] `dotnet test` → SC-10 passes.
- [ ] Architecture tests verify `Plugins.Abstractions` only depends on Domain + Application.
- [ ] Architecture tests verify `IPluginContext` and `IPluginActionContext` do not expose `System.IServiceProvider` (ensuring no service locator leak).

**Dependencies:** Task 10

**Target Files:**
- `src/Vulperonex.Plugins.Abstractions/IVulperonexPlugin.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginContext.cs`
- `src/Vulperonex.Plugins.Abstractions/IPluginActionContext.cs`
- `src/Vulperonex.Application/Workflows/Actions/InvokePluginAction.cs`
- `tests/Vulperonex.Tests.Unit/Plugins/`
- `tests/Vulperonex.Tests.Integration/Plugins/`

**Scale:** M

---

### Checkpoint: Phase 3

- [ ] `dotnet test` → SC-2, SC-3, SC-4, SC-9, SC-10 pass.
- [ ] End-to-end integration flow: SimulationAdapter → Bus → WorkflowEngine → IPlatformChatSender mock.

---

### Phase 4: Twitch Adapter + MemberModule (Vertical Slice 2)

#### Task 12: Twitch Adapter - IRC + EventSub + DisplayHints

**Description:** Implement `TwitchAdapter`: Twitch IRC WebSocket (chat messages) + Twitch EventSub WebSocket (follows, subs, bits, raids, rewards). All Twitch payloads map to Domain Events (no Twitch type leak into Domain). The adapter registers EventTypeKeys during `StartAsync`. Features DisplayHints enrichment (`display.segments`, `user.avatar`, etc.), blocking raw HTML. Exponential backoff reconnection with jitter (max 60s, ±20% jitter).

**Acceptance Criteria:**
- [ ] SC-1 passes: mock Twitch payload produces all 7 MVP `IStreamEvent` entries.
- [ ] SC-6 (WorkflowEngine half): SimulationAdapter and TwitchAdapter (mock IRC) trigger equivalent WorkflowEngine side effects (`SendAsync` called with identical parameters).
- [ ] `display.segments` contains strictly text/emote/badge/mention types, no raw HTML.
- [ ] `display.color` only accepts 6-digit RGB hex; badge ID/value normalization has length and character limits.
- [ ] `StartAsync` is idempotent (double-start does not create redundant sockets).
- [ ] Reconnections back off exponentially (1s → 2s → ... max 60s, with ±20% jitter).
- [ ] EventSub duplicate deliveries are deduplicated via a cache (1000 entries or 10-minute TTL); replays within the 10-minute window are not filtered out by replay flags.
- [ ] Connection state changes publish `PlatformConnectionChangedEvent`.
- [ ] OAuth PKCE callback port listens on `Auth:CallbackPort` (default 7979), retrying up to 3 times (7980, 7981) on conflicts.
- [ ] OAuth callback listener only accepts loopback remote IPs and matching `localhost` / `127.0.0.1` / `[::1]` Host headers; `state` has a 10-minute TTL and is single-use.
- [ ] OAuth PKCE code exchange keeps access tokens in-memory only; `StartAsync` exchanges the encrypted refresh token on startup if present.

**Verification Steps:**
- [ ] `dotnet test` → SC-1, SC-6 WorkflowEngine half pass.
- [ ] Unit test: Twitch IRC message parsing → `UserSentMessageEvent`.
- [ ] Unit test: invalid/expired `state` fails code exchange, logging a warning.
- [ ] Unit test: OAuth callback listener boundary checks (remote IPs, host headers, non-default paths, single-use).
- [ ] Unit test: port conflict auto-increment.
- [ ] Unit test: port exhaustion handles failure gracefully, prompting redirect URI update.
- [ ] Unit test: access tokens kept strictly in-memory (no settings saves, no logs).
- [ ] Unit test: refresh token encryption is invoked.
- [ ] Integration test: `TwitchAdapter` registers all 7 keys + system events in `IStreamEventTypeRegistry`.

**Dependencies:** Task 10, Task 9, Task 8, Task 7

**Target Files:**
- `src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Auth/OAuthCallbackListener.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Irc/TwitchIrcClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/EventSub/TwitchEventSubClient.cs`
- `src/Adapters/Vulperonex.Adapters.Twitch/Mapping/`
- `src/Adapters/Vulperonex.Adapters.Abstractions/IPlatformUserInfoCache.cs`
- `src/Vulperonex.Application/Auth/IOAuthTokenStore.cs`
- `src/Hosts/Vulperonex.Web/appsettings.json`
- `tests/Vulperonex.Tests.Unit/Adapters/Twitch/`
- `tests/Vulperonex.Tests.Integration/Adapters/`

**Scale:** L

---

#### Task 13: MemberModule + Overlay DTO Safety Filtering

**Description:** Implement `MemberModule` (`IHostedService`): subscribe to Domain Events, call `MemberResolver` to create/update `MemberRecord` entries. **`PlatformUserDisplayCache` updates are handled strictly by the adapter**; `MemberModule` has no reference to `IPlatformUserInfoCache` or `Vulperonex.Adapters.Abstractions`. Implement DTO projections for `OverlayModule` to ensure Overlay DTOs only expose whitelisted fields (no MemberId leaks).

**Acceptance Criteria:**
- [ ] SC-8 passes: `UserSentMessageEvent` publishes → creates platform identity, `MemberId` is ULID format.
- [ ] SC-6 (MemberRecord half): Simulation and Twitch adapters produce equivalent database states.
- [ ] `UserSubscribedEvent` updates `IsSubscriber` in `MemberRecord`.
- [ ] Member state replays use `(platform, sourceEventId)` for deduplication; replays do not create redundant rows or duplicate accumulations.
- [ ] Overlay DTO `/overlay/chat` serialized properties strictly match whitelist: `{schemaVersion, eventId, timestamp, displayName, colorHex, segments, badges}` (no `MemberId`, `UserId`).
- [ ] Overlay DTO `/overlay/alerts` serialized properties strictly match whitelist: `{schemaVersion, eventId, timestamp, displayName, eventType, tier}`.
- [ ] Overlay DTO `/overlay/member` serialized properties strictly match whitelist: `{schemaVersion, displayName, avatarUrl, checkInCount}`.
- [ ] `schemaVersion` is fixed to `1`; `eventId` is a public delivery ID (not MemberId or PlatformUserId).

**Verification Steps:**
- [ ] `dotnet test tests/Vulperonex.Tests.Integration` → SC-8 passes.
- [ ] Integration test: SC-6 MemberRecord half — executes Sim and Twitch runs in fresh, isolated SQLite databases and asserts equivalent S1 == S2 snapshots.
- [ ] Unit test: reflectively validates that all serializable properties of Overlay DTO types exactly match their whitelists.

**Dependencies:** Task 7, Task 10, Task 12

**Target Files:**
- `src/Vulperonex.Application/Members/MemberModule.cs`
- `src/Vulperonex.Application/Overlay/OverlayModule.cs`
- `src/Vulperonex.Application/Overlay/Dtos/` (OverlayChatPayload, OverlayAlertPayload, OverlayMemberPayload)
- `tests/Vulperonex.Tests.Unit/Overlay/`
- `tests/Vulperonex.Tests.Integration/Members/`

**Scale:** M

---

### Checkpoint: Phase 4

- [ ] `dotnet test` → SC-1, SC-6a, SC-6b, SC-8 pass.
- [ ] Twitch IRC mock triggers member record creation.
- [ ] Overlay DTO whitelists strictly enforced.

---

### Phase 5: Web Host + SignalR + CLI

#### Task 14a: ASP.NET Minimal API - WorkflowRule CRUD + EventTypes

**Description:** Implement `Vulperonex.Web` WorkflowRule REST APIs and the event-types endpoint: WorkflowRule CRUD operations, validation of event-type registries, static analysis of circular sub-workflow references during saves, i18n error codes (no raw English text), and CQRS separation. Shared write path: CLI and UI both route through the REST API.

**Acceptance Criteria:**
- [ ] `GET /api/rules` → returns `WorkflowRuleSummaryDto` list (queries `IWorkflowRuleQueryService`).
- [ ] `GET /api/rules/{id}` returns the complete rule JSON.
- [ ] Missing rule returns 404 + `WORKFLOW_RULE_NOT_FOUND`.
- [ ] `POST /api/rules` saves a valid rule.
- [ ] Unknown `EventTypeKey` during rule POST → returns 400 + `UNKNOWN_EVENT_TYPE_KEY` (validator checks `IsKnownForWorkflow(key)`, excluding system events).
- [ ] Circular sub-workflow references → 400 + `CIRCULAR_WORKFLOW_REFERENCE`.
- [ ] `PUT /api/rules/{id}` mismatch → 400 + `INVALID_RULE_ID_MISMATCH`.
- [ ] `POST` success → returns **201 Created** with `Location` header.
- [ ] `DELETE` success → returns **204 No Content**.
- [ ] CQRS separation: GET requests route through `IWorkflowRuleQueryService`, write requests route through `IWorkflowRuleRepository`.
- [ ] Unknown action/condition types return 400 + `UNKNOWN_ACTION_TYPE` / `UNKNOWN_CONDITION_TYPE`.
- [ ] Missing required action parameters (e.g., `Template` in `SendChatMessage`) → 400 + `ACTION_MISSING_REQUIRED_PARAM`.

**Dependencies:** Task 10, Task 9, Task 5

**Target Files:**
- `src/Hosts/Vulperonex.Web/Endpoints/WorkflowRuleEndpoints.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/EventTypeEndpoints.cs`
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Program.cs`
- `src/Vulperonex.Infrastructure/Workflows/WorkflowRuleRepository.cs`
- `src/Vulperonex.Infrastructure/Workflows/WorkflowRuleQueryService.cs`
- `tests/Vulperonex.Tests.Integration/Web/`
- `tests/Vulperonex.Tests.Integration/Web/WorkflowRuleCqrsInteractionTests.cs`

**Scale:** M

---

#### Task 14b: ASP.NET Minimal API - Simulate / Config / Member Endpoints

**Description:** Implement additional endpoints for CLI interaction:
- **Simulate**: `POST /api/simulate/{eventType}` (invokes `ISimulationAdapter`). Accepts only short aliases: `chat`→`user.message`, `follow`→`user.followed`, `sub`→`user.subscribed` to avoid conflicts.
- **Config**: `GET|PUT /api/config/{key}` (invokes `ISystemSettingsService`). Validates: (1) prefix denylist first (`security.*` / `oauth.*` → 403), (2) registry lookup (unknown key → 400).
- **Member**: `GET /api/members`, `GET /api/members/{id}`.

**Acceptance Criteria:**
- [ ] `POST /api/simulate/chat` triggers `ISimulationAdapter.SimulateAsync`.
- [ ] `GET|PUT /api/config/security.*` → 403 + `CONFIG_KEY_SECURITY_NAMESPACE`.
- [ ] `GET|PUT /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE`.
- [ ] Unknown `oauth.*` keys return 403 (denylist evaluated first).
- [ ] `GET /api/members` accepts a limit parameter (default 50, max 200).

**Dependencies:** Task 14a, Task 9, Task 8, Task 7

**Scale:** M

---

#### Task 15: SignalR Hub + Overlay Push + Dual-Port Kestrel

**Description:** Implement `/hubs/events` SignalR Hub (admin UI) and `/hubs/overlay/*` Overlay Hubs (OBS). Configure Kestrel dual ports (ApiPort 5000 / OverlayPort 5001) with automatic increment pairs on conflict, default loopback-only binding.

**Acceptance Criteria:**
- [ ] SC-5 passes: chat overlay client receives payload within 5s of event publishing.
- [ ] Overlay port (5001) does not require authentication.
- [ ] Bindings use both IPv4 (`127.0.0.1`) and IPv6 (`::1`) loopback addresses.
- [ ] Overlay hub JSON payloads strictly match whitelists (no MemberId leaks).

**Dependencies:** Task 13, Task 14a

**Scale:** M

---

#### Task 16: CLI - simulate / config / member / rule Commands

**Description:** Implement CLI commands and REPL: `simulate`, `config`, `member`, `rule`, and `twitch auth`. The CLI communicates exclusively via HTTP REST APIs (no direct database access). Correct outputs route to stdout; errors (4xx/5xx) route to stderr with exit code 1. Database path resolves from `appsettings.json` `Database:Path` or uses default OS app-data directories. Web host and CLI must access the same database.

**Dependencies:** Task 14b, Task 15

**Scale:** M

---

### Checkpoint: Phase 5

- [ ] SC-2, SC-5, SC-8, SC-9 pass.
- [ ] Rules CRUD and circular reference validation work.
- [ ] Config namespace protection and CLI integration verified.
