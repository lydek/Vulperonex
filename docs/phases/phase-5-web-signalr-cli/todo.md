# Phase 5 Todo - Web Host + SignalR + CLI

> Plan: `docs/phases/phase-5-web-signalr-cli/plan.md`
> Parent checklist: `tasks/todo.md`

---

## Task 14a - Minimal API WorkflowRule CRUD + EventTypes

- [ ] Task 14a-0: implement Web host composition and API integration-test harness.
- [ ] Task 14a-1: align WorkflowRule persistence, query DTOs, and CQRS read/write paths.
- [ ] Task 14a-2: implement WorkflowRule validation and machine-readable error codes.
- [ ] Task 14a-3: implement WorkflowRule CRUD/enable/disable/delete endpoints with CQRS interaction tests.
- [ ] Task 14a-4: implement `GET /api/event-types` with static `isSimulatable` alias mapping.

## Task 14b - Minimal API Simulate / Config / Member

- [ ] Task 14b-1: implement `POST /api/simulate/{alias}` for `chat`, `follow`, and `sub` only.
- [ ] Task 14b-2: implement `GET|PUT /api/config/{key}` with `security.*` and `oauth.*` prefix denylist before registry lookup.
- [ ] Task 14b-3: implement member list/show query endpoints through `IMemberQueryService`.

## Task 15 - SignalR Hub + Overlay Push + Dual-Port Kestrel

- [ ] Task 15a: implement SignalR management/overlay hub contracts and host registration.
- [ ] Task 15b: implement overlay event forwarding, exact SignalR JSON key-set tests, and SC-5.
- [ ] Task 15c: implement dual-port loopback Kestrel binding, port-pair allocation, and exhaustion behavior.

## Task 16 - CLI

- [ ] Task 16a: implement CLI HTTP foundation, stdout/stderr rules, and backend error passthrough.
- [ ] Task 16b: implement CLI `rule list|show|enable|disable|delete`.
- [ ] Task 16c: implement CLI `config get|set` and `member list|show`.
- [ ] Task 16d: implement CLI `simulate chat|follow|sub` and shared DB path override guard.
- [ ] Task 16e: complete Phase 5 checkpoint review and manual overlay handoff.

## Checkpoint 5

- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] SC-2, SC-5, SC-8, and SC-9 pass.
- [ ] WorkflowRule CRUD and circular reference detection pass end to end.
- [ ] `GET /api/event-types` excludes `platform.connection_changed` and reports `isSimulatable` only for `chat`, `follow`, and `sub`.
- [ ] Config protected namespace checks pass: `security.*` -> `CONFIG_KEY_SECURITY_NAMESPACE`; `oauth.*` -> `OAUTH_CREDENTIAL_NAMESPACE`.
- [ ] Unknown protected config key such as `oauth.unknown.refresh_token` returns `OAUTH_CREDENTIAL_NAMESPACE`, proving prefix denylist precedes registry lookup.
- [ ] Member endpoints cover list/show, paging limits, `INVALID_QUERY_PARAM`, and `MEMBER_NOT_FOUND`.
- [ ] SignalR overlay chat receives an event within 5 seconds for SC-5.
- [ ] SignalR serialization exact key-set tests cover chat, alert, and member overlay DTOs.
- [ ] Two Kestrel ports bind to IPv4 loopback `127.0.0.1` and IPv6 loopback `::1`; non-loopback socket test is rejected.
- [ ] Port-pair allocation tries `5000/5001` through `5008/5009` and returns null on exhaustion before the host throws `PortExhaustedException`.
- [ ] CLI rule/config/member/simulate commands pass integration tests.
- [ ] CLI 4xx/5xx handling writes only backend `error` code to stderr and exits non-zero.
- [ ] CLI simulate chat fixture rule triggers mock sender through the HTTP API path.
- [ ] Manual check: CLI simulate chat reaches overlay SignalR.
- [ ] `ASPNETCORE_ENVIRONMENT=Development` plus `appsettings.Development.json` does not override main `appsettings.json` `Database:Path`.
- [ ] Phase 4 SC-6a/SC-6b equivalence is strengthened with follow/sub/donate payloads and assertions for cache state, member state, `TotalBitsGiven`, and subscriber tier.
- [ ] Before Task 15 implementation, synthetic `eventId` semantics are re-evaluated: platform ids can correlate across clients; fallback ULIDs are local single-instance delivery ids only.
- [ ] Git staged set is task-scoped; unrelated untracked files remain uncommitted.
