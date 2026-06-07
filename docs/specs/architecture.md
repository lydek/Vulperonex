# Architectural Core Concepts

> [← Back to Master Specification](../SPEC.md)

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

`PlatformIdentity` has a `UNIQUE (Platform, PlatformUserId)` constraint. The shipped resolver does a `SELECT` (existing → return) then EF `Add` + `SaveChanges`, serialized by a **process-wide `static SemaphoreSlim` gate** so concurrent first-events for the same identity don't double-insert. The UNIQUE constraint remains the last-resort backstop. *(Implementation note: an earlier design proposed raw `INSERT OR IGNORE` relying on SQLite WAL with no app-level lock; the shipped code instead uses the gate + EF — simpler and provider-portable for the single-writer desktop scenario.)*

**CA Boundary:** Application only defines the `IMemberResolver` port interface (`ResolveMemberIdAsync(PlatformIdentity) -> MemberId`); the EF Core implementation (`MemberResolver`) lives in Infrastructure and must not appear in Application or Domain.

```csharp
// Application port (interface only in Application)
public interface IMemberResolver
{
    // returns MemberId (ULID string) — existing or newly created
    Task<string> ResolveMemberIdAsync(PlatformIdentity identity, CancellationToken cancellationToken = default);
}

// Infrastructure implementation (pseudo-code) — gate serializes get-or-create
await Gate.WaitAsync(ct);   // private static readonly SemaphoreSlim Gate = new(1, 1)
var existing = await db.PlatformIdentities
    .Where(x => x.Platform == identity.Platform && x.PlatformUserId == identity.PlatformUserId)
    .Select(x => x.MemberId).SingleOrDefaultAsync(ct);
if (existing is not null) return existing;
var memberId = NewUlidString();
db.Members.Add(new MemberEntity { MemberId = memberId, ... });
db.PlatformIdentities.Add(new PlatformIdentityEntity { MemberId = memberId, Platform = ..., PlatformUserId = ... });
await db.SaveChangesAsync(ct);
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
    DisplayName      TEXT,           -- cached display name
    AvatarUrl        TEXT,
    ColorHex         TEXT,
    BadgesJson       TEXT,           -- JSON array of badge strings
    IsSubscriber     INTEGER NOT NULL DEFAULT 0,
    SubscriptionTier TEXT,           -- "1000" | "2000" | "3000" | null
    TotalBitsGiven   INTEGER NOT NULL DEFAULT 0,
    FetchedAt        TEXT NOT NULL,  -- ISO-8601 DateTimeOffset (EF default mapping)
    PRIMARY KEY (Platform, PlatformUserId)
);
```

**Interface (Adapters.Abstractions):**

```csharp
public interface IPlatformUserInfoCache
{
    Task<PlatformUserDisplayInfo?> GetAsync(string platform, string platformUserId, CancellationToken ct = default);
    // cache miss → create default PlatformUserDisplayInfo row
    //   (DisplayName=null, AvatarUrl=null, ColorHex=null, Badges=[], IsSubscriber=false,
    //    SubscriptionTier=null, TotalBitsGiven=0, FetchedAt=UtcNow), then apply updater.
    //   Never returns null post-update. (No separate SetAsync — upsert is via UpdateAsync.)
    Task<PlatformUserDisplayInfo> UpdateAsync(
        string platform, string platformUserId,
        Func<PlatformUserDisplayInfo, PlatformUserDisplayInfo> updater,
        CancellationToken ct = default);
}

public sealed record PlatformUserDisplayInfo(
    string Platform,
    string PlatformUserId,
    string? DisplayName,
    string? AvatarUrl,
    string? ColorHex,           // null or ^#[0-9A-Fa-f]{6}$; CSS functions / named colors / alpha not accepted
    IReadOnlyCollection<string> Badges,
    bool IsSubscriber,
    string? SubscriptionTier,
    long TotalBitsGiven,
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

**Workflow-visible events** (operators can build rules against these; `IsKnownForWorkflow = true`):

| Event | EventTypeKey | Trigger Condition |
|---|---|---|
| `UserSentMessageEvent` | `user.message` | Chat message |
| `UserFollowedEvent` | `user.followed` | New follow |
| `UserDonatedEvent` | `user.donated` | Twitch Bits / YT SuperChat |
| `UserSubscribedEvent` | `user.subscribed` | Subscription |
| `UserGiftedSubscriptionEvent` | `user.gifted_sub` | Gifted subscription |
| `ChannelRaidedEvent` | `channel.raided` | Raid (currently Twitch-specific concept) |
| `RewardRedeemedEvent` | `reward.redeemed` | Channel points redemption / equivalent |

**System events** (excluded from `GetAll()`; `IsKnownForWorkflow = false`; used for internal routing / UI status / cross-module fan-out):

| Event | EventTypeKey | Purpose |
|---|---|---|
| `PlatformConnectionChangedEvent` | `platform.connection_changed` | Adapter connect / disconnect (§4.7). Reason ∈ `null` / `"reconnecting"` / `"auth_failed"` / `"irc_disconnected"` / `"eventsub_disconnected"`. |
| `MemberCheckedInEvent` | `system.member.checked_in` | Emitted by `TriggerCheckInAction` after incrementing check-in count. Drives `/overlay/member` stamp board + member UI live updates. |
| `WorkflowSystemEvent` | `workflow.timer` (currently the only published key) | Workflow-internal scheduling. Published by `WorkflowTimerHostedService` so timer-driven rules ride the same bus + filter matchers as platform events. Registered by `WorkflowInternalEventTypeBootstrapper`. |

All events are **immutable `record` types** implementing `IStreamEvent`. Events **are not persisted** — only written to log files (with a configurable retention period). System events are excluded from `IStreamEventTypeRegistry.GetAll()` (so they cannot accidentally appear in the rule-builder dropdown) but are still routed through the bus.

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
├── EventTypeKey: string?   // "user.message", "user.followed" etc. Lifted to the rule root in Phase 8 (no longer nested inside Trigger). NULL only when IsSubWorkflow = true
├── IsSubWorkflow: bool     // true → no own trigger; invoked only via InvokeSubWorkflowAction. EventTypeKey and Trigger must both be null/absent (else 400 SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER)
├── Priority: int          // Smaller number = higher priority (1 executes before 10)
├── CreatedAt: DateTimeOffset
├── IsEnabled: bool
├── Version: int           // Optimistic-concurrency token; bumped on save. PUT version mismatch → 409 WORKFLOW_RULE_CONFLICT
├── ExecutionMode: Serial | Parallel   // (named ConcurrencyMode pre-Phase-8)
├── MaxParallelism: int    // Only applicable when ExecutionMode = Parallel; valid range [1, 64]; out-of-range → INVALID_ACTION_CONFIG
├── TimeoutSeconds: int    // Rule-level execution budget; valid range [0, 86400]; out-of-range → INVALID_ACTION_CONFIG
├── Throttle: WorkflowThrottlePolicy   // { MaxConcurrent [0,64], CooldownSeconds [0,86400], PerUserCooldown, PerUserCooldownSeconds [0,86400] }; defaults to None
├── MatchCondition: string?   // Optional rule-level NCalc gate, evaluated after the typed trigger filter (lifted out of Trigger in Phase 8); evaluation failures logged with RuleId (§4.26)
│
├── Trigger: WorkflowTrigger?          // null for sub-workflow rules
│   └── Filter: Dictionary<string,string>   // Typed per-event-type filter keys (§4.26); validated against ITriggerMetadataProvider.GetFilterFieldsFor(EventTypeKey); unknown key → 400 INVALID_FILTER_KEY. Phase 8 replaced the old nested EventTypeKey/PlatformFilter/MatchCondition; rule-level platform filtering was retired
│
├── Conditions: List<IWorkflowCondition>   // AND logic — all must pass
│   ├── UserRoleCondition (User Identity Condition)
│   │   ├── Roles: StreamRole flags (Subscriber | Moderator | Vip | Follower | Broadcaster — mapped from adapter badge/role fields)
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
    │   ├── TargetPlatform: string?    // null = source platform; when non-null, save-time only validates non-empty string (does not validate if adapter is enabled)
    │   ├── Channel: string?           // null = source channel; "internalized" — executor auto-derives from the trigger event when blank
    │   └── DedupKey: string?          // null = auto-generated from the execution key; explicit override rarely needed
    │
    ├── InvokeSubWorkflowAction (Invoke Sub-Workflow)
    │   └── WorkflowId: ULID
    │
    └── InvokePluginAction (Invoke Plugin Action)      // Covers: trigger effects, add points, play sound, etc.
        ├── PluginId: string
        ├── ActionId: string
        ├── Params: IReadOnlyDictionary<string, JsonElement>   // structured JSON params
        └── Args: IReadOnlyDictionary<string, string>          // flat string-keyed args; convenience for plugins that don't need JsonElement
```

**Rule-level failure handling & per-step fields (Phase 8):**
- `OnFailureSteps: List<IWorkflowAction>` — compensating steps run when a normal step fails; same action catalog and per-action fields as `Actions`.
- Every `IWorkflowAction` also carries `ExecutionCondition: string?` (optional per-step NCalc gate — step is skipped when it evaluates false) and `OutputVariable: string?` (optional name capturing the step result for later steps). Per-action error fields are documented in §4.8.
- Trigger filtering, the metadata contract that backs the editor, and NCalc/filter observability are specified in §4.26.

**Full built-in action catalog** (post-Phase-7D reality; the three above are the canonical examples — many domain-specific actions ship as built-ins instead of pure plugin extensions for ergonomics):

| Action | Type discriminator | Layer | Summary |
|---|---|---|---|
| `SendChatMessageAction` | `sendChatMessage` | core | Render template + dedup + outbox. |
| `InvokeSubWorkflowAction` | `invokeSubWorkflow` | core | Fan into another rule with `Args`. |
| `InvokePluginAction` | `invokePlugin` | core | Plugin-routed catch-all. |
| `TriggerCheckInAction` | `triggerCheckIn` | core (depends on `CheckInModule`) | Increments check-in + publishes `MemberCheckedInEvent`. Platform field optional (auto-derives from trigger event). |
| `TriggerEffectAction` | `triggerEffect` | core | Pushes effect to overlay hub. |
| `UpdateCounterAction` | `updateCounter` | core | Increments / sets a named counter. |
| `AddLotteryTicketsAction` | `addLotteryTickets` | core (depends on `LotteryModule`) | Adds tickets to a member's lottery pool. |
| `RandomPickerAction` | `randomPicker` | core | Weighted random pick → output variable. |
| `EmitOverlayWidgetAction` | `emitOverlayWidget` | core | Pushes ad-hoc widget payload to `/hubs/overlay/widgets`. |
| `EmitSystemEventAction` | `emitSystemEvent` | core | Republishes a `WorkflowSystemEvent` for cross-rule fan-out. |
| `DelayAction` | `delay` | core | Pauses the step graph for N ms. |
| `StopIfAction` | `stopIf` | core | Conditional short-circuit on an NCalc expression. |
| `LookupPlatformUserAction` | `lookupTwitchUser` | core (uses `IHelixClient`) | Resolves a username via the platform API. Discriminator preserved for back-compat. |
| `RefundRewardRedemptionAction` | `refundTwitchRedemption` | core (uses `IHelixClient`) | Cancels a channel-point redemption. Discriminator preserved for back-compat. |
| `ShoutoutAction` | `shoutout` | core (uses `IHelixClient`) | Issues a platform shoutout. |

All actions in this catalog live in `Vulperonex.Application.Workflows.Actions` and are now SPEC §6.1-compliant after the Phase 7G rename (see §6.1 for the resolution log). NetArchTest gates in `tests/Vulperonex.Tests.Architecture/` enforce the rule going forward — adding a new `Twitch*`-prefixed type to Application or Domain will fail CI.

**Priority Resolution:** By `Priority ASC`, then `CreatedAt ASC`, and finally `Id ASC` (ULID lexicographical order, ensuring no DB unstable sorting issues).

**Concurrency Semantics:**
- `Serial` (default): **Scope is a single `WorkflowRule`** (each rule has an independent queue; queuing in rule A does not affect rule B). Events for the same rule execute one at a time.
- `Parallel`: Events for the same rule execute concurrently up to `MaxParallelism`. Valid range for `MaxParallelism` is `[1, 64]`; out-of-range is rejected on rule save (400 + `INVALID_ACTION_CONFIG`).
- Different rules matching the same event always execute independently (no cross-rule serialization).

**Actions driven by plugins (Hot-swappable):** `SendChatMessage` and `InvokeSubWorkflow` are built-in. All domain-specific actions (`TriggerEffect`, `AddPoints`, `PlaySound`) are `InvokePluginAction` — they require loading the corresponding plugin. If the plugin is missing, the action logs a warning and is skipped.

**Condition Evaluation Short-Circuit:** The first failed condition stops evaluation (ordered lowest cost to highest).

---

## 4.7 Platform Adapter Resilience (G5)

**Twitch live ingestion topology (post-Phase-7G):**

```
TwitchConnectionOrchestrator (BackgroundService, in Vulperonex.Web)
  ├─ Resolves broadcaster:
  │   • SystemSettingKey.TwitchChannelName   (preferred)
  │   • Twitch:ChannelName        config     (fallback)
  │   • Twitch:BroadcasterId      config     (legacy override)
  │   → IHelixClient.LookupUserAsync(channelName)  resolves the numeric id
  │
  ├─ TwitchIrcChatSource (wraps TwitchLib.Client.TwitchClient)
  │   • Chat → builds Vulperonex's TwitchIrcMessage shape
  │   • OnConnected / OnDisconnected → PlatformConnectionChangedEvent
  │
  └─ TwitchEventSubSource (wraps TwitchLib.EventSub.Websockets.EventSubWebsocketClient)
      • Subscriptions created on WebsocketConnected via IHelixClient.CreateEventSubSubscriptionAsync(sessionId)
      • Session-id deduplication for subscription re-creation
      • Typed handlers map to existing TwitchMockPayload shapes via TwitchAlertPayloadFactory
      • WebsocketConnected / WebsocketDisconnected → PlatformConnectionChangedEvent

TwitchAdapter (Vulperonex.Adapters.Twitch, third-party-SDK free) exposes:
  • IngestChatAsync(TwitchIrcMessage, displayCacheOverride?)   ← IRC source
  • IngestAlertAsync(TwitchMockPayload, displayCacheOverride?) ← EventSub source
```

**Reconnection Strategy (Handled entirely within the host orchestrator / TwitchLib clients — Application/Domain layers are unaware):**

```
IRC WebSocket Disconnection:
  → Immediately publish PlatformConnectionChangedEvent { IsConnected: false, Reason: "irc_disconnected" }
  → TwitchLib.Client auto-reconnect with exponential backoff: 1s → 2s → 4s → 8s → ... max 60s
  → Messages during downtime: Silently lost (IRC is best-effort service)
  → On successful reconnection: publish PlatformConnectionChangedEvent { IsConnected: true }
  → On token rejection observed during connect/auth: publish { IsConnected: false, Reason: "auth_failed" }

EventSub WebSocket Disconnection:
  → Immediately publish PlatformConnectionChangedEvent { IsConnected: false, Reason: "eventsub_disconnected" }
  → TwitchLib.EventSub.Websockets manages keepalive + seamless-reconnect URL handoff (session continues across the reconnect)
  → Vulperonex orchestrator handles cold-restart backoff via ReconnectBackoffPolicy.GetDelay(attempt): exponential with ±20% jitter, capped at 60s
  → Twitch guarantees event replay within a 10-minute reconnection window
  → Adapter receives replayed events and publishes them normally; skips duplicate deliveries in EventSubDedupCache matching the same (platform, sourceEventId); dedup cache capped at 1000 entries or 10-minute TTL
  → Subscription Helix POST returning HTTP 409 (already exists) is treated as success
  → On Helix 401/403 during subscription creation: publish { IsConnected: false, Reason: "auth_failed" }
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
    ContinueOnError,   // Failure → Log → Continue to next action
    StopOnError,       // (Default) Failure → Stop remaining actions in the rule
    RetryOnError,      // Failure → Retry according to backoff strategy before giving up
}
```

Each `IWorkflowAction` base class carries:

```
ErrorBehavior:      ErrorBehavior = StopOnError   // Default changed to fail-fast (was ContinueOnError pre-Phase-8)
MaxRetries:         int = 0       // Valid range [0, 10]; out-of-range → INVALID_ACTION_CONFIG
BackoffMs:          int = 500     // Valid range [100, 30000]; out-of-range → INVALID_ACTION_CONFIG
TimeoutMs:          int = 10000   // Valid range [0, 60000]; out-of-range → INVALID_ACTION_CONFIG
                                  // CancellationToken is cancelled after this; executor stops waiting
                                  // .NET cannot forcibly terminate asynchronous tasks — plugins must observe CancellationToken
ExecutionCondition: string?       // Optional per-step NCalc gate; step is skipped when it evaluates false
OutputVariable:     string?       // Optional name to capture the step result for use by later steps
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

Bus-subscribing modules implement `IHostedService` (the concrete one shipped is `MemberModuleHostedService`; `WorkflowModule` below is the illustrative pattern — workflow consumption runs via `WorkflowEngine`, and overlay push via `OverlayEventForwarder`). Subscriptions are acquired during `StartAsync`, released during `StopAsync` while waiting for in-progress handlers to complete (does not discard events still being processed). The logical on/off modules (`workflow`/`member`/`checkin`/`lottery`/`onecommebridge`) are tracked by `ModuleStateService`, not necessarily one `IHostedService` per module (§4.20). **If `ct` triggers cancellation before completion is reached → log warning "shutdown timeout: {count} handlers still running" and force return (does not throw exception); handlers may continue running after process shutdown, which is a system limitation and not treated as an error.**

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
5. Modules                     — MemberModuleHostedService + WorkflowEngine + OverlayEventForwarder (overlay push)
6. Adapters                    — TwitchAdapter (starts publishing only after modules are ready)
7. Web / SignalR Hub
```

**Shutdown order is reversed.** Adapters stop first (no new incoming events) → modules flush in-progress work → infrastructure shuts down.

---

## 4.11 Database Migration Strategy (G9)

**Auto-run on Startup (implemented). Explicit CLI execution is planned — not yet shipped.**

```
On Startup (Always):
  await db.Database.MigrateAsync()
  → Auto-executes only incremental migrations
  → Application never starts with an outdated schema

CLI (Manual Control) — PLANNED, NOT YET IMPLEMENTED:
  vulperonex db migrate        → Execute pending migrations
  vulperonex db status         → List applied / pending migrations
  vulperonex db rollback <id>  → Rollback (requires confirmation prompt)
  (The CLI ships rule / timer / config / member / simulate / twitch groups only;
   there is no `db` command group today. Migrations run solely on host startup.)
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

1. **MigrationClassifier** (`Vulperonex.Infrastructure.Migrations`) — a pure function `Classify(IReadOnlyList<MigrationOperation>) → MigrationRisk` that grades a migration's operations as `Safe` / `ReviewRequired` / `Destructive`:
   ```csharp
   var risk = MigrationClassifier.Classify(migrationBuilder.Operations);
   // DropTable/DropColumn/Rename + raw DROP|DELETE|TRUNCATE  → Destructive
   // raw SQL containing ALTER (e.g. ALTER TABLE … ADD COLUMN) → ReviewRequired (conservative)
   // CreateTable/AddColumn-only                              → Safe
   ```
   Unit-tested in `tests/Vulperonex.Tests.Unit/Infrastructure/Migrations/MigrationClassifierTests.cs`. **Note: any raw SQL containing `ALTER` is treated as review-required (conservative strategy).**
2. **PR Review Requirement** — Reviewers run/inspect the classifier when a migration is added; a `Destructive`/`ReviewRequired` result gates manual review before merge.

> **Drift note:** an earlier design proposed a `Tests.Architecture` test that instantiates each `Migration`, executes `Up()`, and **fails CI** unless the migration carries a `[DestructiveMigration]` attribute. The shipped code ships only the reusable `MigrationClassifier` + its unit tests — there is **no `[DestructiveMigration]` attribute and no auto-fail CI gate over real migrations**; destructive-migration safety is review-based, not enforced by a build break.

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
    void Register(StreamEventTypeMetadata metadata);   // (was Register(key, description, isSystemEvent))
    bool IsKnown(string key);             // Includes system events (used for routing/dispatch)
    bool IsKnownForWorkflow(string key);  // Excludes system events (used for WorkflowRule validation)
    IReadOnlyCollection<StreamEventTypeMetadata> GetAll();  // Excludes IsSystemEvent=true items
                                                            // API endpoint projects this to EventTypeDescriptor (adds IsSimulatable)
}

// Registry storage/record type (Vulperonex.Application.EventTypes) — replaces the former internal RegistryDescriptor
public sealed record StreamEventTypeMetadata(
    string Key,
    string Description,
    bool IsSystemEvent = false);  // true for platform.connection_changed; excluded from GetAll(). Impl: InMemoryStreamEventTypeRegistry

// API DTO — returned by GET /api/event-types endpoint (projected from StreamEventTypeMetadata)
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
_registry.Register(new StreamEventTypeMetadata("user.message",  StreamEventDescriptions.UserMessage));
_registry.Register(new StreamEventTypeMetadata("user.followed", StreamEventDescriptions.UserFollowed));

// MyPlugin.InitializeAsync()
_registry.Register(new StreamEventTypeMetadata("plugin.my_plugin.event", "我的外掛程式自訂事件"));
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

The REST API is the sole canonical write path. Both the UI and CLI call the API — neither writes directly to the database. The API surface is loopback-only and guarded by `AdminGuardMiddleware` (no user accounts, but CSRF + Host + Origin/Referer checks on mutations — see §4.17).

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

All UI and CLI access the Web host exclusively via REST; neither client has direct database access. The API port is loopback-only (IPv4 `127.0.0.1` + IPv6 `::1`); there are no user accounts, but mutating requests are guarded (CSRF + Host + Origin/Referer — see §4.17). The overlay port may additionally be LAN-exposed behind an overlay access key.

| Group | Method | Path | Application Port |
|---|---|---|---|
| WorkflowRule | GET | `/api/rules` | `IWorkflowRuleQueryService` — **MVP has no pagination**, returns all rules; sorting: `Priority ASC, CreatedAt ASC, Id ASC` |
| WorkflowRule | GET | `/api/rules/{id}` | `IWorkflowRuleQueryService` |
| WorkflowRule | POST | `/api/rules` | `IWorkflowRuleRepository` — **201 Created** + `Location: /api/rules/{newId}` header; body contains the newly created rule |
| WorkflowRule | PUT | `/api/rules/{id}` | `IWorkflowRuleRepository` — 200 OK; mismatch between body ID and route ID → **400 `INVALID_RULE_ID_MISMATCH`** |
| WorkflowRule | DELETE | `/api/rules/{id}` | `IWorkflowRuleRepository` — **204 No Content** |
| WorkflowRule | POST | `/api/rules/{id}/enable` | `IWorkflowRuleRepository` — 200 OK |
| WorkflowRule | POST | `/api/rules/{id}/disable` | `IWorkflowRuleRepository` — 200 OK |
| Workflow Timers | GET / POST / PUT / DELETE | `/api/timers[/{id}]` | `IWorkflowTimerRepository` — schedule of `WorkflowSystemEvent` (`workflow.timer`) emissions |
| Event Types | GET | `/api/event-types` | `IStreamEventTypeRegistry.GetAll()` projected to `EventTypeDescriptor` (excludes system events) |
| Metadata | GET | `/api/metadata/triggers` | `ITriggerMetadataProvider` — typed trigger filter fields per `EventTypeKey` |
| Metadata | GET | `/api/metadata/actions` | `IActionMetadataProvider` — `ActionParameterMetadata[]` per action type (drives §4.22 form) |
| Simulation | POST | `/api/simulate/{alias}` | `ISimulationAdapter` — alias set: `chat`, `follow`, `sub`, `giftsub`, `raid`, `bits`, `redeem` |
| Simulation | POST | `/api/simulate/checkin` | `IMemberStreamStateRepository` + bus — direct check-in (§4.21) |
| Config | GET / PUT | `/api/config/{key}` | `ISystemSettingsService` |
| Member | GET | `/api/members` | `IMemberQueryService` |
| Member | GET | `/api/members/{id}` | `IMemberQueryService` |
| Member | GET | `/api/members/{id}/audit` | `IMemberAuditLogRepository` (§4.19) |
| Member | PATCH | `/api/members/{id}/loyalty` | `IMemberAdminService` — requires `If-Match` etag + body `reason` |
| Member | POST | `/api/members/{id}/reset` | `IMemberAdminService` |
| Member | POST | `/api/members/{id}/delete-token` | Issues a 30s confirmation token |
| Member | DELETE | `/api/members/{id}` | Two-stage delete; body must carry the token + reason |
| Twitch OAuth | GET | `/api/twitch/auth/status` | ClientId / ClientSecret / RefreshToken presence flags |
| Twitch OAuth | POST | `/api/twitch/auth/start` | PKCE flow — returns authorize URL |
| Twitch OAuth | POST | `/api/twitch/auth/complete` | PKCE flow — exchanges code |
| Twitch OAuth | POST | `/api/twitch/auth/device/start` | **Device flow** — returns `user_code` + verification URI (for users without a callback-capable browser) |
| Twitch OAuth | POST | `/api/twitch/auth/device/complete` | Device flow — polls token endpoint |
| Twitch OAuth | DELETE | `/api/twitch/auth/token` | Clears the stored refresh token |
| Twitch OAuth | GET | `/api/auth/twitch/callback` | OAuth callback landing (PKCE redirect target) |
| Twitch | GET | `/api/twitch/badges` | `IPlatformBadgeCache` — global + channel badges (§4.23) |
| Twitch | GET | `/api/twitch/rewards` | `ITwitchRewardCache` — broadcaster's channel-point rewards (§4.25) |
| Twitch | POST | `/api/twitch/rewards/refresh` | Force-refresh reward cache; treats unauth as `200 { ready:false }` |
| Chat Outbox | GET | `/api/chat-outbox` | `IChatOutbox` — rendered SendChatMessage history (§4.5) |
| Plugins/Modules | GET | `/api/plugins-modules` | `IModuleStateService` — module + plugin enabled state + dependencies (§4.20) |
| Plugins/Modules | POST | `/api/plugins-modules/{name}/toggle` | Topological toggle with cascade |
| Overlay Presets | GET | `/api/overlay/presets` | `OverlayPresetStore` — list of built-in preset metadata |
| Overlay Assets | POST | `/api/overlay/assets` | `OverlayPresetStore.SaveAsset` — upload a member-card background/stamp image (images only, ≤2MB, extension + content-type validated → `wwwroot/overlay/assets/{guid}.{ext}`); returns the URL stored in `overlay.member.background_url` / `overlay.member.stamp_url` (§4.14.3) |
| Overlay History | DELETE | `/api/overlay/{chat\|alerts\|member}/messages` | Clears the in-memory replay buffer for that hub. **Replay for late-joining OBS sources is delivered via SignalR connection setup, not REST.** |
| Overlay LAN | GET | `/api/overlay/lan-info` | `{ enabled, bindAddress, overlayPort, accessKey, suggestedHosts[] }` — LAN OBS URL + access key (§4.17) |
| Security | GET | `/api/overlay/csrf-token` | Returns the per-process admin CSRF token; loopback + Host check only (CSRF waived). SPA fetches this before mutations (§4.17) |
| Health | GET | `/health` | Liveness |

**Admin guard (§4.17):** every non-GET `/api/*` and all `/api/overlay/*` requests require `X-Admin-Csrf` + a host-matching `Origin`/`Referer` over loopback; the CLI attaches these automatically.

**Simulation Alias → EventTypeKey Mapping** (Enforced by `SimulationAliasRegistry`; uses canonical keys from §4.4):
- `chat` → `user.message`
- `follow` → `user.followed`
- `sub` → `user.subscribed`
- `giftsub` → `user.gifted_sub`
- `raid` → `channel.raided`
- `bits` → `user.donated`
- `redeem` → `reward.redeemed`

Only alias values are accepted; raw EventTypeKey strings are rejected to maintain naming clarity across CLI/REST/WorkflowRules. `POST /api/simulate/checkin` is a separate endpoint (not an alias) because it talks to the member repository directly (§4.21).

**Config Key Registry:** `ISystemSettingsService` operates on typed constants defined in `SystemSettingKey` (rather than arbitrary strings). Any key missing from the registry returns `UNKNOWN_CONFIG_KEY`. New settings require adding constants — free-text keys are not allowed.

**Config Key Casing Rules:** All keys are **canonical lowercase** (`log.min_level`, `oauth.twitch.refresh_token`). Before prefix denylist matching and registry lookups, the API normalizes the incoming `{key}` via `ToLowerInvariant()` — `OAuth.Twitch.Refresh_Token` and `oauth.twitch.refresh_token` are treated as the same key, both triggering the 403 denylist. The database also stores lowercase values.

**Interception Priority (Important):** `/api/config/{key}` requests execute checks in the following order: (1) key normalization (`ToLowerInvariant()`); (2) **protected prefix denylist** (`security.*` → 403 `CONFIG_KEY_SECURITY_NAMESPACE`; `oauth.*` → 403 `OAUTH_CREDENTIAL_NAMESPACE`); (3) **registry lookup** (`UNKNOWN_CONFIG_KEY`). The prefix denylist is executed **before** the registry check — an unknown `oauth.*` key (such as a future `oauth.unknown.refresh_token` not yet in the registry) still returns 403 instead of 400 `UNKNOWN_CONFIG_KEY`.

**Security namespace config keys** (`security.*`) are **intercepted** at `/api/config/{key}` — returning 403 + `CONFIG_KEY_SECURITY_NAMESPACE`. **OAuth credential keys** (`oauth.*`, e.g., `oauth.twitch.refresh_token`) are also **intercepted** at `/api/config/{key}` — returning 403 + `OAUTH_CREDENTIAL_NAMESPACE` (OAuth tokens are only written via the PKCE flow, no REST CRUD permitted). For the MVP, Twitch OAuth credentials are saved by the PKCE flow in Task 12 and cannot be accessed or modified via configuration endpoints. `/api/settings/security/*` is a reserved path prefix; no CRUD endpoints are added in the MVP; the Kestrel loopback-only binding inherently protects these paths without requiring additional middleware.

---

## 4.14 Overlay Architecture (G12)

`OverlayModule` subscribes to relevant domain events, converts them into `OverlayPayload` DTOs, and pushes them to SignalR groups. Frontend overlay pages connect as OBS browser sources.

**Browser Source URLs:**
```
http://localhost:5001/overlay/chat.html — Scrolling Chat
http://localhost:5001/overlay/alerts    — Follow / Subscription / Raid Alerts
http://localhost:5001/overlay/member-card.html — Member Card Display
```

Each URL is an independent Vue route that connects to its SignalR group upon mounting. No authentication is required (OBS must connect directly).

**Chat Overlay Template System:**

- `/overlay/chat` must support **multiple built-in templates / presets**, including at least the Vulperonex default template; built-in templates should map to a "single template directory / single template bundle" concept, rather than hardcoding a single layout.
- Template selection must be a **config-level** capability, rather than requiring users to modify frontend source code; it can be extended in the future to include template lists, previews, and import/export.
- Template rendering must still respect MVP security boundaries: using DTO allowlists and text bindings; **arbitrary raw HTML or `v-html` must not penetrate the event payload**.
- **OneComme compatibility belongs to extension/plugin capabilities, not directly built into core.** Core only needs to provide extensible template presets / package contracts; OneComme compatibility can be implemented via plugins, template importers, or adapter packages.
- **OneComme** is treated as a priority compatibility target. The goal is not to replicate its internal implementation 1:1, but to provide a close enough template structure / import mapping / compatibility contract to reduce migration costs for existing OneComme users while maintaining the boundary between core and third-party template ecosystems.

#### 4.14.1 Overlay Preset Contract (Vue/Static Built-in Presets)

> **⚠️ Partially superseded (see §4.14.3):** the **Static HTML Override** track (`/overlay/custom/{slug}.html`, `custom:{slug}` preset values), the **Custom HTML Upload** subsection below, and the **OneComme-import-via-upload** path were **removed** along with the custom-preset pipeline. Only the **Built-in Presets** row and config-driven customization (text + member-card images via `POST /api/overlay/assets`, §4.14.3) remain. The custom-HTML content below is retained for historical context only.

**Motivation:** Streamers wishing to customize overlay visuals should not be forced to install Node.js / pnpm / Vite. At the same time, Vulperonex must provide high-quality default Vue presets.

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

`/overlay/chat` and `/overlay/member` remain as compatibility aliases only. They redirect to the canonical static HTML entrypoints and must not contain separate renderer implementations.

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
| `vulperonex-default` (Default) | chat | Static HTML scrolling chat with OBS-first layout, badge/name/content line, optional member chip, and dedicated check-in card rendering | Vulperonex |
| `compact-line` | chat | Dense static HTML chat layout for more lines on screen | Vulperonex |
| `member-card-inline` | chat | Static HTML chat layout that emphasizes inline member-card chips | Vulperonex |
| `rotan-checkin` (Default) | member | Purple-gold foil streaming border + SVG paw stamp + halftone grid background; left: avatar/name, right: 10-grid collection stamp board | menber_byRotan (rewritten, no direct reference to original assets) |

**Member Stamp Board Controller (Admin Settings):**

| Setting Key | Type | Default | Description |
|---|---|---|---|
| `overlay.member.background_url` | string (URL) | Empty | Card background image. Empty uses built-in gradient. |
| `overlay.member.stamp_url` | string (URL) | Empty | Custom stamp image. Empty uses built-in SVG paw stamp. |
| `overlay.member.stamps_per_round` | int | 10 | Stamps needed to complete a round. |
| `overlay.chat.show_member_card` | bool | false | Whether to embed member card chips inside chat overlays. |
| `overlay.chat.preset` | string | `kapchat` | Chat preset key (built-in key only; `custom:{slug}` removed with the custom-preset pipeline, §4.14.3) |
| `overlay.member.preset` | string | `rotan-checkin` | Member preset key |
| `overlay.alerts.preset` | string | (default) | Alerts preset key |

**Workflow chat output & overlay personas (§4.14.4):**

| Setting Key | Type | Default | Description |
|---|---|---|---|
| `workflow.chat.output_destination` | string | `dual` | Where `SendChatMessageAction` output goes: `dual` / `platform_only` / `overlay_only`. When it includes overlay, the message is rendered into `/overlay/chat` via `IWorkflowChatOverlaySink` (no platform round-trip needed). |
| `overlay.chat.assistant_display_name` | string | (built-in) | Display name shown for workflow/assistant-authored chat messages in the overlay. |
| `overlay.chat.assistant_avatar_url` | string (URL) | Empty | Avatar for assistant-authored chat messages. |
| `overlay.chat.checkin_display_name` | string | (built-in) | Display name used for check-in cards rendered into the chat overlay. |
| `checkin.reset_time_local` | string `HH:mm` | `05:00` | Local time of day the daily check-in counter rolls over. |
| `checkin.repeat_card_enabled` | bool | true | When true, repeat check-ins within the same day still emit an overlay card (deduplicated by event id); when false, only the first per cycle shows. |

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

**Background:** Phase 7C established the member-card preset + `OverlayMemberHub`, but `TriggerCheckInActionExecutor` only wrote to SQLite `MemberStreamState` and never published events to `OverlayEventForwarder`, leaving `/overlay/member-card.html` / `/overlay/member` with no push updates in practice. Phase 7C cross-hub chat embeds (`memberSnapshot`) also only queried the DB on chat event paths, lacking linkage with check-in actions.

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

#### 4.14.3 Custom HTML Overlay Editing & Deployment Pipeline (Removed / Superseded)

> **⚠️ Removed:** The online Monaco editor, zip/HTML upload, and the custom-preset draft/deploy/validate/history/rollback pipeline described in this section have been **removed entirely** (including the backend `OverlayPresetStore` methods, the `/api/overlay/custom-presets/*` endpoints, the `wwwroot/overlay/custom/` directory, and any existing custom presets).
>
> **Reason:** Arbitrary uploaded/edited HTML/JS/CSS cannot be kept safe and well-formed reliably, and the feature was too advanced for most users.
>
> **Replacement — Overlay Customization:** A constrained, safe customization surface (in `OverlayPresetsView`) that only allows:
> - **Text replacement:** assistant display name / check-in display name / assistant avatar URL, written to existing config keys (`overlay.chat.assistant_display_name`, `overlay.chat.checkin_display_name`, `overlay.chat.assistant_avatar_url`).
> - **Image replacement (member card):** background + stamp images, uploaded via `POST /api/overlay/assets` (images only, ≤2MB, extension + content-type validated) to `wwwroot/overlay/assets/{guid}.{ext}`; the returned URL is stored in the existing config keys (`overlay.member.background_url`, `overlay.member.stamp_url`).
> - Overlays keep their existing config-driven reads (`member-card.js` already fetches and applies those image keys).
> - Built-in preset selection (chat/member/alerts) and OBS/LAN URL copy are unchanged.
>
> The original pipeline design below is retained for historical record only and is **no longer implemented**.

**Background (historical):** Phase 7C introduced `POST /api/overlay/custom-presets` for pure zip uploads, but faced two structural issues:
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
// Program.cs (MVP) — plugins are registered as IVulperonexPlugin DI services;
// StaticPluginRegistry(IEnumerable<IVulperonexPlugin>) collects them as IPluginRegistry.
builder.Services.AddSingleton<IVulperonexPlugin, OneCommeBridgePlugin>();
builder.Services.AddSingleton<IPluginRegistry, StaticPluginRegistry>();
// (There is no AddVulperonexPlugin<T> extension; each plugin implements
//  IVulperonexPlugin.InitializeAsync(IPluginContext) and self-registers its event-type keys.)
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

**Dual-Port Architecture: a loopback-only API port + an Overlay port that is loopback-only by default and may be optionally exposed to the LAN for cross-machine OBS.** Ports are auto-allocated in pairs starting at `(5000, 5001)` (see OQ6); the values below are the defaults.

```
appsettings.json:
  "Web": { "ApiPort": 5000, "OverlayPort": 5001 }   // defaults; actual pair is auto-allocated
  "Overlay": { "Lan": { "Enabled": false, "BindAddress": "0.0.0.0" } }  // opt-in LAN overlay
  "Security": { "CsrfTokenPath": null }              // optional override for the admin CSRF token file
```

**Port binding (Kestrel):**
```
ApiPort     → ALWAYS loopback only (127.0.0.1 + ::1). Admin SPA APIs, all mutations,
              OAuth, and the overlay editor live here and never reach the LAN.
OverlayPort → ALWAYS loopback (admin preview) AND, when Overlay:Lan:Enabled = true,
              additionally bound to Overlay:Lan:BindAddress (default 0.0.0.0 = all interfaces)
              so OBS on another machine can load the live overlay.
```

**Access control is no longer "bind address alone".** A single `AdminGuardMiddleware` enforces two regimes (the earlier "no authentication / no middleware" model is superseded):

1. **Loopback admin/mutation requests** — every non-GET `/api/*` request, plus *all* `/api/overlay/*` requests, must pass:
   - **Loopback** remote IP (null remote IP is rejected — blocks Unix-socket/proxy spoofing).
   - **Host allowlist** (`localhost` / `127.0.0.1` / `[::1]`, port-insensitive) — anti-DNS-rebinding → 400 `ORIGIN_MISMATCH`.
   - **`X-Admin-Csrf` header** matching the per-process admin CSRF token (constant-time compare) → 400 `MISSING_OR_INVALID_CSRF_HEADER`.
   - **`Origin`/`Referer`** present and host-matching (at least one required) → 400 `MISSING_ORIGIN_OR_REFERER_HEADER` / `ORIGIN_MISMATCH` / `INVALID_ORIGIN_HEADER` / `REFERER_MISMATCH` / `INVALID_REFERER_HEADER`.
   - **`GET /api/overlay/csrf-token`** is the bootstrap exception: loopback + Host check only (waives CSRF) so the SPA can fetch the token. It issues the current token.

2. **Non-loopback (LAN) requests** — confined to the live overlay surface and gated by an **overlay LAN access key**:
   - Static SPA/overlay HTML + assets: GET only, no key (public client code; sub-resource loads cannot attach headers).
   - SignalR hubs (`/hubs/*`) and a small allowlist of overlay-safe config GETs (`overlay.{chat,member,alerts}.preset`, `overlay.chat.show_member_card`, `overlay.member.background_url`, `overlay.member.stamp_url`): require the key via `?k=<key>` query or `X-Overlay-Key` header.
   - Everything else (admin APIs, any mutation, OAuth, editor, `/health`, `/openapi`) → 403.

**Admin CSRF token:** 256-bit Base64Url, regenerated **every process start**, written to `.admin-csrf-token` (or `Security:CsrfTokenPath`) with owner-only ACL, deleted on shutdown. Restarting invalidates open admin tabs (refresh to re-fetch). *Known trade-off:* any local process able to issue a loopback request to `/api/overlay/csrf-token` can read the token — accepted for a single-machine desktop tool with no central auth.

**Overlay LAN access key:** 256-bit Base64Url stored in `SystemSettings` (`overlay.lan.access_key`), generated once when LAN access is first enabled and **stable across restarts** so OBS URLs keep working. `GET /api/overlay/lan-info` (admin/loopback) returns the key + suggested LAN host URLs for copy-paste into OBS.

**Overlay DTO Security:** Overlay DTOs must be public secure projections — even if the server is permanently loopback-only, strict DTO allowlists are required (preventing accidental future field leakage, and preventing SignalR serialization over-exposure):

| Overlay | Allowed Fields | Forbidden Fields |
|---|---|---|
| `/overlay/chat` (`OverlayChatPayload`) | SchemaVersion, EventId, Timestamp, DisplayName, ColorHex, Segments, Badges, Roles?, AvatarUrl?, MemberSnapshot?, Variant? | MemberId, UserId, TotalBitsGiven |
| `/overlay/alerts` (`OverlayAlertPayload`) | SchemaVersion, EventId, Timestamp, DisplayName, EventType, Tier, Replayed | MemberId, PlatformUserId |
| `/overlay/member` (`OverlayMemberPayload`) | SchemaVersion, EventId, Timestamp, DisplayName, AvatarUrl?, CheckInCount (current session only), RoundIndex, StampSlotInRound | MemberId, TotalLoyalty, LinkedPlatforms |

`SchemaVersion` is fixed to `1`. `EventId` is the overlay public delivery ID used for frontend deduplication, and must not expose MemberId, PlatformUserId, or other internal identities; platform-provided IDs are preferred (IRC `msg-id` / EventSub `message_id`), and adapters generate ULIDs (marked as synthetic) in their absence. `Timestamp` is the UTC ISO-8601 event time, used for frontend sorting. **All three payloads now carry `EventId` + `Timestamp` (member gained them for client-side dedup of repeat check-in cards — §4.14.2).** `MemberSnapshot` (chat) is the optional cross-hub member chip (§4.14, gated by `overlay.chat.show_member_card`); `Variant`/`Roles` are render hints; `Replayed` (alerts) flags an EventSub replay redelivery. `OverlayEventForwarder` maps domain events to these restricted DTOs prior to SignalR pushing. Allowlist enforcement occurs at the DTO type level — no dynamic mapping.

**OBS Browser Source URLs:**
```
http://localhost:5001/overlay/chat.html
http://localhost:5001/overlay/alerts
http://localhost:5001/overlay/member-card.html
```

**DB Path Resolution Rules (Shared by CLI and Web Host):** DB path resolution: `appsettings.json → Database:Path` (if present), falling back to OS app-data default path (see §4.11). **`Database:Path` does not allow overrides via `appsettings.{Environment}.json` or environment variables** — both Web Host and CLI only read the primary `appsettings.json` to guarantee they access the same database. If custom paths are needed in development, modify `appsettings.json` directly (avoiding Development overrides).

**Kestrel Bindings (API loopback-only; Overlay loopback + optional LAN):**
```csharp
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // API port: always loopback only — admin/mutating/auth surface never reaches the LAN.
    kestrel.Listen(IPAddress.Loopback,     apiPort);
    kestrel.Listen(IPAddress.IPv6Loopback, apiPort);

    // Overlay port: always loopback (admin preview)…
    kestrel.Listen(IPAddress.Loopback,     overlayPort);
    kestrel.Listen(IPAddress.IPv6Loopback, overlayPort);

    // …plus an optional LAN bind for cross-machine OBS (gated at the app layer by the overlay key).
    if (lanEnabled && TryParseBindAddress(lanBindAddress, out var bindIp) && !IPAddress.IsLoopback(bindIp))
        kestrel.Listen(bindIp, overlayPort);   // e.g. IPAddress.Any for 0.0.0.0
});
```

---
