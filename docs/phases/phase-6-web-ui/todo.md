# Phase 6 Todo List: Web UI + Logging + Desktop Shell

> Detailed Plan: `docs/phases/phase-6-web-ui/plan.md`
> Parent Checklist: `tasks/todo.md`
> **Implementation Order**: Task 19 → Task 20a → Task 22 → Task 20b-l → Task 18 → Task 21 (Order from dependency graph; see details in plan.md § Recommended Implementation Order)
> [!IMPORTANT]
> **Precondition Gate**: The three manual verification checkpoints in the Parent Plan's Phase 5 (including CLI E2E wrap-up, Twitch OAuth real browser authorization with full code exchange + refresh_token storage, and REPL manual checks) must be marked as completed before Phase 6 implementation can begin.
> **Current Status**: Completed Web UI/rule JSON editor/overlay history inside Phase 6 serves as a baseline for Phase 7; incomplete non-workflow parity items like Photino/manual verification are deferred until after Phase 7 convergence.

---

## Task 19 - Vue Frontend Foundation

- [x] Task 19a: Execute `corepack enable`; establish the `src/frontend` package using Vite 7.3, Vue 3.5, and TypeScript, pinning `"packageManager": "pnpm@9.15.4"`. All new packages in Task 19 (Vue 3.5, Vite 7.3, PrimeVue 4 Unstyled, UnoCSS Preset Wind 4, Pinia, vue-i18n, oxlint, vue-tsc) require prior approval (ask-first protocol).
- [x] Task 19b: Establish router, Pinia, layout shells, API clients, and `VITE_API_URL` overrides, distinguishing the Admin Hub `/hubs/events` from Overlay dedicated Hubs.
- [x] Task 19c: Establish vue-i18n manifest `src/frontend/src/i18n/manifest.json` configured as `{ "locales": ["zh-TW", "en-US"], "default": "zh-TW" }`, while providing both `zh-TW` and `en-US` locale files, displaying keys on missing strings.
- [x] Task 19d: Establish dashboard status cards: API health, Twitch auth status, no-Twitch mode (Log/Logs widgets are marked as Defer).
- [x] Task 19e: Establish the `useStreamEvents` composable.
- [x] Task 19f: Establish `/overlay/chat`, `/overlay/alerts`, and `/overlay/member` route skeletons (the server does not push events to `/hubs/overlay/member` in MVP; the overlay simply connects to the Hub and renders empty skeletons).
- [x] Task 19g: Complete frontend base configurations and XSS text binding protections unit tests (rendering `<script>` or `displayName` in both ChatOverlay and AlertsOverlay is verified to be text nodes without script elements), and verify `pnpm dev` launches without errors (manual verification).

## Task 20 - Web Admin UI

> All paths for frontend console Views and Components in this task are flattened into `src/frontend/src/views/admin/` and `src/frontend/src/components/admin/`, replacing deeply nested directories (BB3, CC1, CC2, CC3).
> Vitest test naming conforms to the `should * when *` format (e.g., `should preserve textarea content when API returns 400`).

- [x] Task 20a: The simulation panel supports chat/follow/sub short aliases, displaying ack responses and accepted/eventId/platformUserId information upon success.
- [x] Task 20b: The event monitor displays SignalR envelopes and recent event lists. Strictly lock the envelope schema fields to `{ type, eventId, platform, occurredAt }` (aligned with the Phase 5 backend `StreamEventEnvelope` record). Schema expansions for `schemaVersion` and `data` are deferred to Phase 7.
- [x] Task 20c: The member panel supports strictly read-only list/show operations; does not provide seed/delete buttons, and does not add member CRUD endpoints. Member Read-Only Negative Test Assertion (Z10): All member fields (such as names, platform identifiers, etc.) are strictly read-only in the Web UI, preventing direct editing, and Vitest must assert that no seed/delete action entries appear. Test data seeding and cleanup are managed via CLI/manual test surfaces.
- [x] Task 20d: The rule panel supports list/show, displaying enabled, version, priority, and createdAt, adding confirmation dialogs for operations like deletion. Implement optimistic locking support (II17): the frontend carries a `version` field in the DTO when updating Rules; when the backend returns 409 Conflict, the frontend must capture the error and pop up a dedicated optimistic locking conflict warning, guiding users to reload or overwrite. (list/show + enable/disable + delete confirmation + 409 conflict warnings; create/update JSON editing is handled in Task 20f).
- [x] Task 20e: EventTypeKey Dropdown implementation: **strictly filters and excludes** `platform.connection_changed` (isSystemEvent: true), with badges in the Dropdown indicating the three canonical simulatable keys, while other keys (`user.donated`, `user.gifted_sub`, `channel.raided`, `reward.redeemed`) are explicitly marked as unsupported. (reusable `EventTypeKeyDropdown` component; server-side `registry.GetAll()` already filters out IsSystemEvent=true, so the dropdown directly displays isSimulatable badges + "no simulator yet" markings, mounted directly by Task 20f's rule create form).
- [x] Task 20f: Rule create/update supports JSON file uploads (limits to 1MB / `.json` extension + MIME + JSON.parse triple check) and manual **JSON Textarea editing**, preserving contents and focusing on the textarea (`inputRef.value?.focus({ preventScroll: false })`) upon submission failure, rendering API validation errors inline. **Implement JSON Textarea 1MB limit triple check (II15)**: implement textarea `maxlength` limits; debounce paste checks by `300ms`, rejecting parsing and displaying a toast warning if lengths exceed 1MB; save pasted raw text in non-reactive variables instead of assigning directly to Vue reactive refs, protecting against main thread lockups and OOM crashes from repeated Vue attribute change detections.
- [x] Task 20g: The Twitch auth panel supports status, start redirection (system default browser), and resetting tokens. **Twitch OAuth 302 Redirect to Root Path (II4, II29)**: following Twitch authorization, the backend callback endpoint consumes the `code`, completes token exchange, and securely encrypts and stores refresh tokens, before issuing a `302` redirect back to the local Web UI root path (`/`). The Web UI does not receive OAuth `code`s or raw errors; authorization outcomes are rendered via `platform.connection_changed` state events, `GET /api/twitch/status` queries, and toast/status cards. **Twitch Reset and Emit Changes (II25)**: executing a Twitch Reset, besides clearing backend refresh tokens, must actively disconnect the connection with Twitch and push status change events to all subscribed Overlay Hubs/Web Clients to reset states. Displays no-Twitch mode when ClientId is missing. (panel UI + start/reset/no-Twitch mode completed; backend `GET /auth/callback` 302 + `platform.connection_changed` broadcasts are immediately completed by Task 20j, reusing the existing `/api/twitch/auth/complete` and token store).
- [x] Task 20h: Populate MVP error codes in `zh-TW.ts` and `en-US.ts` translations and coverage unit tests (asserting that errorCodes.ts constants exist and are non-empty individually); 5xx errors display `INTERNAL_ERROR` i18n + `console.error`. (`i18n/errorCodes.ts` mirrors the backend ErrorCodes.cs and contains INTERNAL_ERROR / NETWORK_ERROR; `api/errors.ts` consolidates 5xx → INTERNAL_ERROR + console.error; vitest asserts each constant bilingually + covers `describeApiError` behaviors).
- [x] Task 20i: Complete browser manual E2E acceptance checks, covering the complete rule creation -> click simulation -> overlay display -> status update -> deletion workflows (strictly using `docs/phases/phase-6-web-ui/manual-verification.md` § Task 20 Browser Manual Checklist as the sole source of truth).
- [x] Task 20j: OAuth closure: SignalR `platform.connection_changed` drives automatic re-rendering and status synchronization of the UI Twitch status card, and tests simulate `platform.connection_changed` events in Vitest to verify the complete update of UI status cards and OAuth states. **Polling Fallback Exponential Backoff Sequence (II22, II25)**: SignalR connection drops trigger `HubConnection.onclose`; failure to reconnect starts HTTP Polling as a fallback. The Polling sequence starts with a `30s` base delay, multiplying by a `2x` factor on each failure, up to a maximum backoff cap of `300s`. Immediate duplicate calls at 0s are forbidden. When `onreconnected` successfully restores the connection, immediately release backoff timers, stopping Polling calls. Assert this exponential backoff sequence and timer release in Vitest. (backend `GET /auth/callback` completes code exchange + 302 back to `/` + `platform.connection_changed` broadcasts; DELETE token synchronizes broadcasts; frontend TwitchAuthView watches envelopes to trigger reloads; `useExponentialPollingFallback` manages 30s base / 2x factor / 300s cap / stops timers on connection / starts idempotently; Vitest covers backoff sequences and timer releases).
- [x] Task 20k: Twitch OAuth E2E manual end-to-end checks, covering start, status, and reset flows, writing manual test results into `manual-verification.md` upon completion.
- [x] Task 20l: a11y and WCAG AA support (II16): UI components and operations configure basic a11y ARIA labels (such as `aria-label`, `aria-describedby`), and align with WCAG AA contrast standards (minimum contrast ratio of 4.5:1 between foreground and background), verified in Vitest tests. (ConfirmDialog: role=dialog/aria-modal/aria-labelledby/focus/ESC; HubStatusChip: role=status/aria-label; EventTypeKeyDropdown: select aria-label; MembersView list rows change to role=button/tabindex=0/Enter/Space triggers + aria-label; Vitest a11y baseline passes 100% green. Color contrast ratios use status-card green #1b6a4f / red #b3261e / yellow #855600 against white #ffffff, all ≥ 4.5:1, recorded in `manual-verification.md` manual verifications).

## Task 18 - Serilog + AppLogs

- [x] Task 18a: Configure Console, rolling file, and SQLite AppLogs sinks (avoiding duplicate configurations of `PRAGMA auto_vacuum`, which is already handled in Task 5 DB bootstrap).
- [x] Task 18b: Add structured fields: EventTypeKey, Platform, MemberId, WorkflowRuleId, ActionType. **De-identification and Privacy Compliance (II24)**: The `MemberId` field strictly logs pseudonymized ULIDs, strictly forbidding logging any PII (such as real names, e-mails, or raw platform account IDs).
- [x] Task 18c: Implement `log.min_level` hot-reloading. (via `LogLevelHotReloadWorker` polling SystemSettings every 10 seconds, applying changes to the shared `LoggingLevelSwitch` without requiring a restart).
- [x] Task 18d: Implement AppLogs retention/size cleanup workers (whichever is triggered first between retention and size-based policies, with defaults `log.db_max_size_mb = 50MB` and `log.db_retention_days = 30 days`), executing `VACUUM` after size cleanups, consolidated via `AppLogsCleanupWorker.ExecuteOnce()`.
- [x] Task 18e: Complete logging integration tests, including de-identification compliance assertions for `MemberId`.

## Task 21 - Photino Desktop Shell

- [x] Task 21a: The Desktop host launches the Web host and loads the Vue UI, configuring `<TargetFramework>net10.0-windows</TargetFramework>` and supporting Windows 10 1809+. **Single Instance Detection and Mutex Lock (II17)**: Launches utilize a .NET `NamedMutex` to perform Single Instance checks. If an active instance already exists, exit directly or pop up an error warning, preventing port occupancy and SQLite locking conflicts.
- [x] Task 21b: Integrate port pair allocations, switching to the next pair if either port is occupied (PortPairAllocator unit tests were completed in Task 15; integrated here).
- [x] Task 21c: WebView2 absence detection, displaying dialogs containing download links (`https://go.microsoft.com/fwlink/p/?LinkId=2124703`).
- [x] Task 21d: Migration failure detection and dialog rendering (containing [Open log folder] (`%LOCALAPPDATA%\Vulperonex\logs`) and [Exit] buttons).
- [x] Task 21e: Web host crash detection and embedded fallback HTML rendering. **Restart Count Limits and Vitest Assertions (II13)**: Mock Web host crash restarts; the first 3 crashes automatically retry restarts, and upon the 4th crash, stop retrying, showing a UI fallback warning: "Multiple restart attempts failed, please restart the Vulperonex service manually." Complete mock Web host unit tests, asserting that the first 3 automatic restarts occur, and the 4th terminates with a UI fallback warning.
- [x] Task 21f: Complete Desktop shell integration unit tests and manual connection smokes. Define and implement IPC communication bridges between C# and Photino-Vue frontend, locking the data structure exactly to `{ type: string, payload: any }`, verifying IPC Bridge structure compatibility in unit tests (II19).
- [x] Task 21g: .NET 10.0 + Photino 3.x compatibility pre-check (II30): execute compatibility checks, providing "WebView2 fallback or standalone Kestrel service fallback" mitigations in case native runtimes crash.

## Task 22 - Overlay Event History Persistence + Cleanup Surface

> Design Source: `ref/Omni-Commander/OmniCommander.Infrastructure/Overlay/ChatHistoryService.cs` (SystemSettings single-row JSON persistence pattern; no new EF entities or migrations).
> Dependencies: Task 19 (frontend skeleton), Task 20a (simulate exists for verification). Recommended to complete before Task 20b.

- [x] Task 22a: Declare the `IOverlayHistoryService<TPayload>` (Application layer) interface: `GetRecent()`, `AddAsync(payload)`, `ClearAllAsync()`; define three SystemSettings key constants `Overlay.History.Chat`, `Overlay.History.Alerts`, and `Overlay.History.Member` with default caps (chat=30, alerts=20, member=20), where capacities can be overridden via `Overlay.History.Cap.{HubName}`.
- [x] Task 22b: Implement `OverlayHistoryService<TPayload>` (Infrastructure layer): `ConcurrentQueue<TPayload>` cache + `SemaphoreSlim(1,1)` write lock; `LoadFromDb()` rehydrates on construction (DB missing or deserialization failure logs warnings and falls back to empty caches instead of failing fast); `AddAsync` enqueues -> trims to cap -> writes the full JSON list back to corresponding SystemSetting rows; `ClearAllAsync` clears cache and clears SystemSettings rows.
- [x] Task 22c: DI register three typed services: `IOverlayHistoryService<OverlayChatPayload>`, `IOverlayHistoryService<OverlayAlertPayload>`, and `IOverlayHistoryService<OverlayMemberPayload>` (Singleton).
- [x] Task 22d: Add `Replayed: bool` field (default `false`) to `OverlayAlertPayload`; OverlayChatPayload and OverlayMemberPayload do not add it.
- [x] Task 22e: Adapt `OverlayEventForwarder`: call `history.AddAsync` before `Clients.All.SendAsync("event", ...)` for chat/alerts payloads. History write failures are logged as warnings and do not block broadcasts. The member hub does not write in MVP (no event sources).
- [x] Task 22f: The three hubs override `OnConnectedAsync`, pushing existing history payloads line-by-line to `Clients.Caller`; the alerts hub sets `with { Replayed = true }` for every replayed payload.
- [x] Task 22g: Add `OverlayHistoryEndpoints`: `DELETE /api/overlay/{chat|alerts|member}/messages` -> calls corresponding `ClearAllAsync` -> broadcasts `cleared` events via `Clients.All.SendAsync("cleared", new { hubName })` -> returns `204 No Content`.
- [x] Task 22h: Frontend `useOverlayHub` handles upserts by `eventId` (deduplication); resets lists upon receiving hub `cleared` events; exposes a `clear()` method (calling `clearOverlayHistory(hubName)` API internally).
- [x] Task 22i: `api/client.ts` adds `clearOverlayHistory(hubName: OverlayHubName)` (DELETE `/api/overlay/{hubName}/messages`).
- [x] Task 22j: The three overlay route views (`ChatOverlayView`, `AlertOverlayView`, `MemberOverlayView`) add "Clear" buttons to headers (with confirmation dialogs, reusing Task 20d confirmation paradigms). AlertOverlay pushes `replayed = true` events to lists without triggering animation/audio hooks (audio is deferred to Phase 7).
- [x] Task 22k: `AdminStatusView` overlay hub cards add "Clear" buttons (with confirmation dialogs); lists/cards refresh instantly upon click.
- [x] Task 22l: i18n adds `overlay.clear`, `overlay.clearConfirm`, and `overlay.clearConfirmTitle` keys (synchronized across zh-TW and en-US).
- [x] Task 22m: dotnet unit tests: `OverlayHistoryService<T>` `AddAsync` dequeues oldest + writes to SystemSettings JSON within cap limits; LoadFromDb error recovery; `OverlayEventForwarder` history write/broadcast sequences; Hub `OnConnectedAsync` replay (alerts `Replayed=true`); Clear endpoint cache/SystemSettings/broadcast complete flow.
- [x] Task 22n: Vitest: `useOverlayHub` eventId deduplication, cleared event list resets, AlertOverlay replayed animation gating assertions via spy, and clear button confirmation + API call.
- [x] Task 22o: Browser manual: (1) open `/simulate`, send chat -> F5 refresh `/overlay/chat` shows history; (2) admin clear button -> overlay clears instantly -> send another, displaying live; (3) alerts F5 refresh restores history **without** replaying animations; (4) write results to `manual-verification.md` § Task 22 section.

## Phase 6 Checkpoint

- [x] **Precondition: Phase 5 Gate Self-Check**: The three manual verification checkpoints in the Parent Plan's Phase 5 (including CLI E2E wrap-up, Twitch OAuth real browser authorization with full code exchange + refresh_token storage, and REPL manual checks) must be marked as completed before Phase 6 implementation can begin.
- [x] Complete self-checks for all sub-tasks in Tasks 18-22 as `[x]`.
- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes.
- [x] `cd src/frontend; pnpm vue-tsc --noEmit` -> TypeScript type checking passes successfully with 0 errors (Vue SFC requires vue-tsc instead of tsc).
- [x] `cd src/frontend; pnpm test` -> all Vitest tests pass (covering composables, overlay XSS, Member negatives, JSON textareas, simulate badges, OAuth closures, and i18n coverage).
- [x] `cd src/frontend; pnpm build` -> build succeeds without error output.
- [x] `cd src/frontend; pnpm lint` -> runs `oxlint` syntax checks, frontend lint passes 100% green without errors.
- [x] `cd src/frontend; pnpm dev` -> launches Vite dev server for smoke tests (appearance of `VITE ... ready in` indicates success; Ctrl+C manually to continue subsequent checkpoint steps).
- [x] Browser manual: Web UI status cards render correctly; simulate chat/follow/sub successfully pushes events to independent Overlay Hubs and renders them.
- [x] Browser manual: Web UI member list/show read-only displays function normally; Rule JSON Textarea edits, creations, deletions, and Twitch OAuth status and initiations complete successfully.
- [x] Desktop manual: Photino packages successfully load UIs, and execute corresponding fallbacks/dialogs under port conflicts, WebView2 absence, and migration failures.
- [x] Document: establish and update `manual-verification.md` in `docs/phases/phase-6-web-ui/` to record manual verification results, following the Phase 5 manual-verification.md format and templates exactly.
- [x] Git staging sets are limited to Phase 6 task scopes; Phase 5.5/CLI resolver dirty diffs are excluded.
- [x] **Manual Security Compliance Audit**:
  - [x] **Overlay DTO Read-Only Safety**: Reflection verifies that DTO JSON key sets match whitelists exactly (detailed whitelist specifications match Parent Plan `tasks/plan.md` Task 15 exact DTO specifications, i.e., chat/alerts exclude `memberId`/`platformUserId`; member excludes `memberId`/`totalLoyalty`/`linkedPlatforms` and adopts snapshot structure, not containing `eventId`/`timestamp`).
  - [x] **Dual Ports Dual Binding**: API and Overlay dual ports bind to Loopback (IPv4/IPv6) in Production.
  - [x] **OAuth PKCE Security Boundary**:
    - [x] `state` CSRF verification: state mismatches, exceeding the 10-minute TTL, or already-used states are rejected, blocking code exchanges.
    - [x] OAuth callback listener: loopback-only (127.0.0.1 / ::1) + Host header allowlist + accepts strictly default paths + single-use closure.
    - [x] Logger Scrub sensitive words: logger scrubs exclude access tokens, authorization codes, code_verifiers, and raw refresh tokens.
  - [x] **Configuration Protected Namespace Protections**:
    - [x] `/api/config` read/write blocks: `security.*` / `oauth.*` are forbidden (returns 403); unknown `oauth.*` keys are blocked preferentially.
    - [x] CLI configuration protection: `config set security.*`/`config set oauth.*` are rejected, returning 403.
  - [x] **Ciphertext Encryption and Lifecycles**:
    - [x] `machine.key` is automatically generated on startup when missing, with restrictive OS permissions configured (Windows ACL current user FullControl / Unix 0600); failures fail-fast.
    - [x] AES-256-GCM token encryption: GCM random nonces prevent tampering; encrypting identical plaintext twice produces different ciphertexts (verifying random nonces). Bound to AAD setting keys, throwing `CredentialDecryptionException` on tampering or cross-key copies.
    - [x] Refresh token envelopes utilize standard Base64 format (not Base64Url), decoding via `Convert.FromBase64String`.
  - [x] **Plugin Isolation Protections**:
    - [x] Plugin/Action contexts strictly forbid exposing `System.IServiceProvider` (ensured by PR code reviews and architectural boundaries, not requiring separate ArchUnit/NetArchTest tests).
