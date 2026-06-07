# Feature Spec: Twitch Badge Cache & Channel-Point Reward Cache

> [← Back to Master Specification](../../SPEC.md)

### 4.23 Twitch Badge Cache & Simulator Badge UI (Phase 7E)

**Background & Motivation:**
Chat overlays must display Twitch native badges correctly (VIP / Moderator / Subscriber / Founder / custom channel badges like "Artist" or "Sponsor"). The current implementation had two defects:
1. **Broken badge icons on real Twitch paths:** The IRC parser parsed `badges` IRC tags into key strings like `subscriber/0` or `vip/1` and wrote them to `PlatformUserDisplayInfo.Badges`. `OverlayEventForwarder.ForwardChatEventAsync` broadcast these keys directly as URLs to the overlay via SignalR, causing `<img :src>` to render as the key string rather than the actual image URL.
2. **Simulator lacks badge selection:** `SimulateControlsPanel.vue` only allowed selecting `Subscriber/Moderator/VIP/Follower` text roles, rendered as `chat-role-pill` text capsules; it could not trigger badge icon paths or select custom channel badges.

References: `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchChannelApiService.cs` (`GetGlobalBadgesAsync` / `GetChannelBadgesAsync`), `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/IdentityService.cs` (`SyncBadgesAsync` / `GetBadgeUrl` caching `badge_{set}_{ver}` → URL with a 7-day TTL), `ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchMessageEnricher.cs` (parses badges KVPs into URL lists and writes to ChatMessage during message enrichment).

**Design & Specifications:**

> **Phase 7G rename (§6.1):** the platform-specific names below were generalized — `ITwitchHelixClient` → `IHelixClient`, `ITwitchBadgeCache` → `IPlatformBadgeCache`, `TwitchBadgeDescriptor` → `PlatformBadgeDescriptor` (the `TwitchBadgeCache` *implementation* class keeps its name). Read the Phase 7E names below as their current generalized equivalents.

1. **`ITwitchHelixClient` Badge Endpoints:**
   - Adds `Task<IReadOnlyDictionary<string, TwitchBadgeDescriptor>> GetGlobalBadgesAsync(CancellationToken)`: maps to `GET helix/chat/badges/global`.
   - Adds `Task<IReadOnlyDictionary<string, TwitchBadgeDescriptor>> GetChannelBadgesAsync(string broadcasterId, CancellationToken)`: maps to `GET helix/chat/badges?broadcaster_id={id}`.
   - Returned dictionary keys formatted as `{set_id}_{version}`, with `TwitchBadgeDescriptor` values containing `SetId`, `Version`, `ImageUrl1x`, `Title?`, and `Description?`.

2. **`ITwitchBadgeCache` (Application Interface) + `TwitchBadgeCache` (Infrastructure Implementation):**
   - Interface: `string? Get(string key)`, `IReadOnlyList<TwitchBadgeDescriptor> ListAll()`, `Task SyncGlobalAsync(CancellationToken)`, `Task SyncChannelAsync(string broadcasterId, CancellationToken)`, and `bool IsReady`.
   - Implementation: thread-safe `ConcurrentDictionary<string, TwitchBadgeDescriptor>`; retains old data and logs warning on sync failure.
   - Note: The cache is in-memory and must be synchronized upon application restart; no disk persistence.

3. **`TwitchBadgeSyncHostedService`:**
   - Fires-and-forget `SyncGlobalAsync` + `SyncChannelAsync(broadcasterId)` inside `StartAsync` (broadcaster ID comes from `Twitch:BroadcasterId` configuration, skipped if not set).
   - Synchronization failure does not block application startup.

4. **`OverlayEventForwarder` Badge Resolution:**
   - `ForwardChatEventAsync` and `TryResolveMemberSnapshotAsync` inject `ITwitchBadgeCache`, mapping `display?.Badges` key lists to URL lists via `cache.Get`. Keys without corresponding URLs are filtered out to prevent broken images.
   - The `OverlayChatPayload.Badges` broadcast via SignalR is corrected to carry resolved URL lists (contract schemaVersion remains unchanged).
   - Text roles are still output (`event.roles`) for future preset usage; this period's `ChatPresetDefault.vue` no longer renders text role chips.

5. **Simulator Badge/Color Passthrough:**
   - `SimulationRequest` (`SimulationKind.Message`) adds `IReadOnlyCollection<string> Badges` and `string? ColorHex`.
   - `SimulateEndpoints` accepts `badges: string[]` and `colorHex: string?` from the request body, and calls `IPlatformUserInfoCache.UpdateAsync` for the simulated user (`simulation:{userId}`) prior to calling `adapter.SimulateAsync`. This writes `Badges` + `ColorHex` to the cache, unifying resolution paths between simulated and real Twitch paths.
   - This design avoids adding badge fields to domain events, keeping the domain model clean.

6. **New API Endpoint `GET /api/twitch/badges`:**
   - Returns `{ global: TwitchBadgeDescriptor[], channel: TwitchBadgeDescriptor[] }`, allowing the frontend picker UI to list available badges.
   - Returns empty arrays with `Cache-Control: no-store` headers when the cache is not yet ready.
   - Subject to admin auth restrictions (consistent with other `/api/twitch/*` endpoints).

7. **Frontend `SimulateControlsPanel.vue` UI Refactor:**
   - `onMounted` calls `getTwitchBadges()`, grouping and displaying badges by `setId` in a stamp chip grid (`<img>` + tooltip titles); clicking toggles their inclusion in `selectedBadges`.
   - Adds a "Username Color" field: hex input paired with a real-time color swatch (defaults to `#FFCA28`).
   - Submit includes `badges: selectedBadges` and `colorHex` in the request body.
   - Removes the "Streamer Roles" text checkbox area (backward compatibility: if legacy tests pass `roles`, they are derived to corresponding badge keys backend-side; hidden from UI).

8. **Frontend `ChatPresetDefault.vue` Adjustment:**
   - Removes the `chat-role-pill` text role chip rendering section; badge icons become the sole identity indicators.
   - Badge `<img>` rendering remains as-is; adds `onerror` fallback to hide broken images.

**Acceptance Criteria:**
- Simulator sends chat messages containing badges; `/overlay/chat` displays native Twitch PNG badges rather than text chips.
- Real Twitch IRC chat messages (VIP / Moderator / Subscriber) display icons normally in overlays.
- `GET /api/twitch/badges` returns the global collection (broadcaster, moderator, vip, subscriber, founder, etc.) and custom channel badges.
- Username hex colors are applied to overlay `chat-username` style via `style="color"`.
- Cache misses do not result in broken images on overlays.
- Unit testing coverage: `TwitchBadgeCacheTests`, `OverlayEventForwarderBadgeResolutionTests`, `SimulateEndpointsBadgeTests`, and frontend `SimulateControlsPanel.test.ts`.

**Boundaries:**
- No local downloading of badge image files for base64 embedding; Twitch CDN URLs are used throughout.
- No support for uploading custom badges (only displays badges registered on Twitch).
- No integration with BTTV / 7TV / FFZ badges or emotes.
- No audit logging for badge changes.
- No automatic periodic background cache refresh (requires restart to pull new custom badges; a 24h refresh hosted service can be added in a future phase).

---


---

### 4.25 Twitch Channel-Point Reward Cache + Reward Picker UI (Phase 7G)

**Background & Motivation:**

Workflow rules with the `reward.redeemed` trigger expose a `RewardName` filter (`MatchRewardRedeemed`) that exact-matches `RewardTitle` on the published `RewardRedeemedEvent`. Pre-Phase-7G the UI rendered the field as a free-text input — operators had to retype the exact reward title themselves, which was error-prone and offered no discovery of available rewards. The simulate panel had the same problem with its `Reward ID` text input.

**Design & Specifications:**

1. **Helix listing on `IHelixClient`:**
   - `GET helix/channel_points/custom_rewards?broadcaster_id={id}` → `TwitchRewardDescriptor { Id, Title, Cost, IsEnabled, ImageUrl? }`.
   - Image URL fallback: `image.url_4x → 2x → 1x → default_image.url_4x → 2x → 1x`.
   - Required scope: `channel:read:redemptions` (already in the post-Phase-7G scope set; re-grant required on upgrade).

2. **`ITwitchRewardCache` singleton (`Vulperonex.Web.TwitchAuth`):**
   - Surface: `bool IsReady`, `DateTimeOffset? LastRefreshedAt`, `IReadOnlyList<TwitchRewardDescriptor> List()`, `Task RefreshAsync(CancellationToken)`, `void QueueRefresh()`.
   - In-memory only (no persistence); replaced wholesale on each successful refresh.
   - Broadcaster id resolution chain mirrors §4.7: `Twitch:BroadcasterId` → `SystemSettingKey.TwitchChannelName` (DB) → `Twitch:ChannelName` (config) → `IHelixClient.LookupUserAsync(channelName)`.
   - Swallow 401/403/`HttpRequestException` → leave snapshot untouched, log warning, surface `ready=false` via the endpoint. UI distinguishes "no rewards" from "not authed".

3. **OAuth integration:**
   - `TwitchAuthEndpoints.MapPost("/complete")` and `MapPost("/device/complete")` call `rewardCache.QueueRefresh()` after `badgeSync.QueueSync()` so the reward list is warmed before the operator opens the rule editor.

4. **HTTP surface (also listed in §4.13.1):**
   | Method + Path | Behavior |
   |---|---|
   | `GET /api/twitch/rewards` | Returns `{ ready, lastRefreshedAt, rewards: TwitchRewardDescriptor[] }` from the cache snapshot; no network call. |
   | `POST /api/twitch/rewards/refresh` | `await cache.RefreshAsync(ct)` then returns the same payload. Missing auth → `200 { ready:false }` rather than an error so the UI can show "authorize first" without an alert banner. |

5. **Trigger filter — dynamic options source:**
   - `FilterFieldDto` gains an optional `OptionsSource: string?` field (free-text identifier, e.g. `"twitch.rewards"`).
   - The `RewardName` field on the `reward.redeemed` trigger is declared with `OptionsSource: "twitch.rewards"`.
   - Frontend `TriggerEditor.vue` renders that field as a strict `<select>` driven by the reward cache store, with:
     - "Any" option at top (clears the filter)
     - One option per fetched reward (`value=title, label=title`)
     - A synthetic "{stored} (no longer available)" option prepended when the saved filter value is not in the current snapshot, so rules with stale titles don't lose their selection
     - A refresh button + status line ("{count} rewards · updated {time}" / "Authorize Twitch to load rewards." / error code)

6. **Simulate panel — same picker:**
   - `SimulateControlsPanel.vue` replaces the `rewardId` text input with the same `<select>` of fetched rewards (label = title, value = id).
   - On submit, the request body sends **both** `rewardId` and `rewardTitle` (resolved from the picked reward). The backend `SimulateRequest` adds an optional `RewardTitle` field; `ToSimulationRequest` falls back to `rewardId` if `rewardTitle` is absent (back-compat).
   - This fixes a latent bug: pre-Phase-7G `SimulateRequest` set both `RewardId` and `RewardTitle` to the operator's `rewardId` text input, so `MatchRewardRedeemed` (which matches by `RewardTitle`) never fired correctly on simulated events unless the operator happened to type the title into the id field.

**Acceptance Criteria:**
- Pre-OAuth: trigger filter shows "Any" + the "Authorize Twitch to load rewards." hint.
- Post-OAuth: trigger filter dropdown populates within one round-trip.
- Adding a new reward on Twitch + clicking ↻ shows it without restart.
- Saving a rule with a chosen reward then redeeming on Twitch fires the rule (existing `MatchRewardRedeemed` semantics, unchanged).
- Simulating a redemption with a chosen reward fires rules filtered by `RewardName`.

**Boundaries:**
- No icon rendering yet (Image URL is in the DTO but UI shows text only).
- No automatic refresh schedule (manual + OAuth-complete only).
- Refund action is untouched (still requires `rewardId` + `redemptionId`).

---
