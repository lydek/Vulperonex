# Vulperonex MVP — Todo List

> Detailed descriptions can be found in tasks/plan.md
> Updated: 2026-05-13

---

## Global Execution Rules

- [ ] Define BDD Given / When / Then scenarios for every behavior first.
- [ ] Implement each scenario following the TDD flow: RED → GREEN → REFACTOR.
- [ ] Every automated test name must comply with `Given_*_When_*_Then_*` (C#) or `should * when *` (Vitest), or contain a `// Given / When / Then` block (verified by Checkpoint code reviews).
- [ ] Domain implementation complies with tactical DDD: Entities, Value Objects, Domain Events, and invariants reside in correct boundaries.
- [ ] Application boundaries comply with light CQRS: command/write ports are separated from query/read DTO services.
- [ ] DCI Role/Behavior (SPEC §4.1b): Role objects contain pure Domain logic without Infrastructure dependencies; Context/Interaction resides in Application; MVP does not implement runtime roles, reflection, or mixins; architecture tests verify Role contains no Infrastructure references.
- [ ] Manual verification only supplements Photino / OBS / browser runtime and does not replace automated tests.

---

## Phase 1: Solution Skeleton + Domain Foundation

> Detailed Slice Todo: `docs/phases/phase-1-foundation/todo.md`

- [x] **Task 1** — Create the .NET Solution structure and all csproj skeletons (including `Vulperonex.Adapters.Abstractions`, `Vulperonex.Adapters.Twitch`, `Vulperonex.Adapters.Simulation`).
- [x] **Task 2** — Domain: `IStreamEvent` interface, 7 MVP event records + `PlatformConnectionChangedEvent`, `StreamUser` value object, `StreamEventKeys` constants (including the `platform.connection_changed` constant).
- [x] **Task 3** — Domain: `MemberRecord`, `PlatformIdentity`, `LoyaltyInfo` (Entity/VO/invariant); **Application ports**: `IMemberRepository` (write), `IMemberQueryService` (read) — ports are strictly in the Application layer, not Domain.

### ✅ Checkpoint 1
- [x] `dotnet build` compiles clean.
- [x] Architecture tests: Domain has no Infrastructure / Platform references.
- [x] Domain unit test coverage > 90%.

---

## Phase 2: Event Bus + Infrastructure

> Detailed Slice Todo: `docs/phases/phase-2-infrastructure/todo.md`

- [x] **Task 4** — `IStreamEventBus` + `InMemoryStreamEventBus` (Channel, handler isolation, and `WaitForIdleAsync`).
- [x] **Task 5** — EF Core + SQLite + first database migrations (including `MigrationClassifier` architecture tests).
- [x] **Task 6** — TDQ overflow handling + `ActionExecutionLog` deduplication (at-least-once guarantee).
- [x] **Task 7** — `MemberResolver` (INSERT OR IGNORE atomic GetOrCreate, **Infrastructure-only implementation**; Application only references `IMemberResolver` port) + `PlatformUserDisplayCache` (L1/L2, Infrastructure-only, Application/Domain do not reference).
- [x] **Task 8** — `ISystemSettingsService` (SQLite-backed, hot-reload `IObservable`, AES-256-GCM encrypted OAuth tokens + `IOAuthTokenStore` + `SystemSettingKey` constants).

### ✅ Checkpoint 2
- [x] `dotnet test` passes.
- [x] End-to-end integration: publish event → bus → handler.
- [x] `MemberResolver` concurrent resolution tests pass.
- [x] Task 5: `PRAGMA auto_vacuum` = 2 (FULL) bootstrap assertion passes.
- [x] Task 7: `IPlatformUserInfoCache.UpdateAsync` cache miss → default row (`Badges` empty) passes.
- [x] Task 8: AES-256-GCM AAD cross-key copy → `CredentialDecryptionException` passes.

---

## Phase 3: Simulation Adapter + WorkflowEngine

> Detailed Slice Todo: `docs/phases/phase-3-workflow/todo.md`

- [x] **Task 9** — `SimulationAdapter` + `IStreamEventTypeRegistry` (SC-3, SC-4).
- [x] **Task 10** — `WorkflowEngine`: condition evaluation, Serial/Parallel actions, and ErrorBehavior/Timeout (SC-2, SC-9).
- [x] **Task 11** — Plugin System: `IVulperonexPlugin` interface, `InvokePluginAction` executor (SC-10).

### ✅ Checkpoint 3
- [x] SC-2, SC-3, SC-4, SC-9, SC-10 pass.
- [x] End-to-end integration flow: SimulationAdapter → Bus → WorkflowEngine → IPlatformChatSender mock.

---

## Phase 4: Twitch Adapter + MemberModule

> Detailed Slice Todo: `docs/phases/phase-4-twitch-member/todo.md`

- [x] **Task 12** — `TwitchAdapter`: IRC + EventSub + DisplayHints + exponential backoff reconnection (SC-1, SC-6a WorkflowEngine half).
- [x] **Task 13** — `MemberModule` + `OverlayModule` DTO safety filtering (SC-8).

### ✅ Checkpoint 4
- [x] SC-1, SC-6a (Task 12) + SC-6b (Task 13), SC-8 pass.
- [x] Overlay DTO whitelists strictly enforced (chat/alert contains platform-provided delivery ID/timestamps; member is snapshot without event metadata).

---

## Phase 5: Web Host + SignalR + CLI

> Detailed Slice Todo: `docs/phases/phase-5-web-signalr-cli/todo.md`

- [x] **Task 14a** — Minimal API: WorkflowRule CRUD + EventTypes endpoint + i18n error codes + circular sub-workflow detection + Action schema validation (unknown types / missing parameters / invalid config) + CQRS architecture tests.
- [x] **Task 14b** — Minimal API: Simulate / Config / Member endpoints + `security.*` / `oauth.*` protected namespace blocking.
- [x] **Task 15** — SignalR Hub + Overlay Push + Dual-port Kestrel + automatic port pair increments (SC-5).
- [x] **Task 16** — CLI: simulate / config / member / rule commands (via HTTP REST APIs, loopback-only, no authentication required).

### ✅ Checkpoint 5
- [x] SC-2, SC-5, SC-8, SC-9 pass.
- [x] Rules CRUD and circular reference validation work.
- [x] `security.*` config key blocking (GET + PUT) verified.
- [x] `oauth.*` config key blocking (GET + PUT → 403 + `OAUTH_CREDENTIAL_NAMESPACE`) + CLI passthrough to stderr verified.
- [x] Phase 5 error codes centralized and HTTP status mapping table covered.
- [x] CLI rule / config / member / simulate E2E integration tests pass.
- [x] CLI simulate chat fixture rule + mock sender verification passes.
- [x] Task 13f Phase 4 SC-6a/SC-6b equivalence enhancements completed: follow/sub/donate payloads added, cache/member state/TotalBitsGiven/subscriber tier side effects verified.
- [x] CLI simulate → Overlay SignalR E2E manual smoke tests complete, results recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`.
- [x] Phase 5 CLI E2E: automatic SQLite migrations verified; published CLI verified against independent Web API process.
- [x] Phase 5 Twitch OAuth CLI: CLI PKCE authorization entry, loopback-only callbacks, and encrypted refresh token saves verified.
- [x] Phase 5 CLI REPL: command trees, help, status APIs, Ctrl+C cancellation, TTY line editor, and Tab auto-completion verified.
- [x] CLI helper shortcut script `tools/cli.ps1` added.
- [x] Task 15: dual-port loopback bindings (IPv4 127.0.0.1 + IPv6 ::1) verified.
- [x] Task 14b: `GET/PUT /api/config/oauth.twitch.refresh_token` → 403 + `OAUTH_CREDENTIAL_NAMESPACE` verified.
- [x] Task 14b: `GET /api/config/oauth.unknown.refresh_token` (unknown key) → 403 + `OAUTH_CREDENTIAL_NAMESPACE` verified.
- [x] Task 16: `ASPNETCORE_ENVIRONMENT=Development` + `appsettings.Development.json` settings do not override `Database:Path` verified.

---

## Phase 5.5: Rapid-Test Enablement

> Detailed Slice Todo: `docs/phases/phase-5_5-rapid-test/todo.md`

- [x] **Task 17a** — CLI rule create / update.
- [x] **Task 17b** — `chat.html` overlay porting and architecture tests.
- [x] **Task 17c** — E2E fixture chat → workflow → overlay integration tests (ChatReplyChainTests).
- [x] **Task 17d** — Cookbook documentation and PASS verification.
- [x] **Task 17e** — CLI ID resolution + missing argument UX + destructive operation confirmations.

### ✅ Checkpoint 5.5
- [x] Zero-warning compilations and all tests green.
- [x] CLI `rule create/update` with 4xx passthrough green.
- [x] CLI missing argument / prefix / `--name` / `--yes` workflows green.
- [x] `chat.html` payload key architecture tests green.
- [x] Cookbook AI Agent + physical verification PASS.

---

## Phase 6: Logs + Frontend + Photino

> Detailed Slice Todo: `docs/phases/phase-6-web-ui/todo.md`

- ~~**Task 17**~~ — Removed (MockYouTube Adapter deferred).
- [x] **Task 19** — Vue Frontend Skeleton: Vite 7.3 + PrimeVue 4 Unstyled + UnoCSS + Pinia + useStreamEvents + dual-language base and manifest.
- [x] **Task 20** — Web Admin UI: dashboard status, simulate panel, event monitor, read-only members, JSON Textarea Rule CRUD, Twitch OAuth auth start/reset, and `zh-TW` / `en-US` i18n support.
- [x] **Task 18** — Serilog Three Sinks + AppLogs Cleanup Worker (`log.db_retention_days` + `log.db_max_size_mb` size-based cleanup) + hot-reload log levels.
- [x] **Task 21** — Photino Desktop Shell + port conflict resolution + static fallback pages.

### ✅ Checkpoint 6
- [x] `dotnet test` → All active SC tests pass (SC-1~SC-6, SC-8~SC-10; SC-7 removed).
- [x] `pnpm test` → Frontend tests pass.
- [x] `pnpm lint` → Frontend lint passes.
- [x] `pnpm build` → wwwroot builds successfully.
- [x] All Task 18-21 sub-tasks checked.
- [x] Manual verification: all manual verification checklists under `docs/phases/phase-6-web-ui/manual-verification.md` PASS.
- [x] Security review: Overlay DTO whitelists, loopback dual-port bindings, AES-256-GCM encryption with AAD binding, machine.key permissions, and OAuth callback CSRF state validation verified.

---

## Phase 7: Workflow Parity with Omni-Commander

> Detailed Slice Todo: `docs/phases/phase-7-workflow-parity/todo.md`

- [x] **Task 23** — Variable / Expression substrate: `ExpressionContext` + template resolver + NCalc evaluator.
- [x] **Task 24** — Step `ExecutionCondition` + `OutputVariable`.
- [x] **Task 25** — Rule-level throttle + timeout.
- [x] **Task 26** — `OnFailureActionsJson` rescue chain + replay phase key.
- [x] **Task 28** — Hot reload immutable rule snapshot cache.
- [x] **Task 29** — Trigger filter + `MatchCondition`.
- [x] **Task 27** — Sub-workflow flag + Args plumbing, maintaining stable `InvocationId`.
- [x] **Task 30** — Executor expansion (strongly-typed DTOs and whitelists).
- [x] **Task 32** — `ChatOutboxService` rate limit + observable skipped/failed state.
- [x] **Task 31** — `WorkflowTimer` scheduler (single-instance restart idempotency).
- [x] **Task 33** — Web UI builder upgrade for Phase 7 schema.
- [x] **Task 34** — Plugin Action Args surface.
- [x] **Task 35** — Manual verification + Omni parity sign-off.

### Checkpoint 7
- [x] All sub-tasks for Tasks 23-35 completed.
- [x] Web build and backend builds/tests pass.
- [x] Manual verification recorded in `docs/phases/phase-7-workflow-parity/manual-verification.md`.

---

## Phase 7A: Workflow Editor UX Alignment with Omni-Commander

> Detailed Slice Todo: `docs/phases/phase-7a-workflow-editor-ux/todo.md`

- [x] **Task 36** — Workflow editor baseline repair: trigger filter addition workflows, editor regression tests.
- [x] **Task 37** — Visual builder: Conditions / Actions / OnFailure visually editable via forms.
- [x] **Task 38** — Variable picker: context-aware variables insertion.
- [x] **Task 39** — JSON fallback demotion: JSON editor as advanced fallback / import-export surface.
- [x] **Task 40** — Omni parity verification: editor UX checklist and alignment.

### Checkpoint: Phase 7A
- [x] All sub-tasks for Tasks 36-40 checked.
- [x] Manual verification recorded in `docs/phases/phase-7a-workflow-editor-ux/manual-verification.md`.

---

## Success Criteria Mapping

| SC | Task |
|----|------|
| SC-1: 7 MVP Events | Task 12 |
| SC-2: WorkflowEngine executing rule | Task 10 |
| SC-3: SimulationAdapter no Twitch refs | Task 9 |
| SC-4: Domain no Twitch symbols | Task 2 (Continuous) |
| SC-5: Overlay SignalR push in 5s | Task 15 |
| SC-6a: Simulation ≡ Twitch (WorkflowEngine half) | Task 12 |
| SC-6b: Simulation ≡ Twitch (MemberRecord DB state) | Task 13 |
| SC-7: Removed | — |
| SC-8: ULID MemberRecord creation | Task 13 |
| SC-9: SendChatMessage platform routing | Task 10 |
| SC-10: Plugin action triggering | Task 11 |

---

## Phase 7B: Chat Output Observability and Overlay Template Presets

> Detailed Slice Todo: `docs/phases/phase-7b-chat-overlay-presets/todo.md`

- [x] **Task 41** — Simulation chat output observable surface: verify `SendChatMessage` details in simulation mode.
- [x] **Task 42** — Chat overlay preset system: switch templates via UI.
- [x] **Task 43** — OneComme compatibility path: extension/plugin integration.

### Checkpoint: Phase 7B
- [x] All sub-tasks for Tasks 41-43 completed.
- [x] Verification recorded in `docs/phases/phase-7b-chat-overlay-presets/manual-verification.md`.

---

## Phase 7C: Member Card Overlay, Custom HTML Extension, Member-in-Chat

> Detailed Slice Todo: `docs/phases/phase-7c-member-overlay-extension/todo.md`

- [/] **Task 44** — Member Card Overlay Default Preset (Rotan-Checkin).
- [/] **Task 45** — Member Card Admin Controller: settings UI, i18n, sanitization.
- [ ] **Task 46** — Custom HTML Overlay Upload: zip path traversal checks, size caps.
- [ ] **Task 47** — Overlay Preset Resolver: settings-driven 302 redirects.
- [ ] **Task 48** — Member Snapshot in Chat Hub: DTO whitelist + chip rendering.
- [ ] **Task 49** — OneComme Bridge Plugin Contract (Scaffold Only).

### Checkpoint: Phase 7C
- [ ] All sub-tasks for Tasks 44-49 met.
- [ ] Verification recorded in `docs/phases/phase-7c-member-overlay-extension/manual-verification.md`.

---

## Phase 7D: CheckIn→Member Binding, Custom HTML Editor, Unified Monitor Page, Member Editable Surface

> Detailed Slice Todo: `docs/phases/phase-7d-checkin-binding-editor-monitor-member/todo.md`

- [x] Tasks 50-54 — Track A: CheckIn → MemberOverlay Binding.
- [x] Tasks 55-60 — Track B: Custom HTML Editor.
- [/] Tasks 61-64 — Track C: Unified Monitor Page.
- [x] Tasks 65-68 — Track D: Member Editable Surface.
- [x] Tasks 69-72 — Track E: Module & Plugin Management System.
- [x] Tasks 73-75 — Track F: Extended Event & Loyalty Simulation.
- [x] Tasks 76-78 — Track G: Visual Workflow Rules UI.

### Checkpoint: Phase 7D
- [ ] All sub-tasks for Tasks 50-78 completed with `[x]` self-check — Task 61e and Browser manual matrix pending.
- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` — 2026-05-26 EXIT=0
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` — 2026-05-26 453/453 EXIT=0
- [x] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint` — 2026-05-26 vue-tsc EXIT=0, vitest 191/191, build EXIT=0; lint residue in vendor `public/overlay/libs/vue.global.js`, not introduced in this phase.
- [ ] Browser manual checks all PASS (defined in `plan.md` Checkpoint area).
- [ ] Security review all PASS — security checklist needs evidence synchronized in `manual-verification.md`.
- [x] `manual-verification.md` records dated entries and evidence commits — 2026-05-26 dated entries recorded (backend regression fix + monitor redesign).
