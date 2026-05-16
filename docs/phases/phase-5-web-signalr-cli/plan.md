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
- [ ] Endpoint registration is split by feature (`WorkflowRule`, `EventTypes`, `Simulate`, `Config`, `Member`) rather than kept inline.
- [ ] Error envelopes use stable `error` codes and do not include backend human-readable prose.
- [ ] Integration tests can boot the web host with in-memory infrastructure/fakes.
- [ ] No production endpoint binds to non-loopback addresses in this slice.

**Verification:**
- [ ] Web integration smoke test can call a health/smoke endpoint or test-only route through the real host pipeline.
- [ ] Architecture check confirms Web references Application/Infrastructure but Domain/Application do not reference Web.

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
- [ ] Unknown event key returns `UNKNOWN_EVENT_TYPE_KEY`.
- [ ] `platform.connection_changed` is known but invalid as a workflow trigger.
- [ ] Circular sub-workflow references return `CIRCULAR_WORKFLOW_REFERENCE`.
- [ ] Unknown action/condition types return `UNKNOWN_ACTION_TYPE` / `UNKNOWN_CONDITION_TYPE`.
- [ ] Missing required action params return `ACTION_MISSING_REQUIRED_PARAM`.
- [ ] Invalid bounds/config return `INVALID_ACTION_CONFIG`.
- [ ] Invalid regex returns `INVALID_REGEX_PATTERN`.
- [ ] PUT route/body id mismatch returns `INVALID_RULE_ID_MISMATCH`.

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
- [ ] `POST /api/rules` creates a rule and returns `201`.
- [ ] `POST /api/rules` returns `201 Created` with `Location: /api/rules/{newId}`.
- [ ] `PUT /api/rules/{id}` updates a rule and returns the updated rule.
- [ ] `PUT /api/rules/{id}` with body id not equal to route id returns `INVALID_RULE_ID_MISMATCH`.
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
- [ ] `isSimulatable` is static and true only for public aliases: `chat`, `follow`, `sub`.
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
- [ ] `/hubs/overlay/chat`, `/hubs/overlay/alerts`, and `/hubs/overlay/member` exist for OBS/browser clients.
- [ ] Management hub can receive all `IStreamEvent` categories, including `PlatformConnectionChangedEvent`.
- [ ] Overlay hub DTOs remain the Phase 4 public payload DTOs, not domain entities.

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
- [ ] Alert-worthy events reach `/hubs/overlay/alerts`.
- [ ] Member hub is connected as an MVP skeleton but does not invent unsupported events.
- [ ] SignalR JSON key-set tests exactly match overlay DTO public contracts.
- [ ] Before implementation, re-evaluate synthetic `eventId`: platform-provided ids identify the same event across clients; fallback ULIDs only guarantee local single-instance delivery ids.

**Verification:**
- [ ] SC-5 integration test publishes a mock/simulated chat event and observes the overlay hub payload within 5 seconds.
- [ ] Exact JSON key-set tests cover chat, alert, and member payloads over SignalR serialization.

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

**Verification:**
- [ ] Unit tests cover first available pair, partial-pair occupation, all-pairs exhaustion, and no throw in allocator.
- [ ] Integration/socket tests verify IPv4/IPv6 loopback binds and non-loopback rejection.

**Likely Files:**
- `src/Hosts/Vulperonex.Web/Infrastructure/PortPairAllocator.cs`
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
- [ ] 4xx/5xx responses write the response `error` code to stderr and exit non-zero.
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

**Verification:**
- [ ] CLI simulate chat fixture rule triggers mock sender through the API path.
- [ ] Manual test: CLI simulate chat reaches the overlay SignalR client.
- [ ] Integration test proves `ASPNETCORE_ENVIRONMENT=Development` plus `appsettings.Development.json` does not override `Database:Path`.

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
- [ ] Phase 4 SC-6a/SC-6b equivalence is strengthened with follow/sub/donate payloads and assertions on cache state, member state, `TotalBitsGiven`, and subscriber tier.

**Verification:**
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] Manual overlay SignalR check recorded in the phase todo.
- [ ] Git staged set is task-scoped and excludes unrelated untracked files.

**Likely Files:**
- `docs/phases/phase-5-web-signalr-cli/todo.md`
- `tasks/todo.md`
- Phase 5 source/test files from prior slices

**Size:** S
