# Phase 7C Manual Verification

## Status Matrix

| Area | Status | Evidence |
| --- | --- | --- |
| Member card overlay preset | PARTIAL | `MemberOverlayView.vue` rebuilt and `vite build` passed |
| Member admin settings | PARTIAL | config endpoints + admin panel landed |
| URL sanitize helper | PASS | `src/utils/overlayAssetUrl.test.ts` |
| Custom HTML upload backend | PASS | `Phase7cOverlayPresetTests` |
| Preset resolver backend | PASS | `Phase7cOverlayPresetTests` |
| Member snapshot in chat payload | PASS | `SignalRHubTests` + `OverlayDtoWhitelistTests` |
| Chat member chip presets | PASS | `ChatOverlayView.test.ts` |
| OneComme bridge contract | PASS | plugin csproj + contract + docs |

## Commands Run

Frontend:

```powershell
cd src/frontend
corepack pnpm vue-tsc --noEmit
.\node_modules\.bin\vitest.cmd run src/views/overlay/ChatOverlayView.test.ts src/utils/overlayAssetUrl.test.ts
.\node_modules\.bin\vite.cmd build
```

Backend:

```powershell
dotnet build src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false
dotnet build src/Plugins/Vulperonex.Plugins.OneCommeBridge/Vulperonex.Plugins.OneCommeBridge.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false
dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~OverlayDtoWhitelistTests|FullyQualifiedName~SystemSettingKeyTests" /m:1 /nr:false /p:UseSharedCompilation=false
dotnet test tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Phase7cOverlayPresetTests|FullyQualifiedName~SignalRHubTests" /m:1 /nr:false /p:UseSharedCompilation=false
```

## Outstanding Manual Checks

| Flow | Expected result | Status |
| --- | --- | --- |
| Visit `/overlay/member` with no events | card hidden, no runtime error | PENDING |
| Simulate member check-in burst | queue animates sequentially | PENDING |
| Upload custom preset through admin page | slug appears in list and custom URL opens | PENDING |
| Set `overlay.chat.preset=custom:{slug}` | `/overlay/chat` redirects to custom HTML | PENDING |
| Toggle `overlay.chat.show_member_card` | chat member chip shows/hides correctly in browser | PENDING |

## Known Blockers

- Full `dotnet build Vulperonex.sln` still fails on pre-existing syntax errors in `src/Hosts/Vulperonex.Desktop/Program.cs`.
- Full frontend `pnpm test` still fails on pre-existing `MembersView` / `TwitchAuthView` tests.
- Full frontend `pnpm lint` still fails on pre-existing legacy static overlay assets under `public/overlay/**`.

## Dated Entry

## 2026-05-24 - Phase 7C implementation pass

- Verifier: Codex
- Environment: Windows, .NET 10 SDK, Vite 7
- Commands / Steps:
  1. Added backend custom preset upload/list/delete/resolver APIs
  2. Added member snapshot payload and system config changed broadcast
  3. Added frontend overlay preset admin page, member chip preset, sanitize helper test
  4. Added OneComme bridge scaffold and docs
- Expected result: Phase 7C code paths compile and targeted tests pass
- Actual result: targeted web/plugin builds and targeted unit/integration/frontend tests passed; full solution/frontend suites still blocked by unrelated pre-existing failures
- Result: PARTIAL
