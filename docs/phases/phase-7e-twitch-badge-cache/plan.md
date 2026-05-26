# Phase 7E Implementation Plan: Twitch Badge Cache and Simulator Badge UI

> Parent Plan: `tasks/plan.md`
> SPEC Mapping: `docs/SPEC.md` §4.23
> Reference Sources: `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchChannelApiService.cs`, `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/IdentityService.cs` (`SyncBadgesAsync` / `GetBadgeUrl`), `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchMessageEnricher.cs`
> Prerequisites: Phase 7D is complete. Twitch OAuth is able to fetch BroadcasterId (`Twitch:BroadcasterId` config).
> Goal: Ensure that simulated chat messages and real Twitch IRC messages correctly display native Twitch badge icons on the chat overlay (including global badges like VIP / Moderator / Subscriber / Founder, and channel custom badges like "Artist" or "Sponsor").

---

## Problems

1. **Broken badge display on real Twitch path**: [OverlayEventForwarder.cs:88](../../../src/Hosts/Vulperonex.Web/SignalR/OverlayEventForwarder.cs:88) directly sends `PlatformUserDisplayInfo.Badges` (parsed IRC badge *keys* like `subscriber/0`, `vip/1`) to the overlay. `ChatPresetDefault.vue:37` renders with `<img :src="badgeUrl">`, causing the src to remain `"subscriber/0"` instead of the actual image URL.
2. **Simulator cannot display badges**: `SimulateControlsPanel.vue` only offers selection of text roles like `Subscriber/Moderator/VIP/Follower`, rendered on the overlay as `chat-role-pill` text capsules (e.g., black chips for `SUBSCRIBER` `MODERATOR` `VIP` `FOLLOWER`), which is inconsistent with actual Twitch badge icons.
3. **No support for custom channel badges**: Streamers' custom badges (e.g., Artist / Sponsor / Founder upgrades) have no way to be selected in the simulator.

---

## Scope

### In-Scope

- Add `GetGlobalBadgesAsync()` and `GetChannelBadgesAsync(broadcasterId)` to `ITwitchHelixClient`, returning `IReadOnlyDictionary<string, string>` where key = `{set}_{ver}` and value = `image_url_1x`.
- `ITwitchBadgeCache` (Application) + `TwitchBadgeCache` (Infrastructure, in-memory `ConcurrentDictionary`):
  - `Get(string key) → string? url`
  - `SyncGlobalAsync(ct)`, `SyncChannelAsync(broadcasterId, ct)`
  - `ListAll() → IReadOnlyList<TwitchBadgeDescriptor>` (for the picker UI list).
- `TwitchBadgeSyncHostedService`: synchronizes global badges on startup after the Twitch token is ready; channel synchronization is triggered when the broadcaster ID becomes available.
- `OverlayEventForwarder.ForwardChatEventAsync` resolves badge keys → URLs; bypasses keys without corresponding URLs (no longer sending raw keys as URLs).
- `SimulationRequest` / `SimulationKind.Message` extended with `Badges` (IReadOnlyCollection<string> keys) and `ColorHex` (nullable).
- `SimulateEndpoints` accepts `badges` (string[]) and `colorHex`; updates `IPlatformUserInfoCache` with the simulated user's `Badges` and `ColorHex` before publishing, unifying the parsing path for `OverlayEventForwarder`.
- New endpoint `GET /api/twitch/badges`: returns global + channel descriptors for the frontend picker.
- Frontend:
  - `api/client.ts`: `getTwitchBadges()`, adds `badges` and `colorHex` to `SimulateRequestBody`.
  - `SimulateControlsPanel.vue` overhaul:
    - Add "Identity Badges" group displaying selectable chip images by set group.
    - Add "Username Color" hex input + color swatch.
    - Hide old `Streamer Roles` multi-select (mapping badge keys to `StreamRole` for backward compatibility).
  - Frontend overlay `ChatPresetDefault.vue` removes `chat-role-pill` section (choosing badges over text roles; this phase adopts badges for rendering); retains the `event.roles` field for future presets.

### Out-of-Scope

- Downloading / base64-encoding badge icons (relying on CDN URLs, matching Omni-Commander's `GetBadgeUrl` strategy).
- Custom badge uploads (displaying only badges already registered on Twitch).
- BTTV / 7TV emote / badge integrations.
- Audit logs for historical badge changes.

---

## Architecture

```
┌─────────────────────────┐
│ TwitchBadgeSyncHosted   │ on start → SyncGlobal
│ Service                 │ on broadcasterId ready → SyncChannel
└────────────┬────────────┘
             ▼
     ┌──────────────┐         ┌──────────────────────┐
     │ TwitchHelix  │ ◄───────│ ITwitchBadgeCache    │
     │ Client       │         │ (ConcurrentDictionary)│
     └──────────────┘         └─────┬────────────────┘
                                    │ Get(key)→url, ListAll()
                          ┌─────────┴─────────────┐
                          ▼                       ▼
              ┌────────────────────┐  ┌─────────────────────┐
              │ OverlayEvent       │  │ /api/twitch/badges  │
              │ Forwarder          │  │ endpoint            │
              │ (key→URL resolve)  │  │ (descriptor list)   │
              └────────────────────┘  └──────────┬──────────┘
                                                 ▼
                                        SimulateControlsPanel.vue
                                        (badge picker UI)
```

Simulation Flow:
1. User selects `vip/1` + `painter/1` in Sim panel and inputs `#FFCA28`.
2. POST `/api/simulate/chat` with body containing `badges: ["vip/1", "painter/1"]` and `colorHex: "#FFCA28"`.
3. Endpoint upserts into cache: `IPlatformUserInfoCache.UpsertAsync(sim user, badges, colorHex)`.
4. SimulationAdapter publishes `UserSentMessageEvent`.
5. `OverlayEventForwarder` reads badge keys from DisplayCache → resolves via `ITwitchBadgeCache.Get(...)` → maps to URLs → broadcasts.
6. Overlay renders `<img src="https://static-cdn.jtvnw.net/badges/...">`.

---

## Verification

- [ ] When simulated chat messages contain badges, `/overlay/chat` displays native Twitch badge images instead of text chips.
- [ ] Real Twitch IRC chat messages (VIP / Subscribers) render badge icons correctly on the overlay.
- [ ] `GET /api/twitch/badges` returns 28 global + channel custom badges.
- [ ] Picker UI displays badge icons and names (hover shows set/version).
- [ ] Username color hex is applied to the overlay `chat-username`.
- [ ] Cache misses (keys without mapped URLs) are filtered out to prevent broken image icons on the overlay.
- [ ] Unit tests cover: `BadgeCache.Get`, `ForwarderResolveBadges`, and `SimulateEndpoint` upserts to `displayCache`.

---

## Risks & Mitigations

- **Helix 401 Unauthorized**: If synchronization fails before authorization → log a warning, do not block application startup; display fallback empty states in the picker UI.
- **Channel badges missing broadcaster ID**: If `Twitch:BroadcasterId` is unset → only global badges are available, display a hint in the picker.
- **Outdated Cache**: Newly added custom badges require a restart to become visible (no background refresh job is implemented in Phase 7E; Phase 7F can add a 24h refresh hosted service).
