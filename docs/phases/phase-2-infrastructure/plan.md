# Phase 2 Detailed Plan: Event Bus + Infrastructure

> Parent Plan: `tasks/plan.md` Phase 2
> Scope: Tasks 4-8 only
> Goal: Establish the event bus, SQLite/EF Core infrastructure, TDQ at-least-once delivery, member resolution, and system settings services. This ensures subsequent simulation, workflow, Web host, and CLI components can be built on stable Application ports and Infrastructure implementations.

---

## Execution Rules

- Develop each slice on a small branch. Commit immediately after verification. Use `git merge --ff-only` when merging back to `main`.
- For each behavioral requirement, write BDD-style Given / When / Then scenarios first, then implement using TDD RED / GREEN / REFACTOR.
- Application boundaries must adhere to light CQRS: separate commands/write ports from queries/read services. Infrastructure only implements Application/Adapter ports; do not leak EF Core types into the Domain or Application layers.
- Do not add new NuGet packages without prior inquiry and approval. If Task 5 requires adding EF Core SQLite / Design / Tools packages, verify if they already exist in `Directory.Packages.props`. If not, or if a version update is required, ask first.
- The `--no-build` flag is strictly reserved for commands that immediately follow a successful compilation within the same task.
- Keep `.claude/`, DB files, test outputs, and other local files out of commits.
- Phase 2 does not modify the core Domain event shape. If a Phase 1 port/DTO is found to be insufficient, complete it using a minimal Application contract first and maintain architectural tests.

---

## Dependency Order

```
Task 4a IStreamEventBus contract
    -> Task 4b InMemoryStreamEventBus dispatch
    -> Task 4c WaitForIdleAsync and test stability

Task 5a EF Core/SQLite packages and DbContext
    -> Task 5b InitialSchema and configuration
    -> Task 5c DB bootstrap and migration classifier

Task 4c + Task 5c
    -> Task 6a TDQ schema / repository
    -> Task 6b overflow / replay
    -> Task 6c ActionExecutionLog dedup / IClock

Task 5c
    -> Task 7a IMemberResolver port and atomic resolver
    -> Task 7b PlatformUserDisplayCache L1/L2
    -> Task 7c display cache TTL cleanup
    -> Task 8a SystemSettings service
    -> Task 8c OAuth token encryption / machine.key

Task 4c + Task 7c + Task 8a
    -> Task 8b settings hot reload + Task 4/7 runtime setting wiring
```

---

## Task 4a: Define IStreamEventBus Contract

**Description:** Create the event bus contract in the Application layer, clearly defining publish, subscribe, and test idle wait semantics.

**Acceptance Criteria:**
- [ ] `IStreamEventBus` is located in `Vulperonex.Application`.
- [ ] `PublishAsync(IStreamEvent, CancellationToken)` expresses fire-and-forget enqueue semantics.
- [ ] `IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task>)` supports `TEvent : IStreamEvent`. The returned subscription is used for module cleanup during `StopAsync`.
- [ ] `WaitForIdleAsync(CancellationToken)` serves only as a test/verification hook, not exposing handler error counts to callers.

**Verification:**
- [ ] The Application project compiles successfully.
- [ ] Architectural tests confirm that the Application layer does not reference the Infrastructure layer.

**Dependencies:** Task 2

**Files Likely Involved:**
- `src/Vulperonex.Application/EventBus/IStreamEventBus.cs`
- `tests/Vulperonex.Tests.Architecture/Dependencies/LayerDependencyTests.cs`

**Estimated Size:** S

---

## Task 4b: Implement InMemoryStreamEventBus Dispatch

**Description:** Implement the event bus using `Channel<IStreamEvent>` in the Infrastructure layer, featuring assignable matches, handler exception isolation, and a default capacity of 10,000.

**Acceptance Criteria:**
- [ ] `PublishAsync` enqueues and returns quickly without waiting for handlers to complete.
- [ ] `Subscribe<IStreamEvent>` receives all events; `Subscribe<UserSentMessageEvent>` receives only that specific type.
- [ ] Exceptions thrown by a single handler do not affect other handlers.
- [ ] Event types not of interest to a module are silently handled (no-op'ed) by the subscriber; the bus does not treat unknown events as errors.
- [ ] The Channel capacity defaults to 10,000; override settings will be wired in Task 8.

**Verification:**
- [ ] Unit test: Publish 5 events where one handler throws an exception, and the remaining handlers still receive their events.
- [ ] Unit test: `Subscribe<IStreamEvent>` receives a concrete event.
- [ ] Unit test: `Subscribe<UserSentMessageEvent>` does not receive `UserFollowedEvent`.
- [ ] Unit test: `PublishAsync` returns in < 10ms when a handler executes `Task.Delay(100ms)`.

**Dependencies:** Task 4a

**Files Likely Involved:**
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/EventBus/InMemoryStreamEventBusTests.cs`

**Estimated Size:** M

---

## Task 4c: Stabilize WaitForIdleAsync and Dispatch Lifecycle

**Description:** Complete the idle detection required for testing to ensure the queue is empty and all handlers have finished before resolving, while ensuring handler exceptions are only logged and not rethrown.

**Acceptance Criteria:**
- [ ] `WaitForIdleAsync` resolves once the queue is empty and all dispatched handlers have completed.
- [ ] Handler exceptions are caught and logged, not thrown through `WaitForIdleAsync`.
- [ ] Cancellation tokens can abort the wait.
- [ ] Tests do not rely on fixed sleep times; use idle hooks or deterministic synchronization instead.

**Verification:**
- [ ] Unit test: Call `WaitForIdleAsync` immediately after publishing; it resolves only when handlers complete.
- [ ] Unit test: `WaitForIdleAsync` still resolves after a handler throws an exception.
- [ ] Unit test: Aborting with a token terminates the wait and reports cancellation.

**Dependencies:** Task 4b

**Files Likely Involved:**
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/EventBus/InMemoryStreamEventBusTests.cs`

**Estimated Size:** S

---

## Task 5a: Establish EF Core / SQLite Foundation and DbContext

**Description:** Establish `VulperonexDbContext` and the entry point for the Infrastructure data layer, allowing tests to utilize SQLite fixtures to set up the database.

**Acceptance Criteria:**
- [ ] `VulperonexDbContext` is located in Infrastructure.
- [ ] The DbContext exposes DbSets or configuration entry points required for Phase 2: Members, PlatformIdentities, WorkflowRules, SystemSettings, AppLogs, PlatformUserDisplayInfo.
- [ ] EF Core types do not leak into Domain or Application contracts.
- [ ] SQLite test fixtures can establish transient databases.

**Verification:**
- [ ] Integration test: DbContext can open and establish a connection in a temp SQLite database.
- [ ] Architectural test: Application does not reference the EF Core provider or Infrastructure.

**Dependencies:** Task 3

**Files Likely Involved:**
- `src/Vulperonex.Infrastructure/Data/VulperonexDbContext.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/`
- `tests/Vulperonex.Tests.Integration/Infrastructure/SqliteFixture.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/VulperonexDbContextTests.cs`

**Estimated Size:** M

---

## Task 5b: InitialSchema Migration and Table Configuration

**Description:** Add the first batch of migrations and EF Core configurations covering Member, Workflow, Settings, Logs, and display cache schemas.

**Acceptance Criteria:**
- [ ] The `InitialSchema` migration can be generated and applied.
- [ ] The `SystemSettings` table contains `Key`, `Value`, `Category`, and `UpdatedAt`.
- [ ] The `WorkflowRules` table contains TEXT columns `ConditionsJson` and `ActionsJson`.
- [ ] `PlatformIdentities` has a unique constraint on `(Platform, PlatformUserId)`.
- [ ] `PlatformUserDisplayInfo` has a primary key on `(Platform, PlatformUserId)`.
- [ ] Migrations do not contain unreviewed destructive operations.

**Verification:**
- [ ] Integration test: Essential tables and indexes can be queried after applying the migration.
- [ ] Integration test: Creating duplicate `(Platform, PlatformUserId)` entries triggers a unique constraint or is handled safely via the resolver's `INSERT OR IGNORE`.

**Dependencies:** Task 5a

**Files Likely Involved:**
- `src/Vulperonex.Infrastructure/Migrations/`
- `src/Vulperonex.Infrastructure/Data/Configurations/MemberRecordConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/PlatformIdentityConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/WorkflowRuleConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/SystemSettingConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/AppLogConfiguration.cs`
- `src/Vulperonex.Infrastructure/Data/Configurations/PlatformUserDisplayInfoConfiguration.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/SchemaTests.cs`

**Estimated Size:** M

---

## Task 5c: DB Bootstrap, auto_vacuum, and MigrationClassifier

**Description:** Implement database migration bootstrap on startup and establish a migration safety gate that classifies destructive or review-required migrations using EF operations and raw SQL analysis.

**Acceptance Criteria:**
- [ ] Database bootstrap executes `PRAGMA auto_vacuum = FULL` before `MigrateAsync()`.
- [ ] `PRAGMA auto_vacuum` returns `2` after bootstrap.
- [ ] `MigrationClassifier` inspects `MigrationBuilder.Operations`.
- [ ] Raw SQL containing `DROP`, `DELETE`, `TRUNCATE`, or `RENAME` is classified as destructive.
- [ ] Raw SQL containing any `ALTER` statement is classified as review-required.
- [ ] Raw SQL must not bypass classification just because it is not an EF operation type.

**Verification:**
- [ ] Architecture/unit test: `DROP TABLE` raw SQL -> classified as destructive.
- [ ] Architecture/unit test: `RENAME TABLE` raw SQL -> classified as destructive.
- [ ] Architecture/unit test: `ALTER TABLE AddColumn` raw SQL -> classified as review-required.
- [ ] Architecture/unit test: `DELETE FROM` raw SQL -> classified as destructive.
- [ ] Architecture/unit test: `TRUNCATE` raw SQL -> classified as destructive.
- [ ] Integration test: `PRAGMA auto_vacuum = 2` after bootstrap and migration.

**Dependencies:** Task 5b

**Files Likely Involved:**
- `src/Vulperonex.Infrastructure/Data/DatabaseBootstrapper.cs`
- `src/Vulperonex.Infrastructure/Migrations/MigrationClassifier.cs`
- `tests/Vulperonex.Tests.Architecture/Migrations/MigrationClassifierTests.cs`
- `tests/Vulperonex.Tests.Integration/Infrastructure/DatabaseBootstrapperTests.cs`

**Estimated Size:** M

---

## Task 6a: Create TDQ and ActionExecutionLog Schema/Repository

**Description:** Create the persistence models and repositories required for TDQ and side-effect deduplication, completing data structures and basic CRUD first.

**Acceptance Criteria:**
- [ ] `TransientDeliveryQueue` can store event payload, event type, created/updated timestamps, and replay metadata.
- [ ] `ActionExecutionLog` schema contains a deduplication key, `Status` (`Pending`, `Completed`, `Failed`), and `AttemptCount`.
- [ ] `ActionExecutionLog` can query Completed/Failed/Pending states.
- [ ] The TDQ payload can carry a pre-generated `InvocationId`.

**Verification:**
- [ ] Integration test: Enqueueing a TDQ item allows the same payload to be read back.
- [ ] Integration test: `ActionExecutionLog` can insert pending, mark completed, and mark failed states.
- [ ] Unit/integration test: The `InvocationId` in the TDQ payload remains unchanged when read back.

**Dependencies:** Task 5c

**Files Likely Involved:**
- `src/Vulperonex.Infrastructure/EventBus/TransientDeliveryQueue.cs`
- `src/Vulperonex.Infrastructure/EventBus/ActionExecutionLog.cs`
- `src/Vulperonex.Infrastructure/Migrations/`
- `tests/Vulperonex.Tests.Integration/EventBus/TransientDeliveryQueueTests.cs`

**Estimated Size:** M

---

## Task 6b: Implement Channel Overflow and Startup Replay

**Description:** Upgrade the Task 4 TDQ stub to a full implementation: write to the TDQ when the Channel is full, replay unprocessed items upon startup, and remove them upon success.

**Acceptance Criteria:**
- [ ] Simulating a full Channel writes events to the TDQ without discarding them.
- [ ] Startup replay reads unprocessed TDQ items and republishes them.
- [ ] TDQ items are deleted after successful processing.
- [ ] Replay does not rely on event history persistence semantics; TDQ only saves pending delivery items.

**Verification:**
- [ ] Integration test: Forcing the Channel to be full writes events to the TDQ.
- [ ] Integration test: Recreating the bus/bootstrapping triggers replay of TDQ events.
- [ ] Integration test: TDQ items are deleted after successful replay.

**Dependencies:** Task 6a, Task 4c

**Files Likely Involved:**
- `src/Vulperonex.Infrastructure/EventBus/InMemoryStreamEventBus.cs`
- `src/Vulperonex.Infrastructure/EventBus/TdqReplayService.cs`
- `tests/Vulperonex.Tests.Integration/EventBus/TdqReplayTests.cs`

**Estimated Size:** M

---

## Task 6c: Implement ActionExecutionLog Deduplication and IClock

**Description:** Implement at-least-once side-effect deduplication protocol, featuring stale Pending retries, AttemptCount tracking, permanent failures, and fake clock testing.

**Acceptance Criteria:**
- [ ] The general action deduplication key is `(EventId, WorkflowRuleId, ActionIndex)`.
- [ ] The `InvokeSubWorkflowAction` deduplication key is `(EventId, WorkflowRuleId, ActionIndex, InvocationId)`. The `InvocationId` is generated before action execution and persisted in the payload.
- [ ] `Completed` or `Failed` log entries are skipped during replay.
- [ ] `Pending` entries with elapsed time > 30s are retried and their `AttemptCount` incremented.
- [ ] Entries with `AttemptCount >= MaxRetries+1` are marked as `Failed` and not retried further.
- [ ] The stale threshold uses `IClock` instead of hardcoding `DateTime.UtcNow`.

**Verification:**
- [ ] Integration test: Repeating execution with the same key skips the second attempt.
- [ ] Integration test (fake clock): Stale Pending > 30s triggers a retry and increments `AttemptCount`.
- [ ] Integration test: Reaching the `AttemptCount` cap marks the status as `Failed` and skips subsequent replays.
- [ ] Unit test: Replaying does not regenerate the `InvocationId`.

**Dependencies:** Task 6b

**Files Likely Involved:**
- `src/Vulperonex.Application/Time/IClock.cs`
- `src/Vulperonex.Infrastructure/Time/SystemClock.cs`
- `src/Vulperonex.Infrastructure/EventBus/ActionExecutionLogStore.cs`
- `tests/Vulperonex.Tests.Integration/EventBus/ActionExecutionLogTests.cs`

**Estimated Size:** M

---

## Task 7a: Implement IMemberResolver Port and Atomic Resolver

**Description:** Define the `IMemberResolver` port in the Application layer, and implement the atomic GetOrCreate using SQLite `INSERT OR IGNORE + SELECT` in the Infrastructure layer.

**Acceptance Criteria:**
- [ ] `IMemberResolver` is located in Application, returning only `MemberId` or an equivalent Application DTO without exposing EF entities.
- [ ] `MemberResolver` is located in Infrastructure.
- [ ] Concurrent resolution of the same `(Platform, PlatformUserId)` creates only one `MemberRecord`.
- [ ] `MemberId` is a ULID string.

**Verification:**
- [ ] Integration test: Resolving the same user concurrently across 10 tasks creates only 1 MemberRecord.
- [ ] Integration test: Returned MemberId conforms to the ULID format.
- [ ] Architectural test: Application does not reference the Infrastructure resolver.

**Dependencies:** Task 5c

**Files Likely Involved:**
- `src/Vulperonex.Application/Members/IMemberResolver.cs`
- `src/Vulperonex.Infrastructure/Members/MemberResolver.cs`
- `tests/Vulperonex.Tests.Integration/Members/MemberResolverTests.cs`

**Estimated Size:** M

---

## Task 7b: Implement PlatformUserDisplayCache L1/L2

**Description:** Implement `IPlatformUserInfoCache` at the Adapter-Infrastructure boundary, featuring an L1 LRU cache, L2 SQLite store, and display info replacement.

**Acceptance Criteria:**
- [ ] The `IPlatformUserInfoCache` contract is located in `Adapters.Abstractions`.
- [ ] `PlatformUserDisplayCache` is located in the Infrastructure or adapter infrastructure implementation layer; Application/Domain must not reference it.
- [ ] L1 miss -> L2 check -> platform fetch/update routing can be verified using test doubles.
- [ ] L1 capacity defaults to 500; settings overrides will be wired in Task 8.
- [ ] Display states like `TotalBitsGiven` use replacement semantics rather than delta accumulation.

**Verification:**
- [ ] Unit test: L1 hit does not query L2.
- [ ] Unit/integration test: L1 miss backfills L1 from L2.
- [ ] Unit test: State updates replace existing values with new absolute values.

**Dependencies:** Task 7a

**Files Likely Involved:**
- `src/Adapters/Vulperonex.Adapters.Abstractions/IPlatformUserInfoCache.cs`
- `src/Vulperonex.Infrastructure/Cache/PlatformUserDisplayCache.cs`
- `src/Vulperonex.Infrastructure/Cache/LruCache.cs`
- `tests/Vulperonex.Tests.Unit/Infrastructure/Cache/LruCacheTests.cs`
- `tests/Vulperonex.Tests.Integration/Cache/PlatformUserDisplayCacheTests.cs`

**Estimated Size:** M

---

## Task 7c: Complete Display Cache UpdateAsync and TTL Cleanup

**Description:** Complete cache miss default rows, `UpdateAsync` updater semantics, and a background worker for the 24h TTL cleanup.

**Acceptance Criteria:**
- [ ] `UpdateAsync` creates a default row for non-existent users: `AvatarUrl=null`, `ColorHex=null`, `Badges=Array.Empty<string>()`, `IsSubscriber=false`, `SubscriptionTier=null`, `TotalBitsGiven=0`, `FetchedAt=UtcNow`. It then applies the updater immediately so that the final return is not null.
- [ ] `UpdateAsync` applies the updater and persists changes for existing users.
- [ ] TTL defaults to 24h.
- [ ] Expired rows are cleared by a background worker.
- [ ] TTL remains a hardcoded default; settings overrides will be wired in Task 8.

**Verification:**
- [ ] Integration test: Cache miss `UpdateAsync` creates a default row without throwing exceptions.
- [ ] Integration test: Expired rows are deleted by the cleanup worker, while unexpired rows are preserved.
- [ ] Unit test: `Badges` defaults to an empty array instead of null.

**Dependencies:** Task 7b

**Files Likely Involved:**
- `src/Vulperonex.Infrastructure/Cache/PlatformUserDisplayCache.cs`
- `src/Vulperonex.Infrastructure/Cache/PlatformUserDisplayCacheCleanupWorker.cs`
- `tests/Vulperonex.Tests.Integration/Cache/PlatformUserDisplayCacheTests.cs`

**Estimated Size:** S

---

## Task 8a: Implement ISystemSettingsService SQLite-Backed Get/Set

**Description:** Establish the system settings Application port and SQLite-backed Infrastructure implementation, supporting typed get/set and default fallbacks.

**Acceptance Criteria:**
- [ ] `ISystemSettingsService` is located in Application.
- [ ] `Get<T>(key, default)` correctly deserializes types.
- [ ] `SetAsync` performs upserts into the `SystemSettings` table.
- [ ] `SystemSettingKey` constants include the following keys owned by Phase 2 or directly depended upon by subsequent MVP tasks:
   - `OAuthTwitchRefreshToken = "oauth.twitch.refresh_token"`
   - `StreamingPlatform = "streaming.platform"`
   - `BusChannelCapacity = "bus.channel_capacity"`
   - `OverlayDisplayCacheL1Capacity = "overlay.display_cache_l1_capacity"`
   - `OverlayDisplayCacheTtlHours = "overlay.display_cache_ttl_hours"`
   - `LogMinLevel = "log.min_level"`
   - `LogDbRetentionDays = "log.db_retention_days"`
   - `LogDbMaxSizeMb = "log.db_max_size_mb"`
   - `LogFileRetentionDays = "log.file_retention_days"`
   - (If subsequent tasks in 10/14/18 require additional runtime keys, they must expand `SystemSettingKey` in their respective tasks; free-text keys are not permitted)
- [ ] All keys are canonical lowercase; stored as lowercase in the database.
- [ ] REST security blocking for the protected namespace is deferred to Task 14b; this task only provides the service and token store foundation.

**Verification:**
- [ ] Unit/integration test: Querying a missing key returns the default value.
- [ ] Integration test: Get returns the typed value after Set.
- [ ] Unit/integration test: Keys are normalized to lowercase on write.
- [ ] Integration test: Updated timestamp changes.

**Dependencies:** Task 5c

**Files Likely Involved:**
- `src/Vulperonex.Application/Settings/ISystemSettingsService.cs`
- `src/Vulperonex.Application/Settings/SystemSettingKey.cs`
- `src/Vulperonex.Infrastructure/Settings/SystemSettingsService.cs`
- `tests/Vulperonex.Tests.Integration/Settings/SystemSettingsServiceTests.cs`

**Estimated Size:** M

---

## Task 8b: Settings Hot Reload Changes Observable

**Description:** Add `IObservable<SettingChangedEvent>` to the settings service and wire the Task 4/7 hardcoded defaults to Phase 2 runtime settings. Hot reloading of log levels is deferred to Task 18, but this task establishes the same notification mechanism.

**Acceptance Criteria:**
- [ ] `SetAsync` emits a `SettingChangedEvent` after writing to the database.
- [ ] Subscribers receive the key, old value/new value, or a payload sufficient for reloading.
- [ ] An exception in a subscriber does not interrupt the settings write operation.
- [ ] `InMemoryStreamEventBus` reads `bus.channel_capacity` on initialization, falling back to the 10,000 default from Task 4 if missing; updates to the setting apply to subsequent capacity/overflow thresholds.
- [ ] `PlatformUserDisplayCache` reads `overlay.display_cache_l1_capacity` and `overlay.display_cache_ttl_hours` on initialization, falling back to Task 7's 500 / 24h defaults if missing; settings changes update the L1 capacity and TTL.
- [ ] Subsequent Task 18 can override log levels using the same `Changes` mechanism, but Phase 2 does not implement a Serilog/log-level subscriber.

**Verification:**
- [ ] Unit/integration test: Subscribers receive notifications after `SetAsync`.
- [ ] Unit test: Setting writes do not fail when a subscriber throws an exception.
- [ ] Unit/integration test: The event bus capacity/overflow threshold utilizes the configured value; falls back to 10,000 if missing.
- [ ] Unit/integration test: The display cache utilizes the configured L1 capacity/TTL; falls back to 500 / 24h if missing.
- [ ] Unit/integration test: Modifying the display cache capacity trims the L1 cache or enforces the new capacity limit during subsequent inserts.

**Dependencies:** Task 8a, Task 4c, Task 7c

**Files Likely Involved:**
- `src/Vulperonex.Application/Settings/SettingChangedEvent.cs`
- `src/Vulperonex.Infrastructure/Settings/SystemSettingsService.cs`
- `tests/Vulperonex.Tests.Unit/Settings/SystemSettingsChangeTests.cs`

**Estimated Size:** S

---

## Task 8c: OAuth Token Encryption, machine.key, and IOAuthTokenStore

**Description:** Implement secure storage for OAuth refresh tokens: AES-256-GCM versioned envelopes, AAD bound to setting keys, machine.key creation/permissions, and decryption error handling.

**Acceptance Criteria:**
- [ ] `IOAuthTokenStore` is located in Application/Auth, defined as:
   - `StoreRefreshTokenAsync(string platform, string rawToken)` — Encrypts and upserts into SystemSettings.
   - `GetRefreshTokenAsync(string platform)` -> `string?` (Returns null if the key doesn't exist; throws `CredentialDecryptionException` on machine.key error/loss).
   - MVP only permits `"twitch"` as a platform value; other values throw `ArgumentException("Unknown OAuth platform: {platform}")`.
- [ ] Refresh tokens are stored in `SystemSettings.Value` as `"v1:" + Base64(nonce(12B) || ciphertext || tag(16B))`.
- [ ] Uses standard Base64; subsequent decoding uses `Convert.FromBase64String` instead of Base64Url.
- [ ] The AES-GCM key comes from `machine.key` raw 32 bytes in the OS app-data root, without KDF.
- [ ] AAD = setting key name UTF-8 bytes, passed to `AesGcm.Encrypt()`. Decryption passes the same key name; AAD is **not stored in the envelope** (binding ciphertext to the key name to prevent cross-key copy attacks); decryption fails on cross-key copies.
- [ ] Automatically generates 32 cryptographically random bytes if `machine.key` does not exist.
- [ ] Restricts file permissions immediately upon creation: Windows current user ACL FullControl with inheritance removed; Unix chmod 0600.
- [ ] Throws `IOException` and fails fast if chmod/ACL assignment fails.
- [ ] Throws `CredentialDecryptionException` on decryption failure due to a missing or incorrect machine.key, prompting the caller to re-authorize.
- [ ] The key path is locked to the OS app-data root, independent of `Database:Path`. Platform paths:
   - Windows: `%AppData%\Vulperonex\machine.key` (`Environment.SpecialFolder.ApplicationData`)
   - macOS: `~/Library/Application Support/Vulperonex/machine.key`
   - Linux: Follows `XDG_DATA_HOME` (defaults to `~/.local/share/Vulperonex/machine.key` if unset)
- [ ] `MachineKeyProvider` injects an `IFileSystem` abstraction (custom lightweight port or `System.IO.Abstractions` NuGet — if the latter, verify central version and ask first per SPEC §8.2 rules) to ensure unit tests can verify ACL/chmod behavior using fakes without actual I/O.

**Verification:**
- [ ] Unit test: `StoreRefreshTokenAsync("twitch", "raw-token")` -> `GetRefreshTokenAsync("twitch")` returns `"raw-token"` (round-trip verification).
- [ ] Unit test: `StoreRefreshTokenAsync("twitch", ...)` writes to the `SystemSettingKey.OAuthTwitchRefreshToken` key, with `SystemSettings.Category = "oauth"`.
- [ ] Unit test: `GetRefreshTokenAsync` returns `null` if the key is missing; throws `CredentialDecryptionException` on machine.key error (does not return null or crash).
- [ ] Unit test: `StoreRefreshTokenAsync("unknown-platform", ...)` throws `ArgumentException` (MVP only permits `"twitch"`).
- [ ] Unit test: Encrypting the same token twice produces different ciphertexts (nonce randomness).
- [ ] Unit test: Tampering with ciphertext/tag throws `CredentialDecryptionException`.
- [ ] Unit test: AAD cross-key copy (calling the low-level encrypt helper directly without `IOAuthTokenStore`) throws `CredentialDecryptionException`.
- [ ] Unit/integration test: The DB persisted value does not equal the raw refresh token.
- [ ] Unit test: `machine.key` is created with exactly 32 bytes.
- [ ] Integration test (temp dir): Non-existent machine.key is created with 32 bytes and restrictive OS permissions (Windows ACL user-only / Unix 0600).
- [ ] Platform-specific or abstract test: ACL/chmod failure throws `IOException`.

**Dependencies:** Task 8a

**Files Likely Involved:**
- `src/Vulperonex.Application/Auth/IOAuthTokenStore.cs`
- `src/Vulperonex.Application/Auth/CredentialDecryptionException.cs`
- `src/Vulperonex.Infrastructure/Auth/OAuthTokenStore.cs`
- `src/Vulperonex.Infrastructure/Security/MachineKeyProvider.cs`
- `tests/Vulperonex.Tests.Unit/Auth/OAuthTokenStoreTests.cs`
- `tests/Vulperonex.Tests.Integration/Auth/OAuthTokenStorePersistenceTests.cs`

**Estimated Size:** M

---

## Phase 2 Checkpoint

**Acceptance Criteria:**
- [ ] Tasks 4a-8c are completed and committed in small slices.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] End-to-end publish -> bus -> handler workflow passes.
- [ ] MigrationClassifier raw SQL destructive/review-required tests pass.
- [ ] DB bootstrap `PRAGMA auto_vacuum = 2` verification passes.
- [ ] TDQ overflow -> replay -> delete cycle passes.
- [ ] ActionExecutionLog Completed/Failed/Pending retry semantics pass.
- [ ] MemberResolver concurrent testing passes.
- [ ] Display cache miss `UpdateAsync` default row creation passes.
- [ ] AES-256-GCM tamper and AAD cross-key copy tests pass.
- [ ] Architectural tests confirm no Infrastructure/EF leaks in Domain/Application.
- [ ] `git status --short --ignored` displays only expected ignored local files.

**Review Threshold:**
- [ ] Manually review architectural layer dependencies before beginning Phase 3: confirm no Infrastructure references leaked into Domain/Application, EF Core types are not exposed, and TDQ/deduplication is indeed at-least-once safe.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|------|----------|
| EF Core / SQLite package or tool version mismatch | High | Confirm central package versions before Task 5; ask first before adding packages; use repo-local restore configurations. |
| EventBus fire-and-forget tests become flaky | Medium | Utilize `WaitForIdleAsync` and deterministic synchronization instead of fixed sleep times. |
| TDQ replay causes duplicate side effects | High | Complete `ActionExecutionLog` state machine and fake clock testing before wiring in the Workflow action executor. |
| SQLite concurrent write race conditions | Medium | Utilize `INSERT OR IGNORE + SELECT` in the resolver; cover with multi-task concurrent execution tests. |
| Token encryption cross-platform file permission differences | Medium | Abstract machine key path/permission logic; separate Windows ACL and Unix chmod testing. |

---

## Open Questions

- Does Task 5 already include references to EF Core SQLite / Design / Tools packages? If not, ask first and obtain approval before adding NuGet packages.
