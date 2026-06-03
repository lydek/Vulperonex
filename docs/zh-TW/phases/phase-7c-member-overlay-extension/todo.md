# Phase 7C Todo

## Task 44 - Member Card Overlay

- [x] static `member-card.html` default preset works as the member overlay surface
- [x] Member hub event queue drives the card animation flow
- [x] Deterministic stamp randomizer extracted into `utils/deterministicRandom.ts`
- [x] CSS base/theme split for member card assets
- [x] Standalone `wwwroot/overlay/member-card.html` kept as static reference path

## Task 45 - Member Card Admin Controller

- [x] `overlay.member.background_url`
- [x] `overlay.member.stamp_url`
- [x] `/api/config` allowlist updated
- [x] Members admin panel saves both settings
- [x] URL sanitize helper covered by vitest
- [x] `system.config_changed` broadcast added

## Task 46 - Custom HTML Overlay Upload

- [x] `POST /api/overlay/custom-presets`
- [x] slug validation `[a-z0-9-]{1,64}`
- [x] zip path traversal rejection
- [x] upload and expanded size cap `5 MB`
- [x] loopback-only enforcement
- [x] `DELETE /api/overlay/custom-presets/{slug}`
- [x] `GET /api/overlay/custom-presets`
- [x] admin upload/list/delete page
- [x] i18n copy for the admin page
- [x] integration tests for invalid slug / large file / traversal / redirect flow
- [x] DTOs avoid exposing server-internal paths

## Task 47 - Overlay Preset Resolver

- [x] `overlay.chat.preset`
- [x] `overlay.member.preset`
- [x] `overlay.alerts.preset`
- [x] config allowlist updated
- [x] `GET /overlay/{hub}` resolver
- [x] `GET /api/overlay/presets`
- [x] admin preset settings page
- [x] redirect keeps query string

## Task 48 - Member Snapshot In Chat

- [x] `overlay.chat.show_member_card`
- [x] `OverlayChatPayload.memberSnapshot`
- [x] whitelist test updated
- [x] chat event path resolves member snapshot through member query + display cache
- [x] `ChatPresetMemberCardEmbed`
- [x] default chat preset can render member chip
- [x] standalone `wwwroot/overlay/chat.html` can render member chip
- [x] vitest covers chip rendering and sanitize helper
- [ ] 1000-event burst observation benchmark

## Task 49 - OneComme Bridge Contract

- [x] `src/Plugins/Vulperonex.Plugins.OneCommeBridge/`
- [x] `IOverlayTemplateImporter`
- [x] `OverlayTemplateImportResult`
- [x] `docs/plugins/onecomme-bridge.md`
- [x] solution registration
- [x] `CONTRIBUTING.md` plugin contribution note

## Verification

- [x] `dotnet build src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj --no-restore`
- [x] `dotnet build src/Plugins/Vulperonex.Plugins.OneCommeBridge/Vulperonex.Plugins.OneCommeBridge.csproj --no-restore`
- [x] `dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~OverlayDtoWhitelistTests|FullyQualifiedName~SystemSettingKeyTests"`
- [x] `dotnet test tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Phase7cOverlayPresetTests|FullyQualifiedName~SignalRHubTests"`
- [x] `corepack pnpm vue-tsc --noEmit`
- [x] targeted verification: `MonitorOverlayPanel.test.ts`, `overlayAssetUrl.test.ts`
- [x] `vite build`
- [ ] full `dotnet build Vulperonex.sln`
- [ ] full `dotnet test Vulperonex.sln --no-build`
- [ ] full `pnpm test`
- [ ] full `pnpm lint`

## Current Blockers

- `src/Hosts/Vulperonex.Desktop/Program.cs` has pre-existing syntax errors, so full solution build is not green yet.
- Full frontend `pnpm test` still has pre-existing failures in `MembersView` and `TwitchAuthView`.
- Full frontend `pnpm lint` still scans legacy static overlay assets under `public/overlay/**` and reports many pre-existing issues.
