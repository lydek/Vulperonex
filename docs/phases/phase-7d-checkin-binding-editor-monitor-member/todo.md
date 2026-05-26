# Phase 7D Todo: CheckIn Binding, Custom HTML Editor, Unified Monitor Page, Member Editable Surface

> Corresponding Plan: `docs/phases/phase-7d-checkin-binding-editor-monitor-member/plan.md`
> Parent Todo: `tasks/todo.md`
> SPEC: `docs/SPEC.md` §4.14.2, §4.14.3, §4.18, §4.19

## Track A — CheckIn → MemberOverlay Binding

### Task 50 - MemberCheckedInEvent Domain Event

- [x] Task 50a: Define `MemberCheckedInEvent` record under `Vulperonex.Domain.Events`.
- [x] Task 50b: Implement `IStreamEvent` (EventId / OccurredAt / Platform / EventTypeKey).
- [x] Task 50c: Register `EventTypeKey = "system.member.checked_in"` in `StreamEventTypeRegistry`.
- [x] Task 50d: Unit tests cover construction + EventTypeKey consistency.

### Task 51 - TriggerCheckInActionExecutor Emits Event

- [x] Task 51a: Inject `IStreamEventBus` into the executor.
- [x] Task 51b: Calculate RoundIndex / StampSlotInRound (using `overlay.member.stamps_per_round` setting).
- [x] Task 51c: Publish event after successful increment (after transaction commits).
- [x] Task 51d: Do not publish on failure paths.
- [x] Task 51e: Unit tests mock event bus to verify publish calls.

### Task 52 - OverlayEventForwarder Subscribes to MemberCheckedInEvent

- [x] Task 52a: Inject `IHubContext<OverlayMemberHub>` + `IOverlayHistoryService<OverlayMemberPayload>`.
- [x] Task 52b: Subscribe using `stream.OfType<MemberCheckedInEvent>().Subscribe(...)`.
- [x] Task 52c: Implement `ForwardMemberCheckInEventAsync` using `SafeSendAsync` + `TryPersistAsync`.
- [x] Task 52d: Integration tests: publish event → hub group receives it + history is queryable.

### Task 53 - OverlayMemberPayload Expansion + Reflection Tests

- [x] Task 53a: Add `RoundIndex` + `StampSlotInRound` to the payload.
- [x] Task 53b: Update `OverlayDtoWhitelistTests` to include the exact key set.
- [x] Task 53c: Ensure `memberId/totalLoyalty/linkedPlatforms` are excluded.
- [x] Task 53d: Synchronize types in frontend `useOverlayHub.ts`.
- [x] Task 53e: Update `MemberOverlayView` to read `RoundIndex` from the payload.

### Task 54 - CLI simulate checkin Publishes Event

- [x] Task 54a: Update CLI subcommand to use the publish path.
- [x] Task 54b: Existing CLI integration tests pass.
- [x] Task 54c: Update `docs/cli.md` (if it exists).

---

## Track B — Custom HTML Editor

### Task 55 - Draft/Production Directory Refactoring

- [x] Task 55a: `OverlayPresetStore` supports `draft/`, `production/`, and `history/` subdirectories.
- [x] Task 55b: Middleware `RewritePath` redirects `/overlay/custom/{slug}/*` → `production/`.
- [x] Task 55c: Preview path `/overlay/custom/{slug}/draft/*` bypasses rewriting.
- [x] Task 55d: Redirect zip upload extractions to `draft/`.
- [x] Task 55e: Startup migration: detect existing presets missing `production/` and move them to `production/`.
- [x] Task 55f: Phase 7C integration tests remain green.

### Task 56 - Files API Endpoints

- [x] Task 56a: `GET /api/overlay/custom-presets/{slug}/files`
- [x] Task 56b: `GET /files/{path}?env=draft|production`
- [x] Task 56c: `PUT /files/{path}` (draft only)
- [x] Task 56d: `DELETE /files/{path}` (draft only)
- [x] Task 56e: Path sanitization + server-side validation of the resolved absolute path.
- [x] Task 56f: Single file limit of 2MB / total size limit per slug of 10MB.
- [x] Task 56g: Binary uploads are rejected (returns 400).
- [x] Task 56h: Integration tests cover all cases.

### Task 57 - Validation Gate

- [x] Task 57a: Add NuGet packages `AngleSharp` / `ExCSS` / `Jint` (ask-first).
- [x] Task 57b: HTML parse errors trigger issues.
- [x] Task 57c: CSS parse errors trigger issues.
- [x] Task 57d: JS parse errors trigger issues.
- [x] Task 57e: SignalR contract probe (regex).
- [x] Task 57f: Hub URL reference checks.
- [x] Task 57g: External URL warnings.
- [x] Task 57h: File size issues.
- [x] Task 57i: Define Issue DTO (severity/code/message/filePath?/line?).
- [x] Task 57j: `POST /validate` endpoint.
- [x] Task 57k: Unit tests cover each issue type.
- [x] Task 57l: Integration tests cover valid + invalid samples.

### Task 58 - Deploy / Rollback / History

- [x] Task 58a: `POST /deploy` workflow: validate → write history → atomic copy.
- [x] Task 58b: Atomic copy implementation (temp dir + rename).
- [x] Task 58c: History rotation (retain the 10 most recent entries).
- [x] Task 58d: Restore via `POST /rollback?to={ts}`.
- [x] Task 58e: `GET /history` lists entries.
- [x] Task 58f: Concurrent deployments to the same slug return 409.
- [x] Task 58g: Integration tests cover the complete flow.

### Task 59 - Admin Overlay Editor UI

- [x] Task 59a: Add route `/admin/overlay-editor`.
- [x] Task 59b: Add NPM package `monaco-editor` (ask-first).
- [x] Task 59c: Left sidebar: slug list + file tree.
- [x] Task 59d: Center: Monaco editor with syntax highlighting based on file extension.
- [x] Task 59e: Right: iframe preview + draft/production toggle.
- [x] Task 59f: Action bar: Save / Validate / Deploy / Rollback.
- [x] Task 59g: Validation issues panel + jump to line.
- [x] Task 59h: Validate automatically before deployment; block on errors.
- [x] Task 59i: Dirty state confirmation dialog.
- [x] Task 59j: Full i18n support.
- [x] Task 59k: Vitest covers the core flow.

### Task 60 - Zip Upload Integration

- [x] Task 60a: Redirect Phase 7C `POST /api/overlay/custom-presets` extractions to `draft/`.
- [x] Task 60b: Automatically run validation after upload.
- [x] Task 60c: Return `{ slug, issues }` to the UI.
- [x] Task 60d: Rewrite Phase 7C integration tests.
- [x] Task 60e: UI displays issues + guides to Overlay Editor.

---

## Track C — Unified Monitor Page

### Task 61 - /monitor Dashboard Layout

- [x] Task 61a: Register `MonitorDashboardView.vue` and configure router.
- [x] Task 61b: Default route `/` redirects to `/monitor`.
- [x] Task 61c: Wide screen triple-column layout.
- [x] Task 61d: Narrow screen sidebar collapses into a drawer (reusing ConfirmDialog focus trap).
- [ ] Task 61e: Header displays connection/SignalR status chips + Live/Settings toggle.
- [x] Task 61f: Preserve existing routes.

### Task 62 - Simulate Controls Panel

- [x] Task 62a: Extract `SimulateControlsPanel.vue` as a shared component.
- [x] Task 62b: Refactor existing `SimulateView` into a wrapper.
- [x] Task 62c: Batch check-in tool (N times + progress bar).
- [x] Task 62d: Failed alerts utilize existing ApiError handling.
- [x] Task 62e: Vitest covers emit + ack paths for each simulation.

### Task 63 - Overlay Preview Iframe

- [x] Task 63a: Dynamic assembly of iframe `src`.
- [x] Task 63b: Hub tabs to switch targets (chat / member / alerts).
- [x] Task 63c: Background switcher (5 options).
- [x] Task 63d: Preset dropdown (reads from `GET /api/overlay/presets`).
- [x] Task 63e: Custom preset draft/production toggle.
- [x] Task 63f: Reload button (bumps timestamp).
- [x] Task 63g: Maintain iframe sandbox attributes.

### Task 64 - Chat Stream Panel

- [x] Task 64a: Add `ChatStreamPanel.vue`.
- [x] Task 64b: Reuse the `useOverlayHub("chat")` composable.
- [x] Task 64c: List the 50 most recent messages (timestamp / displayName / message snippet).
- [x] Task 64d: Render member chip if `memberSnapshot` is present.
- [x] Task 64e: Add Clear button (does not affect history).
- [x] Task 64f: Vitest covers mock hub event.

---

## Track D — Member Editable Surface

### Task 65 - MemberAuditLogs Migration & Repository

- [x] Task 65a: EF Core migration creates the table (schema details in SPEC §4.19).
- [x] Task 65b: Define and implement `IMemberAuditLogRepository`.
- [x] Task 65c: Enforce append-only restriction (no update / delete).
- [x] Task 65d: Configure index `(MemberId, OccurredAt DESC)`.
- [x] Task 65e: Unit tests cover append and query.
- [x] Task 65f: Cleanup worker executes deletion based on `members.audit_retention_days` (default 365, using the `AppLogsCleanupWorker` pattern).

### Task 66 - Member Mutation Endpoints

- [x] Task 66a: `PATCH /api/members/{id}/loyalty`
- [x] Task 66b: `POST /api/members/{id}/reset`
- [x] Task 66c: `POST /api/members/{id}/delete-token`
- [x] Task 66d: `DELETE /api/members/{id}`
- [x] Task 66e: `GET /api/members/{id}/audit?limit&offset`
- [x] Task 66f: `If-Match` etag concurrency (based on `UpdatedAt` ticks hash).
- [x] Task 66g: `reason` validation (3-500 characters).
- [x] Task 66h: Loopback-only middleware enforcement.
- [x] Task 66i: Write audit logs on every mutation.
- [x] Task 66j: Integration tests cover all cases, concurrency, and token expiration.
- [x] Task 66k: OpenAPI documentation updated.

### Task 67 - Member Edit UI

- [x] Task 67a: `AdjustLoyaltyModal.vue` (before/after diff + reason).
- [x] Task 67b: `ResetModal.vue` (checkboxes + reason).
- [x] Task 67c: `DeleteConfirmDialog.vue` (two-step deletion + token).
- [x] Task 67d: `AuditLogDrawer.vue` (timeline + infinite scroll).
- [x] Task 67e: Upgraded MembersView includes action buttons.
- [x] Task 67f: 409 conflict toast + auto reload.
- [x] Task 67g: Full i18n support.
- [x] Task 67h: Accessibility (dialog roles + focus trap).
- [x] Task 67i: Vitest covers each modal flow.

### Task 68 - Workflow Audit Integration

- [x] Task 68a: Inject `IMemberAuditLogRepository` into `TriggerCheckInActionExecutor`.
- [x] Task 68b: Write audits after successful increments (ActorKind=workflow / ActorId=ruleId).
- [x] Task 68c: Unit tests verify mock repository receives audit calls.
- [x] Task 68d: Do not write audits on failure paths.

---

## Track E — Module & Plugin Management System

### Task 69 - Module & Plugin State Persistence & API Integration

- [x] Task 69a: Support switch status settings via `modules.enabled.{name}` keys.
- [x] Task 69b: Implement `GET /api/plugins-modules` to return status and dependencies.
- [x] Task 69c: Implement `POST /api/plugins-modules/{name}/toggle` to transition status and raise `SettingChangedEvent`.
- [x] Task 69d: Log state transitions to system audits (`ActorKind='user'`, `Operation='disable_module'`).
- [x] Task 69e: Unit and integration tests cover API correctness.

### Task 70 - Topological Chaining for Module Dependencies

- [x] Task 70a: Backend implements topological dependency analysis and cascading status updates.
- [x] Task 70b: Disabling `MemberModule` cascade-disables `CheckInModule` and `LotteryModule`.
- [x] Task 70c: Enabling `CheckInModule` prompts auto-enabling of or blocks on disabled `MemberModule`.
- [x] Task 70d: Frontend switch integration exhibits the affected module lists in a confirmation dialog.
- [x] Task 70e: Test cascading activation and shutdown logics to prevent invalid states.

### Task 71 - Dynamic Enable/Disable and No-Op Design for Hosted Services

- [x] Task 71a: Hosted Services detect corresponding module switch status on startup.
- [x] Task 71b: Subscribe to `SettingChangedEvent`, unsubscribe from EventBus, and switch to No-Op when status becomes `false`.
- [x] Task 71c: `IWorkflowActionExecutor` throws `DependencyMissingException` if its module is disabled.
- [x] Task 71d: Unit tests mock service state to verify workflow action prevention.

### Task 72 - Module Management UI Page

- [x] Task 72a: Create the "Modules & Plugins" tab under `/admin/settings`.
- [x] Task 72b: Grid layout featuring core Hosted Services and OneComme plugins.
- [x] Task 72c: Switch controls integrated with dependency warnings.
- [x] Task 72d: Vitest covers module card rendering and toggle interactions.

---

## Track F — Extended Event & Loyalty Simulation

### Task 73 - Extended Event Simulation with Stream Roles (StreamRole Flags)

- [x] Task 73a: `SimulateRequest` DTO `Roles` field supports array parsing.
- [x] Task 73b: Map string arrays to correct `StreamRole` flags.
- [x] Task 73c: Pass flags to user contexts in simulated events.
- [x] Task 73d: Unit tests cover role combinations and boundary parsing.

### Task 74 - Check-in & Loyalty Simulation Endpoints

- [x] Task 74a: Implement `POST /api/simulate/checkin` endpoint.
- [x] Task 74b: Accept `platformUserId`, `displayName`, `skipCooldown`, and `stampCount`.
- [x] Task 74c: Call `IMemberResolver` and `IMemberStreamStateRepository` to increment check-ins.
- [x] Task 74d: Publish `MemberCheckedInEvent` to the EventBus upon successful increments.
- [x] Task 74e: Integration tests verify mock endpoints and state transitions.

### Task 75 - Simulation UI Extensions (SimulateControlsPanel)

- [x] Task 75a: `SimulateControlsPanel.vue` adds a role multi-select checkbox group.
- [x] Task 75b: Add specialized forms for loyalty and check-in simulations.
- [x] Task 75c: Simulate buttons invoke `POST /api/simulate/checkin` API.
- [x] Task 75d: Vitest covers interaction tests for simulation controls.

---

## Track G — Visual Workflow Rules UI

### Task 76 - Visual Condition Builder Frontend Component

- [x] Task 76a: Implement `ConditionBuilder.vue` featuring line editing instead of plain text.
- [x] Task 76b: Support dropdown selectors for `[Variable]`, `[Operator]`, and `[Target Value]`.
- [x] Task 76c: Fetch metadata from `StreamEventTypeRegistry` to prevent invalid variables.
- [x] Task 76d: Frontend compiles visual conditions into NCalc expression strings automatically.
- [x] Task 76e: Vitest covers condition addition/removal and expression generation.

### Task 77 - Strongly-Typed Dynamic Action Form

- [x] Task 77a: Implement `DynamicActionForm.vue`.
- [x] Task 77b: Read back-end `ActionParameterMetadata` to generate strongly-typed input elements.
- [x] Task 77c: Replace free-text JSON with sliders, toggles, selectors, and other inputs.
- [x] Task 77d: Vitest covers component mappings and parameter type renders.

### Task 78 - Variable Picker Panel

- [x] Task 78a: Implement floating variable Picker panel activated by clicking or typing `{`.
- [x] Task 78b: Display available variables for the selected event and global variables.
- [x] Task 78c: Clicking inserts the variable in `{user.name}` format at the cursor position.
- [x] Task 78d: Vitest covers cursor positioning and text insertions.

---

## Checkpoint: Phase 7D

- [ ] All sub-tasks for Tasks 50-78 are completed with `[x]` self-check — Task 61e and Browser manual matrix pending.
- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` — 2026-05-26 EXIT=0
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` — 2026-05-26 453/453 EXIT=0
- [x] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint` — 2026-05-26 vue-tsc EXIT=0, vitest 191/191, build EXIT=0; lint residue in vendor `public/overlay/libs/vue.global.js`, not introduced in this phase.
- [ ] Browser manual checks all PASS (defined in `plan.md` Checkpoint area).
- [ ] Security review all PASS — security checklist needs evidence synchronized in `manual-verification.md`.
- [x] `manual-verification.md` records dated entries and evidence commits — 2026-05-26 dated entries recorded (backend regression fix + monitor redesign).
