# Phase 2 Todo List: Event Bus + Infrastructure

> Detailed Plan: `docs/phases/phase-2-infrastructure/plan.md`
> Parent Todo List: `tasks/todo.md`

---

## Task 4: Event Bus

- [x] Task 4a: Define `IStreamEventBus` contract
- [x] Task 4b: Implement `InMemoryStreamEventBus` dispatch, assignable matches, and handler isolation
- [x] Task 4c: Stabilize `WaitForIdleAsync` and dispatch lifecycle

## Task 5: EF Core + SQLite Infrastructure

- [x] Task 5a: Establish EF Core / SQLite foundation and `VulperonexDbContext`
- [x] Task 5b: Add `InitialSchema` migration and table configurations
- [x] Task 5c: Implement DB bootstrap, `PRAGMA auto_vacuum = FULL`, and `MigrationClassifier`

## Task 6: TDQ + at-least-once Guarantees

- [x] Task 6a: Create TDQ and `ActionExecutionLog` schema/repository
- [x] Task 6b: Implement Channel overflow and startup replay
- [x] Task 6c: Implement `ActionExecutionLog` deduplication state machine and `IClock`

## Task 7: MemberResolver + PlatformUserDisplayCache

- [x] Task 7a: Implement `IMemberResolver` port and atomic resolver
- [x] Task 7b: Implement `PlatformUserDisplayCache` L1/L2
- [x] Task 7c: Complete display cache `UpdateAsync` default row and TTL cleanup

## Task 8: SystemSettings + Token Secure Storage

- [x] Task 8a: Implement `ISystemSettingsService` SQLite-backed Get/Set
- [x] Task 8b: Implement settings hot reload `Changes` observable, wiring bus capacity / display cache capacity+TTL overrides
- [x] Task 8c: Implement OAuth token encryption, `machine.key`, and `IOAuthTokenStore`

## Phase 2 Checkpoint

- [x] Full solution compilation passes
- [x] Full solution tests pass
- [x] End-to-end publish → bus → handler workflow passes
- [x] `MigrationClassifier` raw SQL destructive/review-required tests pass
- [x] DB bootstrap `PRAGMA auto_vacuum = 2` verification passes
- [x] TDQ overflow → replay → delete cycle passes
- [x] `ActionExecutionLog` Completed/Failed/Pending retry semantics pass
- [x] `MemberResolver` concurrent testing passes
- [x] `IPlatformUserInfoCache.UpdateAsync` cache miss → default row creation passes
- [x] `bus.channel_capacity` overrides EventBus default 10,000 passes
- [x] `overlay.display_cache_l1_capacity` / `overlay.display_cache_ttl_hours` overrides display cache default 500 / 24h passes
- [x] AES-256-GCM tamper and AAD cross-key copy tests pass
- [x] Architectural tests confirm no Infrastructure/EF leaks in Domain/Application
- [x] Git status clean (excluding ignored local files)
- [x] Complete Phase 2 review before beginning Phase 3
