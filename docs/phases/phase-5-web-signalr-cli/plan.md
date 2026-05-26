# Phase 5 Plan - Web Host + SignalR + CLI

> Parent Plan: `tasks/plan.md`
> Parent Checklist: `tasks/todo.md`
> Scope: Tasks 14a, 14b, 15, 16

---

## Planning Rules

- Maintain an API-first approach and integration-testable design in this phase. The CLI and future UI must share the same Minimal API write paths.
- Retain light CQRS: GET endpoints utilize query/read services; write endpoints utilize command/write repositories or application services.
- Backend error responses only expose machine-readable error codes. Human-readable strings belong to the UI/i18n boundaries.
- The Web host is restricted to loopback and remains without authentication in the MVP phase. Do not introduce API keys or external binding addresses in this phase.
- Use BDD/TDD per scenarios. Each implementation slice should be accompanied by focused tests and task-scoped commits.
- No new package dependencies are allowed without prior approval.
- Ensure unrelated local files do not enter commits, especially untracked design drafts.

---

## Shared Contracts

### JSON and Endpoint Conventions

- All JSON serialization for Web APIs and SignalR utilizes `System.Text.Json` configured with `JsonSerializerDefaults.Web`. Therefore, REST endpoints, SignalR payloads, and Phase 4 Overlay DTO contracts share the same camelCase naming conventions.
- Minimal API endpoint registrations utilize `IEndpointRouteBuilder` extension methods, separated by functional areas. Do not invent custom endpoint discovery frameworks in Phase 5.
- Task 14a-0 exposes `AddOpenApi()` and `/openapi/v1.json` strictly on loopback. The CLI does not require a generated client in the MVP, but the OpenAPI document must exist for Phase 6 frontend/API alignment.

### Phase 5 Error Codes

All Phase 5 API and CLI paths utilize central constants in `src/Hosts/Vulperonex.Web/Errors/ErrorCodes.cs` (or equivalents if implementation proves constants belong in Application). Error packaging utilizes `{ "error": "ERROR_CODE", "meta": {...} }`.
Naming conventions: `UNKNOWN_*` indicates the key or identifier is missing from allowlists/registries; `INVALID_*` indicates the submitted value is known but validation fails on format, range, schema, or path/body consistency.

| Code | HTTP Status | First Task | Notes |
|------|-------------|------------|-------|
| `WORKFLOW_RULE_NOT_FOUND` | 404 | 14a-3 | Missing ID during rule display/delete/enable/disable |
| `UNKNOWN_EVENT_TYPE_KEY` | 400 | 14a-2 | Includes system events rejected as workflow triggers |
| `CIRCULAR_WORKFLOW_REFERENCE` | 400 | 14a-2 | Analyzed during static persistence |
| `UNKNOWN_ACTION_TYPE` | 400 | 14a-2 | Action schema validation |
| `UNKNOWN_CONDITION_TYPE` | 400 | 14a-2 | Condition schema validation |
| `ACTION_MISSING_REQUIRED_PARAM` | 400 | 14a-2 | e.g. missing `Template` |
| `INVALID_ACTION_CONFIG` | 400 | 14a-2 | Range and enum validation |
| `INVALID_REGEX_PATTERN` | 400 | 14a-2 | Invalid or overly long Regex |
| `INVALID_RULE_ID_MISMATCH` | 400 | 14a-3 | PUT path/body ID mismatch |
| `UNKNOWN_SIMULATE_EVENT_TYPE` | 400 | 14b-1 | Unknown public simulation alias |
| `CONFIG_KEY_SECURITY_NAMESPACE` | 403 | 14b-2 | `security.*` GET/PUT |
| `OAUTH_CREDENTIAL_NAMESPACE` | 403 | 14b-2 | `oauth.*` GET/PUT |
| `UNKNOWN_CONFIG_KEY` | 400 | 14b-2 | Unknown non-protected config key |
| `INVALID_QUERY_PARAM` | 400 | 14b-3 | Member list pagination/filter validation |
| `MEMBER_NOT_FOUND` | 404 | 14b-3 | Missing ID during member display |

### Single Source of Truth for Simulation Aliases

- Public simulation aliases exist in an injectable `SimulationAliasRegistry` or equivalent Singleton: `chat -> user.message`, `follow -> user.followed`, `sub -> user.subscribed`.
- `GET /api/event-types`, `POST /api/simulate/{alias}`, and CLI simulation commands all consume this shared registry. Do not hardcode alias mapping tables in endpoint handlers separately.

### Manual Verification Log

- Phase 5 manual checks are recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`.
- Each entry contains the date, validator name, command/browser/OBS setup, expected behavior, observed behavior, and pass/fail results.

### Phase 5 Pending Topics

- SignalR overlay reconnect/replay behavior: Deferred to Task 19/Phase 6 frontend. Phase 5 only proves real-time push; unless Task 15b testing fails to stably verify real-time delivery, event replay on missing connections is deferred beyond Phase 5.
- CLI JSON output mode: Isolated to Task 16 implementation reviews. Phase 5 can return JSON for successful API-formatted data; unless CLI commands require supporting both human-readable and machine-readable outputs simultaneously, dedicated `--json` contracts are deferred beyond Phase 5.
- Web host shutdown signals: Deferred to Task 21 Photino shell. User-visible overlay UX shutdown semantics belong to Phase 6+.
- Phase 4 `TwitchAdapter` lazy `??=` race conditions: Managed by Task 14a-0/15a integration reviews if Phase 5 involves real OAuth flow builds.
- Non-loopback binding admin/overlay hub validation: Future LAN/remote OBS tasks. If future phases permit LAN binding or port forwarding, hub validation must be enforced before publishing changes.

### Phase 5 Pre-implementation Dependencies

- Task 13f follow-up: Strengthen Phase 4 SC-6a/SC-6b equivalence, including follow/sub/donate payloads and assertions on cache states, member states, `TotalBitsGiven`, and subscriber tiers. The Phase 5 checkpoint depends on this follow-up work being completed or explicitly exempted; this is not an implementation slice for Phase 5.

---

## Dependency Graph

```text
Task 14a-0 Web host assembly and API test tools
    -> Task 14a-1 WorkflowRule persistence/query alignment
    -> Task 14a-2 WorkflowRule validation and error codes
    -> Task 14a-3 WorkflowRule CRUD endpoints and CQRS tests
    -> Task 14a-4 EventTypes endpoint

Task 14b-1 Simulation Endpoint
    Depends on Task 14a-0, Task 9
Task 14b-2 Configuration Endpoint
    Depends on Task 14a-0, Task 8
Task 14b-3 Member Query Endpoint
    Depends on Task 14a-0, Task 7

Task 15a SignalR hub contracts and host registration
    Depends on Task 14a-0, Task 13
Task 15b Overlay event forwarding and SC-5
    Depends on Task 15a
Task 15c Dual-port loopback Kestrel and port allocation
    Depends on Task 15a

Task 16a CLI HTTP infrastructure and error passthrough
    Depends on Task 14b, Task 15c
Task 16b Rule commands
    Depends on Task 16a, Task 14a-3
Task 16c Configuration and Member commands
    Depends on Task 16a, Task 14b-2, Task 14b-3
Task 16d Simulation commands and manual overlay paths
    Depends on Task 16a, Task 14b-1, Task 15b
Task 16e Phase 5 checkpoint review
    Depends on all Phase 5 slices
```

---

## Task 14a-0 - Web Host Assembly and API Test Tools

**Description:** Transform `Vulperonex.Web` from an empty host into a testable Minimal API composition root. Register endpoint modules, shared error packaging helpers, JSON options, workflow services, and integration test hooks, without implementing the full endpoint surface yet.

**Acceptance Criteria:**
- [ ] `Program.cs` exposes a composable application builder suitable for integration testing.
- [ ] Endpoint registrations are separated by functional areas using `IEndpointRouteBuilder` extension methods (e.g. `MapWorkflowRuleEndpoints`, `MapEventTypeEndpoints`) rather than kept inline.
- [ ] `System.Text.Json` is configured with `JsonSerializerDefaults.Web`.
- [ ] Register `AddOpenApi()` and expose `/openapi/v1.json` strictly on the loopback API surface.
- [ ] Endpoints and tests can utilize central Phase 5 error code constants and status mappings.
- [ ] Error packaging utilizes stable `error` codes and does not include backend human-readable descriptions.
- [ ] Integration tests can launch the Web host using in-memory infrastructure/fakes.
- [ ] In this slice, no production endpoints bind to non-loopback addresses.

**Verification:**
- [ ] Web integration smoke tests can invoke health or smoke test endpoints through the actual host pipeline.
- [ ] Architectural checks verify that Web references Application/Infrastructure, but Domain/Application does not reference Web.
- [ ] Architectural tests prove that directly reading `Configuration["Database:Path"]` is only permitted in `IDatabasePathResolver` or its implementations; endpoints and other startup services must use the resolver instead of reading raw keys.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Program.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/`
- `src/Hosts/Vulperonex.Web/Errors/`
- `src/Hosts/Vulperonex.Web/Configuration/IDatabasePathResolver.cs`
- `tests/Vulperonex.Tests.Integration/Web/`

**Size:** S

---

## Task 14a-1 - WorkflowRule Persistence and Query Alignment

**Description:** Align `WorkflowRuleEntity`, repositories, query services, and DTO shapes with REST API contracts before endpoint implementation. Keep read DTOs separate from write entities.

**Acceptance Criteria:**
- [ ] Workflow rules persist fields required by the API/UI: ID, name, event type key, enabled flag, priority, conditions, actions, `CreatedAt`, and metadata required for active workflow execution.
- [ ] Rule list sorting is stable: `Priority ASC, CreatedAt ASC, Id ASC`.
- [ ] The query path returns DTOs via `IWorkflowRuleQueryService`.
- [ ] The write path remains behind `IWorkflowRuleRepository` or application command services.
- [ ] Existing WorkflowEngine tests continue to pass.

**Verification:**
- [ ] Repository/query service tests cover create, update, delete, list, and display behaviors.
- [ ] CQRS interaction fakes prove that GET paths do not call write repository methods.

**Files Likely Involved:**
- `src/Vulperonex.Application/Workflow/`
- `src/Vulperonex.Infrastructure/Workflow/`
- `tests/Vulperonex.Tests.Integration/Workflow/`

**Size:** M

---

## Task 14a-2 - WorkflowRule Validation and Error Codes

**Description:** Implement save-time validations for event keys, system events, circular sub-workflow references, Action schemas, Condition schemas, Regex patterns, config ranges, cooldown ranges, parallelism limits, template lengths, and path/body ID mismatches.

**Acceptance Criteria:**
- [ ] Validation failures utilize central Phase 5 error code constants and HTTP status mappings.
- [ ] Unknown event keys return `UNKNOWN_EVENT_TYPE_KEY`.
- [ ] `platform.connection_changed` is recognized but invalid as a workflow trigger.
- [ ] Circular sub-workflow references return `CIRCULAR_WORKFLOW_REFERENCE`.
- [ ] Unknown Action/Condition types return `UNKNOWN_ACTION_TYPE` / `UNKNOWN_CONDITION_TYPE`.
- [ ] Missing required Action parameters return `ACTION_MISSING_REQUIRED_PARAM`.
- [ ] Invalid ranges/configurations return `INVALID_ACTION_CONFIG`.
- [ ] Invalid Regex patterns return `INVALID_REGEX_PATTERN`.
- [ ] PUT path/body ID mismatches return `INVALID_RULE_ID_MISMATCH`; endpoints short-circuit this before invoking deeper workflow rule validators.

**Verification:**
- [ ] Unit tests named with Given/When/Then cover each error code.
- [ ] Validation tests assert strictly on error codes, not localized copy.
- [ ] Template rendering continues to cover: preserving original text for unknown placeholders (e.g. `{event.unknown}`), replacing null/empty placeholders with empty strings.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Errors/`
- `tests/Vulperonex.Tests.Unit/Web/WorkflowRuleValidatorTests.cs`

**Size:** M

---

## Task 14a-3 - WorkflowRule CRUD Endpoints

**Description:** Implement WorkflowRule REST CRUD on top of validated application write/query paths.

**Acceptance Criteria:**
- [ ] `GET /api/rules` lists rules via the query service.
- [ ] `GET /api/rules/{id}` returns a single rule or `WORKFLOW_RULE_NOT_FOUND`.
- [ ] `POST /api/rules` creates rules, returning `201 Created` with a `Location: /api/rules/{newId}` header.
- [ ] `PUT /api/rules/{id}` updates rules, returning the updated rule.
- [ ] `PUT /api/rules/{id}` where the body ID does not equal the path ID returns `INVALID_RULE_ID_MISMATCH` from the endpoint layer, without invoking validators or repositories.
- [ ] `DELETE /api/rules/{id}` deletes rules, returning `204`; missing rules return `WORKFLOW_RULE_NOT_FOUND`.
- [ ] Enable/disable endpoints update only active status, returning `204`; missing rules return `WORKFLOW_RULE_NOT_FOUND`.
- [ ] GET path interaction tests prove that write repositories are not called.

**Verification:**
- [ ] In-memory SQLite integration tests cover CRUD, not founds, path/body ID mismatches, validation failures, and response status codes.
- [ ] Existing SC-2 and SC-9 workflow behaviors remain passing (green).

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Endpoints/WorkflowRuleEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/WorkflowRuleEndpointTests.cs`

**Size:** M

---

## Task 14a-4 - EventTypes Endpoint

**Description:** Implement `GET /api/event-types` for UI dropdowns and CLI discovery.

**Acceptance Criteria:**
- [ ] The endpoint returns registered workflow-visible event keys.
- [ ] `platform.connection_changed` is excluded from workflow-visible results.
- [ ] `isSimulatable` originates from the shared `SimulationAliasRegistry`, being true only for public aliases (`chat`, `follow`, `sub`).
- [ ] The endpoint does not require Twitch OAuth/sockets to start.

**Verification:**
- [ ] Integration tests utilize a fake registry and assert exact keys along with `isSimulatable`.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Endpoints/EventTypeEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/EventTypeEndpointTests.cs`

**Size:** S

---

## Task 14b-1 - Simulation Endpoint

**Description:** Implement `POST /api/simulate/{alias}` as a REST interface for CLI/manual testing.

**Acceptance Criteria:**
- [ ] Only `chat`, `follow`, and `sub` aliases are accepted.
- [ ] Raw canonical keys (e.g. `user.message`) are rejected at this endpoint.
- [ ] Request body schemas are fixed: `chat` accepts `{ platformUserId?, displayName?, roles?, message? }`; `follow` accepts `{ platformUserId?, displayName?, roles? }`; `sub` accepts `{ platformUserId?, displayName?, roles?, tier? }`.
- [ ] Unknown aliases return `UNKNOWN_SIMULATE_EVENT_TYPE`.
- [ ] Alias validation utilizes the shared `SimulationAliasRegistry`.
- [ ] Endpoints invoke `ISimulationAdapter` and publish via normal event bus paths.

**Verification:**
- [ ] Integration tests cover accepted aliases, unknown aliases, canonical key rejections, and simulation side-effects for chat senders.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Endpoints/SimulateEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/SimulateEndpointTests.cs`

**Size:** S

---

## Task 14b-2 - Configuration Endpoint

**Description:** Implement `GET|PUT /api/config/{key}` with protected namespace checks before registry lookup.

**Acceptance Criteria:**
- [ ] `security.*` returns `403` + `CONFIG_KEY_SECURITY_NAMESPACE` for both GET and PUT.
- [ ] `oauth.*` returns `403` + `OAUTH_CREDENTIAL_NAMESPACE` for both GET and PUT.
- [ ] Unknown non-protected keys return `400` + `UNKNOWN_CONFIG_KEY`.
- [ ] Unknown protected keys still return protected namespace errors, not unknown key errors.
- [ ] While current OAuth refresh tokens are owned by `IOAuthTokenStore` and do not exist in the config registry, they are still checked against this blacklist before registry lookup. This prevents future `oauth.*` registry entries from accidentally becoming readable or writable via `/api/config`.
- [ ] Permitted keys utilize `ISystemSettingsService`.

**Verification:**
- [ ] Integration tests cover known protected keys, unknown protected keys, unknown non-protected keys, GET, PUT, and success paths.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Endpoints/ConfigEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/ConfigEndpointTests.cs`

**Size:** S

---

## Task 14b-3 - Member Query Endpoint

**Description:** Implement a read-only member list/display API via `IMemberQueryService`.

**Acceptance Criteria:**
- [ ] `GET /api/members` supports `limit` (default 50, max 200) and `offset` parameters.
- [ ] Member list responses include `total` for pagination UIs.
- [ ] Member list sorting is stable. Phase 5 defaults to `MemberId ASC` if a `LastSeen` column is not yet present; changes to `LastSeen DESC, MemberId ASC` when `LastSeen` is introduced later.
- [ ] Invalid query parameters return `INVALID_QUERY_PARAM`.
- [ ] `GET /api/members/{id}` returns a single member or `MEMBER_NOT_FOUND`.
- [ ] Endpoints do not invoke member write repositories.

**Verification:**
- [ ] Integration tests seed DB data and cover lists, pagination, invalid pagination, display, and missing members.

**Files Likely Involved:**
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`
- `src/Vulperonex.Infrastructure/Members/MemberQueryService.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/MemberEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/MemberEndpointTests.cs`

**Size:** M

---

## Task 15a - SignalR Hub Contracts and Host Registration

**Description:** Add admin and overlay SignalR hubs, register SignalR in the host, and keep hub contracts explicit before event forwarding.

**Acceptance Criteria:**
- [ ] Provide `/hubs/events` for admin clients.
- [ ] Canonical overlay hub paths are `/hubs/overlay/chat`, `/hubs/overlay/alerts`, and `/hubs/overlay/member` to align with SPEC overlay URL naming.
- [ ] Admin hubs can receive all `IStreamEvent` classes, including `PlatformConnectionChangedEvent`.
- [ ] Overlay hub DTOs remain Phase 4 public payload DTOs, not exposing domain entities.
- [ ] Phase 5 assumes loopback-only/no authentication; non-loopback scenarios reference the aforementioned open question and are not opened in this phase.
- [ ] Admin hubs do not expose client-to-server invokable methods; Phase 5 only permits server push to clients.

**Verification:**
- [ ] Hub connection tests prove each path accepts SignalR connections.
- [ ] Hub contract tests assert that Domain/Application Twitch symbols do not leak into hub payloads.
- [ ] Confirm `event-id-decision.md` review notes (reviewer/date/decision) are filled out before starting Task 15b.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Hubs/EventHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayChatHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayAlertsHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayMemberHub.cs`
- `tests/Vulperonex.Tests.Integration/Web/SignalRHubTests.cs`

**Size:** M

---

## Task 15b - Overlay Event Forwarding and SC-5

**Description:** Subscribe to stream events and forward overlay-secure DTOs through the appropriate overlay hubs.

**Acceptance Criteria:**
- [ ] Chat events arrive at `/hubs/overlay/chat` within 5 seconds to conform to SC-5.
- [ ] Performance budgets are tracked separately from SC pass/fail timeouts: publish to hub dispatch target < 500ms, hub to client target < 500ms, full local path P95 target < 1s. SC-5 still uses 5s as a CI safety limit.
- [ ] Alert event sets are explicitly restricted to `user.followed`, `user.subscribed`, `user.donated`, `user.gifted_subscription`, `channel.raided`. Phase 5 implements at least follow/sub overlay alert forwarding, leaving other events as candidates for subsequent adapter coverage.
- [ ] The member hub serves as an MVP connection skeleton, not inventing unsupported events.
- [ ] SignalR JSON key set tests exactly match overlay DTO public contracts.
- [ ] Synthetic `eventId` semantics are recorded in `docs/phases/phase-5-web-signalr-cli/event-id-decision.md`: platform-provided IDs identify identical events across clients; fallback ULIDs only guarantee local single-instance delivery IDs.

**Verification:**
- [ ] SC-5 integration tests publish chat events via `SimulationAdapter`, routing through the real event bus path, and observe the overlay hub payload within 5 seconds.
- [ ] Test outputs log elapsed times from publishing to hub dispatch, and hub to client; performance regressions exceeding the 1s local target remain visible even if the 5s SC timeout passes.
- [ ] Exact JSON key set tests cover chat, alert, and member payloads by deserializing SignalR wire payloads rather than utilizing reflection. This is defense-in-depth against Phase 4 DTO `System.Text.Json` key set tests.
- [ ] Review the event ID decision document before implementing overlay forwarding.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Overlay/`
- `tests/Vulperonex.Tests.Integration/Web/OverlaySignalRTests.cs`

**Size:** M

---

## Task 15c - Dual-Port Loopback Kestrel and Port Allocation

**Description:** Implement API/overlay port pair allocation and loopback-only Kestrel bindings.

**Acceptance Criteria:**
- [ ] Port pairs attempt starting from `5000/5001`, followed by `5002/5003`, up to `5008/5009`.
- [ ] When all port pairs are exhausted, `PortPairAllocator.TryAllocate()` returns `null`.
- [ ] Host startup transforms null allocations into a `PortExhaustedException` with a clear error message.
- [ ] Both ports bind strictly to `IPAddress.Loopback` and `IPAddress.IPv6Loopback`.
- [ ] Tests reject non-loopback socket attempts.
- [ ] `PortPairAllocator` shares `IPortAvailabilityProbe.IsAvailable(int port)` with the OAuth callback port selector to prevent divergent socket availability behaviors.

**Verification:**
- [ ] Unit tests cover first available pair, partial occupancy, complete exhaustion, and ensuring no exceptions are thrown inside the allocator.
- [ ] Integration/socket tests verify IPv4/IPv6 loopback bindings and non-loopback rejections.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Infrastructure/PortPairAllocator.cs`
- `src/Hosts/Vulperonex.Web/Infrastructure/IPortAvailabilityProbe.cs`
- `src/Hosts/Vulperonex.Web/Infrastructure/PortExhaustedException.cs`
- `src/Hosts/Vulperonex.Web/Program.cs`
- `tests/Vulperonex.Tests.Unit/Web/PortPairAllocatorTests.cs`

**Size:** M

---

## Task 16a - CLI HTTP Infrastructure and Error Passthrough

**Description:** Build CLI command foundations around HTTP calls to the loopback API. The CLI must not access SQLite or Application repositories directly.

**Acceptance Criteria:**
- [ ] The CLI API base URL resolves using the `VULPERONEX_API_URL` environment variable, falling back to the default loopback URL `http://localhost:5000` if missing.
- [ ] 2xx responses write success outputs strictly to stdout.
- [ ] 4xx/5xx responses write returned `error` codes to stderr, exiting with exit code `1`; network/connection failures also exit with `1` in the MVP.
- [ ] The CLI preserves backend codes like `WORKFLOW_RULE_NOT_FOUND`, `MEMBER_NOT_FOUND`, and protected namespace errors.
- [ ] The CLI features no direct database access paths.

**Verification:**
- [ ] CLI tests utilize a mock HTTP server/handler and assert stdout, stderr, and exit code behaviors.
- [ ] Architectural tests reject CLI references to Infrastructure persistence if they occur.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Cli/Program.cs`
- `src/Hosts/Vulperonex.Cli/Http/`
- `tests/Vulperonex.Tests.Integration/Cli/`

**Size:** M

---

## Task 16b - CLI Rule Commands

**Description:** Implement `rule list|show|enable|disable|delete` on top of the WorkflowRule API.

**Acceptance Criteria:**
- [ ] `rule list` calls `GET /api/rules`.
- [ ] `rule show` passes through `WORKFLOW_RULE_NOT_FOUND`.
- [ ] `rule enable` and `rule disable` invoke API state endpoints.
- [ ] `rule delete` calls DELETE and treats `204` as a success.

**Verification:**
- [ ] CLI integration tests cover each command and error passthrough.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Cli/Commands/RuleCommands.cs`
- `tests/Vulperonex.Tests.Integration/Cli/RuleCommandTests.cs`

**Size:** S

---

## Task 16c - CLI Configuration and Member Commands

**Description:** Implement `config get|set` and `member list|show` on top of the REST API.

**Acceptance Criteria:**
- [ ] Protected config namespace errors pass through to stderr and exit with non-zero statuses.
- [ ] Unknown config keys pass through as `UNKNOWN_CONFIG_KEY`.
- [ ] `member list` supports limit/offset parameters.
- [ ] `member show` passes through `MEMBER_NOT_FOUND`.

**Verification:**
- [ ] CLI tests cover configuration and member command successes and backend error passthroughs.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Cli/Commands/ConfigCommands.cs`
- `src/Hosts/Vulperonex.Cli/Commands/MemberCommands.cs`
- `tests/Vulperonex.Tests.Integration/Cli/ConfigCommandTests.cs`
- `tests/Vulperonex.Tests.Integration/Cli/MemberCommandTests.cs`

**Size:** S

---

## Task 16d - CLI Simulation Commands and Database Path Rules

**Description:** Implement `simulate chat|follow|sub` and verify shared database path rules configured for the Web host and CLI.

**Acceptance Criteria:**
- [ ] `simulate chat`, `simulate follow`, and `simulate sub` invoke REST simulation endpoints.
- [ ] Unknown simulation aliases fail via `UNKNOWN_SIMULATE_EVENT_TYPE` before or during API routing.
- [ ] The Web host and CLI read `Database:Path` strictly from the main `appsettings.json`.
- [ ] `appsettings.{Environment}.json` and environment variables cannot override `Database:Path`.
- [ ] The implementation mechanism is explicit: database path resolution reads the main `appsettings.json` via a dedicated resolver, which ignores environment-specific providers and environment variables for this key, and then provides the resolved value to launch the Web host and CLI.

**Verification:**
- [ ] CLI simulated chat triggers fake senders via the API path.
- [ ] Manual test: CLI simulated chat reaches the overlay SignalR client; results are recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`.
- [ ] Integration tests prove that `ASPNETCORE_ENVIRONMENT=Development`, `appsettings.Development.json`, and environment variables do not override `Database:Path` in the main `appsettings.json`.

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Cli/Commands/SimulateCommands.cs`
- `src/Hosts/Vulperonex.Cli/Configuration/`
- `src/Hosts/Vulperonex.Web/Configuration/`
- `tests/Vulperonex.Tests.Integration/Cli/SimulateCommandTests.cs`

**Size:** M

---

## Task 16e - Phase 5 Checkpoint Review

**Description:** Execute full Phase 5 verification gates and handle review follow-ups before starting Phase 6.

**Acceptance Criteria:**
- [ ] Tasks 14a-0 to 14b-3, 15a-15c, and 16a-16d are completed and committed in small slices.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [ ] `dotnet test` passes SC-2, SC-5, SC-8, and SC-9.
- [ ] WorkflowRule CRUD and circular reference detection pass end-to-end.
- [ ] Configuration protected namespace tests pass for `security.*` and `oauth.*`.
- [ ] CLI rule/config/member/simulate commands pass integration tests.
- [ ] CLI simulated chat rules and fake senders pass.
- [ ] CLI simulation to overlay SignalR manual path is documented.
- [ ] Loopback-only IPv4/IPv6 dual-binding tests pass.
- [ ] Task 13f Phase 4 SC-6a/SC-6b follow-ups are completed or explicitly exempted before closing Phase 5.

**Verification:**
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] Manual overlay SignalR checks are recorded in the phase checklist.
- [ ] Manual verification entries are recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`.
- [ ] Git staging sets are limited to task scopes, excluding unrelated untracked files.

**Files Likely Involved:**
- `docs/phases/phase-5-web-signalr-cli/todo.md`
- `docs/phases/phase-5-web-signalr-cli/manual-verification.md`
- `tasks/todo.md`
- Phase 5 source/test files from prior slices

**Size:** S
