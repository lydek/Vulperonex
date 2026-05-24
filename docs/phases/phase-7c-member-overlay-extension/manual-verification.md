# Phase 7C Manual Verification

> Scope: Member Card Overlay default preset、Member Card admin controller、Custom HTML overlay upload、Overlay preset resolver、Member-in-Chat cross-hub embed、OneComme bridge plugin contract.
> References:
> - `docs/phases/phase-7c-member-overlay-extension/plan.md`
> - `docs/phases/phase-7c-member-overlay-extension/todo.md`
> - `docs/SPEC.md` §4.14.1

## Verification Status

| Area | Path / Component | Status | Evidence |
| --- | --- | --- | --- |
| Member card overlay default preset | `MemberOverlayView.vue` + `member-card.css` | PARTIAL | Stage 1+2 retroactive impl; visual smoke pending verifier sign-off |
| Member card CSS base + theme token | `member-card.css` (base) / `member-card-twitch.css` (override) | PARTIAL | 93% line reduction in theme file; manual theme switch smoke pending |
| Deterministic stamp util | `utils/deterministicRandom.ts` | PARTIAL | Single canonical module imported by Vue side; static HTML uses `OverlayCommon.getDeterministicRandom` |
| Member card admin settings | `MembersView.vue` + `overlay.member.background_url` / `stamp_url` keys | PARTIAL | Panel renders + i18n complete; vitest pending |
| URL sanitize | `sanitizeAssetUrl()` + `cssUrl()` in `MemberOverlayView.vue` | PARTIAL | Helper implemented; vitest pending |
| setInterval lifecycle | `MemberOverlayView.vue` `onUnmounted` | PARTIAL | clearInterval on unmount confirmed in code review |
| Custom HTML upload | `POST /api/overlay/custom-presets` | NOT STARTED | Task 46 pending |
| Preset resolver | `GET /overlay/{hub}` | NOT STARTED | Task 47 pending |
| Member snapshot in chat | `OverlayChatEvent.memberSnapshot` | NOT STARTED | Task 48 pending |
| OneComme bridge plugin contract | `Vulperonex.Plugins.OneCommeBridge` | NOT STARTED | Task 49 pending |

## Browser Manual Checklist

> All flows pending verifier sign-off. Dated PASS/FAIL entries to be appended below per project convention (see `phase-7b-chat-overlay-presets/manual-verification.md` for format).

| Flow | Expected result | Status |
| --- | --- | --- |
| Visit `/overlay/member` with no events | Card hidden, no errors in console | PENDING |
| Simulate check-in event for known member | Card slides in, displays avatar + name + N stamps (N = checkInCount % 10 or 10 if multiple of 10) | PENDING |
| Simulate 12 consecutive check-ins for same member | Cards queue, render sequentially, round counter advances (round 1: 10 stamps, round 2: 2 stamps) | PENDING |
| Set `overlay.member.background_url` to valid https URL | Card background reflects URL within 10s | PENDING |
| Set `overlay.member.background_url` to `javascript:alert(1)` | Sanitize rejects, card uses default gradient | PENDING |
| Set `overlay.member.stamp_url` to URL with `"` or `)` | Sanitize rejects (CSS injection guard) | PENDING |
| Switch member-card.html to use `member-card-twitch.css` | Card theme switches to purple/gold without structural change | PENDING |
| Navigate away from `/overlay/member` | No leaked setInterval (verify via DevTools Performance) | PENDING |
| Upload custom HTML overlay (Task 46) | Slug listed in `/admin/overlay-presets`, accessible at `/overlay/custom/{slug}/index.html` | NOT STARTED |
| Upload zip with `../../../etc/passwd` entry | Server returns 400, no files extracted outside target dir | NOT STARTED |
| Upload zip > 5MB | Server returns 413 | NOT STARTED |
| Set `overlay.chat.preset=custom:my-preset` (Task 47) | `/overlay/chat` 302 redirects to custom HTML | NOT STARTED |
| Enable `overlay.chat.show_member_card` (Task 48) | Chat overlay shows member chip inline for member chats only | NOT STARTED |

## Security Checklist

| Item | Status | Evidence |
| --- | --- | --- |
| URL scheme allowlist (`https?:` + `data:image/...`) | IMPL | `MemberOverlayView.vue` `ALLOWED_SCHEMES` regex |
| CSS url() escape character blacklist (`"`, `'`, `(`, `)`, `\`) | IMPL | `MemberOverlayView.vue` `sanitizeAssetUrl` |
| Path traversal zip protection | NOT STARTED | Task 46c |
| Loopback-only upload endpoint | NOT STARTED | Task 46e |
| Upload size cap (5MB) | NOT STARTED | Task 46d |
| Member snapshot DTO whitelist reflection test | NOT STARTED | Task 48d |
| `overlay.member.*` config keys do not require auth (admin-only via loopback) | INHERITED FROM PHASE 6 | sec gate inherited |

## Verification Commands

Frontend:
```
cd src/frontend
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
```

Backend:
```
dotnet build Vulperonex.sln -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Vulperonex.sln --no-build -m:1 -nr:false -p:UseSharedCompilation=false
```

## Dated Verification Entries

> Append entries below as work lands. Format matches `phase-6-web-ui/manual-verification.md` convention:
> ```
> ## YYYY-MM-DD - Task NN <subject>
> - Verifier: <name>
> - Environment: <OS, runtime, browser>
> - Commands / Steps: 1. ... 2. ...
> - Expected result: ...
> - Actual result: ...
> - Result: PASS / FAIL
> - Evidence / commit: <sha>
> ```

(no entries yet — Stage 3 work pending)
