# Phase 7D Implementation Plan: CheckIn→Member Binding, Custom HTML Editor, Unified Monitor Page, Member Editable Surface, Module & Plugin Management, Simulation Event Extension, Intuitive Workflow UI

> Parent Plan: `tasks/plan.md`
> Parent Todo: `tasks/todo.md`
> SPEC Mapping: `docs/SPEC.md` §4.14.2, §4.14.3, §4.18, §4.19, §4.20, §4.21, §4.22
> Reference Sources: `ref/Omni-Commander/OmniCommander.UI/src/components/monitor/MonitorDashboard.vue`, `MonitorControls.vue`, `MonitorOverlay.vue`, `members/MemberDashboard.vue`, `editor/EditorDashboard.vue`, `ModuleManagementTab.vue`, `DynamicActionForm.vue`, `ConditionBuilder.vue`
> Prerequisites: Phase 7C is complete (member overlay preset, URL sanitize, custom preset zip upload, cross-hub member snapshot, OneComme bridge contract).
> Goal: Fill Phase 7C user experience gaps and pain points — check-in real push to member overlay, custom HTML online editing and validation, simulation and preview merged into a single page, member editable surface with audits, central switch control for core services and external plugins (with topological chain reaction), simulation event extension (roles and check-in/loyalty simulation), and visual form editing for workflow conditions and actions.
> Boundaries: The OneComme bridge plugin implementation remains deferred to Phase 7E (the contract was completed in Phase 7C).

---

## Goals

Phase 7C completed the overlay infrastructure but left critical user-reported gaps:

1. **CheckIn and member overlay are not bound** — `TriggerCheckInActionExecutor` only writes to the DB and never publishes the `MemberCheckedInEvent`; `OverlayEventForwarder` does not forward to `OverlayMemberHub`. Consequently, `/overlay/member` receives no events during actual workflow triggers.
2. **Cannot validate custom HTML upload validity** — Phase 7C zip upload has no syntax or SignalR contract checks after landing, so users must run OBS to know if it connects to the hub.
3. **High switching cost between Simulate / overlay / chat pages** — Streamers debugging must jump between multiple routes and cannot simulate events, view overlays, and check the chat data layer simultaneously on a single page.
4. **Member page is unreasonably read-only** — Real-world scenarios require manual adjustments of loyalty / check-in count / reset / deleting test members, which currently can only be done via CLI.
5. **Lack of central switch and dependency chaining for modules/plugins** — Cannot configure whether to enable specific modules. Dependency relationships between modules are unhandled (e.g., if the member core is disabled, check-in/raffle modules still run blindly, causing state drift).
6. **Incomplete event simulation** — Lacks simulation for stream roles, check-in, and loyalty points.
7. **Unintuitive workflow rule configuration** — Users must manually write JSON or complex NCalc expressions, lacking validation and visual guidance.

Phase 7D lands seven lines of work:

- **Binding Line**: `MemberCheckedInEvent` + forwarder + DTO extension
- **Editor Line**: Monaco-based editor + draft/production + validation gate (replacing pure zip)
- **Monitor Line**: `/monitor` unified page combining simulation, preview, and chat stream
- **Member Edit Line**: Adjust / reset / delete operations + audit log
- **Module Mgmt Line**: Centralized control for feature modules and external plugins, topological cascading shutdown gates, and auto-activation
- **Simulation Ext Line**: Stream roles (StreamRoleFlags) multi-select simulation, check-in and loyalty API/UI simulation
- **Visual Rule UI Line**: Condition Builder, strongly-typed Dynamic Action Form, and floating variable selector panel

---

## Scope

### In-Scope

- `MemberCheckedInEvent` domain event + publish from `TriggerCheckInActionExecutor`
- `OverlayEventForwarder` subscribes to the new event and forwards to `OverlayMemberHub`
- `OverlayMemberPayload` extended with `RoundIndex` / `StampSlotInRound` fields + reflection test expansion
- CLI `simulate checkin` command upgraded to publish events instead of writing directly to repository
- `/admin/overlay-editor` Monaco-based online editing page
- Draft/production directory structure + history rotation
- Validation gate: AngleSharp / ExCSS / Jint parsing + SignalR contract probe + file size checks
- 8 new API endpoints (`/api/overlay/custom-presets/{slug}/files`, `validate`, `deploy`, `rollback`, `history`)
- `/monitor` unified page + Simulate controls + Overlay preview iframe + Chat stream panel
- Member admin editable surface: AdjustLoyaltyModal, ResetModal, DeleteConfirmDialog, AuditLogDrawer
- `MemberAuditLogs` SQLite table + migration
- Four new endpoints `PATCH/POST/DELETE /api/members/{id}/*` + `If-Match` concurrency guard
- Reflection whitelist tests extended (member endpoints do not expose internal PKs)
- Module/plugin status transition APIs (`GET/POST /api/plugins-modules/*`) integrated with `ISystemSettingsService`
- Topological sorting cascade logic for module dependency shutdown, and dynamic state hot-reloading for Hosted Services
- Feature module management tab under `/admin/settings`
- Simulation endpoints extended with stream roles flags, plus new `POST /api/simulate/checkin` for simulating check-in/loyalty
- `/monitor` simulation console supports role multi-selection, check-in, and loyalty simulation
- `ConditionBuilder.vue` visual condition editor with automatic NCalc output
- `DynamicActionForm.vue` strongly-typed action parameter editor and floating variable selector panel

### Out-of-Scope

- Full importer implementation for the OneComme bridge plugin (Phase 7E)
- New action types like `TriggerAdjustLoyaltyAction` (handled in a separate slice)
- Bulk operations / import / export for members
- OBS scene preview integration on the `/monitor` page (pure overlay iframe only)
- Multi-user collaboration / file locking mechanism for the overlay editor
- Encryption / digital signature for audit logs

---

## Task Breakdown

### Track A — CheckIn → MemberOverlay Binding (SPEC §4.14.2)

## Task 50 - MemberCheckedInEvent Domain Event

**Description:** Add `MemberCheckedInEvent` under `Vulperonex.Domain.Events` as a subscribable event for check-in actions.

**Acceptance Criteria:**
- [ ] Define `MemberCheckedInEvent` record (fields specified in SPEC §4.14.2).
- [ ] Implement `IStreamEvent` (EventId, OccurredAt, Platform, EventTypeKey="system.member.checked_in").
- [ ] Register `EventTypeKey` in `StreamEventTypeRegistry`.
- [ ] Unit tests cover construction + EventTypeKey consistency.

## Task 51 - TriggerCheckInActionExecutor Emits Event

**Description:** After successful increment, publish `MemberCheckedInEvent` to `IStreamEventBus`, attaching displayName / avatarUrl / loyalty snapshot.

**Acceptance Criteria:**
- [ ] Inject `IStreamEventBus` into the executor.
- [ ] Calculate `RoundIndex` / `StampSlotInRound` after a successful increment.
- [ ] Publish the event after transaction commits (avoid emitting false events on failures).
- [ ] Maintain the existing `ActionExecutionResult`.
- [ ] Unit tests: verify mock `IStreamEventBus` receives publish calls with correct fields.
- [ ] Failure paths: do not publish if increment fails.

## Task 52 - OverlayEventForwarder Subscribes to MemberCheckedInEvent

**Description:** Add `stream.OfType<MemberCheckedInEvent>().Subscribe(...)` in `OverlayEventForwarder.StartAsync` to map events to `OverlayMemberPayload` and push to `OverlayMemberHub` + write to history.

**Acceptance Criteria:**
- [ ] Add private method `ForwardMemberCheckInEventAsync`.
- [ ] Inject `IHubContext<OverlayMemberHub>` + `IOverlayHistoryService<OverlayMemberPayload>`.
- [ ] Pushing and writing history utilize existing `SafeSendAsync` / `TryPersistAsync` helpers.
- [ ] Integration tests: publish event → hub group receives it + history endpoint is queryable.
- [ ] Logger warning paths match chat hub (failure in avatar/cache does not block pushing).

## Task 53 - OverlayMemberPayload Field Expansion + Reflection Tests

**Description:** Add `RoundIndex` and `StampSlotInRound` to the payload, update frontend types, and extend reflection whitelist tests.

**Acceptance Criteria:**
- [ ] Add the two fields to `OverlayMemberPayload`.
- [ ] Update `OverlayDtoWhitelistTests` to include the exact key set.
- [ ] Ensure `memberId` / `totalLoyalty` / `linkedPlatforms` are excluded.
- [ ] Update types in frontend `useOverlayHub.ts`.
- [ ] Update `MemberOverlayView` to use `RoundIndex` from the payload instead of calculating it on the frontend.

## Task 54 - CLI simulate checkin Publishes Event

**Description:** Upgrade the CLI `simulate checkin` subcommand from directly calling the repository to publishing `MemberCheckedInEvent` (aligning with the real workflow path).

**Acceptance Criteria:**
- [ ] Update the CLI subcommand to call the publish path.
- [ ] Existing CLI integration tests pass.
- [ ] Update the `docs/cli.md` documentation (if it exists).

---

### Track B — Custom HTML Editor (SPEC §4.14.3)

## Task 55 - Draft/Production Directory Refactoring

**Description:** Restructure the existing `wwwroot/overlay/custom/{slug}/` directory into `production/`, `draft/`, and `history/`. Redirect zip upload extractions to `draft/`. Rewrite OBS access path `/overlay/custom/{slug}/index.html` to `production/index.html` via middleware.

**Acceptance Criteria:**
- [ ] `OverlayPresetStore` supports the three subdirectories.
- [ ] Middleware handles rewriting `/overlay/custom/{slug}/*` → `/overlay/custom/{slug}/production/*`.
- [ ] Preview path `/overlay/custom/{slug}/draft/*` bypasses rewriting.
- [ ] Redirect zip uploads to `draft/` instead of the root directory.
- [ ] Existing Phase 7C integration tests remain green.
- [ ] Automatically migrate existing uploaded presets (startup detects presets missing the `production/` subdirectory and moves content to `production/`).

## Task 56 - Files API Endpoints (list / read / write / delete)

**Description:** Provide 4 file CRUD endpoints to support Monaco editor loading and saving draft contents.

**Acceptance Criteria:**
- [ ] `GET /api/overlay/custom-presets/{slug}/files` lists files (including draft/production diff).
- [ ] `GET /files/{path}?env=draft|production` reads file contents (UTF-8 text, returns 400 for binary).
- [ ] `PUT /files/{path}` writes to the draft directory (cannot write directly to production).
- [ ] `DELETE /files/{path}` deletes a single file in the draft directory.
- [ ] Path sanitization: forbids `..`, absolute paths, control characters; server-side validates that the resolved absolute path is strictly within the `draft/` directory.
- [ ] Limit single file writes to 2MB.
- [ ] Total size limit per slug is 10MB (draft + production + history); return 413 if exceeded.
- [ ] Integration tests cover path traversal, size limits, binary uploads, and non-existent slugs.

## Task 57 - Validation Gate

**Description:** Endpoint `POST /validate` parses draft files for syntax and contract verification, returning a list of issues.

**Acceptance Criteria:**
- [ ] Introduce NuGet packages: `AngleSharp`, `ExCSS`, `Jint`.
- [ ] HTML parse errors are reported as issues (severity=error).
- [ ] CSS parse errors are reported as issues (severity=error).
- [ ] JS parse errors are reported as issues (severity=error).
- [ ] SignalR contract probe (regex): missing `OverlayCommon.initSignalRConnection(` or `signalR.HubConnectionBuilder` triggers a warning.
- [ ] Missing reference to `/hubs/overlay/{chat|alerts|member}` triggers a warning.
- [ ] External URL references trigger warnings (with positions).
- [ ] Large files/packages trigger errors.
- [ ] The Issue DTO contains `severity`, `code`, `message`, `filePath?`, `line?`.
- [ ] Unit tests cover at least one case for each issue type.
- [ ] Integration tests: valid samples pass, invalid samples report corresponding issues.

## Task 58 - Deploy / Rollback / History Endpoints

**Description:** Deploy acts as an atomic directory copy, archiving old production files to history. Rollback restores from history.

**Acceptance Criteria:**
- [ ] `POST /deploy` runs validation first; error issues block deployment; on success, archive production to history and copy draft → production.
- [ ] Atomic copy implementation (temp directory + rename).
- [ ] History rotation: retain the 10 most recent entries (sorted by timestamp), pruning older ones.
- [ ] `POST /rollback?to={ts}`: restores production from the specified timestamp, archiving current production to a new history entry.
- [ ] `GET /history` lists history timestamps and sizes.
- [ ] Integration tests: deploy changes are reflected in the OBS URL, history is generated correctly, and rollback restores files.
- [ ] Concurrency guard: concurrent deployments to the same slug return 409 (via a simple in-memory lock).

## Task 59 - Admin Overlay Editor UI

**Description:** Add `/admin/overlay-editor` page featuring Monaco editor, file tree, draft/production toggle, iframe preview, and buttons for validation, deployment, and rollback.

**Acceptance Criteria:**
- [ ] Add route `/admin/overlay-editor`.
- [ ] Introduce NPM package: `monaco-editor`.
- [ ] Left sidebar: slug list + file tree of the selected slug.
- [ ] Center: Monaco editor with syntax highlighting based on file extension (html, css, js, json).
- [ ] Right: iframe live preview with draft/production toggle.
- [ ] Action bar: Save draft / Validate / Deploy / Rollback dropdown.
- [ ] Validation issues panel: displays errors in red and warnings in yellow; clicking jumps to the corresponding line in the editor.
- [ ] Automatically validate before deployment; block deployment if errors exist.
- [ ] Dirty state guard: display a confirmation dialog if switching files with unsaved draft changes.
- [ ] Full i18n support.
- [ ] Vitest covers file tree rendering, validation issue rendering, and deployment confirmation flows.

## Task 60 - Zip Upload Integration + Existing Endpoint Adjustments

**Description:** Adjust Phase 7C's `POST /api/overlay/custom-presets` to extract files into the `draft/` directory and return validation results from the new endpoints.

**Acceptance Criteria:**
- [ ] Redirect zip extractions to the `draft/` directory.
- [ ] On successful upload, automatically validate and return `{ slug, issues }` to the UI.
- [ ] UI displays issues and prompts: "Please fix in the Overlay Editor and deploy."
- [ ] Update existing Phase 7C integration tests to align with the new behavior.

---

### Track C — Unified Monitor Page (SPEC §4.18)

## Task 61 - /monitor Route + Dashboard Layout

**Description:** Add `/monitor` as the new default landing page. Triple-column layout for wide screens, stacked for narrow screens.

**Acceptance Criteria:**
- [ ] Register `MonitorDashboardView.vue` and configure router.
- [ ] Default route `/` redirects to `/monitor`.
- [ ] Wide screen: sidebar + main content + aside column layout.
- [ ] Narrow screen: sidebar collapses into a drawer (utilizing the ConfirmDialog focus trap).
- [ ] Header: platform connection status chip + SignalR status chip + Live/Settings toggle.
- [ ] Existing `/simulate`, `/overlay/*`, and `/admin/members` routes are preserved.

## Task 62 - Simulate Controls (sidebar)

**Description:** Componentize the event simulation features of the existing `SimulateView` and embed them into the `/monitor` sidebar.

**Acceptance Criteria:**
- [ ] Extract `SimulateControlsPanel.vue` (refactor existing `SimulateView` into a wrapper).
- [ ] Cover chat, follow, sub, giftsub, raid, bits, redeem, and check-in simulations.
- [ ] Add a "Batch Check-in" tool (N execution rounds with a progress bar).
- [ ] Display toast messages on failures (using existing ApiError handling).
- [ ] Vitest covers trigger emits and acknowledgment paths for each simulation type.

## Task 63 - Overlay Preview Iframe (main content)

**Description:** Central preview area containing an iframe displaying chat, member, or alerts overlays, with background, preset, and draft/production toggles, plus a Reload button.

**Acceptance Criteria:**
- [ ] Iframe `src` resolved dynamically: `/overlay/{hub}?preset={key}&t={ts}`.
- [ ] Hub tabs to switch preview targets (chat, member, alerts).
- [ ] Background switcher: transparent, green, pink, custom color picker, or custom image URL (sanitized following §4.14.1).
- [ ] Preset selection dropdown (reads from `GET /api/overlay/presets`).
- [ ] Support draft/production toggle for custom presets.
- [ ] Reload button (bumps timestamp).
- [ ] Maintain iframe sandbox safety boundaries (`allow-scripts allow-same-origin` required).

## Task 64 - Chat Stream Panel (aside column)

**Description:** Right column chat stream panel subscribing to `/hubs/overlay/chat`, displaying messages in a minimalist table layout with member chips (ignoring preset styles).

**Acceptance Criteria:**
- [ ] Add `ChatStreamPanel.vue`.
- [ ] Reuse the existing `useOverlayHub("chat")` composable.
- [ ] List the 50 most recent messages (timestamp, displayName, message snippet).
- [ ] Render a chip (avatar + checkInCount) if the payload contains `memberSnapshot`.
- [ ] Add a Clear button to clear local stream (does not affect backend history).
- [ ] Vitest covers correct table rendering on mock hub events.

---

### Track D — Member Editable Surface (SPEC §4.19)

## Task 65 - MemberAuditLogs Migration & Repository

**Description:** Create the `MemberAuditLogs` table, entity, and repository.

**Acceptance Criteria:**
- [ ] EF Core migration creates the table (schema details in SPEC §4.19).
- [ ] Implement `IMemberAuditLogRepository` interface and repository.
- [ ] Append-only restriction: repository only exposes `AppendAsync` and `QueryAsync` (no update/delete).
- [ ] Index configuration: `(MemberId, OccurredAt DESC)`.
- [ ] Unit tests cover append and query ordering.
- [ ] Cleanup worker: automatically deletes records exceeding `members.audit_retention_days` (default 365), aligning with the `AppLogsCleanupWorker` pattern.

## Task 66 - Member Mutation Endpoints

**Description:** Expose `PATCH /loyalty`, `POST /reset`, `DELETE /`, and `GET /audit` endpoints.

**Acceptance Criteria:**
- [ ] All mutation endpoints protected by `If-Match` etag concurrency; return 409 on mismatch.
- [ ] The `reason` field is required, ranging from 3 to 500 characters.
- [ ] `PATCH /loyalty` writes audit log with before/after JSON snapshots.
- [ ] `POST /reset` offers selective reset for loyalty/checkIn, writing to audit logs.
- [ ] `DELETE /` requires fetching a 30s disposable token via `POST /delete-token` first, passing the token in the request body.
- [ ] `GET /audit?limit&offset` paginated query endpoint.
- [ ] Loopback-only enforcement (using existing middleware).
- [ ] Integration tests cover all cases, concurrency, and token expiration.
- [ ] OpenAPI documentation updated.

## Task 67 - Member Edit UI (modals + drawer)

**Description:** Add edit modals to the Admin members page.

**Acceptance Criteria:**
- [ ] `AdjustLoyaltyModal.vue`: loyalty form + before/after diff preview + reason field.
- [ ] `ResetModal.vue`: checkboxes + reason field.
- [ ] `DeleteConfirmDialog.vue`: two-step deletion (get token → confirm execution).
- [ ] `AuditLogDrawer.vue`: right-side drawer listing audit logs in a timeline with infinite scroll.
- [ ] MembersView upgraded from read-only to containing action buttons (adjust, reset, delete) + audit history button.
- [ ] Handle 409 conflicts gracefully: display warning toast and reload member list.
- [ ] Full i18n support.
- [ ] Accessibility: correct dialog roles + focus trap (reusing ConfirmDialog).
- [ ] Vitest covers each modal flow and concurrency error handling.

## Task 68 - Workflow Audit Integration

**Description:** Configure `TriggerCheckInActionExecutor` to write to audit logs (ActorKind=workflow, ActorId=ruleId).

**Acceptance Criteria:**
- [ ] Inject `IMemberAuditLogRepository` into the executor.
- [ ] Write to audit logs after successful increments.
- [ ] Unit tests: verify mock repository receives audit calls.
- [ ] Failure paths: do not write audits if increment fails.

---

## Checkpoint: Phase 7D

- [ ] All sub-tasks for Tasks 50-68 meet their acceptance criteria.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual checks:
  - [ ] Trigger simulation check-in → `/overlay/member` displays card within 5s.
  - [ ] Create a slug in `/admin/overlay-editor` → write code → validate → deploy → load production OBS URL to verify changes.
  - [ ] Introduce broken HTML during validation → verify error blocks deployment.
  - [ ] `/monitor` triple-column layout on wide screens / stacked drawer on narrow screens, verify simulation controls immediately update overlay preview and chat stream.
  - [ ] Adjust loyalty in `AdjustLoyaltyModal` → verify audit drawer records the entry.
  - [ ] Complete two-step deletion in `DeleteConfirmDialog` → verify member is deleted.
  - [ ] Edit the same member in two tabs simultaneously → verify 409 mismatch prompts reload.
- [ ] Security review:
  - [ ] Custom preset path traversal checks (draft, production, and history).
  - [ ] Verify editor PUT/DELETE cannot write to production.
  - [ ] Ensure delete token expiration strictly enforces 30s TTL.
  - [ ] Ensure audit logs are append-only.
  - [ ] Ensure `OverlayMemberPayload` reflection whitelist is correct.
  - [ ] Ensure member mutation endpoints are loopback-only.
- [ ] `manual-verification.md` records dated entries and evidence commits.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Large Monaco editor bundle drags down admin page loading | Medium | Lazy-load the `/admin/overlay-editor` route using dynamic imports |
| Draft/production middleware rewrite conflicts with SPA routing | High | Restrict rewriting strictly to `/overlay/custom/*`, ensuring no path overlap; add integration tests |
| Frequent `MemberCheckedInEvent` overflows OverlayHub | Medium | Reuse existing `IOverlayHistoryService` capacity limits (20); verify latency via 1000-event burst benchmarks |
| Audit log write failures block mutations | Medium | Keep audit append within the database transaction; rollback mutation on audit failures to avoid drift |
| Front-end caches and reuses delete confirm tokens | Medium | Set tokens as single-use with a 30s TTL, removing them from the server cache immediately after use |
| Validation gate dependencies (AngleSharp/ExCSS/Jint) break existing build pipelines | Medium | Safe NuGet additions; isolate dependencies in the new module; ensure existing build pipelines remain unaffected |
| Zip upload migration breaks pre-existing uploaded presets | Medium | Automatically detect and migrate existing files without `production/` subdirectory upon startup; add unit tests for migrations |

---

## Out-of-Scope

- Full importer implementation for the OneComme plugin (Phase 7E)
- Multi-streamer or multi-channel data separation
- Cloud audit log synchronization
- Bulk member operations (merge / split / import)
- Monaco editor multi-user collaboration / file locking
- Integrating OBS scene management in the `/monitor` preview area
- New workflow action types like `TriggerAdjustLoyalty` / `TriggerDecreaseLoyalty`

---

## Mapping to SPEC Sections

| SPEC | Task |
| --- | --- |
| §4.14.2 CheckIn → MemberOverlay Binding | Task 50-54 |
| §4.14.3 Custom HTML Editing Pipeline | Task 55-60 |
| §4.18 Unified Monitor Page | Task 61-64 |
| §4.19 Member Editable Surface | Task 65-68 |
