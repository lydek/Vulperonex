# Phase 7E Todo

## Backend

- [x] `ITwitchHelixClient`: Add `GetGlobalBadgesAsync(ct)` + `GetChannelBadgesAsync(broadcasterId, ct)` + `TwitchBadgeDescriptor`.
- [x] `TwitchHelixClient`: Implement GET `chat/badges/global` and GET `chat/badges?broadcaster_id={id}`; JSON DTO.
- [x] `Application/Twitch/ITwitchBadgeCache.cs`.
- [x] `Hosts/Vulperonex.Web/TwitchAuth/TwitchBadgeCache.cs`: `ConcurrentDictionary`, global/channel layers, thread-safe sync, normalize `/`→`_`.
- [x] `Hosts/Vulperonex.Web/TwitchAuth/TwitchBadgeSyncHostedService.cs`: StartAsync fire-and-forget SyncGlobal + SyncChannel (broadcaster id from `Twitch:BroadcasterId`).
- [x] DI Registration.
- [x] `OverlayEventForwarder`: Inject cache, resolve badge key → URL; bypass pre-resolved http(s) URLs.
- [x] `SimulateEndpoints`: Accept `badges` / `colorHex`, update sim user via `IPlatformUserInfoCache.UpdateAsync` before publishing.
- [x] New endpoint `GET /api/twitch/badges` → `{ ready, global, channel }`.

## Frontend

- [x] `api/client.ts`: `getTwitchBadges()`, `SimulateRequestBody.badges` / `.colorHex`, `TwitchBadgeDescriptor` types.
- [x] `SimulateControlsPanel.vue`:
  - [x] Fetch badges onMounted.
  - [x] "Identity Badges" chip grid + toggle.
  - [x] "Username Color" hex input + color picker.
  - [x] Submit includes `badges` + `colorHex`.
  - [x] Remove `Streamer Roles` UI (keep `selectedRoles` programmatically but hide from UI).
- [x] `ChatPresetDefault.vue`: Remove `chat-role-pill` rendering block + hardcoded styles; hide broken images on img `onerror`.

## Tests

- [x] `TwitchBadgeCacheTests`: sync, get, miss, channel overrides global, Helix failure handling.
- [x] `OverlayEventForwarderTests`: constructor signature updated with badge cache.
- [x] `OverlayDtoWhitelistTests`: add `roles` key (fixing a pre-existing test failure).
- [x] `ExecutorExpansionTests.RecordingTwitchHelixClient`: stub badge methods.
- [x] Frontend `SimulateControlsPanel.test.ts`: fetchMock routes by URL, verify simulate calls filter.

## Docs

- [x] `plan.md`
- [x] `todo.md`
- [x] `manual-verification.md`
- [x] `docs/SPEC.md` §4.23
