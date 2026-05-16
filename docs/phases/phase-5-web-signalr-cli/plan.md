# Phase 5 Plan - Web Host + SignalR + CLI

> Parent plan: `tasks/plan.md`
> Parent checklist: `tasks/todo.md`
> Scope: Tasks 14a, 14b, 15, and 16

---

## Planning Rules

- Keep this phase API-first and integration-testable. The CLI and future UI must share the same Minimal API write path.
- Preserve light CQRS: GET endpoints use query/read services; write endpoints use command/write repositories or application services.
- Backend error responses expose machine-readable error codes only. Human-readable strings remain a UI/i18n concern.
- The web host is loopback-only and unauthenticated for MVP. Do not introduce API keys or external bind addresses in this phase.
- Use BDD/TDD per scenario. Each implementation slice should land with focused tests and a task-scoped commit.
- Do not add new package dependencies without ask-first approval.
- Keep unrelated local files out of commits, especially untracked design drafts.

---

## Shared Contracts

### JSON And Endpoint Conventions

- All Web API and SignalR JSON serialization uses `System.Text.Json` with `JsonSerializerDefaults.Web` so REST endpoints, SignalR payloads, and Phase 4 Overlay DTO contracts all use the same camelCase naming policy.
- Minimal API endpoint registration uses `IEndpointRouteBuilder` extension methods, one extension per feature area. Do not invent a custom endpoint discovery framework in Phase 5.
- Task 14a-0 exposes `AddOpenApi()` and `/openapi/v1.json` loopback-only. CLI does not need generated clients in MVP, but the OpenAPI artifact must exist for Phase 6 frontend/API alignment.

### Phase 5 Error Codes

All Phase 5 API and CLI paths use central constants in `src/Hosts/Vulperonex.Web/Errors/ErrorCodes.cs` or a shared equivalent if implementation proves the constants belong in Application. Error envelopes use `{ "error": "ERROR_CODE", "meta": {...} }`.
Naming convention: `UNKNOWN_*` means a key or identifier is not in the allowlist/registry; `INVALID_*` means the submitted value is known but fails format, range, schema, or route/body consistency validation.

| Code | HTTP status | First task | Notes |
|------|-------------|------------|-------|
| `WORKFLOW_RULE_NOT_FOUND` | 404 | 14a-3 | Rule show/delete/enable/disable missing id |
| `UNKNOWN_EVENT_TYPE_KEY` | 400 | 14a-2 | Includes system events rejected as workflow triggers |
| `CIRCULAR_WORKFLOW_REFERENCE` | 400 | 14a-2 | Static save-time analysis |
| `UNKNOWN_ACTION_TYPE` | 400 | 14a-2 | Action schema validation |
| `UNKNOWN_CONDITION_TYPE` | 400 | 14a-2 | Condition schema validation |
| `ACTION_MISSING_REQUIRED_PARAM` | 400 | 14a-2 | Example: missing `Template` |
| `INVALID_ACTION_CONFIG` | 400 | 14a-2 | Bounds and enum validation |
| `INVALID_REGEX_PATTERN` | 400 | 14a-2 | Invalid or overlong regex |
| `INVALID_RULE_ID_MISMATCH` | 400 | 14a-3 | PUT route/body id mismatch |
| `UNKNOWN_SIMULATE_EVENT_TYPE` | 400 | 14b-1 | Unknown public simulate alias |
| `CONFIG_KEY_SECURITY_NAMESPACE` | 403 | 14b-2 | `security.*` GET/PUT |
| `OAUTH_CREDENTIAL_NAMESPACE` | 403 | 14b-2 | `oauth.*` GET/PUT |
| `UNKNOWN_CONFIG_KEY` | 400 | 14b-2 | Unknown non-protected config key |
| `INVALID_QUERY_PARAM` | 400 | 14b-3 | Member list paging/filter validation |
| `MEMBER_NOT_FOUND` | 404 | 14b-3 | Member show missing id |

### Simulate Alias Source Of Truth

- Public simulate aliases live in one injectable `SimulationAliasRegistry` or equivalent singleton: `chat -> user.message`, `follow -> user.followed`, `sub -> user.subscribed`.
- `GET /api/event-types`, `POST /api/simulate/{alias}`, and CLI simulate commands all consume this shared registry. Do not hard-code the alias map separately in endpoint handlers.

### Manual Verification Records

- Manual Phase 5 checks are recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`.
- Each entry includes date, verifier, command/browser/OBS setup, expected behavior, observed behavior, and pass/fail result.

### Phase 5 Open Questions

- SignalR overlay reconnect/replay behavior: owner Task 19/Phase 6 frontend. Phase 5 only proves live push; missed-event replay is a Phase 6+ decision.
- CLI JSON output mode: owner Task 16 implementation review. Phase 5 can return JSON for successful API-shaped data, but a dedicated `--json` contract is Phase 6+ unless required by implementation.
- Web host shutdown signaling: owner Task 21 Photino shell. Client-visible shutdown semantics for overlay UX are Phase 6+.
- Phase 4 `TwitchAdapter` lazy `??=` race: owner Task 14a-0/15a integration review if Phase 5 touches real OAuth flow construction.
- Management/overlay hub auth for non-loopback binding: owner future LAN/remote OBS task. If a future phase allows LAN binding or port forwarding, hub auth becomes mandatory before that change ships.

### Phase 5 Pre-Implementation Dependency

- Task 13f follow-up: strengthen Phase 4 SC-6a/SC-6b equivalence with follow/sub/donate payloads and assertions for cache state, member state, `TotalBitsGiven`, and subscriber tier. Phase 5 checkpoint depends on this follow-up being done or explicitly waived; it is not a Phase 5 implementation slice.

---

## Dependency Graph

```text
Task 14a-0 Web host composition and API test harness
    -> Task 14a-1 WorkflowRule persistence/query alignment
    -> Task 14a-2 WorkflowRule validation and error codes
    -> Task 14a-3 WorkflowRule CRUD endpoints and CQRS tests
    -> Task 14a-4 EventTypes endpoint

Task 14b-1 Simulate endpoints
    depends on Task 14a-0, Task 9
Task 14b-2 Config endpoints
    depends on Task 14a-0, Task 8
Task 14b-3 Member query endpoints
    depends on Task 14a-0, Task 7

Task 15a SignalR hub contracts and host registration
    depends on Task 14a-0, Task 13
Task 15b Overlay event forwarding and SC-5
    depends on Task 15a
Task 15c Dual-port loopback Kestrel and port allocation
    depends on Task 15a

Task 16a CLI HTTP foundation and error passthrough
    depends on Task 14b, Task 15c
Task 16b Rule commands
    depends on Task 16a, Task 14a-3
Task 16c Config and member commands
    depends on Task 16a, Task 14b-2, Task 14b-3
Task 16d Simulate commands and manual overlay path
    depends on Task 16a, Task 14b-1, Task 15b
Task 16e Phase 5 checkpoint review
    depends on all Phase 5 slices
```

---

## Task 14a-0 - Web Host Composition And API Test Harness

**Description:** Turn `Vulperonex.Web` from an empty host into a testable Minimal API composition root. Register endpoint modules, shared error envelope helpers, JSON options, workflow services, and integration-test hooks without implementing the full endpoint surface yet.

**Acceptance Criteria:**
- [ ] `Program.cs` exposes a composable app builder suitable for integration tests.
- [ ] Endpoint registration is split by feature using `IEndpointRouteBuilder` extension methods (`MapWorkflowRuleEndpoints`, `MapEventTypeEndpoints`, etc.) rather than kept inline.
- [ ] `System.Text.Json` is configured with `JsonSerializerDefaults.Web`.
- [ ] `AddOpenApi()` is registered and `/openapi/v1.json` is exposed on the loopback API surface.
- [ ] Central Phase 5 error code constants and the status mapping table are available to endpoints and tests.
- [ ] Error envelopes use stable `error` codes and do not include backend human-readable prose.
- [ ] Integration tests can boot the web host with in-memory infrastructure/fakes.
- [ ] No production endpoint binds to non-loopback addresses in this slice.

**Verification:**
- [ ] Web integration smoke test can call a health/smoke endpoint or test-only route through the real host pipeline.
- [ ] Architecture check confirms Web references Application/Infrastructure but Domain/Application do not reference Web.
- [ ] Architecture test proves direct reads of `Configuration["Database:Path"]` are allowed only inside `IDatabasePathResolver` or its implementation; endpoints and other startup services must consume the resolver instead of reading the raw key.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Program.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/`
- `src/Hosts/Vulperonex.Web/Errors/`
- `tests/Vulperonex.Tests.Integration/Web/`

**Size:** S

---

## Task 14a-1 - WorkflowRule Persistence And Query Alignment

**Description:** Align `WorkflowRuleEntity`, repository, query service, and DTO shape with the REST API contract before endpoint implementation. Keep read DTOs separate from write entities.

**Acceptance Criteria:**
- [ ] Workflow rules persist the fields needed by API/UI: id, name, event type key, enabled flag, priority, conditions, actions, `CreatedAt`, and metadata needed by existing workflow execution.
- [ ] Rule list ordering is stable: `Priority ASC, CreatedAt ASC, Id ASC`.
- [ ] Query path returns DTOs through `IWorkflowRuleQueryService`.
- [ ] Write path remains behind `IWorkflowRuleRepository` or an application command service.
- [ ] Existing WorkflowEngine tests continue to pass.

**Verification:**
- [ ] Repository/query service tests cover create, update, delete, list, and show behavior.
- [ ] CQRS interaction fake proves GET paths do not call write repository methods.

**Likely Files:**
- `src/Vulperonex.Application/Workflow/`
- `src/Vulperonex.Infrastructure/Workflow/`
- `tests/Vulperonex.Tests.Integration/Workflow/`

**Size:** M

---

## Task 14a-2 - WorkflowRule Validation And Error Codes

**Description:** Implement save-time validation for event keys, system events, circular sub-workflow references, action schemas, condition schemas, regex patterns, config bounds, cooldown bounds, parallel limits, template length, and route/body id mismatch.

**Acceptance Criteria:**
- [ ] Validation failures use the central Phase 5 error code constants and HTTP status mapping.
- [ ] Unknown event key returns `UNKNOWN_EVENT_TYPE_KEY`.
- [ ] `platform.connection_changed` is known but invalid as a workflow trigger.
- [ ] Circular sub-workflow references return `CIRCULAR_WORKFLOW_REFERENCE`.
- [ ] Unknown action/condition types return `UNKNOWN_ACTION_TYPE` / `UNKNOWN_CONDITION_TYPE`.
- [ ] Missing required action params return `ACTION_MISSING_REQUIRED_PARAM`.
- [ ] Invalid bounds/config return `INVALID_ACTION_CONFIG`.
- [ ] Invalid regex returns `INVALID_REGEX_PATTERN`.
- [ ] PUT route/body id mismatch returns `INVALID_RULE_ID_MISMATCH`; the endpoint short-circuits this before invoking the deeper workflow rule validator.

**Verification:**
- [ ] Unit tests cover each error code with Given/When/Then naming.
- [ ] Validation tests assert error code only, not localized copy.
- [ ] Template rendering remains covered: unknown placeholders such as `{event.unknown}` are preserved, and null/empty placeholder values render as empty strings.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Errors/`
- `tests/Vulperonex.Tests.Unit/Web/WorkflowRuleValidatorTests.cs`

**Size:** M

---

## Task 14a-3 - WorkflowRule CRUD Endpoints

**Description:** Implement WorkflowRule REST CRUD over the validated application write/query paths.

**Acceptance Criteria:**
- [ ] `GET /api/rules` lists rules through the query service.
- [ ] `GET /api/rules/{id}` returns one rule or `WORKFLOW_RULE_NOT_FOUND`.
- [ ] `POST /api/rules` creates a rule and returns `201 Created` with `Location: /api/rules/{newId}`.
- [ ] `PUT /api/rules/{id}` updates a rule and returns the updated rule.
- [ ] `PUT /api/rules/{id}` with body id not equal to route id returns `INVALID_RULE_ID_MISMATCH` from the endpoint layer and does not invoke the validator or repository.
- [ ] `DELETE /api/rules/{id}` deletes a rule and returns `204`; missing rule returns `WORKFLOW_RULE_NOT_FOUND`.
- [ ] Enable/disable endpoints update only enabled state and return `204`; missing rule returns `WORKFLOW_RULE_NOT_FOUND`.
- [ ] GET path interaction tests prove the write repository is not called.

**Verification:**
- [ ] In-memory SQLite integration tests cover CRUD, not-found, route/body id mismatch, validation failure, and response status codes.
- [ ] SC-2 and SC-9 existing workflow behavior remains green.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Endpoints/WorkflowRuleEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/WorkflowRuleEndpointTests.cs`

**Size:** M

---

## Task 14a-4 - EventTypes Endpoint

**Description:** Implement `GET /api/event-types` for UI dropdowns and CLI discovery.

**Acceptance Criteria:**
- [ ] Endpoint returns registered workflow-visible event keys.
- [ ] `platform.connection_changed` is excluded from workflow-visible results.
- [ ] `isSimulatable` is derived from the shared `SimulationAliasRegistry` and true only for public aliases: `chat`, `follow`, `sub`.
- [ ] Endpoint does not require Twitch OAuth/socket startup.

**Verification:**
- [ ] Integration test uses fake registrars and asserts exact keys plus `isSimulatable`.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Endpoints/EventTypeEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/EventTypeEndpointTests.cs`

**Size:** S

---

## Task 14b-1 - Simulate Endpoints

**Description:** Implement `POST /api/simulate/{alias}` as the REST surface for CLI/manual testing.

**Acceptance Criteria:**
- [ ] Only `chat`, `follow`, and `sub` aliases are accepted.
- [ ] Raw canonical keys like `user.message` are rejected on this endpoint.
- [ ] Unknown alias returns `UNKNOWN_SIMULATE_EVENT_TYPE`.
- [ ] Alias validation uses the shared `SimulationAliasRegistry`.
- [ ] Endpoint calls `ISimulationAdapter` and publishes through the normal bus path.

**Verification:**
- [ ] Integration tests cover accepted aliases, unknown alias, canonical-key rejection, and mock sender side effects for chat.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Endpoints/SimulateEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/SimulateEndpointTests.cs`

**Size:** S

---

## Task 14b-2 - Config Endpoints

**Description:** Implement `GET|PUT /api/config/{key}` with protected namespace checks before registry lookup.

**Acceptance Criteria:**
- [ ] `security.*` returns `403` + `CONFIG_KEY_SECURITY_NAMESPACE` for GET and PUT.
- [ ] `oauth.*` returns `403` + `OAUTH_CREDENTIAL_NAMESPACE` for GET and PUT.
- [ ] Unknown non-protected keys return `400` + `UNKNOWN_CONFIG_KEY`.
- [ ] Unknown protected keys still return the protected namespace error, not unknown-key.
- [ ] The denylist is checked before registry lookup even though current OAuth refresh tokens are owned by `IOAuthTokenStore` and do not live in the config registry. This prevents future `oauth.*` registry entries from accidentally becoming readable or writable through `/api/config`.
- [ ] Allowed keys use `ISystemSettingsService`.

**Verification:**
- [ ] Integration tests cover known protected keys, unknown protected keys, unknown non-protected keys, GET, PUT, and success paths.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Endpoints/ConfigEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/ConfigEndpointTests.cs`

**Size:** S

---

## Task 14b-3 - Member Query Endpoints

**Description:** Implement read-only member list/show APIs through `IMemberQueryService`.

**Acceptance Criteria:**
- [ ] `GET /api/members` supports `limit` default 50, max 200, and `offset`.
- [ ] Member list response includes `total` for paging UI.
- [ ] Member list ordering is stable, defaulting to `LastSeen DESC, MemberId ASC` when those fields are available from the query service.
- [ ] Invalid query params return `INVALID_QUERY_PARAM`.
- [ ] `GET /api/members/{id}` returns one member or `MEMBER_NOT_FOUND`.
- [ ] Endpoints do not call member write repositories.

**Verification:**
- [ ] Integration tests seed DB and cover list, paging, invalid paging, show, and missing member.

**Likely Files:**
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`
- `src/Vulperonex.Infrastructure/Members/MemberQueryService.cs`
- `src/Hosts/Vulperonex.Web/Endpoints/MemberEndpoints.cs`
- `tests/Vulperonex.Tests.Integration/Web/MemberEndpointTests.cs`

**Size:** M

---

## Task 15a - SignalR Hub Contracts And Host Registration

**Description:** Add management and overlay SignalR hubs, register SignalR in the host, and keep hub contracts explicit before forwarding events.

**Acceptance Criteria:**
- [ ] `/hubs/events` exists for management clients.
- [ ] Canonical overlay hub routes are `/hubs/overlay/chat`, `/hubs/overlay/alerts`, and `/hubs/overlay/member` to match SPEC overlay URL naming.
- [ ] Management hub can receive all `IStreamEvent` categories, including `PlatformConnectionChangedEvent`.
- [ ] Overlay hub DTOs remain the Phase 4 public payload DTOs, not domain entities.
- [ ] Phase 5 assumes loopback-only/no-auth. If a future phase allows LAN binding or port forwarding, management and overlay hub authentication must be designed before enabling it.

**Verification:**
- [ ] Hub connection tests prove each route accepts a SignalR connection.
- [ ] Hub contract tests assert no Domain/Application Twitch symbols leak into hub payloads.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Hubs/EventHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayChatHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayAlertsHub.cs`
- `src/Hosts/Vulperonex.Web/Hubs/OverlayMemberHub.cs`
- `tests/Vulperonex.Tests.Integration/Web/SignalRHubTests.cs`

**Size:** M

---

## Task 15b - Overlay Event Forwarding And SC-5

**Description:** Subscribe to stream events and forward overlay-safe DTOs through the appropriate overlay hubs.

**Acceptance Criteria:**
- [ ] Chat events reach `/hubs/overlay/chat` within 5 seconds for SC-5.
- [ ] Performance budget is tracked separately from the SC pass/fail timeout: publish-to-hub-send target < 500ms, hub-to-client target < 500ms, full local path P95 target < 1s. SC-5 still uses 5s as the CI-safe upper bound.
- [ ] Alert-worthy events reach `/hubs/overlay/alerts`.
- [ ] Member hub is connected as an MVP skeleton but does not invent unsupported events.
- [ ] SignalR JSON key-set tests exactly match overlay DTO public contracts.
- [ ] Synthetic `eventId` semantics are documented in `docs/phases/phase-5-web-signalr-cli/event-id-decision.md`: platform-provided ids identify the same event across clients; fallback ULIDs only guarantee local single-instance delivery ids.

**Verification:**
- [ ] SC-5 integration test publishes a mock/simulated chat event and observes the overlay hub payload within 5 seconds.
- [ ] Test output records publish-to-hub-send and hub-to-client elapsed time so regressions above the 1s local target are visible even if the 5s SC timeout still passes.
- [ ] Exact JSON key-set tests cover chat, alert, and member payloads by deserializing the SignalR wire payload, not by reflection. This is defense-in-depth with Phase 4 DTO `System.Text.Json` key-set tests.
- [ ] Event id decision doc is reviewed before overlay forwarding is implemented.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Overlay/`
- `tests/Vulperonex.Tests.Integration/Web/OverlaySignalRTests.cs`

**Size:** M

---

## Task 15c - Dual-Port Loopback Kestrel And Port Allocation

**Description:** Implement API/overlay port-pair allocation and loopback-only Kestrel binding.

**Acceptance Criteria:**
- [ ] Port pairs try `5000/5001`, then `5002/5003`, through `5008/5009`.
- [ ] `PortPairAllocator.TryAllocate()` returns `null` when all pairs are exhausted.
- [ ] Host startup turns null allocation into `PortExhaustedException` with a clear message.
- [ ] Both ports bind to `IPAddress.Loopback` and `IPAddress.IPv6Loopback`.
- [ ] Non-loopback socket attempts are rejected by tests.
- [ ] `PortPairAllocator` and the OAuth callback port selector share `IPortAvailabilityProbe.IsAvailable(int port)` to avoid divergent socket availability behavior.

**Verification:**
- [ ] Unit tests cover first available pair, partial-pair occupation, all-pairs exhaustion, and no throw in allocator.
- [ ] Integration/socket tests verify IPv4/IPv6 loopback binds and non-loopback rejection.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Infrastructure/PortPairAllocator.cs`
- `src/Hosts/Vulperonex.Web/Infrastructure/IPortAvailabilityProbe.cs`
- `src/Hosts/Vulperonex.Web/Infrastructure/PortExhaustedException.cs`
- `src/Hosts/Vulperonex.Web/Program.cs`
- `tests/Vulperonex.Tests.Unit/Web/PortPairAllocatorTests.cs`

**Size:** M

---

## Task 16a - CLI HTTP Foundation And Error Passthrough

**Description:** Build the CLI command foundation around HTTP calls to the loopback API. The CLI must not directly access SQLite or application repositories.

**Acceptance Criteria:**
- [ ] CLI resolves API base URL from config/default loopback settings.
- [ ] 2xx responses write success output to stdout only.
- [ ] 4xx/5xx responses write the response `error` code to stderr and exit code `1`; network/connectivity failures also exit code `1` in MVP.
- [ ] CLI preserves backend codes such as `WORKFLOW_RULE_NOT_FOUND`, `MEMBER_NOT_FOUND`, and protected namespace errors.
- [ ] CLI has no direct DB access path.

**Verification:**
- [ ] CLI tests use a fake HTTP server/handler and assert stdout, stderr, and exit code behavior.
- [ ] Architecture test rejects CLI references to Infrastructure persistence where not allowed.

**Likely Files:**
- `src/Hosts/Vulperonex.Cli/Program.cs`
- `src/Hosts/Vulperonex.Cli/Http/`
- `tests/Vulperonex.Tests.Integration/Cli/`

**Size:** M

---

## Task 16b - CLI Rule Commands

**Description:** Implement `rule list|show|enable|disable|delete` over the WorkflowRule API.

**Acceptance Criteria:**
- [ ] `rule list` calls `GET /api/rules`.
- [ ] `rule show` passes through `WORKFLOW_RULE_NOT_FOUND`.
- [ ] `rule enable` and `rule disable` call the API state endpoints.
- [ ] `rule delete` calls DELETE and treats `204` as success.

**Verification:**
- [ ] CLI integration tests cover each command and error passthrough.

**Likely Files:**
- `src/Hosts/Vulperonex.Cli/Commands/RuleCommands.cs`
- `tests/Vulperonex.Tests.Integration/Cli/RuleCommandTests.cs`

**Size:** S

---

## Task 16c - CLI Config And Member Commands

**Description:** Implement `config get|set` and `member list|show` over the REST API.

**Acceptance Criteria:**
- [ ] Protected config namespace errors pass through to stderr with non-zero exit.
- [ ] Unknown config keys pass through as `UNKNOWN_CONFIG_KEY`.
- [ ] `member list` supports limit/offset arguments.
- [ ] `member show` passes through `MEMBER_NOT_FOUND`.

**Verification:**
- [ ] CLI tests cover success and backend error passthrough for config and member commands.

**Likely Files:**
- `src/Hosts/Vulperonex.Cli/Commands/ConfigCommands.cs`
- `src/Hosts/Vulperonex.Cli/Commands/MemberCommands.cs`
- `tests/Vulperonex.Tests.Integration/Cli/ConfigCommandTests.cs`
- `tests/Vulperonex.Tests.Integration/Cli/MemberCommandTests.cs`

**Size:** S

---

## Task 16d - CLI Simulate Commands And DB Path Rule

**Description:** Implement `simulate chat|follow|sub` and verify the shared DB path rule for web host and CLI configuration.

**Acceptance Criteria:**
- [ ] `simulate chat`, `simulate follow`, and `simulate sub` call the REST simulate endpoint.
- [ ] Unknown simulate aliases fail before or through the API with `UNKNOWN_SIMULATE_EVENT_TYPE`.
- [ ] Web host and CLI read `Database:Path` only from main `appsettings.json`.
- [ ] `appsettings.{Environment}.json` and environment variables cannot override `Database:Path`.
- [ ] The implementation mechanism is explicit: database path resolution reads main `appsettings.json` through a dedicated resolver that ignores environment-specific providers and environment variables for this key, then supplies the resolved value to Web host and CLI startup.

**Verification:**
- [ ] CLI simulate chat fixture rule triggers mock sender through the API path.
- [ ] Manual test: CLI simulate chat reaches the overlay SignalR client, with result recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`.
- [ ] Integration test proves `ASPNETCORE_ENVIRONMENT=Development`, `appsettings.Development.json`, and environment variables do not override `Database:Path`.

**Likely Files:**
- `src/Hosts/Vulperonex.Cli/Commands/SimulateCommands.cs`
- `src/Hosts/Vulperonex.Cli/Configuration/`
- `src/Hosts/Vulperonex.Web/Configuration/`
- `tests/Vulperonex.Tests.Integration/Cli/SimulateCommandTests.cs`

**Size:** M

---

## Task 16e - Phase 5 Checkpoint Review

**Description:** Run the full Phase 5 verification gate and address review follow-ups before moving to Phase 6.

**Acceptance Criteria:**
- [ ] `dotnet test` passes for SC-2, SC-5, SC-8, and SC-9.
- [ ] WorkflowRule CRUD and circular reference detection pass end to end.
- [ ] Config protected namespace tests pass for `security.*` and `oauth.*`.
- [ ] CLI rule/config/member/simulate commands pass integration tests.
- [ ] CLI simulate chat fixture rule and mock sender pass.
- [ ] CLI simulate to overlay SignalR manual path is documented.
- [ ] Loopback-only IPv4/IPv6 dual bind tests pass.
- [ ] Task 13f Phase 4 SC-6a/SC-6b follow-up is complete or explicitly waived before Phase 5 closes.

**Verification:**
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] Manual overlay SignalR check recorded in the phase todo.
- [ ] Manual verification entries are recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`.
- [ ] Git staged set is task-scoped and excludes unrelated untracked files.

**Likely Files:**
- `docs/phases/phase-5-web-signalr-cli/todo.md`
- `docs/phases/phase-5-web-signalr-cli/manual-verification.md`
- `tasks/todo.md`
- Phase 5 source/test files from prior slices

**Size:** S
