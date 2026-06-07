# Resolved Design Decisions & Queries

> [← Back to Master Specification](../SPEC.md)

## 9. Success Criteria (MVP)

> **Note:** The `Test_Method_Names` below are **illustrative acceptance-criteria identifiers**, not literal test method names. Actual tests follow the §7.4 `Given_<State>_When_<Action>_Then_<Expectation>` convention and live under `tests/` (e.g. SC-3 → `tests/Vulperonex.Tests.Architecture/Adapters/SimulationAdapterIsolationTests.cs`; SC-6 → `tests/Vulperonex.Tests.Integration/Adapters/TwitchWorkflowEquivalenceTests.cs`; cache → `…/Cache/PlatformUserDisplayCacheTests.cs`). Match by intent, not by string.

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

**Decision: Normalized header + JSON columns for Trigger, Conditions, Actions, OnFailure steps, and Throttle.**

```sql
-- Schema after Phase 8 consolidation (ConsolidateWorkflowRuleSchema migration)
CREATE TABLE WorkflowRules (
    Id                   TEXT PRIMARY KEY,
    Name                 TEXT NOT NULL,
    EventTypeKey         TEXT,                          -- Nullable since Phase 8; NULL for sub-workflow rules
    TriggerJson          TEXT,                          -- Serialized WorkflowTrigger (typed Filter dict); NULL for sub-workflows
    MatchCondition       TEXT,                          -- Optional rule-level NCalc gate (lifted out of TriggerJson in Phase 8)
    IsSubWorkflow        INTEGER NOT NULL DEFAULT 0,
    ConditionsJson       TEXT NOT NULL DEFAULT '{}',
    ActionsJson          TEXT NOT NULL DEFAULT '[]',
    OnFailureActionsJson TEXT NOT NULL DEFAULT '[]',
    IsEnabled            INTEGER NOT NULL DEFAULT 1,
    Priority             INTEGER NOT NULL DEFAULT 0,
    CreatedAt            TEXT NOT NULL,                 -- ISO-8601 DateTimeOffset (EF default mapping)
    ExecutionMode        TEXT NOT NULL DEFAULT 'Serial',   -- was ConcurrencyMode pre-Phase-8
    MaxParallelism       INTEGER NOT NULL DEFAULT 1,
    ThrottleJson         TEXT NOT NULL DEFAULT '{}',
    TimeoutSeconds       INTEGER NOT NULL DEFAULT 30,
    Version              INTEGER NOT NULL DEFAULT 0     -- Optimistic-concurrency token
);
CREATE INDEX IX_WorkflowRules_CreatedAt ON WorkflowRules (CreatedAt);
```

Rule headers are normalized (queryable, indexable). Trigger filter, conditions, actions, on-failure steps, and throttle policy are stored as JSON (fluid schema — new plugin types do not require database migrations). Safe deserialization leverages EF Core 10 JSON mapping. **Phase 8 retired the separate `UpdatedAt` / `PlatformFilter` / `ConcurrencyMode` columns and the inner `eventTypeKey` / `matchCondition` fields that previously lived inside `TriggerJson`; the listing index moved from `EventTypeKey` to `CreatedAt` (the default sort key).**

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
| `INVALID_FILTER_KEY` | 400 | `POST/PUT /api/rules` — Trigger filter contains a key not in the event type's typed filter metadata (§4.26) |
| `SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER` | 400 | `POST/PUT /api/rules` — `isSubWorkflow=true` but an `eventTypeKey` / `trigger` was supplied |
| `WORKFLOW_RULE_CONFLICT` | 409 | `PUT /api/rules/{id}` — optimistic-concurrency `Version` mismatch |
| `MISSING_OR_INVALID_CSRF_HEADER` | 400 | Any guarded request (loopback mutation / `/api/overlay/*`) missing or with a wrong `X-Admin-Csrf` (§4.17) |
| `MISSING_ORIGIN_OR_REFERER_HEADER` | 400 | Guarded request with neither `Origin` nor `Referer` |
| `ORIGIN_MISMATCH` | 400 | `Origin`/Host not in the loopback allowlist (anti-DNS-rebinding) |
| `INVALID_ORIGIN_HEADER` | 400 | `Origin` header not a valid absolute URI |
| `REFERER_MISMATCH` | 400 | `Referer` host not in the loopback allowlist |
| `INVALID_REFERER_HEADER` | 400 | `Referer` header not a valid absolute URI |

CLI-only code `CLI_API_URL_NOT_LOOPBACK` is emitted client-side when `VULPERONEX_API_URL` is not a loopback URL.

---

### OQ5 — Web Host Authentication Model ✅ (revised — see §4.17)

Resolved in §4.17 (G15). The original "both ports loopback-only, no authentication" model was hardened during Phase 6+ and **superseded**:
- **API port**: loopback-only (IPv4 127.0.0.1 + IPv6 ::1). **Overlay port**: loopback-only by default, optionally bound to the LAN (`Overlay:Lan:Enabled`) for cross-machine OBS.
- **Not unauthenticated.** `AdminGuardMiddleware` requires, for loopback mutations and all `/api/overlay/*`: Host allowlist + per-process `X-Admin-Csrf` token + matching `Origin`/`Referer`. LAN requests are restricted to the overlay surface and require an overlay access key (`?k=` / `X-Overlay-Key`).
- Local OBS uses `http://localhost:5001/overlay/chat.html` / `…/member-card.html`; remote OBS uses `http://<lan-host>:5001/overlay/chat.html?k=<overlay-key>` (key + URLs from `GET /api/overlay/lan-info`). `/overlay/chat` and `/overlay/member` remain compatibility redirects only.

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
