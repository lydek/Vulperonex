# Phase 6 Manual Verification

> Related todo: `docs/phases/phase-6-web-ui/todo.md`

This file records manual checks that cannot be fully proven by automated tests: browser UI end-to-end flows, Photino Desktop shell behavior, Twitch OAuth full round-trip, and overlay display verification.

## Template

```markdown
## YYYY-MM-DD - <verification name>

- Verifier:
- Environment:
- Commands / Steps:
- Expected result:
- Actual result:
- Result: PASS / FAIL
- Evidence / commit:
```

## Task 20 Browser Manual Checklist

Run `dotnet run --project src/Hosts/Vulperonex.Web` (or Photino Desktop), open `http://localhost:5000/` in browser.

Required checks:

- Dashboard: API health card shows green; Twitch status card shows `clientIdConfigured` / `hasRefreshToken` / `connected` state correctly.
- Simulate panel: `chat`, `follow`, `sub` buttons send events; ack response shows `accepted`, `eventTypeKey`, `eventId`, `platformUserId`, `displayName`.
- Event monitor: SignalR envelopes appear in real time after simulate.
- Member panel: `list` / `show` work; no `seed` / `delete` buttons present.
- Rule panel: create (JSON Textarea) → enable → disable → delete full lifecycle; JSON parse error keeps textarea content + refocuses.
- Twitch panel (no ClientId): no-Twitch mode shown; auth start button disabled.
- Twitch panel (with ClientId): auth start opens system default browser to Twitch auth URL in Photino Desktop environment, and redirects within the same tab in Web UI browser environment. Both behaviors must be verified.
- Twitch reset: reset button triggers disconnect; `platform.connection_changed` drives UI to disconnected state.

## Task 20k - Twitch OAuth E2E Checklist

Requires a valid `Twitch:ClientId` in `appsettings.Development.json`.

Steps:
1. `twitch auth start` (CLI) or click auth start button (Web UI) → system browser opens Twitch authorization page (in Desktop, starts default browser; in Web UI, same-tab redirect).
2. Authorize in browser → Twitch redirects to `http://localhost:7979/auth/callback`.
3. Verify backend receives code → exchanges for token → stores encrypted refresh_token, and then performs a `302` redirect back to Web UI root (`/`) without exposing OAuth `code` or raw token data to the Web UI.
4. Verify `GET /api/twitch/status` returns `{ clientIdConfigured: true, hasRefreshToken: true, connected: true }`.
5. Verify Web UI Twitch panel auto-refreshes via `platform.connection_changed` SignalR event.
6. `twitch auth reset` (CLI) or click reset (Web UI) → verify `hasRefreshToken: false` + `connected: false`.
7. Repeat steps 1-4 to confirm round-trip is repeatable.
8. Simulate connection fallback: In browser F12 Network panel, Block WebSocket connections or simulate Offline → verify SignalR `HubConnection.onclose` is triggered → verify browser starts HTTP Polling of `/api/twitch/status` at a 30s base interval, and polling stops when reconnected.

> ⚠ Full OAuth round-trip (steps 1-4) is required for Phase 6 Gate. `auth start` opening a browser URL alone is insufficient.

## Task 22 Overlay History Persistence Checklist

Run `dotnet run --project src/Hosts/Vulperonex.Web`, open `http://localhost:5000/` in two browser tabs (tab A = `/simulate`, tab B = `/overlay/chat`).

Required checks:

1. **Cross-route persistence**: In tab A submit a chat simulate, then close tab B and reopen `/overlay/chat`. Newly opened tab must show the recently submitted message in the list (replayed via hub `OnConnectedAsync`).
2. **Cross-restart persistence**: Submit a chat simulate in tab A, stop the Web host (Ctrl+C), restart with `dotnet run --project src/Hosts/Vulperonex.Web`, reload `/overlay/chat`. The message must reappear in the list (rehydrated from `SystemSettings` `overlay.history.chat`).
3. **Alert replay does not retrigger animation**: With tab `/overlay/alerts` open, submit a `follow` simulate → live banner animates. Close and reopen `/overlay/alerts` → entry appears in list with dimmed (`data-replayed="true"`) style, **no** live banner animation.
4. **Cap enforcement**: Submit > 30 chat simulates rapidly; only the most recent 30 are visible after a fresh reload.
5. **Clear from overlay route**: Click "Clear" header button on `/overlay/chat` → confirm dialog → confirm → list empties immediately; reload page → list still empty.
6. **Clear from admin status**: Click "Clear" on the `Chat Overlay Hub` card in `/` → confirm dialog → confirm → other tabs on `/overlay/chat` receive `cleared` event and list empties without reload.
7. **Cancel and ESC dismiss confirm dialog**: Both Cancel button click and ESC key dismiss the dialog without clearing.
8. **Member hub clear**: Even though member hub has no live events during MVP, clearing must succeed and return 204 without errors.

> ⚠ All eight checks must pass for Task 22 to be considered complete and the Phase 6 Gate to clear overlay persistence requirements.

## Task 21 Desktop Shell Checklist

Run `dotnet run --project src/Hosts/Vulperonex.Desktop`.

Required checks:
- Photino window opens; Web UI loads at allocated port.
- Occupy port 5000 or 5001 → app switches to next pair (5002/5003).
- Occupy all 5 pairs (5000-5009) → Photino dialog shows no-ports error.
- No WebView2 → dialog with download link appears.
- EF Core migration failure → dialog with [Open log folder] + [Exit].
- Web host crash → fallback HTML + Restart button; Restart retries ≤ 3 times.
- Restart limit: Force Web host to crash 4 times consecutively → verify the Restart button is disabled on the 4th crash, and a clear prompt "Please restart the application manually" is shown.
- Simulate chat in Desktop UI → overlay receives event end-to-end.

---

<!-- Add verification entries below this line -->

## 2026-05-22 - Task 19 Frontend Skeleton + Overlay Wiring

- Verifier: lydek
- Environment: Windows 11, .NET 10.0.203, pnpm@9.15.4 via corepack, Chrome
- Commands / Steps:
  1. `dotnet run --project src/Hosts/Vulperonex.Web`
  2. `cd src/frontend; corepack pnpm dev`
  3. Open `http://localhost:5173/`, walk through `/` (status dashboard) and `/overlay/{chat,alerts,member}`.
  4. Submit `chat`, `follow`, `sub` from `/simulate`; observe overlay routes and admin status hub cards.
- Expected result: Vite dev server boots without error; all overlay views mount and SignalR chips show `Connected`; XSS-style payloads render as literal text.
- Actual result: Dev server ready, overlay hub cards reflect live state, simulate flows reach overlays as expected.
- Result: PASS
- Evidence / commit: a75e0b7, 0301b38

## 2026-05-22 - Task 22 Overlay History Persistence

- Verifier: lydek
- Environment: Windows 11, .NET 10.0.203, pnpm@9.15.4 via corepack, Chrome
- Commands / Steps: Eight checks under `## Task 22 Overlay History Persistence Checklist` (cross-route persistence, cross-restart rehydrate, alert replay without animation, cap enforcement, clear from overlay header, clear from admin status, dialog dismiss paths, member hub clear).
- Expected result: All eight checks behave per spec; SystemSettings rows `overlay.history.{chat,alerts,member}` persist across Web host restart; alert replay marks entries with `data-replayed="true"` and skips the live banner.
- Actual result: Persistence, replay flag gating, and clear surfaces all observed as expected; no console errors.
- Result: PASS
- Evidence / commit: c231026, 96668f8, 6b57434, 2adff68
