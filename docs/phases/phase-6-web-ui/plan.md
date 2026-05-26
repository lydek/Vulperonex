# Phase 6 Implementation Plan: Web UI + Logging + Desktop Shell

> Parent Plan: `tasks/plan.md`
> Parent Checklist: `tasks/todo.md`
> Scope: Tasks 18-22
> Precondition: Phase 5 CLI / Web API / SignalR / Twitch OAuth manual verification completed and recorded in `docs/phases/phase-5-web-signalr-cli/manual-verification.md`
> [!IMPORTANT]
> **Precondition Gate**: The three manual verification checkpoints in the Parent Plan's Phase 5 (including CLI E2E wrap-up, Twitch OAuth real browser authorization, and REPL manual checks) must be marked as completed before Phase 6 implementation can begin.
> **Current Status**: Completed Web UI/rule JSON editor/overlay history inside Phase 6 serves as a baseline for Phase 7; incomplete non-workflow parity items like Photino/manual verification are deferred until after Phase 7 convergence.
> **⚠ OAuth Gate Note**: "Real browser authorization" must include full code exchange and encrypted refresh_token storage, not just launching the browser via `auth start`. If `manual-verification.md` in Phase 5 only records launching the authorization URL, this Gate cannot pass until full OAuth round-trip verification is completed in a valid `Twitch:ClientId` environment.

---

## Estimation and Onboarding Instructions (II18, II21)

- **Phase 5.5 Convergence and CLI Integration Cleanup**: **0.5 Days** (II21) (Focus on CLI id resolution isolation, dirty diff git cleanup, and completing Phase 5 real OAuth round-trip authorization test logs, ensuring a clean master branch and complete audit trail).
- **Phase 6 Implementation Schedule**: **7 Days** (II18)
  - Task 19: Frontend Foundation and SignalR/Polling Integration: **2 Days**
  - Task 20: Five Admin Panels (including 1MB JSON paste protection, LWW deduplication, and exponential backoff): **3 Days**
  - Task 18: Serilog Three Sinks and AppLogs Cleanup Mechanism: **1 Day**
  - Task 22: Overlay Event History Persistence + Cleanup Surface (reusing SystemSettings + Omni-Commander patterns): **0.5 Days**
  - Task 21: Photino Desktop Shell (including Single Instance detection, IPC Bridge, and crash restart limits): **1 Day**
- **Onboarding Instructions**: Developers must execute `corepack enable` to pin pnpm@9.15.4 in `src/frontend` before starting; if Windows permissions block creating global pnpm shims, use `corepack pnpm@9.15.4 <command>` for all frontend commands instead. When launching Vite dev server via `pnpm dev` or `corepack pnpm@9.15.4 dev` for smoke tests, the appearance of `VITE ... ready in` in stdout indicates success, allowing manual Ctrl+C to proceed to subsequent steps.

---

## Goals

Phase 6 ports the loopback Web API capabilities verified by the Phase 5 CLI into Vue Web UI, forming a long-running local control panel. The first view must be a functional admin interface, not a landing page: users can verify API/Twitch status, simulate events, view members, manage WorkflowRules, monitor SignalR events, and finally package it via Photino Desktop Shell as a desktop entry point.

---

## Design Principles

- The Web UI is a visual extension of CLI manual smoke tests; do not reinvent backend workflows or read/write SQLite directly.
- Frontend error handling displays backend errors strictly via error codes, translated by vue-i18n; the backend continues to emit machine-readable errors.
- The API base URL does not hardcode local ports: browsers call same-origin APIs via relative paths, with Vite dev override support via `VITE_API_URL`.
- The UI follows a functional tool aesthetic: high information density, clean, stable, avoiding marketing hero elements or decorative visuals.
- Rule editing first provides a reliable CLI-equivalent JSON path and basic forms; full visual builders can be expanded in subsequent slices.
- Overlay routes are separated from administrative routes; overlay routes only handle displays, not administrative controls.
- Frontend i18n files adopt the same externally extensible approach as CLI i18n: locale lists correspond to locale filenames, displaying keys on missing strings to avoid UI crashes.
- Follow the ask-first protocol before adding new npm/NuGet dependencies; existing tools can be used directly for verification.

---

## Dependency Graph

```text
Task 18 Serilog/AppLogs
    -> Dashboard log/status widgets can consume later

Task 19 Frontend foundation
    -> Task 20 Management UI
    -> Task 21 Desktop Shell

Task 20 Management UI
    -> Rule/member/simulate/Twitch flows verified in browser

Task 21 Desktop Shell
    -> Ships Web UI through local desktop entry
```

Task 18 can be implemented separately from Task 19, but Phase 6 Web UI tasks should complete Task 19 first, otherwise Task 20/21 will lack shared frontend foundations.

---

## Task 18 - Serilog Three Sinks + AppLogs Cleanup Worker

**Description:** Configure Console, Rolling File, and SQLite AppLogs sinks, adding structured field enrichers, and implementing a cleanup worker for `log.db_retention_days` / `log.db_max_size_mb`. `log.min_level` must be hot-reloaded via SystemSettings. The default for `log.db_max_size_mb` is `50` (50MB) (II23, HH30), and the default for `log.db_retention_days` is `30` (30 days).

**Acceptance Criteria:**
- [ ] Console, rolling file, and SQLite AppLogs are all writable, avoiding duplicate configurations of `PRAGMA auto_vacuum` (HH11).
- [ ] AppLogs rows contain structured fields: EventTypeKey, Platform, MemberId, WorkflowRuleId, ActionType.
- [ ] **De-identification and Privacy Compliance (II24, HH29)**: The `MemberId` field strictly logs pseudonymized ULIDs (pseudonymous), with explicit non-PII (Non-PII) comments in code and logs. It is strictly forbidden to log any PII (such as real names, e-mails, or raw platform account IDs).
- [ ] After calling `config set log.min_level Warning`, Debug/Information logs are no longer written, without requiring a restart.
- [ ] Size-based cleanup and retention cleanup are integrated into a single background worker, **whichever is triggered first** (HH18). Size-based cleanup utilizes `PRAGMA page_count * page_size` check, followed by an explicit `VACUUM` upon execution.
- [ ] Both retention cleanup and size cleanup can be triggered for testing individually via `AppLogsCleanupWorker.ExecuteOnce()`, independent of background timing.

**Verification:**
- [ ] **dotnet Integration Test (Backend) (II3)**:
    - `Given_AppLogs_When_PublishedEvent_Then_ContainsPseudonymizedMemberId` verifies log fields, asserting the non-PII nature of the pseudonymous `MemberId` with comments.
    - `Given_LogSettings_When_MinLevelWarning_Then_SuppressDebugAndInfo` verifies hot-reloaded log levels.
    - `Given_AppLogs_When_SizeThresholdExceeded_Then_TriggerCleanupAndVacuum` verifies whichever-triggered-first cleanup policies and database page size reduction after vacuuming.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.

**Dependencies:** Task 5, Task 8

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Web/Logging/`
- `src/Vulperonex.Infrastructure/Logging/`
- `tests/Vulperonex.Tests.Integration/Logging/`

**Size:** M

---

## Task 19 - Vue Frontend Skeleton + SignalR Composable

**Description:** Establish an operational Vite/Vue application in `src/frontend`, containing routing, Pinia, vue-i18n, API clients, SignalR composables, overlay route skeletons, and administrative admin layouts.
- **Task 19a Startup and Lockfile-only Pre-check (II14)**: Before starting this task, developers must execute `corepack enable` in `src/frontend` to ensure pinned pnpm@9.15.4 takes effect; if Windows permissions prevent global pnpm shims, use `corepack pnpm@9.15.4 <command>` instead. Since pnpm 9.15.4's `install` does not support `--dry-run`, the pre-check command is fixed to `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts`, validating version compatibility of Vite 7.3, PrimeVue 4, UnoCSS, and other frontend stacks without executing lifecycle scripts.
- All stack packages (Vue 3.5, Vite 7.3, PrimeVue 4 Unstyled, UnoCSS Preset Wind 4, Pinia, vue-i18n, oxlint, vue-tsc, and all transitive dependencies) require user consent (ask-first protocol) before first installation.
- **Git Scope Auto Gate (II20)**: Configure `simple-git-hooks` or `husky` paired with `commitlint` in `src/frontend`. Establish automated hooks to execute scope checks during the `commit-msg` phase, strictly blocking git commits that do not conform to Conventional Commits formats, providing automated protection from the toolchain level.

**Acceptance Criteria:**
- [ ] `corepack enable` is executed before this task starts, or fallback to `corepack pnpm@9.15.4 <command>` if Windows shim permissions are blocked; and `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts` passes, ensuring no version compatibility conflicts (II14).
- [ ] `src/frontend/package.json` pins `"packageManager": "pnpm@9.15.4"` (`pnpm --version` returns `9.15.4`; do not use wildcards like `9.x.x`), and provides `dev`, `test`, `build`, and `lint` scripts.
- [ ] `package.json` contains `"lint": "oxlint --config oxlint.json"`; `oxlint.json` contains Vue 3 + TypeScript rule sets; `pnpm lint` yields 0 errors (oxlint serves as the designated linter, Eslint is not used).
- [ ] Vite build outputs to `src/Hosts/Vulperonex.Web/wwwroot`, with generated files excluded from commits.
- [ ] API clients support relative base URLs and `VITE_API_URL` overrides.
- [ ] `useStreamEvents` connects to `/hubs/events` (Admin Hub), updating reactive states upon envelope arrival.
- [ ] `/overlay/chat` (connecting to `/hubs/overlay/chat` independent Hub) and `/overlay/alerts` (connecting to `/hubs/overlay/alerts` independent Hub) mount and connect; the frontend consumes DTO contracts defined by the backend, restated as "consumer whitelists" (the consumer does not deconstruct or forward extra fields). The detailed DTO whitelist specifications are cross-referenced to the parent plan's [tasks/plan.md § Task 15 DTO Specifications](file:///d:/code/Vulperonex/tasks/plan.md) to avoid line number change invalidations.
- [ ] `/overlay/member` (connecting to `/hubs/overlay/member` independent Hub) mounts, displaying an MVP skeleton empty state UI (the server does not push events to `/hubs/overlay/member` in MVP; does not crash, proving post-MVP pages do not crash).
- [ ] **useEventStore Setup Store Design (II7, II10)**:
    - Communication between the frontend and the Admin Hub `/hubs/events` is handled by the `useStreamEvents` composable, but all received event envelopes must be forwarded and consolidated into the Pinia Setup Store `useEventStore` (located in `src/frontend/src/stores/eventStore.ts`).
    - **Naming and Unidirectional Data Flow Specifications (II10)**: Pinia stores strictly adopt Setup Store syntax, with naming rules requiring `use[Feature]Store` format (e.g., `useAuthStore`, `useWorkflowStore`, `useMonitorStore`). States must be exposed as read-only (`readonly(state)`) and modified strictly via Store Actions, maintaining unidirectional data flows.
    - **Data Deduplication and Consistency (Last-write-wins) (II7)**: Stores store and maintain events using `eventId` as keys. `useEventStore` serves as the implementation location for the **Last-write-wins reducer**. When both SignalR and HTTP Polling fallbacks deliver identical `eventId`s, compare envelope `occurredAt` timestamps, adopting a `last-write-wins` (LWW) override policy to write the latest occurredAt payload to the store.
    - **Overlay Independence and Decoupling Design**: Overlays (`/overlay/chat`, `/overlay/alerts`) do not share `useEventStore` states. They must directly and individually call their respective `useOverlayHub(hubName)` composables to connect to independent Hubs (`/hubs/overlay/chat` and `/hubs/overlay/alerts`). This ensures overlay security boundaries (consuming only whitelist fields, preventing sensitive admin console data from leaking into overlay pages, guarding against DTO overflows).
- [ ] The admin status dashboard page displays strictly status cards (API health, Twitch auth status, no-Twitch mode), with the Dashboard Log Widget marked as **Defer (Non-MVP)**.
- [ ] UI text is driven by vue-i18n, providing at least `zh-TW` and `en-US` locale files controlled by the manifest file `src/frontend/src/i18n/manifest.json` configured as `{ "locales": ["zh-TW", "en-US"], "default": "zh-TW" }`. Missing strings display keys directly without crashing.
- [ ] XSS Boundary: overlay displays utilize text bindings, avoiding `v-html` rendering for external event contents.

**Verification:**
- [ ] **TypeScript Type Safety Gate**: Before executing `pnpm run build`, `pnpm vue-tsc --noEmit` must pass successfully with 0 errors (Vue SFC requires vue-tsc instead of tsc).
- [ ] **Vitest (Frontend) (II3, II8, II9)**:
    - All unit tests conform to the BDD format `should [behavior] when [scenario]` (II8) (e.g. `should safe-render chat when XSS payloads injected`).
    - Test coverage gates demand (II9): `Branch Coverage ≥ 70%`, `Statement Coverage ≥ 80%`.
    - Unit tests for core composables and stores pass successfully.
- [ ] `cd src/frontend; pnpm test` -> composable and store unit tests pass.
- [ ] `cd src/frontend; pnpm build` -> wwwroot contains index.html + assets.
- [ ] `cd src/frontend; pnpm lint` -> runs `oxlint` syntax checks, frontend lint passes 100% green without errors.
- [ ] **Browser Manual Verification**: Open the Web host home page; the admin layout loads successfully and API status displays correctly.
- [ ] **Browser Manual Verification**: Open `/overlay/chat`, then execute CLI `simulate chat hello from ui smoke`, verifying the event is received on screen.

**Dependencies:** Task 15

**Files Likely Involved:**
- `src/frontend/package.json`
- `src/frontend/vite.config.ts`
- `src/frontend/src/main.ts`
- `src/frontend/src/router/`
- `src/frontend/src/api/`
- `src/frontend/src/composables/useStreamEvents.ts`
- `src/frontend/src/i18n/`
- `src/frontend/src/views/overlay/`
- `src/frontend/src/views/admin/`
- `src/frontend/src/components/admin/`
- `src/frontend/tests/`

**Size:** M

---

## Task 20 - Web Admin UI: simulate / member / rule / Twitch auth

**Description:** Implement core workflows operational within browsers. All paths for frontend console Views and Components in this task are flattened into `src/frontend/src/views/admin/` and `src/frontend/src/components/admin/`, replacing deeply nested directories (BB3, CC1, CC2, CC3). This task allows users to complete key Phase 5 manual verification steps without the CLI: simulating events, verifying member side-effects, creating/enabling/disabling/deleting rules, resetting/starting Twitch OAuth, while implementing comprehensive integrity protections.

**Acceptance Criteria:**
- [ ] The simulation panel supports chat/follow/sub, displaying acknowledgements upon submission: accepted, eventTypeKey, eventId, platformUserId, displayName.
- [ ] The event monitor displays `/hubs/events` envelopes, strictly targeting the schema `{ type, eventId, platform, occurredAt }` (the actual format emitted by the Phase 5 backend, defined in `src/Hosts/Vulperonex.Web/SignalR/OverlayEventForwarder.cs` under `StreamEventEnvelope`). Schema expansions for `schemaVersion` and `data` are deferred to Phase 7 (Non-MVP).
- [ ] The member panel supports strictly read-only list/show operations; **does not provide seed/delete buttons, and does not add member CRUD endpoints**. Test data seeding and cleanup are managed via CLI/manual test surfaces.
- [ ] **Member Read-Only Negative Test Assertion (Z10)**: All member fields (such as names, platform identifiers, etc.) are strictly read-only in the Web UI, preventing editing in the frontend, and seed/delete action entries must not appear.
- [ ] The rule panel supports list/show/create/update/enable/disable/delete.
- [ ] **Optimistic Locking Support (II17)**: The frontend must carry a `version` field in the DTO when updating Rules to support backend optimistic locking checks. When the backend returns 409 Conflict, the frontend must capture this error and pop up a dedicated optimistic locking conflict warning, guiding the user to reload or overwrite.
- [ ] Rule create/update supports JSON file uploads or manual JSON textareas; API validation errors display near fields or in summary areas.
- [ ] **JSON Textarea 1MB Limit Triple Check (II15)**:
    - Implement textarea `maxlength` limits.
    - Debounce paste checks by `300ms`; when the data size exceeds 1MB, reject parsing and display a toast warning.
    - Save pasted raw text in non-reactive variables (such as plain objects or custom plain refs/variables) instead of assigning directly to Vue reactive refs, protecting against main thread lockups and OOM crashes from repeated Vue attribute change detections.
- [ ] Successful rule enable/disable/delete immediately reflects status changes on screen, not remaining silent.
- [ ] **a11y ARIA and WCAG AA Contrast Standards (II16)**: UI components and operations configure basic a11y ARIA labels (such as `aria-label`, `aria-describedby`), and align with WCAG AA contrast standards (minimum contrast ratio of 4.5:1 between foreground and background).
- [ ] The Twitch panel displays `clientIdConfigured` / `hasRefreshToken`; missing ClientId displays no-Twitch mode, avoiding generating authorize URLs.
- [ ] The Twitch panel supports auth start/reset. **Twitch OAuth 302 Redirect to Root Path (II4, II29)**: Following successful or failed Twitch OAuth authorization, the backend callback endpoint must consume the `code`, complete token exchange, and securely encrypt and store refresh tokens, before issuing a `302` redirect back to the local Web UI root path (`/`). The Web UI does not receive OAuth `code`s or raw errors; authorization outcomes are rendered via `platform.connection_changed` state events, `GET /api/twitch/status` queries, and toast/status cards.
- [ ] **Twitch Reset and Emit Changes (II25)**: Executing a Twitch Reset, besides clearing backend refresh tokens, must actively disconnect the connection with Twitch and push status change events to all subscribed Overlay Hubs/Web Clients to reset states.
- [ ] **Polling Fallback Exponential Backoff Sequence (II22, II25)**:
    - SignalR connection drops trigger `HubConnection.onclose`; failure to reconnect starts HTTP Polling as a fallback.
    - The Polling fallback sequence starts with a `30s` base delay, multiplying by a `2x` factor on each failure, up to a maximum backoff cap of `300s`. Immediate duplicate calls at 0s are forbidden.
    - When `onreconnected` successfully restores the connection, immediately release backoff timers, stopping Polling calls.
- [ ] Error codes like 409 `WORKFLOW_RULE_CONFLICT`, 400/403/404 are rendered using i18n, preserving the raw code.
- [ ] All destructive operations utilize confirmation dialogs; dialogs are not nested inside cards.

**Verification:**
- [ ] Vitest: simulate form success/error rendering, matching basic a11y ARIA and contrast standards.
- [ ] Vitest: member list empty/success/error states, implementing negative test assertions to ensure no seed/delete entries appear and all member fields are read-only (Z10).
- [ ] Vitest: rule enable/disable/delete successfully updates local state; tests capture 409 optimistic locking conflicts and UI warnings.
- [ ] Vitest: tests JSON textarea 1MB paste debounce and crash prevention, ensuring data >1MB is rejected and not written to reactive states.
- [ ] Vitest: tests SignalR connection drop, verifying exponential backoff delay calculations (30s base, 2x factor, max 300s) and timer release upon connection recovery, avoiding immediate 0s calls.
- [ ] Vitest: error code i18n coverage, ensuring MVP error codes feature translated strings.
- [ ] 【dotnet Integration Test】(backend reset path): `POST /api/twitch/reset` -> TwitchAdapter disconnect + `IEventBus.Publish(PlatformConnectionChangedEvent { platform: "twitch", connected: false })` (C# integration test, not Vitest; verifies backend reset -> emit complete behavioral chain).
- [ ] Browser manual: complete `simulate chat` -> member appears -> rule create -> disable -> enable -> delete via the Web UI (per `manual-verification.md` § Task 20 Browser Manual Checklist).
- [ ] Browser manual: UI displays no-Twitch mode when Twitch ClientId is missing; launches Twitch URL on auth start when ClientId is present. Backend completes code exchange and refresh token storage on successful authorization, issuing a 302 redirect back to `/`, and Web UI displays success status via status/event streams (per `manual-verification.md` § Task 20k).

**Dependencies:** Task 19, Task 14a, Task 14b, Phase 5 Task 16f/16g manual gates

**Files Likely Involved:**
- `src/frontend/src/views/admin/`
- `src/frontend/src/components/admin/`
- `src/frontend/src/api/`
- `src/frontend/src/i18n/`
- `src/frontend/tests/`

**Size:** L, split into multiple small commits during implementation.

---

## Task 21 - Photino Desktop Shell + Static Fallback

**Description:** Let `Vulperonex.Desktop` launch the Web host and load the Vue UI. Port conflicts, WebView2 absence, migration failures, and Web host crashes must yield clear and actionable fallbacks. Given that this project utilizes .NET 10.0 and Photino uses version 3.x, the implementation must include a ".NET 10.0 + Photino 3.x compatibility pre-check," and provide "WebView2 fallback or standalone Kestrel service fallback" mitigations in case native runtimes crash (II30).

**Acceptance Criteria:**
- [ ] `dotnet run --project src/Hosts/Vulperonex.Desktop` launches the desktop window and loads the Web UI.
- [ ] **Single Instance Detection (II17)**: Desktop Shell launches must utilize a .NET `NamedMutex` (named mutex) to execute Single Instance checks. If an active instance already exists, exit directly or pop up an error warning, preventing port occupancy and SQLite locking conflicts.
- [ ] **Photino IPC DTO Schema (II19)**: Define and implement IPC communication bridges between C# and Photino-Vue frontend, locking the data structure exactly to `{ type: string, payload: any }`.
- [ ] API/overlay port pairs must be simultaneously available to be used; switch to the next pair if either port is occupied.
- [ ] Display dialogs and do not leave half-started processes when all port pairs from 5000/5001 to 5008/5009 are exhausted.
- [ ] Show download links and exit options when WebView2 is missing.
- [ ] Show Open log folder / Exit when migrations fail.
- [ ] Show embedded fallback HTML and a Restart button when the Web host crashes.
- [ ] **Web Host Crash Restart Limits and Vitest Assertions (II13)**: Mock Web host crash restart behaviors; the first 3 crashes automatically retry restarts, and upon the 4th crash, stop retrying, showing a UI fallback warning: "Multiple restart attempts failed, please restart the Vulperonex service manually."
- [ ] The Desktop host does not modify the loopback-only safety boundaries of the Web API.

**Verification:**
- [ ] Unit test/Vitest: mock Web host crash restarts, asserting that the first 3 automatic restarts occur, and the 4th terminates with a UI fallback warning: "Multiple restart attempts failed, please restart the Vulperonex service manually" (II13).
- [ ] Unit test/Vitest: verify the Photino IPC Bridge matches the `{ type: string, payload: any }` structure exactly.
- [ ] Unit test: mock WebView2 detector reports missing, triggering dialog callbacks.
- [ ] Unit test: mock migration failure, ensuring the dialog contains Open log folder / Exit.
- [ ] Unit test: mock Web host crash, ensuring the fallback HTML displays Restart.
- [ ] Manual: NamedMutex single instance check successfully rejects duplicate launches and prompts warnings (II17).
- [ ] Manual: application switches to 5002/5003 when 5000 or 5001 is occupied.
- [ ] Manual: clear warnings are visible when all port pairs are occupied.
- [ ] Manual: simulated chat -> overlay event is visible inside the Desktop shell.

**Dependencies:** Task 19, Task 20

**Files Likely Involved:**
- `src/Hosts/Vulperonex.Desktop/`
- `src/Hosts/Vulperonex.Desktop/Resources/fallback.html`
- `tests/Vulperonex.Tests.Unit/Desktop/`

**Size:** M

---

## Task 22 - Overlay Event History Persistence + Cleanup Surface

**Description:** Overlay (chat / alerts / member) events are currently entirely in-memory. SPA route changes, F5 page refreshes, application restarts, or OBS browser source reloads clear them out. Task 22 introduces a small, restart-resilient ring buffer, and provides clearing entries on both the admin side and the overlay side, enabling "history verification during tests" and "OBS reconnect screen backfilling." The design directly reuses the Omni-Commander `ChatHistoryService` pattern (`ref/Omni-Commander/OmniCommander.Infrastructure/Overlay/ChatHistoryService.cs`): writing JSON in a single row to the `SystemSettings` table, avoiding new tables, migrations, or indexes.

**Acceptance Criteria:**
- [ ] **Storage Carrier**: Reuse the existing `SystemSettings` table with three keys: `Overlay.History.Chat`, `Overlay.History.Alerts`, and `Overlay.History.Member`. The `ValueJson` holds the full serialized payload list (capped, size should be < 10KB). **Do not add EF entities or migrations**.
- [ ] **Service Interface**: `IOverlayHistoryService<TPayload>` generic service providing `GetRecent()`, `AddAsync(payload)`, and `ClearAllAsync()`. Declared in Vulperonex.Application and implemented in Vulperonex.Infrastructure.
- [ ] **Three Typed Registrations**:
    - `IOverlayHistoryService<OverlayChatPayload>` cap **30**
    - `IOverlayHistoryService<OverlayAlertPayload>` cap **20**
    - `IOverlayHistoryService<OverlayMemberPayload>` cap **20** (strictly reserved for schema consistency, not written in MVP).
- [ ] **In-Memory Cache**: `ConcurrentQueue<TPayload>` + `SemaphoreSlim(1,1)` write lock, reusing the Omni-Commander pattern.
- [ ] **Startup Rehydrate**: The service constructor calls `LoadFromDb()` to restore cache from SystemSettings; DB missing or deserialization failure logs warnings and falls back to empty caches instead of failing fast.
- [ ] **`OverlayEventForwarder` Adaptation**: Call `AddAsync` before `Clients.All.SendAsync("event", ...)` for chat/alerts payloads. The member hub does not write in MVP. Forwarder history write failures are logged as warnings and do not block broadcasts.
- [ ] **Hub `OnConnectedAsync` Replay**: Override `OnConnectedAsync` in the three hubs (`OverlayChatHub`, `OverlayAlertsHub`, `OverlayMemberHub`), pushing existing history payloads line-by-line to `Clients.Caller`.
- [ ] **Alert Replayed Flag (Avoiding Animation Storms)**: Add a `Replayed: bool` field (default false) to `OverlayAlertPayload`. Replay paths set `Replayed = true`. The frontend AlertOverlay pushes `replayed = true` events to lists without triggering animation/audio hooks (chat overlays do not need this, as replaying text lists has no side-effects).
- [ ] **Clear API**:
    - `DELETE /api/overlay/chat/messages`
    - `DELETE /api/overlay/alerts/messages`
    - `DELETE /api/overlay/member/messages`
    - Returns `204 No Content`. Execution: clear cache -> clear SystemSettings row -> broadcast `cleared` event to corresponding hubs (frontend clears screens/lists upon receipt).
- [ ] **Web UI Clear Surface (Placed in Both Locations)**:
    - Admin status (`/`): Add "Clear" buttons to the three overlay hub cards (with confirmation dialogs, reusing Task 20d confirmation dialog paradigms).
    - Overlay route header (`/overlay/chat`, `/alerts`, `/member`): Add "Clear" buttons to headers (similar confirmation dialogs).
- [ ] **Frontend Deduplication**: `useOverlayHub` events list handles upserts by `eventId`, preventing duplicate listings of the same event from concurrent replay and live deliveries (guaranteed by unique backend eventIds).
- [ ] **Clear Event Contract**: Hub `cleared` message is `{ hubName: "chat"|"alerts"|"member" }`; frontend `useOverlayHub` resets lists upon receiving the `cleared` event.
- [ ] **Dynamic Capacity Settings**: Capacities can be overridden via SystemSettings keys `Overlay.History.Cap.{HubName}` (default to 30/20/20 on missing keys). MVP does not provide UI configuration entries.

**Verification:**
- [ ] Unit test: `OverlayHistoryService<T>` `AddAsync` dequeues the oldest once cap is exceeded; SystemSettings JSON contains the list within the cap limit.
- [ ] Unit test: Service constructor's `LoadFromDb` restores from existing SystemSettings rows; missing tables or corrupted JSON logs warnings instead of throwing exceptions, falling back to empty caches.
- [ ] Unit test: `OverlayEventForwarder` history writes and broadcasts for chat/alerts/member events occur in the correct sequence (history.AddAsync executes before hub.SendAsync).
- [ ] Unit test: Hub `OnConnectedAsync` pushes all history payloads to callers, with alerts hub replay payloads setting `Replayed = true` while chat hubs lack this flag.
- [ ] Unit test: Clear endpoints clear caches + SystemSettings, broadcasting `cleared` events; repeating calls does not throw exceptions.
- [ ] Vitest: `useOverlayHub` does not double-list duplicate `eventId`s; lists reset on receiving `cleared` events.
- [ ] Vitest: AlertOverlay pushes `replayed = true` events to lists without triggering animation hooks (asserted via spies).
- [ ] Browser manual: Open `/simulate`, send chat -> F5 refresh `/overlay/chat` shows history -> click admin "Clear" -> overlay clears instantly -> send another, displaying live.
- [ ] Browser manual: Send follow while the alerts overlay tab is open -> animation plays; F5 refresh restores history **without** replaying the animation.

**Dependencies:** Task 19 (frontend skeleton), Task 20a (simulate exists for verification). Recommended to complete before Task 20b (event monitor), as monitors can adopt the same history endpoints and deduplication logic.

**Files Likely Involved:**
- `src/Vulperonex.Application/Overlay/IOverlayHistoryService.cs` (new)
- `src/Vulperonex.Infrastructure/Overlay/OverlayHistoryService.cs` (new)
- `src/Hosts/Vulperonex.Web/SignalR/OverlayEventForwarder.cs` (modified)
- `src/Hosts/Vulperonex.Web/SignalR/OverlayHubs.cs` (modified: added `OnConnectedAsync`)
- `src/Hosts/Vulperonex.Web/Endpoints/OverlayHistoryEndpoints.cs` (new)
- `src/Application/Vulperonex.Application/Overlay/Dtos/OverlayAlertPayload.cs` (modified: added `Replayed`)
- `src/frontend/src/composables/useOverlayHub.ts` (modified: deduplication + `cleared`)
- `src/frontend/src/views/overlay/*.vue` (modified: headers add clear buttons; AlertOverlay gating animations)
- `src/frontend/src/views/admin/AdminStatusView.vue` (modified: overlay hub cards add clear buttons)
- `src/frontend/src/api/client.ts` (modified: added `clearOverlayHistory(hubName)`)

**Size:** M (reuses existing SystemSettings + Omni-Commander patterns, no migrations)

---

## Checkpoint: Phase 6 (II2, II6, II26)

- [ ] **Self-Check Gate (II6)**: Confirm all sub-tasks in Tasks 18, 19, 20, and 21 are marked as `[x]`.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [ ] `cd src/frontend; pnpm test` passes.
- [ ] `cd src/frontend; pnpm build` passes.
- [ ] `cd src/frontend; pnpm lint` passes.
- [ ] **Manual Verification and Audit Trail Closure (II2, II26)**:
    - Execute all manual verification steps for Task 20, Task 20k, and Task 21 in `docs/phases/phase-6-web-ui/manual-verification.md` exactly.
    - Confirm all Dated Entries in `docs/phases/phase-6-web-ui/manual-verification.md` are explicitly recorded as `Result: PASS` (II2).
    - Checkpoints for manual items are consolidated as: "**All items in manual-verification.md § Task 20/21 Browser Manual Checklist passed**" (II26).

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|------|----------|
| npm dependencies not installed or mismatched | High | Verify `package.json`/lockfile before starting Task 19; seek prior approval for new packages, strictly pinning pnpm versions and verifying via `install --lockfile-only --ignore-scripts`. |
| Rule visual builder scope becomes too large | Medium | Deliver JSON editors + basic forms first, ensuring alignment with CLI/API contracts. |
| Confusion between Twitch OAuth from Web UI and CLI loopback callback semantics | Medium | The Web UI utilizes strictly Web API Twitch auth endpoints, not saving tokens directly; fails closed when ClientId is missing. |
| SignalR tests flake in CI | Medium | Automated tests verify contracts/states; timings are handled strictly in manual verifications. |
| Desktop/Photino issues obscure Web UI issues | Medium | Verify Web host inside browsers first, before proceeding to Task 21 Desktop shell. |
| Phase 5.5 CLI id resolution worktree not yet converged | Low | Phase 6 docs do not depend on that dirty diff; re-verify API/CLI final command semantics before implementing Task 20. |
| **.NET 10.0 + Photino 3.x Compatibility Unverified (II30)** | Medium | Task 19 verifies the Web UI inside browsers first; Task 21 handles .NET 10.0 + Photino 3.x compatibility checks. In case of native runtime crashes, provide "WebView2 fallback or standalone Kestrel service fallback" mitigations. |

---

## Recommended Implementation Order

1. Task 19a: Run `corepack enable` (fallback to `corepack pnpm@9.15.4 <command>` if Windows shim permissions are blocked); run `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts` (resolving stack version compatibility errors first); install stack packages upon confirmation, and establish frontend package/build/test skeletons.
2. Task 19b: API clients + i18n manifests + dashboard shell.
3. Task 19c: SignalR composables + overlay route skeletons.
4. Task 20a: simulate panels + event monitors.
5. Task 20b: member panels.
6. Task 20c: rule list/show + enable/disable/delete.
7. Task 20d: rule create/update JSON editors + validation displays.
8. Task 20e: Twitch auth panels.
9. Task 22: Overlay history persistence + cleanup surfaces (recommended to run concurrently or immediately following Task 20a, improving manual verification experiences).
10. Task 18: Serilog/AppLogs.
11. Task 21: Photino Desktop shell.

Task 20 is recommended to be split into multiple commits; each panel can be verified manually in a browser once completed.
