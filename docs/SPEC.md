# Specification: Vulperonex — Multi-Platform Live Stream Automation Platform

> **Status:** Approved v0.3 (MVP Scope — Multi-round review completed, ready for Phase 1 implementation)
> **Last Updated:** 2026-05-13
> **Repository:** Brand-new greenfield project repository. This specification describes the **target architecture**. No existing code to migrate.
> **Predecessor Reference:** Omni-Commander (Independent successor — borrowing domain logic concepts, not code)

---

## 1. Goals

**Vulperonex** is a platform-agnostic live stream automation tool that aggregates events from live streaming platforms and drives reactive features (chat overlays, workflows, member tracking, sound effects) via a unified event-driven architecture. The MVP supports Twitch; the architecture is designed to be extensible to other platforms without modifying the Domain/Application layers.

**Why:** Existing tools (including the predecessor project Omni-Commander) are tightly coupled with Twitch-specific concepts. Vulperonex decouples platform-specific event sources from feature consumers via Domain Events, enabling the addition of new platforms with minimal code and supporting testing/simulation as first-class event sources.

### Target Users

- **Streamers:** Running Twitch (MVP scope).
- **Plugin Authors:** Extend behaviors via `IVulperonexPlugin` — capable of both consuming and publishing events.
- **Developers:** Run automated tests against the same event streams as the actual platforms.

### MVP Success Vision

- Twitch Adapter publishes domain events to the Event Bus.
- WorkflowEngine subscribes to and executes rules.
- Desktop shell (Photino) hosts the Vue UI provided by the Web host.
- CLI can simulate events, manage configuration, manage rules, and check members.
- Adding new platform Adapters requires no modification to the Application / Domain layers (architectural isolation).

---

## 2. Tech Stack

### Backend (.NET 10 LTS)

| Item | Choice |
|---|---|
| Language / Runtime | C# 14 / .NET 10 LTS |
| Web Framework | ASP.NET Core Minimal API (10.0) |
| Real-time Communication | SignalR (10.0) |
| ORM | EF Core 10 (SQLite provider) |
| Desktop Shell | Photino.NET 3.x |
| Plugin System | `IVulperonexPlugin` (Custom contract, statically referenced at startup in MVP) |
| Unit Testing | xUnit 3 / NSubstitute / FluentAssertions 7 |
| Testing Methodology | BDD Scenario Definition + TDD Red/Green/Refactor Implementation |

### Frontend (Vue 3.5+ / Vite 7.x)

| Item | Choice |
|---|---|
| Framework | Vue 3.5+ (Standard SFC — Vapor Mode deferred to Phase 2 performance experiments) |
| Build Tool | Vite 7.3 (Rolldown; MVP pinned to v7, not upgrading to v8 — Vite 8 is released but will not be upgraded to during MVP) |
| Language | TypeScript 6.0 |
| UI | PrimeVue 4 (Unstyled) / UnoCSS (Preset Wind 4) |
| State / Communication | Pinia 2.3 / Axios / @microsoft/signalr 10.0 |
| Testing | Vitest 3 / Vue Test Utils 2.5 |
| i18n | vue-i18n 11.x |

---

## 3. Project Structure

```
Vulperonex/
├── src/
│   ├── Vulperonex.Domain/                     # Pure domain — Entities, Value Objects, Events
│   ├── Vulperonex.Application/                 # Use cases, Interfaces, Event Bus contract
│   ├── Vulperonex.Infrastructure/              # EF Core, SQLite, Repositories, Persistence
│   ├── Vulperonex.Plugins.Abstractions/        # IVulperonexPlugin contract (source + sink)
│   │
│   ├── Adapters/
│   │   ├── Vulperonex.Adapters.Abstractions/   # Shared adapter interfaces (IPlatformUserInfoCache etc.)
│   │   ├── Vulperonex.Adapters.Twitch/         # Twitch IRC + EventSub (Incoming + Outgoing)
│   │   └── Vulperonex.Adapters.Simulation/     # CLI / UI / Test simulation
│   │
│   ├── Hosts/
│   │   ├── Vulperonex.Web/                     # ASP.NET Minimal API + SignalR + wwwroot
│   │   ├── Vulperonex.Desktop/                 # Photino shell (wraps Web)
│   │   └── Vulperonex.Cli/                     # CLI: Simulation / Config / Rules / Member commands
│   │
│   └── frontend/                               # Vue 3.5 SPA, built to Web/wwwroot
│       ├── src/
│       │   ├── components/
│       │   ├── composables/
│       │   ├── stores/
│       │   ├── views/
│       │   └── i18n/
│       └── tests/
│
├── tests/
│   ├── Vulperonex.Tests.Unit/
│   ├── Vulperonex.Tests.Integration/
│   └── Vulperonex.Tests.Architecture/          # Layer / Dependency rule enforcement
│
├── docs/                                        # SPEC.md official path: docs/SPEC.md
│   ├── SPEC.md
│   ├── adr/                                     # Architecture Decision Records
│   └── plugins/
│
└── tools/
```

---

## 4. Architectural Core Concepts

### 4.1 Layered Architecture (Clean Architecture)

```
┌─────────────────────────────────────────────┐
│ Hosts (Web / Desktop / CLI)                 │ Depends ↓
├─────────────────────────────────────────────┤
│ Adapters (Twitch / Simulation)              │ Depends ↓
│   + Adapters.Abstractions (IStreamEventSource etc.)│ All Adapters depend on Domain + Adapters.Abstractions + Application
│   (IStreamEventBus is defined in Application; Adapter requires this dependency to publish events) │
├─────────────────────────────────────────────┤
│ Application (UseCases, EventBus, Ports)     │ Depends ↓
├─────────────────────────────────────────────┤
│ Domain (Entities, Events, Value Objects)    │ Innermost, no external dependencies
└─────────────────────────────────────────────┘
Infrastructure cross-cutting: Implements Application ports, depends on Application + Domain, not depended on by Domain/Application.
Arrow reading: Outer layers depend on inner layers (Inner layers know nothing about outer layers).
```

### 4.1a Tactical DDD Boundaries

Vulperonex uses Tactical Domain-Driven Design (DDD) to protect the live stream automation model:

- **Domain Layer (Domain):** Owns platform-neutral entities, value objects, domain events, and invariants.
- **Application Layer (Application):** Owns use case orchestration and defines repository/service ports.
- **Infrastructure, Adapters, Hosts, and UI:** Must not contain domain rules belonging to the Domain or Application layers.
- **Repository Interfaces:** Are ports of the Application layer; EF Core implementations reside in the Infrastructure layer.
- **Domain Events:** Describe facts using the Ubiquitous Language (`UserSentMessageEvent`, `MemberRecord`, `WorkflowRule`) and must not expose any payload types from platform SDKs.
- **Aggregates:** Keep them tiny in the MVP phase. Avoid introducing extensive aggregate roots unless strictly necessary to enforce true transactional consistency boundaries.

### 4.1b DCI-inspired Role/Behavior Decomposition Guidelines

Vulperonex is primarily structured around DDD + Clean Architecture. DCI (Data/Context/Interaction) serves as an auxiliary technique for DDD tactical design and does not replace Repositories, CQRS, or Clean Architecture dependency directions.

**Trigger Condition:** When an Aggregate or Domain service starts taking on behaviors of multiple use-cases and the number of methods grows continuously, employ Role/Behavior decomposition to maintain the Single Responsibility Principle (SRP).

**Boundary Mapping:**

```
Domain Boundaries (Data + Role)
  Entity / Value Object        — State and Invariants
  Role / Behavior objects      — Pure domain behaviors for specific use cases

Application Boundaries (Context + Interaction)
  UseCase / Context class      — Assigns Data to play Roles, orchestrates collaboration
  Ports (Repository / Service) — Decoupled from Infrastructure

Infrastructure (Not in DCI scope)
  EF Core Repository Implementation
  DbContext / SQLite
```

**Enforcement Rules:**
- Role/Behavior objects must contain **pure Domain logic** and must not directly depend on `DbContext`, EF Core, or any Infrastructure types.
- Context/Interaction belongs to the Application use case and accesses Infrastructure via port interfaces.
- **MVP does not perform runtime dynamic role assignment / reflection / mixin** (Part 3 patterns); Roles are statically decomposed at compile-time.

**Architectural Test Discovery Rules:**
- **Target for Identification:** Classes in the `Vulperonex.Domain` namespace whose names end with `Role` or `Behavior` (`*Role`, `*Behavior`).
- **Test Assertion:** The assembly of the above classes must not reference `Vulperonex.Infrastructure`, `Microsoft.EntityFrameworkCore`, or any `*.Infrastructure.*` namespace.
- **Location:** `tests/Vulperonex.Tests.Architecture/Domain/DciRoleIsolationTests.cs` (Implemented in Task 3).

### 4.1c Lightweight CQRS Boundaries

Vulperonex employs lightweight CQRS at the Application layer boundary:

- **Commands:** Change state and enforce invariants.
- **Queries:** Return read-optimized DTOs and must not directly expose EF entities.
- **Write Repository Ports** and **Query Service Ports** are kept separate (`IMemberRepository` and `IMemberQueryService` serve as the baseline pattern).
- MVP uses the same SQLite database for both command and query paths.
- **Do not** add command buses, query buses, Event Sourcing, or separate read databases in the MVP.
- REST endpoints can reside in the same host/controller groups, but command and query endpoints must call separate Application layer ports.

### 4.2 Event Flow

```
[Adapter] ─→ Mapping ─→ IStreamEvent ─→ IStreamEventBus
                                          ↓
                               ┌──────────┼──────────┐
                               ↓          ↓          ↓
                         Workflow Module Overlay Module Member Module
```

- All flows pass through `IStreamEventBus`. Adapters never directly call Handlers.
- Plugins can act simultaneously as **event sources** (publishing to the bus) and **event consumers** (subscribing).
- The Simulation Adapter is a real adapter, not a side channel — testing and the CLI use the exact same paths as the actual platform.

**Bus Semantics (Resolved):**

| Item | Decision |
|---|---|
| Ordering | **Unchecked.** No guaranteed processing order between different handlers. |
| Exception Isolation | **Each handler is wrapped in a try/catch.** Exception → Log (LOG), other handlers continue unaffected. |
| Backpressure | **Default: In-memory `Channel<IStreamEvent>` (10,000 slots).** When depth exceeds threshold → overflow to **Transient Delivery Queue (TDQ)** SQLite table. TDQ semantics: Payload is stored until processing completes, deleted immediately on success, and replayed on startup if unprocessed. TDQ **is not** event persistence — no history is retained. Threshold is configurable via `SystemSettings`. |
| Delivery Semantics | **At-least-once.** TDQ replay on startup means events might be processed multiple times. Built-in side effects (`SendChatMessageAction`, `InvokePluginAction`) are deduplicated via `ActionExecutionLog`. **`MemberModule` uses `INSERT OR IGNORE` atomic GetOrCreate — replay-safe.** **`PlatformUserDisplayCache` updates must use "state replacement" rather than "delta accumulation" semantics** (`TotalBitsGiven` stores absolute value from platform payload, no `+= amount`; TDQ replay does not cause duplicate accumulation). Built-in side-effect operations (`SendChatMessageAction`, `InvokePluginAction`) use the state-based `ActionExecutionLog` table. Deduplication unique key: `(EventId, WorkflowRuleId, ActionIndex)` — EventId + ActionIndex is insufficient as one event can match multiple WorkflowRules. **Known limitation: rule updates may change action order; during old TDQ event replay, the same ActionIndex might point to a different action. MVP accepts this limitation (short TDQ replay window, low probability of rule updates); stable action identity is post-MVP.** For sub-workflow invocations, an additional `InvocationId` (a ULID generated for each `InvokeSubWorkflowAction` invocation) is appended to form `(EventId, WorkflowRuleId, ActionIndex, InvocationId?)`. Deduplication protocol: (1) `INSERT OR IGNORE (key, Status=Pending, AttemptCount=0)`; (2) if ignored and `Status=Completed` → skip; `Status=Failed` → skip (permanent failure, no retry); `Status=Pending` and elapsed > 30s (stale crash) → retry, `AttemptCount++`; `AttemptCount >= MaxRetries+1` and still failing → `UPDATE Status=Failed` (permanently stop, no more retries in future replays); (3) execute side effect; (4) `UPDATE Status=Completed`. **Stale threshold of 30s is injected via `IClock` abstraction (Task 6 supplies fake clock implementation), not hardcoded to `DateTime.UtcNow`.** **InvocationId must be generated and persisted before action execution (or included in TDQ payload) to ensure the same InvocationId is used during TDQ replay — if dynamically generated on each execution, a different dedup key will result, causing duplicate sub-workflow executions.** Plugin operations receive the complete execution key via `IPluginActionContext.ActionExecutionKey` (see §6.3) and the documentation **must** require this key for any external side effects. |
| Publishing Mode | **Fire-and-forget.** `PublishAsync` returns immediately after enqueueing. Caller thread is never blocked by handler execution. |

### 4.3 Identity Model (Two-Tier)

| Tier | Purpose | Example |
|---|---|---|
| `StreamUser` | Platform-bound identity, carried in events, used for display | `{ Platform: "twitch", UserId: "12345", DisplayName: "alice" }` |
| `MemberRecord` | Persistent member, auto-created upon first event | `{ MemberId: ULID, Identities: [twitch:12345], Loyalty: {...} }` |

**Display always uses `StreamUser`. Aggregation/analytics use `MemberId`.**

- `MemberId` uses **ULID** (time-sortable, lexicographically orderable, SQLite index-friendly).
- `PlatformIdentity` table has a composite key `(Platform, PlatformUserId) → MemberId`.

**MemberResolver Race Condition Handling (G3):**

`PlatformIdentity` has a `UNIQUE (Platform, PlatformUserId)` constraint. Resolver uses SQLite `INSERT OR IGNORE` + `SELECT` — an atomic GetOrCreate pattern. No application-level lock is required; SQLite WAL serializes writes.

**CA Boundary:** Application only defines the `IMemberResolver` port interface (`ResolveAsync(string platform, string platformUserId) -> MemberId`); the actual EF Core / raw SQL implementation (`MemberResolver`) is placed in the Infrastructure layer, and must not appear in Application or Domain.

```csharp
// Application port (interface only in Application)
public interface IMemberResolver
{
    /// <returns>MemberId (ULID string) — existing or newly created</returns>
    Task<string> ResolveAsync(string platform, string platformUserId, CancellationToken ct = default);
}

// Infrastructure implementation (pseudo-code) — whichever inserts first wins
await db.ExecuteSqlRawAsync(
    "INSERT OR IGNORE INTO PlatformIdentities (Platform, PlatformUserId, MemberId, ...) VALUES (?,?,?,...)");
var identity = await db.PlatformIdentities
    .FirstAsync(x => x.Platform == platform && x.PlatformUserId == userId);
```

---

### 4.3b Platform User Display Cache (G4)

Overlays need display data (avatars, colors, badges, subscription status, total bits), which is not passed with every platform event, and fetching this data from platform APIs for every event is expensive.

**Two-level cache — strictly limited to Adapter Infrastructure. Application/Domain layers are unaware of its existence.**

```
L1: Bounded in-memory LRU cache
    Default max: 500 entries (~150 KB). Configurable.
    Evicts least recently used on overflow.

L2: SQLite table — PlatformUserDisplayInfo
    L1 miss → check DB (TTL check on FetchedAt)
    DB miss → fetch from platform API → write to DB + backfill L1
    Default TTL: 24 hours. Background worker purges expired rows.
```

**Database Table:**

```sql
CREATE TABLE PlatformUserDisplayInfo (
    Platform         TEXT NOT NULL,
    PlatformUserId   TEXT NOT NULL,
    AvatarUrl        TEXT,
    ColorHex         TEXT,
    BadgesJson       TEXT,           -- JSON array of badge strings
    IsSubscriber     INTEGER NOT NULL DEFAULT 0,
    SubscriptionTier TEXT,           -- "1000" | "2000" | "3000" | null
    TotalBitsGiven   INTEGER NOT NULL DEFAULT 0,
    FetchedAt        INTEGER NOT NULL,  -- Unix timestamp
    PRIMARY KEY (Platform, PlatformUserId)
);
```

**Interface (Adapters.Abstractions):**

```csharp
public interface IPlatformUserInfoCache
{
    ValueTask<UserDisplayInfo?> GetAsync(string platform, string userId);
    Task SetAsync(string platform, string userId, UserDisplayInfo info);
    // cache miss → create default UserDisplayInfo row
    //   (AvatarUrl=null, ColorHex=null, Badges=Array.Empty<string>(), IsSubscriber=false,
    //    SubscriptionTier=null, TotalBitsGiven=0, FetchedAt=UtcNow), then apply updater.
    //   Never returns null post-update.
    Task UpdateAsync(string platform, string userId, Func<UserDisplayInfo, UserDisplayInfo> updater);
}

public record UserDisplayInfo(
    string? AvatarUrl,
    string? ColorHex,           // null or ^#[0-9A-Fa-f]{6}$; CSS functions / named colors / alpha not accepted
    IReadOnlyList<string> Badges,
    bool IsSubscriber,
    string? SubscriptionTier,
    int TotalBitsGiven,
    DateTimeOffset FetchedAt);
```

**Proactive Cache Updates on Related Events:**

Adapters subscribe to domain events and proactively update the cache — no need to wait for TTL to expire:

| Domain Event | Cache Update |
|---|---|
| `UserSubscribedEvent` | `IsSubscriber=true`, `SubscriptionTier`, add subscriber badge |
| `UserDonatedEvent` | `TotalBitsGiven = max(existing, event.TotalBitsGiven)` (**monotonic absolute value replacement**, not `+= amount`; event payload carries cumulative total, TDQ replay does not double accumulate, out-of-order old payload must not roll back cumulative value; if platform back-office manual adjustment requires lowering local value, future explicit admin reset flow is used) |
| `UserFollowedEvent` | Add follower badge |

**Display Hints on Events (DisplayHints):**

Adapters enrich events with structured display hints before publishing. **Raw HTML is strictly forbidden** — overlays are OBS browser sources, and XSS is a real attack surface. Message content is represented as typed segments; frontend renders them securely.

Standardized Hint Keys:

| Key | Type | Example Value |
|---|---|---|
| `display.color` | Hex string format `^#[0-9A-Fa-f]{6}$` (6-digit RGB, does not accept 3-digit shorthand, CSS functions, named colors, 8-digit alpha, or empty strings) | `#FF4A4A` |
| `display.segments` | JSON array of segments | See below |
| `user.avatar` | URL string | `https://cdn.twitch.tv/...` |
| `user.badges` | Comma-separated badges `id/value`; each badge ID charset `[A-Za-z0-9_/\-]`; badge value max 64 chars; max 20; duplicates deduplicated (retaining first occurrence order) | `subscriber/2000,vip` |
| `user.is_subscriber` | `"true"` / `"false"` | `"true"` |
| `user.bits_total` | Integer value string | `"5000"` |

**`display.segments` Format (No Raw HTML):**

```json
[
  { "type": "text",  "value": "hello " },
  { "type": "emote", "id": "Kappa", "url": "https://static-cdn.jtvnw.net/..." },
  { "type": "text",  "value": " world" }
]
```

Allowed segment types: `text`, `emote`, `badge`, `mention`. The frontend securely renders each type using `textContent` (or equivalent DOM API), **forbidding `innerHTML`**. The value of `text` segments can contain arbitrary Unicode (including `<`, `>`) — they are text data, not markup; the browser automatically escapes them during frontend rendering and they are not parsed as HTML. **The security boundary lies in the allowlist of `type` fields and their rendering method, rather than filtering the value of `text` segments**. `emote` and `badge` segments only allow trusted `id` + `url` and do not permit arbitrary HTML attributes.

**Emote / Badge URL Trust Boundaries (MVP Explicitly Out of Scope):** The `url` field is directly populated by the `TwitchAdapter` from the platform API (Twitch CDN URL); the MVP does not perform scheme/domain allowlist validation in the Domain or Overlay layers. The trust boundary is: **only first-party adapters (MVP: TwitchAdapter) can attach overlay DisplayHints to events**. Plugins can publish custom events via `IPluginContext.Events.PublishAsync` (SC-10), but **MVP plugin custom events do not contain overlay DisplayHints** (`OverlayModule` does not push plugin events to the overlay SignalR group); `OverlayModule` only subscribes to the 7 MVP event types defined in the Domain layer (see §4.4). If plugins need to drive the overlay in the future, a URL allowlist validation must be added (post-MVP).

The overlay reads `DisplayHints` directly from events — zero additional database or API calls in the hot path.

### 4.4 Domain Events (MVP Collection)

| Event | EventTypeKey | Trigger Condition |
|---|---|---|
| `UserSentMessageEvent` | `user.message` | Chat message |
| `UserFollowedEvent` | `user.followed` | New follow |
| `UserDonatedEvent` | `user.donated` | Twitch Bits / YT SuperChat |
| `UserSubscribedEvent` | `user.subscribed` | Subscription |
| `UserGiftedSubscriptionEvent` | `user.gifted_sub` | Gifted subscription |
| `ChannelRaidedEvent` | `channel.raided` | Raid (currently Twitch-specific concept) |
| `RewardRedeemedEvent` | `reward.redeemed` | Channel points redemption / equivalent |

All events are **immutable `record` types** implementing `IStreamEvent`. Events **are not persisted** — only written to log files (with a configurable retention period).

### 4.5 Outgoing (Reply Routing)

- One `IPlatformChatSender` per platform. Twitch → Twitch IRC, Simulation → in-memory receiver.
- `WorkflowAction.SendChatMessage` defaults to replying on the **source platform** (from the event's `Platform` field).
- Actions can override to a specific platform via explicit `TargetPlatform` setting (such as `"twitch"`); **"broadcasting to all platforms" is post-MVP** (MVP's `TargetPlatform` only accepts a specific platform name or null; it does not accept `"all"` sentinel).
- `SendChatMessage` in `Simulation` mode **must not be a silent no-op**. Even if not connected to a real chatroom, it must write to an observable in-memory receiver / Chat Outbox / history view so that users can verify the rendered message, platform, channel, dedupKey, and status (sent / skipped / failed).
- The event-driven chat overlay display `/overlay/chat` and workflow `SendChatMessage` are **two distinct data streams**. Unless explicitly configured with a bridge, the chat overlay cannot be treated as the sole validation plane for workflow chat output.

---

## 4.6 WorkflowRule Model

```
WorkflowRule
├── Id: ULID
├── Name: string           // Display label, **non-unique**; CLI/API list/show uses Id as primary key (Name can be duplicated)
├── Priority: int          // Smaller number = higher priority (1 executes before 10)
├── CreatedAt: DateTimeOffset
├── IsEnabled: bool
├── ConcurrencyMode: Serial | Parallel
├── MaxParallelism: int    // Only applicable when ConcurrencyMode = Parallel; limits API blast radius
│
├── Trigger
│   ├── EventTypeKey: string          // "user.message", "user.followed" etc.
│   └── PlatformFilter: string?       // null = all platforms; save-time normalization: trim → empty string → null; lowercase canonical ("Twitch" → "twitch")
│
├── Conditions: List<IWorkflowCondition>   // AND logic — all must pass
│   ├── UserRoleCondition (User Identity Condition)
│   │   ├── Roles: StreamRole flags (Subscriber | Moderator | Vip | Follower — mapped from adapter badge/role fields)
│   │   └── Mode: HasAny | HasAll | NotHave
│   │
│   ├── MessageContentCondition (Message Content Condition) // Only applicable when event carries plain text
│   │   ├── MatchMode: PrefixMatch | ContainsMatch | FullRegex
│   │   ├── Pattern: string            // e.g., "!checkin", "hello", "^!\\w+"
│   │   └── Non-text event behavior: event lacks MessageContent field → condition evaluates to false (skipped, does not throw)
│   │
│   └── CooldownCondition (Cooldown Condition)
│       ├── Scope: Global | PerUser
│       ├── DurationSeconds: int        // Valid range [1, 86400] (max 24h); out-of-range → INVALID_ACTION_CONFIG
│       └── Persistence: in-memory only (cooldown state resets on application restart) — acceptable for MVP; persistent cooldown is post-MVP
│
└── Actions: List<IWorkflowAction>     // Executed sequentially in list order
    ├── SendChatMessageAction (Send Chat Message)
    │   ├── Template: string           // Variable placeholders: {user.displayName}, {event.amount}
    │   └── TargetPlatform: string?    // null = source platform; when non-null, save-time only validates non-empty string (does not validate if adapter is enabled)
    │
    ├── InvokeSubWorkflowAction (Invoke Sub-Workflow)
    │   └── WorkflowId: ULID
    │
    └── InvokePluginAction (Invoke Plugin Action)      // Covers: trigger effects, add points, play sound, etc.
        ├── PluginId: string
        ├── ActionId: string
        └── Params: IReadOnlyDictionary<string, JsonElement>
```

**Priority Resolution:** By `Priority ASC`, then `CreatedAt ASC`, and finally `Id ASC` (ULID lexicographical order, ensuring no DB unstable sorting issues).

**Concurrency Semantics:**
- `Serial` (default): **Scope is a single `WorkflowRule`** (each rule has an independent queue; queuing in rule A does not affect rule B). Events for the same rule execute one at a time.
- `Parallel`: Events for the same rule execute concurrently up to `MaxParallelism`. Valid range for `MaxParallelism` is `[1, 64]`; out-of-range is rejected on rule save (400 + `INVALID_ACTION_CONFIG`).
- Different rules matching the same event always execute independently (no cross-rule serialization).

**Actions driven by plugins (Hot-swappable):** `SendChatMessage` and `InvokeSubWorkflow` are built-in. All domain-specific actions (`TriggerEffect`, `AddPoints`, `PlaySound`) are `InvokePluginAction` — they require loading the corresponding plugin. If the plugin is missing, the action logs a warning and is skipped.

**Condition Evaluation Short-Circuit:** The first failed condition stops evaluation (ordered lowest cost to highest).

---

## 4.7 Platform Adapter Resilience (G5)

**Reconnection Strategy (Handled entirely within the adapter — Application/Domain layers are unaware):**

```
IRC WebSocket Disconnection:
  → Immediately publish PlatformConnectionChangedEvent { IsConnected: false, Reason: "reconnecting" }
  → Exponential backoff: 1s → 2s → 4s → 8s → ... max 60s
  → Messages during downtime: Silently lost (IRC is best-effort service)
  → On successful reconnection: publish PlatformConnectionChangedEvent { IsConnected: true }

EventSub WebSocket Disconnection:
  → Immediately publish PlatformConnectionChangedEvent { IsConnected: false, Reason: "reconnecting" }
  → Same backoff strategy with ±20% jitter applied
  → Twitch guarantees event replay within a 10-minute reconnection window
  → Adapter receives replayed events and publishes them normally; skips duplicate deliveries in dedup cache matching the same (platform, sourceEventId); dedup cache capped at 1000 entries or 10-minute TTL
  → Exceeding 10 minutes: Events permanently lost — no custom replay mechanism constructed
```

**Connection Status Events (Added to Domain Events):**

```csharp
public sealed record PlatformConnectionChangedEvent : IStreamEvent
{
    public string EventId { get; init; } = Ulid.NewUlid().ToString();
    public string EventTypeKey => "platform.connection_changed";
    public required string Platform { get; init; }
    public required bool IsConnected { get; init; }
    public string? Reason { get; init; }   // "reconnecting" | "auth_failed" | null
                                           // auth_failed = Platform IRC/OAuth authentication failure, not Web host authentication
    public StreamUser? User => null;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
```

UI subscribes via SignalR to display connection status indicators. `PlatformConnectionChangedEvent` is a **system event**, excluded from `GET /api/event-types` and does not appear in the WorkflowRule event type dropdown — by default, no workflow rules can trigger on it (used solely for UI status display).

---

## 4.8 Workflow Action Error Handling (G6)

Each action has an `ErrorBehavior` and a global action timeout:

```csharp
public enum ErrorBehavior
{
    ContinueOnError,   // (Default) Failure → Log → Continue to next action
    StopOnError,       // Failure → Stop remaining actions in the rule
    RetryOnError,      // Failure → Retry according to backoff strategy before giving up
}
```

Each `IWorkflowAction` base class carries:

```
ErrorBehavior: ErrorBehavior = ContinueOnError
MaxRetries:    int = 0     // Valid range [0, 10]; out-of-range → INVALID_ACTION_CONFIG
BackoffMs:     int = 500   // Valid range [100, 30000]; out-of-range → INVALID_ACTION_CONFIG
TimeoutMs:     int = 5000  // Valid range [100, 60000]; out-of-range → INVALID_ACTION_CONFIG
                            // CancellationToken is cancelled after this; executor stops waiting
                            // .NET cannot forcibly terminate asynchronous tasks — plugins must observe CancellationToken
```

**Special Cases:**

| Scenario | Behavior |
|---|---|
| Plugin Missing | Warning log → skip (**treated as skip, not counted as action error**; `ErrorBehavior` does not apply — plugin not loaded is a configuration issue, distinct from action execution failure; `StopOnError` does not stop subsequent actions because of this) |
| InvokeSubWorkflow Target Not Found | Warning log → skip |
| Circular Workflow Reference | Detected on rule save (static analysis), intercepted before entering the database |
| Twitch API 429 | Use `RetryOnError` + backoff for `SendChatMessageAction` |
| Action Timeout | Signals `CancellationToken` → stops waiting for task → treated as error → applies `ErrorBehavior`. Executor will not abort the task thread. Plugins must cooperatively observe cancellation, otherwise underlying work may still complete after timeout. |

---

## 4.9 Configuration Storage (G7)

Three tiers of separation:

| Type | Storage | Hot Reload | Remarks |
|---|---|---|---|
| Infrastructure Config | `appsettings.json` | Requires Restart | Ports, file paths, feature flags |
| Runtime Settings | SQLite `SystemSettings` table | ✅ Immediate | Bus thresholds, log TTL, workflow limits |
| OAuth Refresh Token | SQLite AES-256-GCM encrypted, versioned envelope `v1:<Base64>`, key = local `machine.key` | Requires Restart | Only credential requiring protection |
| Platform client_id | `appsettings.json` (Public, committable) | Requires Restart | Public client — no client secret |

**SystemSettings Table (Target of CLI `config get/set`):**

> **Note:** Not all keys in `SystemSettings` can be accessed via `/api/config/{key}` or CLI `config get/set`. Protected namespaces like `security.*` and `oauth.*` are intercepted at the API layer (see §4.13 Secure Namespaces). The CLI `config get/set` operational scope is restricted to non-protected keys (such as `log.*`, `workflow.*`, `overlay.*`, `streaming.*`, `bus.*`).

```sql
CREATE TABLE SystemSettings (
    Key       TEXT PRIMARY KEY,
    Value     TEXT NOT NULL,
    Category  TEXT NOT NULL,   -- "streaming" | "workflow" | "overlay" | "log" | "bus" | "oauth"
                               -- Note: "oauth" category is a protected namespace, inaccessible via /api/config or CLI config get/set
    UpdatedAt INTEGER NOT NULL
);
```

**OAuth Credential Model (Twitch Public Client — PKCE):**

```
No Client Secret — Twitch does not issue secrets for public applications.

OAuth PKCE Flow:
  1. Application generates code_verifier + code_challenge + cryptographically random state (32 bytes Base64Url)
  2. Opens browser → Twitch authorization page (with state parameter)
  3. User authorizes → Redirection to localhost callback (with code + state)
  4. Application validates state matches Step 1, is not expired (TTL 10 mins), and unused (CSRF protection); mismatch, expired, or used → reject, log warning, do not exchange code
  5. Application receives auth_code → exchange (using code_verifier) to obtain token
  6. Encrypts and stores refresh_token; access_token is only retained in memory
  
  OAuth Callback Listener Boundaries:
  - Only accepts loopback (127.0.0.1 / ::1) requests
  - Host header only accepts localhost:{port}, 127.0.0.1:{port}, [::1]:{port}; rejects others to prevent DNS rebinding
  - Both Remote IP allowlist and Host header allowlist must pass; failing either rejects the request
  - Only accepts default callback path (e.g. /auth/callback); other paths → ignored
  - Callback is accepted ONLY ONCE (closes listener immediately after receipt)
  - access_token, authorization code, code_verifier, and refresh_token plain values are not logged; refresh_token plain value is passed to IOAuthTokenStore, encrypted by Task 8

Credential Storage:
  machine.key   → Generated on first runtime, stored fixed in OS Application Data root directory:
                    Windows: %AppData%\Vulperonex\machine.key
                    macOS  : ~/Library/Application Support/Vulperonex/machine.key
                    Linux  : ~/.local/share/Vulperonex/machine.key
                  (Uses Environment.SpecialFolder.ApplicationData on Windows/macOS;
                   XDG_DATA_HOME on Linux)
                  machine.key path is fixed in the OS AppData root and does not change with 
                  custom DB paths in appsettings.json → Database:Path.
                  If the user moves the DB to a custom path, machine.key remains in OS app-data.
                  This ensures machine.key stability under DB migration or custom storage layouts.
                  Never leaves the machine.
                  File Permissions (Set immediately upon creation):
                    Windows: Current User FullControl ACL, inheritance disabled
                    Unix/macOS/Linux: chmod 0600 (Only owner can read/write)
                  Violation of this means machine.key is visible to other OS users, equivalent to refresh_token leakage.
                  chmod/ACL setup failure → fail-fast (throw IOException), no fallback.
                  Security over availability: do not continue encryption/decryption if machine.key cannot be secured.
  refresh_token → Encrypted via AES-256-GCM; stored as TEXT in SystemSettings.Value
                  Encryption envelope (versioned for future migration safety):
                    "v1:" + Base64( nonce(12B) || ciphertext || tag(16B) )
                  (Note: This envelope uses standard Base64 containing +/=/; decoding uses Convert.FromBase64String, not WebEncoders.Base64UrlDecode)
                  - Version prefix "v1:" allows future format changes without breaking stored data
                  - Generates a 12-byte cryptographically random nonce on each encryption call
                  - Contains a 16-byte authentication tag (GCM provides confidentiality + integrity)
                  - Key = machine.key raw 32 bytes (no additional KDF needed; key is already random)
                  - AAD = Setting key name UTF-8 bytes (i.e. "oauth.twitch.refresh_token"), passed to AesGcm.Encrypt(); re-passed during decryption; AAD is not stored in the envelope. Binds ciphertext to key name, preventing cross-key copy attacks.
                  - Stored under SystemSettingKey "oauth.twitch.refresh_token"

Cross-Device Behavior:
  New machine installation → machine.key missing → decryption fails
  → Application detects failure → prompts re-authorization → executes OAuth PKCE again
  → This is the intended correct behavior, not an error.
```

**Hot Reload Interface:**

```csharp
public interface ISystemSettingsService
{
    T Get<T>(string key, T defaultValue);
    Task SetAsync(string key, string value);
    IObservable<SettingChangedEvent> Changes { get; }  // Notifies subscribers on change
}
```

---

## 4.10 Module Lifecycle (G8)

Each module (`WorkflowModule`, `OverlayModule`, `MemberModule`) implements `IHostedService`. Subscriptions are acquired during `StartAsync`, released during `StopAsync` while waiting for in-progress handlers to complete (does not discard events still being processed). **If `ct` triggers cancellation before completion is reached → log warning "shutdown timeout: {count} handlers still running" and force return (does not throw exception); handlers may continue running after process shutdown, which is a system limitation and not treated as an error.**

```csharp
public class WorkflowModule : IHostedService
{
    private readonly IStreamEventBus _bus;
    private readonly List<IDisposable> _subscriptions = new();

    public Task StartAsync(CancellationToken ct)
    {
        _subscriptions.Add(_bus.Subscribe<IStreamEvent>(HandleAsync));
        // Subscribe<T> uses assignable match (covariance):
        // Subscribe<IStreamEvent> receives all events; Subscribe<UserSentMessageEvent> receives only that specific type.
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _subscriptions.ForEach(s => s.Dispose());
        await _pendingWork.WaitForCompletionAsync(ct);
    }
}
```

**Startup Order (Controlled by DI Registration Order):**

```
1. Infrastructure              — DbContext, EF Migrations
2. IStreamEventBus            — Must exist before any subscriber
3. ISystemSettingsService     — Must precede cache (cache reads L1 capacity/TTL config)
4. IPlatformUserInfoCache     — Reads L1 capacity/TTL from ISystemSettingsService
5. Modules                     — MemberModule → OverlayModule → WorkflowModule
6. Adapters                    — TwitchAdapter (starts publishing only after modules are ready)
7. Web / SignalR Hub
```

**Shutdown order is reversed.** Adapters stop first (no new incoming events) → modules flush in-progress work → infrastructure shuts down.

---

## 4.11 Database Migration Strategy (G9)

**Dual Mode: Auto-run on Startup + Explicit CLI Execution.**

```
On Startup (Always):
  await db.Database.MigrateAsync()
  → Auto-executes only incremental migrations
  → Application never starts with an outdated schema

CLI (Manual Control):
  vulperonex db migrate        → Executes pending migrations
  vulperonex db status         → Lists applied / pending migrations
  vulperonex db rollback <id>  → Rollback (requires confirmation prompt)
```

**Migration Safety Rules:**

| Migration Type | Auto-run on Startup |
|---|---|
| ADD TABLE | ✅ Yes |
| ADD COLUMN (Nullable or with default value) | ✅ Yes |
| ADD COLUMN (NOT NULL, no default value) | ❌ CLI Only |
| DROP COLUMN / DROP TABLE | ❌ CLI Only — Requires explicit confirmation |
| RENAME COLUMN / RENAME TABLE | ❌ CLI Only |

**Important Note:** `EF Core MigrateAsync()` executes all pending migrations without checking if they are destructive. The table above describes the **strategy**, not automatic enforcement. Enforcement is achieved via:

1. **CI Migration Classifier** — A test in `Vulperonex.Tests.Architecture` that detects destructive migrations by **instantiating** each `Migration` class, **executing** its `Up(MigrationBuilder)` method against a real `MigrationBuilder` instance, and checking for destructive operation types inside `MigrationBuilder.Operations`:
   ```csharp
   var builder = new MigrationBuilder(activeProvider: "Microsoft.EntityFrameworkCore.Sqlite");
   migration.Up(builder);  // Call actual Up() method
   var destructive = builder.Operations.Any(op =>
       op is DropTableOperation or DropColumnOperation
           or RenameTableOperation or RenameColumnOperation or AlterColumnOperation
       || op is SqlOperation sql
           && Regex.IsMatch(sql.Sql, @"\b(DROP|DELETE|TRUNCATE|ALTER|RENAME)\b",
               RegexOptions.IgnoreCase));
   ```
   If any destructive operation is found and the migration class is not annotated with the `[DestructiveMigration]` attribute, the test fails. This approach avoids fragile method-body reflection and yields the actual list of `Operations` EF Core will execute. **Note: any raw SQL containing `ALTER` is treated as review-required (conservative strategy)** — covering all ALTER variations like `ALTER TABLE ... DROP COLUMN`, `ALTER TABLE ... RENAME`, etc., without further sub-classification.
2. **PR Review Requirement** — Any migration annotated with `[DestructiveMigration]` requires manual review before merging.

EF Core migration files are located in `src/Vulperonex.Infrastructure/Migrations/` in the repository.

**SQLite File Location (Configured via `appsettings.json → Database:Path`):**

```
Windows : %AppData%\Vulperonex\vulperonex.db
Linux   : ~/.local/share/Vulperonex/vulperonex.db
macOS   : ~/Library/Application Support/Vulperonex/vulperonex.db
```

---

## 4.12 EventTypeKey Type Safety (G10)

String-based `EventTypeKey` is flexible (plugins can define new types) but fragile — typos can cause rules to fail silently.

**Protection Mechanism: `IStreamEventTypeRegistry` + Save-time Validation.**

```csharp
public interface IStreamEventTypeRegistry
{
    void Register(string key, string description, bool isSystemEvent = false);
    bool IsKnown(string key);             // Includes system events (used for routing/dispatch)
    bool IsKnownForWorkflow(string key);  // Excludes system events (used for WorkflowRule validation)
    IReadOnlyList<RegistryDescriptor> GetAll();  // Returns RegistryDescriptor (excludes isSystemEvent=true items)
                                                  // API endpoint projects this to EventTypeDescriptor (adds IsSimulatable)
}

// Registry internal storage type (not exposed to the API layer)
internal record RegistryDescriptor(
    string Key,
    string Description,
    bool IsSystemEvent);  // true for platform.connection_changed; excluded from GetAll()

// API DTO — returned by GET /api/event-types endpoint (projected from RegistryDescriptor)
// Does not expose IsSystemEvent (already excluded from GetAll(); frontend should not determine system event by this field)
public record EventTypeDescriptor(
    string Key,
    string Description,
    bool IsSimulatable);  // Populated by endpoint based on static alias map (not passed to Register())
                          // chat→user.message, follow→user.followed, sub→user.subscribed = true; others = false
```

**Duplicate Key Registration Behavior:** Duplicate `Register(...)` for the same key is **first-wins**; subsequent registrations are no-ops. Any metadata conflicts (description, isSystemEvent) retain the first-registered values, logging a warning, without throwing an exception. Both `TwitchAdapter` and `SimulationAdapter` call `Register("user.message", ...)`; both use the same canonical description (provided by `StreamEventDescriptions` constants, see below), so the first registration is correct and does not trigger a warning. `GetAll()` yields exactly one entry per key.

**`GetAll()` Behavior:** Excludes all keys with `isSystemEvent=true` (e.g. `platform.connection_changed`); returns results sorted alphabetically by `Key`; at most one entry per key. The `IsSimulatable` field is populated by the `GET /api/event-types` endpoint before returning, based on a static simulate alias map (not stored in the registry).

**`StreamEventDescriptions` Constants Class (`Vulperonex.Domain`):** All adapters use the same constants as descriptions, ensuring duplicate registrations do not trigger warnings:
```csharp
public static class StreamEventDescriptions
{
    public const string UserMessage   = "使用者發送了聊天訊息";
    public const string UserFollowed  = "使用者追隨了頻道";
    // … remaining 7 MVP event descriptions
}
```

Adapters and plugins register their keys during `StartAsync` / `InitializeAsync`:

```csharp
// TwitchAdapter.StartAsync()
_registry.Register("user.message",  "使用者發送了聊天訊息");
_registry.Register("user.followed", "使用者追隨了頻道");

// MyPlugin.InitializeAsync()
_registry.Register("plugin.my_plugin.event", "我的外掛程式自訂事件");
```

**Security Checkpoints:**

| Timing | Behavior |
|---|---|
| WorkflowRule Save (API / CLI) | Calls `IsKnownForWorkflow(key)` — rejects unknown or system events (`platform.connection_changed`); does not use `IsKnown()` (which includes system events and could mistakenly allow them) |
| UI Rule Editor | Only displays dropdown of registered keys — no free text input |
| Loading rules with unknown keys from DB (Plugin uninstalled) | Logs warning, skips rule — does not crash |

Unknown keys in the database degrade gracefully rather than causing fatal errors — allowing plugins to be removed without corrupting the application.

---

## 4.13 WorkflowRule Editing Interface (G11)

The REST API is the sole canonical write path. Both the UI and CLI call the API — neither writes directly to the database. The server always runs loopback-only with no authentication.

```
REST API  ← Sole write point, enforces all validations
              (EventTypeKey registry check, circular reference detection, schema validation)
  ↑
  ├── Vue UI   — Rule builder form → Axios → API
  └── CLI      — vulperonex rule list / show / enable / disable / delete → HTTP → API
```

Relevant CLI Commands:
```bash
vulperonex rule list
vulperonex rule show    <ruleId|prefix|--name <name>>
vulperonex rule enable  <ruleId|prefix|--name <name>>
vulperonex rule disable <ruleId|prefix|--name <name>> [--yes]
vulperonex rule delete  <ruleId|prefix|--name <name>> [--yes]
```

- `<ruleId>` accepts a full ID, unique ID prefix, or `--name <name>` (mutually exclusive with positional ID). Multiple hits → `AMBIGUOUS_ID` + candidate table; zero hits → `NOT_FOUND`.
- Destructive operations (`disable` / `delete`) in interactive REPL print a `[y/N]` prompt; non-interactive mode requires `--yes`, otherwise `CONFIRMATION_REQUIRED`.
- CLI resolution and confirmation workflow design is frozen in [`docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md`](phases/phase-5_5-rapid-test/cli-id-resolution-decision.md). Added error codes: `MISSING_ARGS` / `AMBIGUOUS_ID` / `NOT_FOUND` / `CONFIRMATION_REQUIRED` / `CANCELLED`.

Complex rule creation (multiple conditions, multiple actions) is limited to the UI in the MVP. The CLI is responsible for listing/showing/enabling/disabling/deleting.

---

### 4.13.1 Complete MVP REST API Interface

All UI and CLI access the Web host exclusively via REST; neither client has direct database access. The server always runs loopback-only (IPv4 `127.0.0.1` + IPv6 `::1`) with no authentication.

| Group | Method | Path | Application Port |
|---|---|---|---|
| WorkflowRule | GET | `/api/rules` | `IWorkflowRuleQueryService` — **MVP has no pagination**, returns all rules; sorting: `Priority ASC, CreatedAt ASC, Id ASC` |
| WorkflowRule | GET | `/api/rules/{id}` | `IWorkflowRuleQueryService` |
| WorkflowRule | POST | `/api/rules` | `IWorkflowRuleRepository` — **201 Created** + `Location: /api/rules/{newId}` header; body contains the newly created rule |
| WorkflowRule | PUT | `/api/rules/{id}` | `IWorkflowRuleRepository` — 200 OK; mismatch between body ID and route ID → **400 `INVALID_RULE_ID_MISMATCH`** (does not silently ignore body ID to prevent accidental overwrites of wrong rules) |
| WorkflowRule | DELETE | `/api/rules/{id}` | `IWorkflowRuleRepository` — **204 No Content** (no body) |
| WorkflowRule | POST | `/api/rules/{id}/enable` | `IWorkflowRuleRepository` — 200 OK; updates `IsEnabled=true` + `UpdatedAt` |
| WorkflowRule | POST | `/api/rules/{id}/disable` | `IWorkflowRuleRepository` — 200 OK; updates `IsEnabled=false` + `UpdatedAt` |
| Event Types | GET | `/api/event-types` | `IStreamEventTypeRegistry` |
| Simulation | POST | `/api/simulate/{eventType}` | `ISimulationAdapter` — `{eventType}` limited to short aliases: `chat` / `follow` / `sub` |
| Config | GET | `/api/config/{key}` | `ISystemSettingsService` |
| Config | PUT | `/api/config/{key}` | `ISystemSettingsService` |
| Member | GET | `/api/members` | `IMemberQueryService` |
| Member | GET | `/api/members/{id}` | `IMemberQueryService` |

**Simulation Alias → EventTypeKey Mapping** (Enforced by endpoint, not caller; uses canonical keys from §4.4):
- `chat` → `user.message`
- `follow` → `user.followed`
- `sub` → `user.subscribed`

Only alias values are accepted; raw EventTypeKey strings are rejected to maintain naming clarity across CLI/REST/WorkflowRules.

**Config Key Registry:** `ISystemSettingsService` operates on typed constants defined in `SystemSettingKey` (rather than arbitrary strings). Any key missing from the registry returns `UNKNOWN_CONFIG_KEY`. New settings require adding constants — free-text keys are not allowed.

**Config Key Casing Rules:** All keys are **canonical lowercase** (`log.min_level`, `oauth.twitch.refresh_token`). Before prefix denylist matching and registry lookups, the API normalizes the incoming `{key}` via `ToLowerInvariant()` — `OAuth.Twitch.Refresh_Token` and `oauth.twitch.refresh_token` are treated as the same key, both triggering the 403 denylist. The database also stores lowercase values.

**Interception Priority (Important):** `/api/config/{key}` requests execute checks in the following order: (1) key normalization (`ToLowerInvariant()`); (2) **protected prefix denylist** (`security.*` → 403 `CONFIG_KEY_SECURITY_NAMESPACE`; `oauth.*` → 403 `OAUTH_CREDENTIAL_NAMESPACE`); (3) **registry lookup** (`UNKNOWN_CONFIG_KEY`). The prefix denylist is executed **before** the registry check — an unknown `oauth.*` key (such as a future `oauth.unknown.refresh_token` not yet in the registry) still returns 403 instead of 400 `UNKNOWN_CONFIG_KEY`.

**Security namespace config keys** (`security.*`) are **intercepted** at `/api/config/{key}` — returning 403 + `CONFIG_KEY_SECURITY_NAMESPACE`. **OAuth credential keys** (`oauth.*`, e.g., `oauth.twitch.refresh_token`) are also **intercepted** at `/api/config/{key}` — returning 403 + `OAUTH_CREDENTIAL_NAMESPACE` (OAuth tokens are only written via the PKCE flow, no REST CRUD permitted). For the MVP, Twitch OAuth credentials are saved by the PKCE flow in Task 12 and cannot be accessed or modified via configuration endpoints. `/api/settings/security/*` is a reserved path prefix; no CRUD endpoints are added in the MVP; the Kestrel loopback-only binding inherently protects these paths without requiring additional middleware.

---

## 4.14 Overlay Architecture (G12)

`OverlayModule` subscribes to relevant domain events, converts them into `OverlayPayload` DTOs, and pushes them to SignalR groups. Frontend overlay pages connect as OBS browser sources.

**Browser Source URLs:**
```
http://localhost:5001/overlay/chat      — Scrolling Chat
http://localhost:5001/overlay/alerts    — Follow / Subscription / Raid Alerts
http://localhost:5001/overlay/member    — Member Card Display
```

Each URL is an independent Vue route that connects to its SignalR group upon mounting. No authentication is required (OBS must connect directly).

**Chat Overlay Template System:**

- `/overlay/chat` must support **multiple built-in templates / presets**, including at least the Vulperonex default template; built-in templates should map to a "single template directory / single template bundle" concept, rather than hardcoding a single layout.
- Template selection must be a **config-level** capability, rather than requiring users to modify frontend source code; it can be extended in the future to include template lists, previews, and import/export.
- Template rendering must still respect MVP security boundaries: using DTO allowlists and text bindings; **arbitrary raw HTML or `v-html` must not penetrate the event payload**.
- **OneComme compatibility belongs to extension/plugin capabilities, not directly built into core.** Core only needs to provide extensible template presets / package contracts; OneComme compatibility can be implemented via plugins, template importers, or adapter packages.
- **OneComme** is treated as a priority compatibility target. The goal is not to replicate its internal implementation 1:1, but to provide a close enough template structure / import mapping / compatibility contract to reduce migration costs for existing OneComme users while maintaining the boundary between core and third-party template ecosystems.

#### 4.14.1 Overlay Preset Contract (Vue Defaults + Custom HTML Extensions)

**Motivation:** Streamers wishing to customize overlay visuals should not be forced to install Node.js / pnpm / Vite. At the same time, Vulperonex must provide high-quality default Vue versions and support third-party extensions (including future OneComme template imports).

**Dual-Track Rendering Pipeline:**

| Pipeline | Target Audience | Path Pattern | Implementation Location |
|---|---|---|---|
| **Built-in Presets** | General users, zero-config ready | `/overlay/chat.html` (chat), `/overlay/member-card.html` (member), `/overlay/alerts` (alerts Vue) | `src/frontend/public/overlay/**` (static HTML/JS) + `src/frontend/src/views/overlay/**` (alerts Vue) |
| **Static HTML Override** | Advanced users / Third-party templates | `/overlay/custom/{slug}.html` | Backend `wwwroot/overlay/` directory; maps to `src/frontend/public/overlay/` in source code (copied during Vite build) |

**Preset Selection Priority (Backend Resolution):**

1. URL directly points to `*.html` → load static file.
2. URL points to `/overlay/{hub}` and `overlay.{hub}.preset` system setting points to `custom:{slug}` → redirect to `/overlay/custom/{slug}.html`.
3. URL points to `/overlay/{hub}` and `overlay.{hub}.preset` points to a built-in preset key:
   - For `chat` and `member` → resolve to static built-in HTML `/overlay/chat.html` or `/overlay/member-card.html` (embedding preset query strings such as `?preset={key}`).
   - For `alerts` → load corresponding Vue preset component (or redirect to `/overlay/alerts`).
4. Default fallback → static `vulperonex-default` preset via `/overlay/chat.html` (chat), static `rotan-checkin` preset via `/overlay/member-card.html` (member), Vulperonex default alert Vue page (alerts).

**Custom HTML Upload (Phase 7C — Post-this-PR):**

- The admin UI provides an HTML/CSS/JS bundle upload interface on the "Overlay Settings" page.
- Bundle format: a single `.html` file (self-contained with inline CSS/JS) or a zip containing `index.html` + relatively referenced assets.
- Upload target: `wwwroot/overlay/custom/{slug}/` (slug sanitized from filename, forbidding `..`, absolute paths, and non-`[a-z0-9-]` characters).
- Security:
  - Uploader must be a local user authenticated as admin (loopback only, adhering to Phase 6 security contracts).
  - Upload size limit: 5MB.
  - Zip decompression uses path traversal protection, verifying each extracted file path is strictly within the target directory.
  - Uploaded files **do not undergo server-side sanitization** (HTML overlays are expected to contain scripts), but files are loaded only by loopback OBS.

**Static HTML SignalR Data Contract:**

Static HTML connects to `/hubs/overlay/{chat|alerts|member}` via `OverlayCommon.initSignalRConnection(hubUrl, handlers)` provided in `js/overlay-common.js`. The event payload structure **shares the same DTO allowlist as Vue presets** (see Event → Overlay Mapping, Phase 6 Task 15 reflection validation). Any new fields must pass reflection tests first.

**Member Card in Chat Overlay (Cross-Hub Embedding):**

When a member triggers a chat message, the backend `OverlayModule` includes an optional `memberSnapshot` field in the chat hub payload (DTO identical to the member hub allowlist, excluding `memberId`/`totalLoyalty`/`linkedPlatforms`). The frontend / static HTML preset can choose whether to render an inline member card chip (showing avatar + check-in count).

Control flag: `overlay.chat.show_member_card` (bool, default `false`, toggled in system settings). Defaults to false to maintain KapChat's minimalist aesthetic.

**OneComme Template Import (Plugin Extension Path, Non-Core):**

- Core only provides the "Custom HTML Upload" contract; OneComme template importing is implemented by an independent plugin (`Vulperonex.Plugins.OneCommeBridge`).
- Plugin responsibility: parses OneComme `template.html` + `template.css`, maps OneComme `comment.*` variables to Vulperonex event DTO fields, generates standalone HTML, and lands it in `wwwroot/overlay/custom/oc-{slug}/` via the upload contract.
- The mapping table and OneComme variables comparison are detailed in `docs/plugins/onecomme-bridge.md` (TBD).

**Built-in Preset Visual Baseline:**

| Preset | Hub | Visual Characteristics | Source Inspiration |
|---|---|---|---|
| `kapchat` (Default) | chat | Transparent borderless, single-line compact, `text-shadow` outline ensuring game screen readability. Badges → Name → Colon → Content | nightdev.com/kapchat |
| `compact` | chat | Two-line condensed, latest 10 messages | Vulperonex Custom |
| `rotan-checkin` (Default) | member | Purple-gold foil streaming border + SVG paw stamp + halftone grid background; left: avatar/name, right: 10-grid collection stamp board | menber_byRotan (rewritten, no direct reference to original assets) |

**Member Stamp Board Controller (Admin Settings):**

| Setting Key | Type | Default | Description |
|---|---|---|---|
| `overlay.member.background_url` | string (URL) | Empty | Card background image. Empty uses built-in gradient. |
| `overlay.member.stamp_url` | string (URL) | Empty | Custom stamp image. Empty uses built-in SVG paw stamp. |
| `overlay.member.stamps_per_round` | int | 10 | Stamps needed to complete a round. |
| `overlay.chat.show_member_card` | bool | false | Whether to embed member card chips inside chat overlays. |
| `overlay.chat.preset` | string | `kapchat` | Chat preset key (built-in key or `custom:{slug}`) |
| `overlay.member.preset` | string | `rotan-checkin` | Member preset key |

**URL Security (Frontend Sanitization):** Any values injected from settings into CSS `url()` only accept `https?:` or `data:image/(png|jpe?g|gif|svg+xml|webp);` schemes, and forbid characters that can break out of `url()` like `"`, `'`, `(`, `)`, `\`, `;` (preventing CSS injection).

**Twitch Client ID Namespace Ownership (ADR):**

`twitch.client_id` does not belong to the `oauth.*` protected namespace (only refresh tokens belong to `oauth.*`, written exclusively by the PKCE flow). `twitch.client_id` is a public value configurable by general users via the admin UI (OAuth client_id is exposed anyway in public client flows in the frontend authorization URL), and is thus grouped under the general `twitch.*` namespace, allowing `/api/config` CRUD. The authorization gate (admin only + loopback only) remains covered by Phase 6's existing security contracts.

**Event → Overlay Mapping:**

| Domain Event / Workflow Action | Primary Overlay Target | Optional Embedding |
|---|---|---|
| `UserSentMessageEvent` | `/overlay/chat` | `memberSnapshot` carried in chat payload (dependent on `overlay.chat.show_member_card`) |
| `UserFollowedEvent`, `UserSubscribedEvent`, `UserGiftedSubscriptionEvent`, `ChannelRaidedEvent` | `/overlay/alerts` | — |
| `TriggerCheckInAction` Executed Successfully | `/overlay/member` main stamp card (first-class since Phase 7D) | `memberSnapshot` synchronized in subsequent chat payloads for that member |
| *(Future `SystemEvent` — Giveaway etc.)* | `/overlay/member` depending on preset | — |

The overlay page reads `DisplayHints` directly from the event payload to retrieve avatars, colors, and badges — zero extra API calls on the rendering path.

---

#### 4.14.2 CheckIn → Member Overlay Binding (Phase 7D)

**Background:** Phase 7C established the `MemberOverlayView` + member-card preset + `OverlayMemberHub`, but `TriggerCheckInActionExecutor` only wrote to SQLite `MemberStreamState` and never published events to `OverlayEventForwarder`, leaving `/overlay/member` with no push updates in practice. Phase 7C cross-hub chat embeds (`memberSnapshot`) also only queried the DB on chat event paths, lacking linkage with check-in actions.

**Phase 7D Design:**

1. **New Domain Event `MemberCheckedInEvent`** (under `Vulperonex.Domain.Events`):
   - Fields: `EventId`, `OccurredAt`, `Platform`, `PlatformUserId`, `DisplayName`, `AvatarUrl?`, `CheckInCount`, `TotalLoyalty`, `RoundIndex`, `StampSlotInRound`.
   - `RoundIndex = ceil(CheckInCount / overlay.member.stamps_per_round)`; `StampSlotInRound = ((CheckInCount - 1) mod stampsPerRound) + 1`.
   - Published to `IStreamEventBus` by `TriggerCheckInActionExecutor` upon successful `IncrementCheckInAsync`.
2. **`OverlayEventForwarder` subscribes to `MemberCheckedInEvent`**, maps it to `OverlayMemberPayload` pushed to `OverlayMemberHub` group, and writes it to `IOverlayHistoryService<OverlayMemberPayload>`.
3. **Chat Embed Sync Reuse:** The chat hub still queries the member cache to obtain snapshots along the `UserSentMessageEvent` processing path (existing Phase 7C behavior), not depending on `MemberCheckedInEvent`, but cache TTL must be shorter than the stamps-per-round cycle so that chat chips reflect new counts immediately after check-in. Validated existing `PlatformUserInfoCache` TTL.
4. **Display Control:**
   - `/overlay/member` is **enabled** by default to receive updates (main landing for OBS browser sources).
   - `/overlay/chat` inline chip rendering is controlled by `overlay.chat.show_member_card` (bool, default false) (existing Phase 7C setting).
   - The two paths do not block each other; either can be enabled independently.
5. **DTO Allowlist Expansion:** After adding `RoundIndex` and `StampSlotInRound` fields to `OverlayMemberPayload`, reflection tests (existing `OverlayDtoWhitelistTests`) must be expanded accordingly while maintaining the `memberId/totalLoyalty/linkedPlatforms` exclusion rules. `TotalLoyalty` **does not** enter the overlay payload (retaining sensitivity); overlay only concerns `CheckInCount`.
6. **CLI Side:** The `simulate checkin` CLI subcommand must publish `MemberCheckedInEvent` (rather than directly calling the repository) to walk the complete overlay push validation chain.

**Verification:**
- Reflection Tests: `OverlayMemberPayload` JSON key set matches the new allowlist precisely.
- Integration Test: After executing `TriggerCheckInActionExecutor`, `OverlayMemberHub` group receives `OverlayMemberPayload`, and it is queryable from the history endpoint.
- Browser Manual: `simulate checkin` → `/overlay/member` displays the card within 5 seconds.

---

#### 4.14.3 Custom HTML Overlay Editing & Deployment Pipeline (Phase 7D replaces Phase 7C pure zip upload)

**Background:** Phase 7C introduced `POST /api/overlay/custom-presets` for pure zip uploads, but faced two structural issues:
- **Unable to validate template legitimacy:** Uploading lands directly in `wwwroot/overlay/custom/{slug}/`; whether the HTML successfully hooks into SignalR or conforms to the DTO contract remained unknown until the user ran OBS manually.
- **High modification costs for users:** Changing a CSS color required re-archiving the zip and re-uploading, offering no online iteration experience.

Phase 7D introduces a **dual-mode pipeline**:

| Mode | Entry | Target |
|---|---|---|
| **Online Monaco Editor (Primary)** | `/admin/overlay-editor` | Online HTML/CSS/JS editing, draft/production dual environments, iframe live preview, pre-deployment lint+probe |
| **Zip Upload (Fallback)** | Existing `POST /api/overlay/custom-presets` | Bulk import of third-party templates. Lands in draft, allowing further tweaks in Monaco editor |

**File Layout:**

```
wwwroot/overlay/custom/{slug}/
├── production/        # Deployed version, OBS load path: /overlay/custom/{slug}/index.html → /overlay/custom/{slug}/production/index.html
│   ├── index.html
│   ├── styles.css
│   └── ...
├── draft/             # Draft version, under editing. Preview path: /overlay/custom/{slug}/draft/index.html
│   ├── index.html
│   └── ...
└── history/           # Deployment history, retains latest N copies (default 10)
    └── {iso-timestamp}/
```

**API Additions:**

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/overlay/custom-presets/{slug}/files` | Lists all relative file paths inside slug + draft/production differences |
| `GET` | `/api/overlay/custom-presets/{slug}/files/{path}?env=draft\|production` | Reads single file content (UTF-8 text) |
| `PUT` | `/api/overlay/custom-presets/{slug}/files/{path}` body=raw | Writes single file to draft |
| `DELETE` | `/api/overlay/custom-presets/{slug}/files/{path}` | Deletes single file from draft |
| `POST` | `/api/overlay/custom-presets/{slug}/validate` | Runs validation gate against draft, returns issues list |
| `POST` | `/api/overlay/custom-presets/{slug}/deploy` | Atomic copy draft → production, moves old production to history/{ts} |
| `POST` | `/api/overlay/custom-presets/{slug}/rollback?to={ts}` | Moves history/{ts} → production |
| `GET` | `/api/overlay/custom-presets/{slug}/history` | Lists history timestamps + sizes |

**Validation Gate:**

`POST /validate` must pass before deployment, otherwise `POST /deploy` returns 422 + issues list. Check items:

1. **File Structure:** `index.html` must exist at the draft root.
2. **HTML Syntax:** Parsed using `AngleSharp` against `index.html`; parse errors block deployment.
3. **CSS Syntax:** Parsed using `ExCSS` for all `*.css` files; parse errors block deployment.
4. **JS Syntax:** Attempted parse of all `*.js` files using `Jint` (parsing only, no execution); syntax errors block deployment.
5. **SignalR Contract Probe:** Regex scanning of all `<script>` and external `*.js` in `index.html`:
   - Must contain `OverlayCommon.initSignalRConnection(` or `signalR.HubConnectionBuilder` strings (confirming hub connection).
   - Must reference at least one `/hubs/overlay/{chat|alerts|member}` URL pattern.
   - Failure in either downgrades to a warning and does not block deployment (template could be pure showcase).
6. **File Size:** Single file limit 2MB, total slug limit 10MB (including history). Exceeding blocks deployment.
7. **Path Security:** All file paths must be relative and contain no `..`; `PUT /files/{path}` re-validates server-side.
8. **External Resource Warnings:** `<script src="http...">`, `<link href="http...">`, `@import url(http...)`, `url(http...)` in HTML/CSS are warned against (not blocked), reminding streamers that offline environments will break them.

**Draft/Production Isolation:**
- Editing drafts does not affect production loaded by OBS.
- `GET /overlay/custom/{slug}/index.html` (no path suffix) consistently maps to `production/index.html`. OBS does not touch draft.
- The preview iframe points to `/overlay/custom/{slug}/draft/index.html`, isolated from production.
- Deploy is an **atomic operation**: verifies history writing is complete, then **copies the entire directory** from draft to production (not rename, avoiding partial file failures); returns 200 only upon successful copy.

**Zip Upload Fallback Integration:**
- The existing zip upload via `POST /api/overlay/custom-presets` remains available. The decompression target shifts to `wwwroot/overlay/custom/{slug}/draft/` (not directly to production).
- Automatically runs validation post-upload, returning results to the admin UI.
- Users can open and tweak the files in the Monaco editor and hit deploy to go live.

**Security:**
- Adheres to Phase 6 loopback-only contracts.
- All path parameters undergo slug + relative path sanitization (no `..`, no absolute paths, no control characters).
- Verifies the final absolute path is within `wwwroot/overlay/custom/{slug}/draft/` before writing.
- History retention is bounded by both count (10) and total slug size (10MB) to prevent disk space explosions.

---

### 4.15 Log Structure (G13)

**Uses Serilog with three Sinks:**

```
Serilog
├── Sink: Console       — Colored, for development, default level: Debug
├── Sink: Rolling file  — Daily rotation, default retention: 7 days
└── Sink: SQLite Table AppLogs — In-app log viewer, default retention: 30 days
```

**Each log entry is enriched with structured context fields:**

```csharp
Log.ForContext("EventTypeKey",   evt.EventTypeKey)
   .ForContext("Platform",       evt.Platform)
   .ForContext("MemberId",       memberId)
   .ForContext("WorkflowRuleId", ruleId)
   .ForContext("ActionType",     actionType)
   .Information("Workflow action executed");
```

**Log levels and retention periods are configurable via `SystemSettings` (hot-loaded, no restart required):**

```
log.min_level             = "Information"   // Debug | Information | Warning | Error
log.file_retention_days   = 7
log.db_retention_days     = 30
log.db_max_size_mb        = 100             // Secondary limit; deletes oldest rows when exceeded (whichever triggers first)
```

The `AppLogs` SQLite table supports the in-app log viewer (filterable by level, EventTypeKey, MemberId). Background worker cleanup logic (whichever triggers first): (1) purges rows older than `log.db_retention_days`; (2) if SQLite page count × page_size exceeds `log.db_max_size_mb`, continuously deletes the oldest rows until estimated size falls below the threshold. **Note: SQLite DELETE does not immediately shrink physical file size**; `PRAGMA auto_vacuum = FULL` (set at DB creation) or an explicit `VACUUM` must run after cleanup; setting `auto_vacuum = FULL` at creation is recommended to return pages to the OS upon deletion. Test verification estimates size via `PRAGMA page_count * page_size` rather than relying on physical `FileInfo.Length`.

---

### 4.16 Plugin Discovery (G14)

**MVP: Static DI Registration Only.** Plugins are referenced as project/package dependencies and registered at compile-time in `Program.cs`. No runtime DLL scanning or `Assembly.LoadFrom()` is performed in the MVP.

```csharp
// Program.cs (MVP)
builder.Services.AddVulperonexPlugin<SoundPlugin>();
builder.Services.AddVulperonexPlugin<MyCustomPlugin>();
```

**Phase 2 (Deferred): Directory Scanning + Runtime Discovery.**

```
Out of Scope (Phase 1):
  - {app_dir}/plugins/*.dll directory scanning
  - Assembly.LoadFrom() / AssemblyLoadContext
  - Hot loading / hot unloading of plugins
```

Phase 2 will introduce: scanning `{app_dir}/plugins/`, optional allowlist/denylist in `appsettings.json`, and `AssemblyLoadContext` for unloading. A single DLL loading failure → logs error → skips → other DLLs remain unaffected.

**Startup Order (MVP):**
1. Resolves statically registered plugins from DI.
2. Calls `InitializeAsync(IPluginContext)` on each plugin.
3. Plugins register their `EventTypeKey` into `IStreamEventTypeRegistry`.

---

### 4.17 Web Host Security (G15)

**Dual-Port Architecture: API Port + Overlay Port, both loopback-only, no authentication required.**

```
appsettings.json:
  "Web": {
    "ApiPort":     5000,   // Configurable
    "OverlayPort": 5001    // Configurable
  }
```

**Always runs loopback-only (no remote access):**
```
ApiPort     5000 → Loopback only (127.0.0.1 and ::1), no authentication required
OverlayPort 5001 → Loopback only (127.0.0.1 and ::1), no authentication required
OBS Browser Source: http://localhost:5001/overlay/chat  (Clean URL, no token)
```

Kestrel binds both ports to `IPAddress.Loopback` (IPv4) + `IPAddress.IPv6Loopback` (IPv6). The security boundary is the bind address itself; no ApiKeyMiddleware or X-Vulperonex-Key headers are required.

**Overlay DTO Security:** Overlay DTOs must be public secure projections — even if the server is permanently loopback-only, strict DTO allowlists are required (preventing accidental future field leakage, and preventing SignalR serialization over-exposure):

| Overlay | Allowed Fields | Forbidden Fields |
|---|---|---|
| `/overlay/chat` | SchemaVersion, EventId, Timestamp, DisplayName, ColorHex, Segments, Badges | MemberId, UserId, TotalBitsGiven |
| `/overlay/alerts` | SchemaVersion, EventId, Timestamp, DisplayName, EventType, Tier | MemberId, PlatformUserId |
| `/overlay/member` | SchemaVersion, DisplayName, AvatarUrl, CheckInCount (current session only) | MemberId, TotalLoyalty, LinkedPlatforms |

`SchemaVersion` is fixed to `1`. `EventId` is the overlay public delivery ID used for frontend deduplication, and must not expose MemberId, PlatformUserId, or other internal identities; platform-provided IDs are preferred (IRC `msg-id` / EventSub `message_id`), and adapters generate ULIDs (marked as synthetic) in their absence. `Timestamp` is the UTC ISO-8601 event time, used for frontend sorting. `/overlay/member` represents a status snapshot, not an event stream, and thus lacks `EventId` / `Timestamp`. `OverlayModule` maps domain events to these restricted DTOs prior to SignalR pushing. Allowlist enforcement occurs at the DTO type level — no dynamic mapping.

**OBS Browser Source URLs:**
```
http://localhost:5001/overlay/chat
http://localhost:5001/overlay/alerts
http://localhost:5001/overlay/member
```

**DB Path Resolution Rules (Shared by CLI and Web Host):** DB path resolution: `appsettings.json → Database:Path` (if present), falling back to OS app-data default path (see §4.11). **`Database:Path` does not allow overrides via `appsettings.{Environment}.json` or environment variables** — both Web Host and CLI only read the primary `appsettings.json` to guarantee they access the same database. If custom paths are needed in development, modify `appsettings.json` directly (avoiding Development overrides).

**Kestrel Dual Loopback Bindings:**
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback,       apiPort);      // IPv4 loopback
    options.Listen(IPAddress.IPv6Loopback,   apiPort);      // IPv6 loopback
    options.Listen(IPAddress.Loopback,       overlayPort);
    options.Listen(IPAddress.IPv6Loopback,   overlayPort);
});
```

---

### 4.18 Unified Monitor Page (Phase 7D)

**Background:** Currently, simulating events, viewing the chat overlay, and viewing member cards reside on different tabs/routes. The streamer's debug workflow requires: admin simulate → switch to `/overlay/chat` to see results → switch to `/overlay/member` to see member card → back to simulate to try again. The context switching creates high driver friction.

**Phase 7D Design:** Introduces a unified `/monitor` page; the old standalone simulate and overlay pages are retained for debugging and E2E testing.

**Layout (Widescreen ≥1280px):**

```
┌─────────────────────────────────────────────────────────────┐
│ Header: Platform Connection + SignalR Status + Live/Settings│
├──────────────┬──────────────────────────────┬───────────────┤
│              │                              │               │
│  Simulate    │  Overlay Preview (iframe)    │  Chat Stream  │
│  Controls    │                              │  (live)       │
│  (sider)     │  ┌────────────────────────┐  │ User: message │
│  • chat      │  │ Preview: chat / member │  │ ...           │
│  • follow    │  │                        │  │               │
│  • sub       │  │  iframe                │  │               │
│  • giftsub   │  │                        │  │               │
│  • raid      │  └────────────────────────┘  │               │
│  • bits      │  BG: transparent/green/      │               │
│  • redeem    │      pink/color/image        │               │
│  • checkin   │  Preset Switch Dropdown      │               │
│  • batch     │  Reload Button               │               │
│              │                              │               │
└──────────────┴──────────────────────────────┴───────────────┘
```

**Layout (Narrow Screen <1280px):**
- Simulate controls transform into a right-side drawer, with a toggle button added to the header.
- Overlay preview and chat stream stack vertically.

**Features:**
1. **Simulate Controls Sider:** Contains all simulate event subcommand UIs (chat / follow / sub / giftsub / raid / bits / redeem / checkin), mapping to existing `/api/simulate/*` endpoints. Adds a "Batch Check-In" tool to fire N check-in events with a single click.
2. **Overlay Preview Iframe:**
   - Dynamic iframe `src` switching between chat / member / alerts, embedding a `?preset={key}&t={ts}` query.
   - Preview background switcher (transparent / green screen / pink screen / solid colors / custom background URL) to inspect overlay appearance against different backdrops.
   - Reload button (bumps query timestamp).
   - Advanced: supports draft/production switching when custom presets are selected (integrated with §4.14.3).
3. **Chat Stream Panel:** Subscribes to `/hubs/overlay/chat`, listing the latest N messages (plain text, including member chip previews), without rendering preset CSS (plain table style), allowing streamers to inspect the "data layer" (decoupled from overlay visuals).
4. **Header Status:** Platform connection status (Twitch ✅/❌), SignalR connection status, and current preset settings summary.

**Route Retention:**
- Standalone `/simulate` page is not deleted (used for CLI E2E and automated testing).
- Standing `/overlay/chat`, `/overlay/member`, and `/overlay/alerts` routes are not deleted (OBS browser sources point directly to these URLs).
- `/monitor` becomes the **new default landing** (replacing `/` default); the existing admin entry remains in the sidebar.

**Real-Time Reaction to Events:** When SignalR is connected, simulated actions trigger immediate, real-time updates in both the preview iframe (notified reversely via the hub) and the chat stream, removing the need for manual reloads.

**i18n:** Full support for zh-TW + en-US.
**a11y:** Sider toggle has `aria-label`, drawer focus trap inherits the ConfirmDialog pattern.

---

### 4.19 Member Admin Editable Surface (Phase 7D)

**Background:** Phase 6 `/admin/members` was read-only to prevent accidental administrative modifications. However, real streamer workflows require:
- Manually adjusting member loyalty / check-in counts (correcting errors, activity compensation).
- Inspecting modification history for a specific member (who, when, why).
- Resetting specific member loyalty without deleting their identities.
- Deleting test members completely.
Using the CLI during a stream has high window-switching costs.

**Phase 7D Design:** Member admin becomes editable. **All changes write to an audit log** to retain traceability.

**New Endpoints:**

| Method | Path | Purpose | Required Body |
|---|---|---|---|
| `PATCH` | `/api/members/{memberId}/loyalty` | Adjusts totalLoyalty / checkInCount | `{ totalLoyalty?: int, checkInCount?: int, reason: string }` |
| `POST` | `/api/members/{memberId}/reset` | Resets loyalty to zero (retains identity) | `{ resetLoyalty: bool, resetCheckIn: bool, reason: string }` |
| `DELETE` | `/api/members/{memberId}` | Deletes member completely (including identity) | `{ reason: string }` |
| `GET` | `/api/members/{memberId}/audit` | Retrieves member change history | query: `?limit=50&offset=0` |

**Audit Table:** Adds `MemberAuditLogs` SQLite table:

```
MemberAuditLogs:
  Id              ULID PK
  MemberId        ULID FK
  OccurredAt      DateTimeOffset
  ActorKind       enum { 'user' | 'workflow' | 'cli' | 'system' }
  ActorId         string?         -- workflow rule id / cli session id / null for user
  Operation       enum { 'adjust_loyalty' | 'adjust_checkin' | 'reset' | 'delete' | 'create' }
  BeforeJson      string?         -- snapshot before
  AfterJson       string?         -- snapshot after
  Reason          string          -- required, non-empty
```

**Concurrency:** All mutation endpoints adopt `If-Match` headers carrying an `etag` (derived from `MemberRecord.UpdatedAt` ticks hash). Version mismatches → 409 Conflict. Frontend prompts reload upon receiving 409.

**Validation:**
- `totalLoyalty >= 0`, `checkInCount >= 0`
- `reason.Length in [3, 500]`
- DELETE requires a confirmation token: request 30s token via `POST /api/members/{id}/delete-token` first, which must be passed in the DELETE body (guarding against accidental clicks).

**Frontend UI:**

| Component | Target Endpoint | Mode |
|---|---|---|
| AdjustLoyaltyModal | `PATCH /loyalty` | Form: new values + adjustment reason. Displays before/after diff. |
| ResetModal | `POST /reset` | Confirmation dialog: reset loyalty / checkIn checkboxes + reason |
| DeleteConfirmDialog | `DELETE /` | Two-stage confirmation: stage 1 retrieves token, stage 2 executes |
| AuditLogDrawer | `GET /audit` | Right drawer: timeline displaying change history, including actor, before/after snapshots, and reasons |

**Workflow Integration:** `TriggerCheckInAction` writes an audit log entry after incrementing, with ActorKind='workflow' and ActorId=ruleId. `TriggerAdjustLoyaltyAction` (if introduced in Phase 7D) follows the same pattern.

**Security:**
- Adheres to loopback-only.
- DELETE token + required reason prevent accidental deletions.
- Audit log is append-only (undeletable/unmodifiable) and retention inherits `log.db_retention_days` (but member audit is tracked independently, defaulting to 365 days).
- Reflection Tests: DTOs returned by endpoints exclude internal PKs except `MemberId`.

---

### 4.20 Module & Plugin Management (Phase 7D)

**Background & Motivation:**
Core services (check-in, counters, lottery points, AV effects, external OneComme Bridge, etc.) execute as Hosted Services or plugins, but lacked a centralized management page to toggle their ON/OFF states. Operating dependent modules blindly when underlying modules are disabled leads to state drift.

**Design & Specifications:**
1. **Module/Plugin Switch State Storage:**
   - Toggle states are stored in the database/system settings via `ISystemSettingsService` using key names `modules.enabled.{moduleName}`.
   - All core Hosted Services dynamically detect this config in `ExecuteAsync` or `StartAsync`, skipping registration, interception, or action execution (entering a No-Op state) if `false`. Already running Hosted Services transition their states immediately upon configuration changes.
   - For `IWorkflowActionExecutor` (e.g. `TriggerCheckInActionExecutor`), actions must be rejected with a `WorkflowExecutionException` if the associated module (e.g. CheckInModule) is disabled.

2. **Dependency Resolution:**
   - **Dependencies defined:**
     - `CheckInModule` (Check-In) -> depends on `MemberModule` (Member Core)
     - `LotteryModule` (Lottery/Points) -> depends on `MemberModule` (Member Core)
     - `OneCommeBridge` (OneComme Plugin) -> no dependencies, requires Core Event Bus
     - `OverlayModule` (Overlay Module) -> no dependencies
   - **Cascading Disable:**
     - Disabling a module depended on by others (e.g., disabling `MemberModule`) **must** trigger cascading dependency disabling.
     - **UI Cascade Warning Gate:** The frontend displays a warning: "Disabling 'Member Core Module' will also turn off the following dependent modules: Check-In Module, Lottery Module. Confirm disable?".
     - Upon user confirmation, the API writes `false` to all dependent modules and appends an audit log with `ActorKind = 'user'` and `Operation = 'disable_module'`.
   - **Cascading Enable:**
     - When enabling a module with dependencies (e.g. enabling `CheckInModule`), if its dependee (e.g. `MemberModule`) is disabled, the system should **automatically enable the dependee** or **display a warning and block startup**.

3. **Module Management Endpoints (API):**
   - `GET /api/plugins-modules`: Lists all module/plugin names, Chinese display names, descriptions, current running status (`IsActive`), and dependencies.
   - `POST /api/plugins-modules/{name}/toggle`: Parameter: `enabled: bool`. Computes dependencies, and returns the complete list of module states after topological changes.

4. **UI Management Page:**
   - Adds a "Functional Modules & Plugins" tab under `/admin/settings` displaying ON/OFF toggles in a card-based grid, categorized with tags (Core Services, Interactions, Audiovisual, External Plugins) and dependency icons.

---

### 4.21 Event & Loyalty Simulation (Phase 7D)

**Background & Motivation:**
Existing event simulation is limited to basic actions like `chat`, `follow`, and `sub`. Crucial support for "StreamRole flags / custom roles" and "loyalty points / check-ins" was missing from the UI and API, leaving streamers unable to verify and debug complex workflows directly inside the Admin panel (e.g., check-in rewards restricted to Moderators, special effects triggered only for VIPs).

**Design & Specifications:**
1. **StreamRole flags Simulation:**
   - Expands the `SimulateRequest` DTO accepted by `/api/simulate/*`: its `Roles` property supports both single string/number and string arrays (e.g., `["subscriber", "moderator", "vip"]`), allowing multiple `StreamRole` flags to be packed into simulated events' User payloads.
   - The simulator UI provides a Checkbox Group allowing streamers to toggle and stack roles arbitrarily.

2. **Check-In & Loyalty Simulation Endpoint:**
   - Adds `POST /api/simulate/checkin` endpoint.
   - Parameters:
     - `platformUserId`: string (ID of the member to simulate check-in, defaults to random)
     - `displayName`: string (Display name of the member to simulate, defaults to random)
     - `skipCooldown`: bool (Whether to bypass check-in cooldown, defaults to true)
     - `stampCount`: int (Number of stamps/check-ins to accumulate, defaults to 1)
   - **Behavior:** The endpoint directly invokes `IMemberResolver` and `IMemberStreamStateRepository` to increment check-in counts. Upon successful persistence in SQLite, it publishes a `MemberCheckedInEvent` to the Event Bus, triggering immediate stamp board visuals on the OBS Overlay and Preview Hub.

---

### 4.22 Intuitive Workflow Rule Editor (Phase 7D)

**Background & Motivation:**
The existing workflow rule configuration interface required users to write specific JSON or raw text expressions, which was highly unintuitive for non-technical streamers. This phase introduces a visual guided editor, completely discarding inefficient free-text configurations.

**Design & Specifications:**
1. **Condition Builder:**
   - Discards raw NCalc text in favor of a row-based visual rule list.
   - Each condition consists of three dropdown menus: `[Variable Selector]` -> `[Comparison Operator]` -> `[Target Value/Constant]`.
   - The Variable Selector dynamically reads registered keys from `StreamEventTypeRegistry` and context variables pre-provided by the Workflow (such as `user.name`, `message.text`, `member.stamps`), avoiding typos via clickable dropdown selections.
   - The frontend component automatically compiles the visual configuration and outputs a standard NCalc expression (e.g., `member.stamps >= 10`) to the backend API.

2. **Dynamic Action Form:**
   - Dynamically generates strong-typed input controls for each Action type (e.g., `TriggerCheckIn`, `RefundTwitchRedemption`, `TriggerEffect`, etc.) based on `ActionParameterMetadata` (types including string, number, boolean, select, text) registered by the backend.
   - Integrates a floating variable selector panel. When focusing on input fields or typing `{`, a list of available variables pops up, allowing users to click and insert variable template strings (such as `{user.displayName}`), preventing typos.

---

### 4.23 Twitch Badge Cache & Simulator Badge UI (Phase 7E)

**Background & Motivation:**
Chat overlays must display Twitch native badges correctly (VIP / Moderator / Subscriber / Founder / custom channel badges like "Artist" or "Sponsor"). The current implementation had two defects:
1. **Broken badge icons on real Twitch paths:** The IRC parser parsed `badges` IRC tags into key strings like `subscriber/0` or `vip/1` and wrote them to `PlatformUserDisplayInfo.Badges`. `OverlayEventForwarder.ForwardChatEventAsync` broadcast these keys directly as URLs to the overlay via SignalR, causing `<img :src>` to render as the key string rather than the actual image URL.
2. **Simulator lacks badge selection:** `SimulateControlsPanel.vue` only allowed selecting `Subscriber/Moderator/VIP/Follower` text roles, rendered as `chat-role-pill` text capsules; it could not trigger badge icon paths or select custom channel badges.

References: `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchChannelApiService.cs` (`GetGlobalBadgesAsync` / `GetChannelBadgesAsync`), `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/IdentityService.cs` (`SyncBadgesAsync` / `GetBadgeUrl` caching `badge_{set}_{ver}` → URL with a 7-day TTL), `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchMessageEnricher.cs` (parses badges KVPs into URL lists and writes to ChatMessage during message enrichment).

**Design & Specifications:**

1. **`ITwitchHelixClient` Badge Endpoints:**
   - Adds `Task<IReadOnlyDictionary<string, TwitchBadgeDescriptor>> GetGlobalBadgesAsync(CancellationToken)`: maps to `GET helix/chat/badges/global`.
   - Adds `Task<IReadOnlyDictionary<string, TwitchBadgeDescriptor>> GetChannelBadgesAsync(string broadcasterId, CancellationToken)`: maps to `GET helix/chat/badges?broadcaster_id={id}`.
   - Returned dictionary keys formatted as `{set_id}_{version}`, with `TwitchBadgeDescriptor` values containing `SetId`, `Version`, `ImageUrl1x`, `Title?`, and `Description?`.

2. **`ITwitchBadgeCache` (Application Interface) + `TwitchBadgeCache` (Infrastructure Implementation):**
   - Interface: `string? Get(string key)`, `IReadOnlyList<TwitchBadgeDescriptor> ListAll()`, `Task SyncGlobalAsync(CancellationToken)`, `Task SyncChannelAsync(string broadcasterId, CancellationToken)`, and `bool IsReady`.
   - Implementation: thread-safe `ConcurrentDictionary<string, TwitchBadgeDescriptor>`; retains old data and logs warning on sync failure.
   - Note: The cache is in-memory and must be synchronized upon application restart; no disk persistence.

3. **`TwitchBadgeSyncHostedService`:**
   - Fires-and-forget `SyncGlobalAsync` + `SyncChannelAsync(broadcasterId)` inside `StartAsync` (broadcaster ID comes from `Twitch:BroadcasterId` configuration, skipped if not set).
   - Synchronization failure does not block application startup.

4. **`OverlayEventForwarder` Badge Resolution:**
   - `ForwardChatEventAsync` and `TryResolveMemberSnapshotAsync` inject `ITwitchBadgeCache`, mapping `display?.Badges` key lists to URL lists via `cache.Get`. Keys without corresponding URLs are filtered out to prevent broken images.
   - The `OverlayChatPayload.Badges` broadcast via SignalR is corrected to carry resolved URL lists (contract schemaVersion remains unchanged).
   - Text roles are still output (`event.roles`) for future preset usage; this period's `ChatPresetDefault.vue` no longer renders text role chips.

5. **Simulator Badge/Color Passthrough:**
   - `SimulationRequest` (`SimulationKind.Message`) adds `IReadOnlyCollection<string> Badges` and `string? ColorHex`.
   - `SimulateEndpoints` accepts `badges: string[]` and `colorHex: string?` from the request body, and calls `IPlatformUserInfoCache.UpsertAsync` for the simulated user (`simulation:{userId}`) prior to calling `adapter.SimulateAsync`. This writes `Badges` + `ColorHex` to the cache, unifying resolution paths between simulated and real Twitch paths.
   - This design avoids adding badge fields to domain events, keeping the domain model clean.

6. **New API Endpoint `GET /api/twitch/badges`:**
   - Returns `{ global: TwitchBadgeDescriptor[], channel: TwitchBadgeDescriptor[] }`, allowing the frontend picker UI to list available badges.
   - Returns empty arrays with `Cache-Control: no-store` headers when the cache is not yet ready.
   - Subject to admin auth restrictions (consistent with other `/api/twitch/*` endpoints).

7. **Frontend `SimulateControlsPanel.vue` UI Refactor:**
   - `onMounted` calls `getTwitchBadges()`, grouping and displaying badges by `setId` in a stamp chip grid (`<img>` + tooltip titles); clicking toggles their inclusion in `selectedBadges`.
   - Adds a "Username Color" field: hex input paired with a real-time color swatch (defaults to `#FFCA28`).
   - Submit includes `badges: selectedBadges` and `colorHex` in the request body.
   - Removes the "Streamer Roles" text checkbox area (backward compatibility: if legacy tests pass `roles`, they are derived to corresponding badge keys backend-side; hidden from UI).

8. **Frontend `ChatPresetDefault.vue` Adjustment:**
   - Removes the `chat-role-pill` text role chip rendering section; badge icons become the sole identity indicators.
   - Badge `<img>` rendering remains as-is; adds `onerror` fallback to hide broken images.

**Acceptance Criteria:**
- Simulator sends chat messages containing badges; `/overlay/chat` displays native Twitch PNG badges rather than text chips.
- Real Twitch IRC chat messages (VIP / Moderator / Subscriber) display icons normally in overlays.
- `GET /api/twitch/badges` returns the global collection (broadcaster, moderator, vip, subscriber, founder, etc.) and custom channel badges.
- Username hex colors are applied to overlay `chat-username` style via `style="color"`.
- Cache misses do not result in broken images on overlays.
- Unit testing coverage: `TwitchBadgeCacheTests`, `OverlayEventForwarderBadgeResolutionTests`, `SimulateEndpointsBadgeTests`, and frontend `SimulateControlsPanel.test.ts`.

**Boundaries:**
- No local downloading of badge image files for base64 embedding; Twitch CDN URLs are used throughout.
- No support for uploading custom badges (only displays badges registered on Twitch).
- No integration with BTTV / 7TV / FFZ badges or emotes.
- No audit logging for badge changes.
- No automatic periodic background cache refresh (requires restart to pull new custom badges; a 24h refresh hosted service can be added in a future phase).

---

## 5. Commands

```bash
# --- Backend ---
dotnet build
dotnet test
dotnet run --project src/Hosts/Vulperonex.Web
dotnet run --project src/Hosts/Vulperonex.Desktop

# --- CLI ---
vulperonex simulate chat   --user alice --message "hi"
vulperonex simulate follow --user alice
vulperonex simulate sub    --user alice --tier 1000
vulperonex simulate checkin --user alice --stamps 1
vulperonex config list
vulperonex config get      log.min_level
vulperonex config set      log.min_level "Debug"
vulperonex rule list
vulperonex rule enable     01HK...
vulperonex rule disable    01HK...
vulperonex rule delete     01HK...
vulperonex member list
vulperonex member show     01HK...
vulperonex member delete   01HK...
vulperonex db migrate
vulperonex db status
```

---

## 6. Development Conventions

### 6.1 Coding Standards

- **C#:** PascalCase for types and methods, camelCase for local variables, `_camelCase` for private fields. File-scoped namespaces and Primary Constructors where appropriate.
- **TypeScript:** camelCase for identifiers, PascalCase for components, kebab-case for view filenames.
- **Key Naming Rules:** The `Domain` and `Application` projects must not contain any `Twitch*` (or other platform-specific) prefixes. Platform vocabularies are strictly restricted to their corresponding `Adapters.<Platform>` projects.

---

## 7. Testing Strategy

### 7.1 Test Pyramid

```
                 ╱╲
                ╱  ╲    Architectural Tests (NetArchTest)
               ╱────╲   - Domain has no infrastructure dependencies
              ╱      ╲  - No "Twitch" strings in Domain/Application
             ╱        ╲
            ╱──────────╲ Integration Tests
           ╱            ╲ - SimulationAdapter → Bus → WorkflowEngine → DB
          ╱              ╲
         ╱────────────────╲ Unit Tests (Vast Majority)
        ╱                  ╲ - Domain logic, mapping, handlers, executors
```

### 7.2 Directory Layout

- `tests/Vulperonex.Tests.Unit/` — Pure unit tests, zero I/O.
- `tests/Vulperonex.Tests.Integration/` — In-memory SQLite + SimulationAdapter End-to-End tests.
- `tests/Vulperonex.Tests.Architecture/` — Architecture layer boundary enforcement.
- `src/frontend/tests/` — Vitest + Vue Test Utils.

### 7.3 Coverage Targets

- **Domain Layer (Domain):** > 90% — measured solely against `Vulperonex.Tests.Unit` (domain contains pure logic, zero I/O).
- **Application Layer (Application):** > 80% — measured solely against `Vulperonex.Tests.Unit`. Integration tests **do not** factor into this threshold (coverlet.msbuild cannot merge reports from separate test projects in a single command). If Application unit coverage drops below 80% because behaviors are covered by integration tests, the solution is to write focused unit tests using Fakes/Mocks rather than relaxing thresholds or switching to merged reports.
- **Adapters (Adapters):** Integrated and verified via SimulationAdapter equivalence tests (real adapters share the same domain mapping logic).

**Enforcement:** Uses **`coverlet.msbuild`** (rather than `coverlet.collector`) to fail builds upon dropping below thresholds. Explicit versions are pinned to prevent deviations — using central package management or `<PackageReference Include="coverlet.msbuild" Version="6.0.2" />` (pinned to the latest stable version during project setup). Wildcard versions are **not accepted** for threshold tools.

Two CI commands (both must pass):
```bash
# Domain ≥ 90%
dotnet test tests/Vulperonex.Tests.Unit \
    /p:CollectCoverage=true \
    /p:Include="[Vulperonex.Domain]*" \
    /p:Exclude="[*.Tests.*]*" \
    /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=average

# Application ≥ 80%
dotnet test tests/Vulperonex.Tests.Unit \
    /p:CollectCoverage=true \
    /p:Include="[Vulperonex.Application]*" \
    /p:Exclude="[*.Tests.*]*" \
    /p:Threshold=80 /p:ThresholdType=line /p:ThresholdStat=average
```
Any command dropping below these thresholds exits with a non-zero code, failing the CI build. `reportgenerator` can be added for HTML reports, but does not act as a threshold gate.

### 7.4 BDD + TDD Discipline

- Every behavior starts from BDD-style scenarios: Given / When / Then.
- Scenarios serve as acceptance contracts; they must map to one or more automated tests before implementation is considered complete.
- Follow TDD: write a failing test first ("Red"), write the minimal code to pass ("Green"), and then refactor under passing tests.
- New domain logic → write failing unit tests based on BDD scenarios first.
- Bug fixes → reproduce using failing tests before modifying production code.
- Refactoring → guarantee tests remain green.
- Integration scenarios should leverage `SimulationAdapter` as much as possible.
- Manual validation supplements BDD+TDD for Photino, OBS, and browser runtime behaviors, but does not replace automated acceptance tests.

**Test Naming Conventions (Minimum Standard):**
- C# test method names: `Given_<State>_When_<Action>_Then_<Expectation>` (using underscores, PascalCase sections)  
  Example: `Given_ValidRule_When_EventMatches_Then_SendChatMessageCalled`
- If standalone scenario files are not used, BDD scenarios **must** be documented in `// Given / When / Then` comment blocks at the top of the test method body.
- Frontend (Vitest): `describe` = Component/Composable Name; `it` = `should <expectation> when <condition>`

---

## 8. Boundaries

### 8.1 Always Do

- Run all applicable test suites prior to any commits: always execute `dotnet test`; once `src/frontend/` exists, `pnpm test` and `pnpm build` must be executed (can be skipped in backend tasks before Task 19). `pnpm lint` is a **manual validation step** (not enforced by CI), executed once at each checkpoint.
- New events must implement `IStreamEvent` and be immutable `record` types.
- Adapter code must reside in `Adapters/Vulperonex.Adapters.<Platform>/`.
- Platform-specific terms must remain **strictly outside** `Domain` and `Application` projects.
- Use `MemberId` (ULID) as the canonical member key, never platform UserIds.
- Execute architectural tests in CI.

### 8.2 Ask First

- Adding top-level projects to the solution (**Task 1 initial projects are pre-authorized, no need to ask individually; only additional new projects outside Task 1 require asking first**).
- Schema migrations that drop or rename columns.
- Adding NuGet / npm **dependency packages** (including dev tools like oxlint — ask once to install; executing lint after installation is a validation step and does not require asking again). Exception: testing/coverage packages required in Phase 1 Task 1c and explicitly named in this SPEC are pre-authorized and do not require asking individually: `xUnit 3`, `NSubstitute`, `FluentAssertions 7`, `NetArchTest`, and `coverlet.msbuild 6.0.2`.
- Modifying the public plugin contract (`IVulperonexPlugin`).
- Modifying the shape of core domain events after Phase 2.

### 8.3 Never Do

- Reference `Twitch*` (or any platform-specific) types in `Application` or `Domain` projects.
- Modify event objects after publishing (state changes should spawn new events).
- Mix command and query operations on the same repository (lightweight CQRS).
- Bypass the event bus — adapters must never call handlers directly.
- Persist events into the database (logging only).
- Commit credentials, OAuth tokens, or `App_Data/*.db` files.

---

## 9. Success Criteria (MVP)

- [ ] **SC-1:** Integration test `TwitchAdapter_PublishesAllSevenMvpEvents` passes: for each of the seven MVP `EventTypeKey`s, simulated Twitch payloads generate corresponding `IStreamEvent`s on the bus (verified via `WaitForIdleAsync` + captured event lists).

- [ ] **SC-2:** Integration test `WorkflowEngine_ExecutesMatchingRule_OnEventTypeKey` passes: when publishing `UserSentMessageEvent`, the `WorkflowRule` with `EventTypeKey = "user.message"` triggers its `SendChatMessageAction`; the mocked `IPlatformChatSender` receives exactly one `SendAsync` call.

- [ ] **SC-3:** Integration test `SimulationAdapter_DoesNotReferenceTwitchTypes` passes: the `Vulperonex.Adapters.Simulation` assembly contains zero type references to `Vulperonex.Adapters.Twitch` (verified via NetArchTest or reflection scans).

- [ ] **SC-4:** Architectural test `Domain_HasNoReferenceToTwitchSymbols` fails builds (Red light) if any `Twitch*` identifiers are introduced into the `Domain` or `Application` projects.

- [ ] **SC-5:** Integration test `OverlayHub_ReceivesSignalRPayload_WithinTimeout` passes: publishing `UserSentMessageEvent` via `SimulationAdapter` asserts that `/overlay/chat` SignalR Hub clients receive `OverlayChatPayload` within **5 seconds** (CI safety timeout). Performance goal (non-blocking): event-to-SignalR latency < 500ms locally, tracked as a baseline rather than a pass/fail gate.

- [ ] **SC-5b:** Integration test `WorkflowSendChatMessage_Simulation_IsObservable` passes: executing a workflow containing `SendChatMessage` on the `Simulation` platform asserts that the observable output plane (in-memory receiver / Chat Outbox / history view) displays the rendered message, platform, channel, dedupKey, and status within **5 seconds**, independent of whether `/overlay/chat` has a bridge.

- [ ] **SC-6:** Two complementary integration tests collectively satisfy this criterion (split into Task 12 + Task 13 implementations):
  - **SC-6a (WorkflowEngine half, Task 12):** `SC6a_SimulationAndTwitch_ProduceSameWorkflowSideEffect`: publishing `UserSentMessageEvent` with identical payloads via `SimulationAdapter` and `TwitchAdapter` (mock IRC) asserts that both produce identical calls to `IPlatformChatSender.SendAsync` after `WaitForIdleAsync`.
  - **SC-6b (MemberRecord half, Task 13):** `SC6b_SimulationAndTwitch_ProduceSameMemberDbState`: executing both with identical payloads (each using an independent fresh SQLite fixture) asserts that both result in identical database states for `MemberRecord` after `WaitForIdleAsync`.
  - Both tests passing = SC-6 achieved.

- **SC-7:** Removed from MVP scope (originally MockYouTube Adapter < 200 LOC validation; non-Twitch platform adapters deferred).

- [ ] **SC-8:** Integration test `MemberResolver_CreatesUlidMemberRecord_WithPlatformIdentity` passes: publishing `UserSentMessageEvent { Platform="twitch", UserId="test123" }` asserts that the `PlatformIdentity` table contains a row `(Platform="twitch", PlatformUserId="test123")` and the `MemberRecord`'s `MemberId` conforms to the ULID format.

- [ ] **SC-9:** Unit tests `SendChatMessageAction_DefaultsToSourcePlatform` and `SendChatMessageAction_RespectsTargetPlatformOverride` pass: validating `IPlatformChatSender` selection logic.

- [ ] **SC-10:** Integration test `Plugin_CanPublishCustomEvent_TriggeringWorkflow` passes: plugin calls `IPluginContext.Events.PublishAsync(customEvent)`; a `WorkflowRule` with a matching `EventTypeKey` triggers; the mocked `IPlatformChatSender` receives `SendAsync`.

- [ ] **SC-11:** Manual / Integration verification `ChatOverlayTemplatePreset_CanSwitchWithoutCodeEdit` passes: `/overlay/chat` supports switching between at least two templates without modifying frontend source code, including at least the built-in default and another installable preset; the payload contract remains unchanged post-switch, and rendering strictly adheres to DTO allowlists and text bindings.

- [ ] **SC-11b:** Extension validation `OneCommeCompatibility_ExtensionContract_Works` passes: OneComme compatibility functions as an pluggable extension / importer / adapter, not requiring core compile-time bindings; verifies recognition of OneComme template folder structures or package metadata, mapping successfully to the Vulperonex chat overlay preset contract.

---

## 10. Resolved Design Decisions

| # | Decision |
|---|---|
| D1 | Plugin Loading: **Static reference at startup** (AssemblyLoadContext / hot-loading deferred). |
| D2 | Workflow Reply Routing: **Defaults to source platform**, permitting action-level overrides via `TargetPlatform`. |
| D3 | Event Persistence: **No persistence.** Written solely to log files (LOG) with configurable retention/cleanup policies. |
| D4 | Plugin Scope: **Plugins can both publish and subscribe** to events (functioning as complete adapters). |
| D5 | Frontend Delivery: **Served via the Web host's `wwwroot`**, Desktop = Web Host + Photino window. |
| D6 | CLI Scope (MVP): **Simulation, config, rules, and member commands.** |
| D6a | CLI Identifier Resolution (Phase 5.5): `rule` positional accepts **full ID / ID prefix / `--name`**; `member` accepts **full ID / ID prefix**; multiple hits → `AMBIGUOUS_ID` + candidate list; destructive operations (`rule disable` / `rule delete` / `member delete`) prompt interactive `[y/N]` confirmation, or require `--yes` in non-interactive mode, otherwise aborting with `CONFIRMATION_REQUIRED`. Design frozen in `docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md`. |
| D7 | Member Identity: `MemberId` is a ULID; `PlatformIdentity (Platform, PlatformUserId)` composite key. |
| D8 | Repository-layer CQRS: Separate `IMemberRepository` (command) and `IMemberQueryService` (query). |

---

## 11. Out of Scope (Phase 1)

- Platform adapters other than Twitch (architecturally prepared, implementations deferred).
- Cross-platform viewer account binding (not applicable to standalone local desktop tools).
- Event replay / Event Sourcing.
- Multi-tenant / SaaS deployments.
- Mobile clients.
- AI-driven workflow suggestions.
- Hot-swappable plugin models (AssemblyLoadContext).

---

## 12. Resolved Queries

All originally deferred questions are now resolved and integrated into the specification.

### OQ1 — Log Retention Default Values ✅

```
log.file_retention_days = 7     (Rotating files)
log.db_retention_days   = 30    (SQLite AppLogs table)
log.db_max_size_mb      = 100   (Secondary threshold — whichever triggers first)
```

Exceeding the size limit deletes the oldest rows until size drops below the limit. All settings are hot-loadable and configurable via `SystemSettings`. See §4.15.

---

### OQ2 — Plugin Sandboxing ✅

**Decision: In-process execution with full trust in the MVP.**

- Local desktop tool — plugin authors are the streamers themselves or trusted developers.
- `IPluginContext` limits the exposed API surface (EventBus + Logger only, does not expose `IServiceProvider`).
- Out-of-process sandboxing: indefinitely deferred unless SaaS multi-tenancy becomes a target.
- Future: leverage `AssemblyLoadContext` for hot-unloading (rather than sandboxing).
- **Documentation Requirement:** Plugins execute with full CLR trust. Streamers must only install plugins from trusted sources.

---

### OQ3 — Workflow Rule Storage ✅

**Decision: Normalized header + JSON columns for Conditions and Actions.**

```sql
CREATE TABLE WorkflowRules (
    Id              TEXT PRIMARY KEY,
    Name            TEXT NOT NULL,
    Priority        INTEGER NOT NULL DEFAULT 100,
    IsEnabled       INTEGER NOT NULL DEFAULT 1,
    ConcurrencyMode TEXT NOT NULL DEFAULT 'Serial',
    MaxParallelism  INTEGER NOT NULL DEFAULT 1,
    EventTypeKey    TEXT NOT NULL,
    PlatformFilter  TEXT,
    ConditionsJson  TEXT NOT NULL,
    ActionsJson     TEXT NOT NULL,
    CreatedAt       INTEGER NOT NULL,  -- Unix milliseconds (DateTimeOffset.ToUnixTimeMilliseconds())
    UpdatedAt       INTEGER NOT NULL   -- Unix milliseconds; updated during enable/disable as well
);
CREATE INDEX IX_WorkflowRules_EventTypeKey ON WorkflowRules (EventTypeKey);
```

Rule headers are normalized (queryable, indexable). Conditions/actions are stored as JSON (fluid schema — new plugin types do not require database migrations). Safe deserialization leverages EF Core 10 JSON mapping.

---

### OQ4 — i18n Coverage ✅

**Decision: Backend returns error codes, UI handles translation. Backend logs remain strictly in English.**

```json
// API Error Response — no human-readable strings
{ "error": "WORKFLOW_RULE_NOT_FOUND", "meta": { "ruleId": "01HK..." } }
```

The Vue UI maps error codes to localized strings via vue-i18n. The backend has no locale awareness. Logs remain in English (machine-readable, consistent across deployments).

**MVP Error Code Contract:**

| Code | HTTP | Endpoints |
|---|---|---|
| `WORKFLOW_RULE_NOT_FOUND` | 404 | `GET/PUT/DELETE /api/rules/{id}`, `POST /api/rules/{id}/enable\|disable` |
| `UNKNOWN_EVENT_TYPE_KEY` | 400 | `POST/PUT /api/rules` |
| `CIRCULAR_WORKFLOW_REFERENCE` | 400 | `POST/PUT /api/rules` |
| `UNKNOWN_SIMULATE_EVENT_TYPE` | 400 | `POST /api/simulate/{eventType}` |
| `UNKNOWN_CONFIG_KEY` | 400 | `GET/PUT /api/config/{key}` |
| `CONFIG_KEY_SECURITY_NAMESPACE` | 403 | `GET/PUT /api/config/{key}` — `security.*` keys intercepted |
| `MEMBER_NOT_FOUND` | 404 | `GET /api/members/{id}` |
| `UNKNOWN_ACTION_TYPE` | 400 | `POST/PUT /api/rules` |
| `ACTION_MISSING_REQUIRED_PARAM` | 400 | `POST/PUT /api/rules` |
| `INVALID_ACTION_CONFIG` | 400 | `POST/PUT /api/rules` — `timeoutMs < 0`, invalid `errorBehavior` |
| `OAUTH_CREDENTIAL_NAMESPACE` | 403 | `GET/PUT /api/config/{key}` — `oauth.*` keys intercepted (no REST CRUD allowed) |
| `INVALID_REGEX_PATTERN` | 400 | `POST/PUT /api/rules` — `MessageContentCondition.FullRegex` pattern invalid or exceeds 512 characters |
| `INVALID_QUERY_PARAM` | 400 | `GET /api/members` — `limit` exceeds 200 or other query parameters invalid |
| `UNKNOWN_CONDITION_TYPE` | 400 | `POST/PUT /api/rules` — Conditions JSON contains unknown condition type |
| `INVALID_RULE_ID_MISMATCH` | 400 | `PUT /api/rules/{id}` — request body ID mismatches route ID |

---

### OQ5 — Web Host Authentication Model ✅

Resolved in §4.17 (G15). Summary:
- Both ports (5000 API + 5001 Overlay) strictly run loopback-only (IPv4 127.0.0.1 + IPv6 ::1), forbidding remote access.
- No authentication — Kestrel bind addresses inherently act as the security boundary.
- Overlays run on a dedicated port 5001. OBS uses `http://localhost:5001/overlay/*` — clean URLs, zero tokens.

---

### OQ6 — Photino Offline Scenarios ✅

Three failure scenarios and their handling:

**Port Conflict (API or Overlay ports occupied):**
```
Ports are always allocated in pairs (ApiPort, OverlayPort).
Default pair: (5000, 5001).

On startup, if either port in a pair is unavailable:
  Try next pair: (5002, 5003) → (5004, 5005) → (5006, 5007) → (5008, 5009)
  Tried in pairs — preventing API from taking overlay's default port.
  All attempts fail → Photino dialog:
    "Ports 5000–5009 are unavailable. Please configure a different port pair in settings."

Configurable: appsettings.json →
  "Web": { "ApiPort": 5000, "OverlayPort": 5001 }
  (Manual overrides skip auto-incrementing; users must resolve conflicts themselves.)
```

**Web Host Crashes During Session:**
```
Photino loses connection → displays embedded static fallback HTML
(Packaged within the Photino binary, zero web host dependencies)
Fallback page displays: error description + [Restart] button
```

**Database Migration Fails on Startup:**
```
Migrations run before Web Host starts
Failure → aborts startup → Photino dialog:
  "Database update failed: {error}"
  Buttons: [Open Log Folder] [Exit]
No automatic repair is performed to prevent data corruption.
```

---

## Next Steps

1. **Ready for Phase 1 implementation.** SPEC.md and plan.md completed multiple review rounds; all P1 issues resolved. Start from Task 1.
2. plan.md contains the complete list of tasks, acceptance criteria, dependency graphs, and documentation outputs.
3. todo.md is the execution checklist — tracking progress there.
4. Implement task-by-task, adhering to BDD scenarios and TDD Red/Green/Refactor.
