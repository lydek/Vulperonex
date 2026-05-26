# Phase 7C Implementation Plan: Member Card Overlay, Custom HTML Extension, Member-in-Chat

> Parent Plan: `tasks/plan.md`
> Parent Todo: `tasks/todo.md`
> Reference Sources: `docs/SPEC.md` §4.14.1 Overlay Preset Contract, `ref/Omni-Commander/OmniCommander.WebApi/wwwroot/member-card.html` (visual inspiration, not directly reused)
> Prerequisites: Phase 7B chat overlay preset contract is complete; Phase 6 Web admin UI + SystemSettingsService are available.
> Goal: Complete the default style + admin controller for the member card overlay, define the upload extension mechanism for custom HTML overlays, and provide an optional cross-hub rendering path for embedding member card data in the chat overlay.
> Boundaries: OneComme template import plugin is a separate slice (Phase 7D); this phase only lands the contract + upload infrastructure.
> Progress Source: Checkboxes in this document are for design/verification draft only; actual completion status is tracked in `todo.md`.

---

## Goals

Phase 7B has established the chat overlay preset contract. Phase 7C addresses the following gaps:

1. **Member card overlay lacks first-class design**: Phase 6 only left the `/overlay/member` skeleton. After the MVP, users need the OBS point collection card visual + adjustable background/stamp/check-in count.
2. **No extension path for custom HTML overlays**: Although Phase 7B opened up Vue preset switching, it still requires users to understand frontend compilation. Streamers usually want to drag `.html` directly into OBS without Vite/pnpm dependencies.
3. **No intersection between member-card and chat overlay**: Users expect "member card chips can be displayed next to chat messages when members leave messages", but currently chat hub and member hub are completely independent.
4. **No hooks for OneComme template ecosystem**: Although Phase 7B documentation explained the plugin path, the actual plugin scaffold and importer contract have not landed.

Phase 7C addresses (1), (2), (3) + the contract part of (4). The actual implementation of the OneComme bridge plugin is deferred to Phase 7D.

---

## Scope

### In-Scope

- Member card overlay Vue default preset (Rotan-checkin style rewrite, without reusing original copyrighted assets)
- Member card admin controller: check-in count display, card background URL, stamp URL settings
- Member card theme token mechanism (base CSS + theme override completed in prerequisite work)
- Custom HTML overlay upload mechanism: multipart endpoint + zip extraction + path traversal prevention
- `wwwroot/overlay/custom/{slug}/` hosting + admin upload UI
- Overlay preset resolver: `overlay.{hub}.preset` settings support `custom:{slug}` resolution
- Chat hub DTO extended with optional `memberSnapshot` field + reflection whitelist tests
- `overlay.chat.show_member_card` flag to control whether chat presets render the member chip
- OneComme bridge plugin contract (interface + scaffold only, implementation deferred)

### Out-of-Scope

- Full implementation of OneComme bridge plugin (Phase 7D)
- Template system for alerts overlay (deferred to Phase 7E or later)
- Third-party template marketplace
- Template hot-reloading / file watcher (deferred to future polish)
- Uploaded files executing arbitrary server-side scripts
- Cloud template synchronization

---

## Task Breakdown

## Task 44 - Member Card Overlay Default Preset (Rotan-Checkin)

**Description:** Upgrade the default visual of `/overlay/member` from a skeleton to a first-class stamp card preset, visually inspired by Rotan checkin overlay but completely rewritten (no original files reused). 10-stamp grid, gold/purple embossed borders, inline paw-print SVG, avatar + name + VIP badge.

**Acceptance Criteria:**
- [x] `MemberOverlayView.vue` renders the complete stamp card, including entrance animations and full-stamp gold flash effects.
- [x] The card is triggered when a `member` event is received via SignalR, automatically collapsing after 7s.
- [x] The number of stamps is determined by `checkInCount % stampsPerRound` for the current round, automatically rolling over to the next round.
- [x] CSS base + theme token architecture: `member-card.css` for structure, `member-card-twitch.css` for pure :root overrides.
- [x] Animation keyframes resolve colors using `var(--mc-*)` in box-shadows, enabling zero keyframe duplication for theme switching.
- [x] Stamp positions, rotations, and scales are deterministically generated using a hash seeded by (member, round, slot).

**Implementation Hints:**
- Vue presets and standalone HTML share the same CSS.
- Do not reference any original files from menber_byRotan (to avoid copyright issues).
- Red-gold theme is default, purple-gold theme serves as the token override example.

## Task 45 - Member Card Admin Controller

**Description:** Add a "Stamp Card Visual Configuration" panel to the Admin UI `/admin/members`, allowing users to configure the card background image URL and stamp image URL. Settings are persisted to SQLite via `ISystemSettingsService`.

**Acceptance Criteria:**
- [x] Add new SystemSettingKeys: `overlay.member.background_url`, `overlay.member.stamp_url`.
- [x] `/api/config` AllowedKeys whitelist includes the above keys.
- [x] MembersView renders the settings panel, including URL inputs + save button + success/error toasts.
- [x] The overlay automatically polls every 10 seconds to reflect new values after a setting changes (currently landed; will be upgraded to SignalR push later).
- [x] Full i18n support (zh-TW + en-US).
- [x] URL sanitization: scheme allowlist (`https?:` + `data:image/...`), forbids CSS url() escape characters like `()'"\;`.
- [ ] Vitest covers the sanitization logic (pending — deferred to Stage 3).

**Implementation Hints:**
- URLs injected into CSS `url()` must escape quotes via the `cssUrl()` helper.
- `setInterval` must be cleared in `onUnmounted` using `clearInterval` (to prevent memory leaks).
- Upgrading configuration change push to SignalR `system.config_changed` events is a polish item; the current 10s polling is acceptable.

## Task 46 - Custom HTML Overlay Upload Infrastructure

**Description:** Provide an upload interface for HTML/CSS/JS bundles in the Admin UI, saved to `wwwroot/overlay/custom/{slug}/`. The backend provides a multipart endpoint + zip extraction + path traversal protection. OBS can directly load `http://localhost:{port}/overlay/custom/{slug}/index.html`.

**Acceptance Criteria:**
- [ ] `POST /api/overlay/custom-presets` multipart endpoint accepting a single `.html` or `.zip`.
- [ ] Slug sanitization: `[a-z0-9-]+`, forbids `..`, absolute paths, empty strings, with a maximum length of 64.
- [ ] Zip extraction verifies path traversal for each entry (if the resolved absolute path lies outside the target directory, the entire package is rejected).
- [ ] Upload file size limit of 5MB (entire package).
- [ ] Loopback-only binding enforced (following Phase 6 security contract).
- [ ] `DELETE /api/overlay/custom-presets/{slug}` removes custom templates.
- [ ] `GET /api/overlay/custom-presets` lists installed slugs + size + upload time.
- [ ] New page in Admin UI `/admin/overlay-presets`: list + upload form + delete confirmation dialog.
- [ ] Full i18n support.
- [ ] Integration tests: zip files with path traversal attacks are rejected, oversized files return 413, invalid slugs return 400.
- [ ] Reflection tests: endpoint returns DTOs without server-internal paths.

**Implementation Hints:**
- Extraction uses `System.IO.Compression.ZipArchive`, validating each entry using `Path.GetFullPath(targetPath).StartsWith(Path.GetFullPath(rootDir))`.
- No server-side HTML sanitization is performed (HTML overlays are expected to contain scripts), but files are only loaded by loopback OBS.
- Max size is guarded by both `RequestSizeLimitAttribute` and multipart `MultipartBodyLengthLimit`.

## Task 47 - Overlay Preset Resolver Backend Route

**Description:** The backend resolves `/overlay/{hub}` requests and decides the response based on the `overlay.{hub}.preset` setting:
- Built-in preset keys (`kapchat`/`compact`/`rotan-checkin`) → return the Vue SPA route.
- `custom:{slug}` → 302 redirect to `/overlay/custom/{slug}/index.html`.
- Direct access to `/overlay/{hub}.html` bypasses resolution.

**Acceptance Criteria:**
- [ ] Add new SystemSettingKeys: `overlay.chat.preset`, `overlay.member.preset`, `overlay.alerts.preset`.
- [ ] Whitelist AllowedKeys for settings.
- [ ] Resolver endpoint: `GET /overlay/{hub}` reads settings and returns Vue SPA or 302 redirect.
- [ ] Preset key validation: unknown keys fallback to default and log a warning.
- [ ] Integration tests: verify correct routing for each (hub, preset key) combination.
- [ ] Add "Overlay Preset" settings dropdown in Admin UI (one for each of chat/member/alerts).
- [ ] Built-in preset list exposed via `GET /api/overlay/presets` endpoint (including custom slugs).

**Implementation Hints:**
- Preset registry covers both built-in (hardcoded) + custom (filesystem scan of `wwwroot/overlay/custom/`).
- 302 redirects retain query strings to support OBS cache busting (`?t=timestamp`).

## Task 48 - Member Snapshot in Chat Hub (Cross-Hub Embed)

**Description:** Add an optional `memberSnapshot` field to the Chat hub `OverlayChatEvent` DTO, automatically populated by the backend when the user is in the member system. The chat preset renders an inline member card chip based on the `overlay.chat.show_member_card` flag.

**Acceptance Criteria:**
- [ ] Add new SystemSettingKey: `overlay.chat.show_member_card` (bool, default false).
- [ ] `OverlayChatEvent` DTO includes `memberSnapshot?: MemberSnapshotDto`.
- [ ] `MemberSnapshotDto` fields exactly match the member hub whitelist (excluding `memberId`/`totalLoyalty`/`linkedPlatforms`).
- [ ] Reflection tests assert that chat hub payload contains exactly and only the expected fields.
- [ ] `OverlayModule` queries the member cache and appends the snapshot in the chat event processing path.
- [ ] ChatPresetDefault (KapChat) does not render the chip (minimalist style).
- [ ] New built-in preset `ChatPresetMemberCardEmbed` (or adding flags to existing presets) renders the inline chip: avatar + check-in count.
- [ ] Standalone HTML chat.html also supports this (sharing the contract via `OverlayCommon`).
- [ ] Vitest covers chip rendering + hiding when show_member_card=false.

**Implementation Hints:**
- Member cache queries must be cached to avoid DB hits on every chat message.
- For non-member chat, `memberSnapshot=null`, and the preset should skip gracefully.
- Chip visual: 32px circular avatar + check-in count badge, without blocking chat text.

## Task 49 - OneComme Bridge Plugin Contract (Scaffold Only)

**Description:** Reserve the contract and plugin scaffold for the future OneComme template importer plugin. The actual importer logic is deferred to Phase 7D.

**Acceptance Criteria:**
- [ ] Add new `src/Plugins/Vulperonex.Plugins.OneCommeBridge/` project (empty scaffold).
- [ ] Define `IOverlayTemplateImporter` interface under `Vulperonex.Application.Overlay.Extensions`.
- [ ] Interface method: `Task<ImportResult> ImportAsync(Stream package, string targetSlug, CancellationToken ct)`.
- [ ] Document `docs/plugins/onecomme-bridge.md` skeleton for OneComme variable mapping (`comment.name` → `displayName`, etc.).
- [ ] Project registered in the solution and builds successfully (even without plugin implementation).
- [ ] CONTRIBUTING document updated with plugin development guidelines.

**Implementation Hints:**
- The contract must use streams instead of file paths (as plugins may receive zip uploads over the network).
- `ImportResult` contains success/failure + list of warnings (which OneComme variables have no mapped equivalent).

---

## Checkpoint: Phase 7C

- [ ] All sub-tasks for Tasks 44-49 meet their acceptance criteria.
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual: Upload a OneComme-style HTML template → set `overlay.chat.preset=custom:test` → `/overlay/chat` renders the custom template.
- [ ] Browser manual: Trigger check-in event → `/overlay/member` displays the card, custom background/stamp URLs take effect.
- [ ] Browser manual: Enable `overlay.chat.show_member_card` → chip displays next to chat message when member leaves a message.
- [ ] Security review:
  - [ ] Path traversal zip attack tests PASS.
  - [ ] CSS url() injection tests PASS.
  - [ ] Member snapshot reflection whitelist PASS.
  - [ ] Upload endpoint loopback-only binding confirmed.
  - [ ] Upload file size limits enforced.
- [ ] `docs/phases/phase-7c-member-overlay-extension/manual-verification.md` records all manual verification PASS/FAIL.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Custom HTML uploads become an attack vector (path traversal, zip bomb) | High | Strict slug sanitization + entry-by-entry path validation + size limits + loopback-only binding |
| User config values injected into CSS `url()` are escaped | Medium | Scheme allowlist + quote char blacklist + string quote escaping helper |
| Adding `memberSnapshot` to chat hub DTO breaks existing Phase 6 reflection whitelist | Medium | Extend reflection tests to cover new fields, enforce whitelist in CI gate |
| Member snapshot DB queries for every chat message hit hot path performance | Medium | Extend existing `PlatformUserDisplayCache` cache mechanism to cover snapshots |
| OneComme contract gets locked too early, proving unusable during plugin implementation | Medium | Contract only defines stream + result + warning list, actual mapping is decided by the plugin |
| Maintaining both custom HTML preset and Vue preset paths incurs high sync cost | Medium | SignalR DTO serves as the single source of truth; preset rendering only reads whitelisted fields |

---

## Out-of-Scope

- Full importer implementation of OneComme bridge plugin (Phase 7D)
- Alerts overlay preset / customizer (Phase 7E or later)
- Template marketplace / cloud synchronization
- Template hot-reloading file watcher
- Uploaded templates executed in sandboxes
- Multi-user / multi-channel overlay settings separation

---

## Mapping to SPEC §4.14.1

| SPEC Element | Landed Task |
| --- | --- |
| Dual-track rendering pipeline (Vue + static HTML) | Task 44 + Task 46 |
| Preset selection precedence | Task 47 |
| HTML upload mechanism | Task 46 |
| Static HTML SignalR data contract | Task 44 (existing in overlay-common.js) + Task 48 |
| Member Card in Chat | Task 48 |
| OneComme plugin path | Task 49 (contract only) |
| Member Stamp Card Controller | Task 45 |
| URL safety | Task 45 + Task 46 |
| `twitch.client_id` namespace ADR | Already landed in Phase 6 patch, not relevant to this phase |
