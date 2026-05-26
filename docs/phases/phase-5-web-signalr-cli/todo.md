# Phase 5 Todo List - Web Host + SignalR + CLI

> Plan: `docs/phases/phase-5-web-signalr-cli/plan.md`
> Parent Checklist: `tasks/todo.md`

---

## Task 14a - Minimal API WorkflowRule CRUD + EventTypes

- [x] Task 14a-0: Implement Web host assembly, `JsonSerializerDefaults.Web`, endpoint extension method registrations, OpenAPI JSON, central error code constants/status mapping, raw `Database:Path` reading architectural tests, and API integration test infrastructure.
- [x] Task 14a-1: Align WorkflowRule persistence, query DTOs, and CQRS read/write paths.
- [x] Task 14a-2: Implement WorkflowRule validation and machine-readable error codes.
- [x] Task 14a-3: Implement WorkflowRule CRUD/enable/disable/delete endpoints, with CQRS interaction tests.
- [x] Task 14a-4: Implement `GET /api/event-types`, including `isSimulatable` from the shared simulation alias registry.

## Task 14b - Minimal API Simulation / Config / Members

- [x] Task 14b-1: Implement `POST /api/simulate/{alias}` strictly for `chat`, `follow`, and `sub`, with fixed chat/follow/sub request body schemas.
- [x] Task 14b-2: Implement `GET|PUT /api/config/{key}` with `security.*` and `oauth.*` prefix reject lists before registry lookup.
- [x] Task 14b-3: Implement member list/display query endpoints via `IMemberQueryService`; Phase 5 currently uses `MemberId ASC` for stable sorting.

## Task 15 - SignalR Hub + Overlay Push + Dual-Port Kestrel

- [x] Task 15a: Implement SignalR admin/overlay hub contracts and host registrations.
- [x] Task 15b: Record synthetic `eventId` semantics, implement overlay event forwarding, explicit alert event sets, exact SignalR JSON key set tests, and SC-5 latency measurements.
- [x] Task 15c: Implement dual-port loopback Kestrel bindings, `IPortAvailabilityProbe.IsAvailable(int port)`, port pair allocations, and exhaustion behaviors.

## Task 16 - CLI

- [x] Task 16a: Implement CLI HTTP infrastructure, `VULPERONEX_API_URL` prioritized base URL resolution, stdout/stderr rules, and backend error passthroughs.
- [x] Task 16b: Implement CLI `rule list|show|enable|disable|delete`.
- [x] Task 16c: Implement CLI `config get|set` and `member list|show`.
- [x] Task 16d: Implement CLI `simulate chat|follow|sub` and shared database path override protections.
- [x] Task 16e: Complete Phase 5 checkpoint review and manual overlay delivery.
- [x] Task 16f: Complete CLI end-to-end manual test gates: automatically apply SQLite migrations on Web API launch, connect real CLI processes to local APIs/DBs, and provide Twitch OAuth PKCE authorization entry points.

## Task 16f - CLI E2E / Twitch OAuth Wrap-up

- [x] Web host executes `DatabaseBootstrapper.MigrateAsync()` on startup; new environment first-runs of `rule list` do not return 500 due to a missing `WorkflowRules` table.
- [x] Web startup project supports `dotnet ef database update --project src\Vulperonex.Infrastructure --startup-project src\Hosts\Vulperonex.Web`.
- [x] Automated tests cover: "un-migrated temp SQLite DB startup returns 200 on `GET /api/rules`".
- [x] Automated tests cover: real Web host + `VulperonexCli.RunAsync` smoke paths to local APIs for rule/config/member/simulate.
- [x] CLI provides `twitch auth start` or equivalent commands, using loopback-only OAuth callback, PKCE `state`/`code_verifier`, and hands refresh tokens to the API for encrypted storage via `IOAuthTokenStore`.
- [x] Twitch OAuth authorization flows do not read/write tokens via `/api/config/oauth.*`; `oauth.*` protected namespace rules remain unchanged.
- [x] Manual validation commands and expected results are recorded in `cli-e2e-verification.md`, including build, API launch, CLI smoke, Twitch authorization pre-configurations, and callback URIs.
- [x] Local terminal runs published CLI (`artifacts/cli-manual/Vulperonex.Cli.exe`) smoke tests against an independent Web API process; Codex sandbox rejects background Web host launches, requiring manual execution.

## Task 16g - CLI Interactive REPL

- [x] Extract CLI command tree: `IConsoleCommand`, `ICommandDispatcher`, `CompositeConsoleCommand`, `CliExecutionContext`, sharing dispatch between one-shot and REPL.
- [x] Preserve existing one-shot `rule/config/member/simulate/twitch` behaviors, adding `help` / `?` built-in commands.
- [x] Add `GET /api/twitch/auth/status`, returning strictly `clientIdConfigured` / `hasRefreshToken` booleans, not returning client IDs or tokens.
- [x] Add minimal REPL entry points: no arguments, `--interactive`, `-i` reading stdin for line-by-line dispatch, supporting `exit` / `quit` / EOF.
- [x] API errors in REPL write strictly to stderr error codes, continuing the session.
- [x] REPL startup banner: prompt ClientId/OAuth status according to `/api/twitch/auth/status`, not generating authorize URLs; continue in no-Twitch mode if `Twitch:ClientId` is missing.
- [x] Re-verify state before executing `twitch auth start` in REPL; do not call `/api/twitch/auth/start` when `clientIdConfigured == false`, prompting `TWITCH_CLIENT_ID_MISSING` and setup instructions directly.
- [x] REPL `twitch auth start` supports Ctrl+C cancellation, outputting `TWITCH_OAUTH_CANCELLED`.
- [x] Line editor: TTY mode Tab prefix completion, Backspace; redirected stdin bypasses line editor.
- [x] Line editor: TTY mode history, Ctrl+C buffer clearing.
- [x] CLI help UX: global help lists commands by category; composite commands without arguments display partial sub-command usage.
- [x] CLI i18n: help/usage/description loaded from `Resources/I18n/manifest.json` and `<culture>.json`, supporting external additions of locales.
- [x] CLI manual-test UX: `simulate` without arguments displays partial help; `rule create/update` supports JSON files; `member seed/delete` supports seeding and clearing test members; `twitch auth reset` supports resetting OAuth refresh tokens before re-running authorization.
- [x] Manually validate REPL: Windows Terminal / PowerShell one-shot, REPL, OAuth, exit/EOF behaviors.

## Phase 5 Dependencies

- [x] Task 13f: Strengthen Phase 4 SC-6a/SC-6b equivalence, including follow/sub/donation payloads, or explicitly exempt before Phase 5 closing.

## Checkpoint 5

- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [x] SC-2, SC-5, SC-8, and SC-9 pass.
- [x] WorkflowRule CRUD and circular reference detection pass end-to-end.
- [x] `GET /api/event-types` excludes `platform.connection_changed`, and `isSimulatable` returns true only for `chat`, `follow`, `sub`.
- [x] Phase 5 error codes are centrally managed and covered by HTTP status mapping tables.
- [x] `UNKNOWN_*` and `INVALID_*` error code naming conventions are recorded and followed.
- [x] Configuration protected namespace checks pass: `security.*` -> `CONFIG_KEY_SECURITY_NAMESPACE`; `oauth.*` -> `OAUTH_CREDENTIAL_NAMESPACE`.
- [x] Unknown protected configuration keys (e.g. `oauth.unknown.refresh_token`) return `OAUTH_CREDENTIAL_NAMESPACE`, confirming prefix reject lists take precedence over registry lookup.
- [x] Member endpoints cover list/display, pagination limits, `INVALID_QUERY_PARAM`, and `MEMBER_NOT_FOUND`.
- [x] SignalR overlay chat receives events within 5 seconds to conform to SC-5, and local latency measurements expose <1s P95 targets.
- [x] SignalR serialization exact key set tests cover chat, alert, and member overlay DTOs.
- [x] SignalR key set tests deserialize wire payloads, not relying on reflection-only DTO checks.
- [x] Synthetic `eventId` decisions are recorded in `docs/phases/phase-5-web-signalr-cli/event-id-decision.md`, completed before overlay forwarding implementation.
- [x] `event-id-decision.md` review notes (reviewer/date/decision) are filled out before starting Task 15b.
- [x] Two Kestrel ports bind strictly to IPv4 loopback `127.0.0.1` and IPv6 loopback `::1`; non-loopback socket tests are rejected.
- [x] Port pair allocations attempt `5000/5001` through `5008/5009`, returning null on exhaustion, which then throws a `PortExhaustedException` from the host.
- [x] CLI rule/config/member/simulate commands pass integration tests.
- [x] CLI 4xx/5xx handling writes strictly backend `error` codes to stderr, exiting with exit code `1`.
- [x] CLI simulated chat rules trigger fake senders via HTTP API paths.
- [x] Manual check: CLI simulated chat reaches overlay SignalR; results are recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`.
- [x] CLI end-to-end smoke: new DB startup does not require manual migrations, and real CLIs successfully execute `rule list`, `config get`, `member list`, `simulate chat|follow|sub`.
- [x] CLI Twitch OAuth smoke: `twitch auth start` generates Twitch authorize URLs, loopback callbacks receive codes, API exchanges tokens, and refresh tokens are encrypted.
- [x] CLI OAuth reset: `twitch auth reset` clears only refresh tokens, allowing `twitch auth start` to repeat manual verifications.
- [x] Empty successful CLI responses feature explicit output: `simulate`, `rule enable/disable/delete`, `member seed/delete`, `twitch auth reset` all print `OK ...` instead of remaining silent in manual tests.
- [x] Web host registers member event consumers; members seeded via `member seed` route through simulation pipelines and appear in `member list`.
- [x] Simulate API returns traceable acks: `accepted`, `eventTypeKey`, `eventId`, `platformUserId`, `displayName`, allowing CLIs to directly see published events on the Web API.
- [x] `ASPNETCORE_ENVIRONMENT=Development` along with `appsettings.Development.json` and environment variables do not override `Database:Path` in the main `appsettings.json`.
- [x] Architectural tests prove raw `Configuration["Database:Path"]` reading is restricted strictly to `IDatabasePathResolver`.
- [x] Task 13f Phase 4 SC-6a/SC-6b follow-ups are completed or explicitly exempted.
- [x] Git staging sets are limited to task scopes; unrelated untracked files are excluded.
