# Phase 7E Manual Verification

## Prerequisites

1. Twitch OAuth is complete (`/api/twitch/auth/status` returns `hasRefreshToken: true`).
2. `appsettings.json` or environment variable `Twitch__BroadcasterId` is set to the actual broadcaster ID (obtain via `curl -H "Authorization: Bearer <token>" -H "Client-Id: <id>" https://api.twitch.tv/helix/users?login=<your_login>`).
3. Start the application: `dotnet run --project src/Hosts/Vulperonex.Web`, and wait for the following logs to appear:
   - `Synced N global Twitch badges.`
   - `Synced M channel Twitch badges for {BroadcasterId}.` (if BroadcasterId is set)

## 1. `/api/twitch/badges` Endpoint

```bash
curl http://localhost:5000/api/twitch/badges | jq '.ready, (.global | length), (.channel | length)'
```

Expected:
- `ready: true`
- `global` contains around 25–35 entries (including broadcaster / moderator / vip / subscriber / founder / premium, etc.)
- `channel` contains the count of custom channel badges (e.g. Artist / Sponsor), or 0 if BroadcasterId is not configured.

## 2. Simulator Badge Picker

1. Open `/admin/simulate` (or `/monitor`).
2. The "Identity Badges" section displays a badge chip grid, each chip containing the badge icon and name.
3. Check `VIP` + `Moderator` + a custom badge (e.g., Artist).
4. Change "Username Color" to `#FFCA28`, verifying the color swatch updates immediately.
5. Keep alias as `chat`, type "testing badge display", and send the message.

## 3. Chat Overlay Display

1. Open `/overlay/chat` in a separate browser tab.
2. When the message arrives, verify:
   - Real badge icons (PNG) appear before the display name, instead of text chips.
   - Display name text is colored `#FFCA28`.
   - No `SUBSCRIBER` / `MODERATOR` text capsules are rendered.
3. Open DevTools Network tab to confirm `<img>` successfully loaded `static-cdn.jtvnw.net/badges/v1/...png`.

## 4. Real Twitch IRC Messages

1. Send messages in the actual Twitch chat room using a VIP / Moderator / Subscriber account.
2. Verify `/overlay/chat` displays the corresponding badge icons without broken images (cache misses must silently hide `<img>`).

## 5. Cache Miss Mitigation

1. Force-simulate a message with an invalid badge key using `curl`:
   ```bash
   curl -X POST http://localhost:5000/api/simulate/chat \
     -H "Content-Type: application/json" \
     -d '{"displayName":"Sim","message":"hi","badges":["bogus_99"]}'
   ```
2. Verify the message appears on the overlay without any badge icons, and no 404 image load errors appear in the console.

## 6. Restart Sync

1. Stop and restart the web application.
2. Confirm logs reprint `Synced N global Twitch badges`.
3. Verify `GET /api/twitch/badges` immediately returns data (loaded at startup, not on-demand).
