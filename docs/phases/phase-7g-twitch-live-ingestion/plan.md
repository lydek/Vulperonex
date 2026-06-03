# Phase 7G — Twitch Live Ingestion (TwitchLib Hybrid) + Reward Cache

> **Status:** Shipped 2026-06-01. This plan is the **retroactive** capture of the work that landed inline during the May/June 2026 session — created as a follow-up to the 2026-06-01 spec-vs-impl review (see `docs/spec-vs-impl-2026-06-01.md` and the original report at `.claude/worktrees/spec-review-2026-06-01/docs/spec-vs-impl-2026-06-01.md`).
> **SPEC sections updated by this phase:** §2 (Tech Stack), §4.4 (system events), §4.6 (action catalog), §4.7 (transport topology + auth_failed), §4.13.1 (API table + simulate aliases + device flow), §4.22 (Advanced flag + filter-aware variable picker), §4.25 (Twitch Reward Cache — new section), §6.1 (layering exception).

## Context

Pre-Phase-7G, `Vulperonex.Adapters.Twitch` shipped a fully assembled domain pipeline (parser, dedup cache, event mapper, OAuth, Helix client) but **no live ingestion source**: `TwitchAdapter.StartAsync` registered event types and stopped there, `PublishIrcMessageAsync` / `PublishMockPayloadAsync` were `internal` and called only by tests, and the adapter wasn't even registered in DI. Production ingestion = `/api/simulate/*` only.

A first attempt during the session built a hand-rolled native `ClientWebSocket` EventSub ingestion. On operator review, the design was reworked to follow the `ref/omni-commander` proven pattern:

- **TwitchLib** instead of hand-rolled WebSocket — battle-tested session/keepalive/reconnect + typed event handlers.
- **Hybrid transport**: IRC (`TwitchLib.Client`) for chat (also covers ban/timeout/chat-cleared moderation events that EventSub chat lacks); EventSub WebSocket (`TwitchLib.EventSub.Websockets`) for alerts.
- **Auto-resolve broadcaster** from a channel login name rather than forcing the operator to type a numeric `BroadcasterId`.
- **Orchestrator** background service with retry-on-unauthorized.
- **Correct OAuth scopes** + **connection-state events** so the UI chip works.

The session also added the **Twitch channel-point Reward Cache** + reward picker UI (driven by operator feedback that the `RewardName` filter was a free-text input that no one knew what to type).

## Approach (shipped)

All TwitchLib code lives in `Vulperonex.Web` (host). The adapter project stays free of third-party SDKs — host handlers translate TwitchLib events into the adapter's existing `TwitchIrcMessage` / `TwitchMockPayload` types and call typed ingest methods. Tests stay TwitchLib-free.

### 1. NuGet (host-only)

`src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj`:
- `TwitchLib.Client 4.0.1`
- `TwitchLib.EventSub.Websockets 0.8.0`

Versions pinned in `Directory.Packages.props`. `TwitchLib.Api` was deliberately **not** added — broadcaster resolve + subscription creation reuse the existing `IHelixClient`.

### 2. Adapter — typed ingest entry points

`Vulperonex.Adapters.Twitch.TwitchAdapter`:
- `public Task IngestChatAsync(TwitchIrcMessage, TwitchDisplayCacheUpdater? override, CancellationToken)` — routes through `PublishIrcMessageAsync`.
- `public Task IngestAlertAsync(TwitchMockPayload, TwitchDisplayCacheUpdater? override, CancellationToken)` — routes through `PublishMappedEventAsync(TwitchEventMapper.Map(payload))`.
- The optional `displayCacheOverride` lets a background host supply a scoped `TwitchDisplayCacheUpdater`; the singleton adapter cannot capture the scoped user-info cache itself.

### 3. IRC chat source

`Vulperonex.Web.TwitchAuth.TwitchIrcChatSource` wraps `TwitchLib.Client.TwitchClient`:
- `ConnectAsync(channelLogin, accessToken, ct)` initializes `ConnectionCredentials(channelLogin, accessToken)` and connects.
- `OnMessageReceived` → builds `TwitchIrcMessage` tags from `chat` (`msg-id`, `user-id`, `display-name`, `color` from `HexColor`, `badges` as `set/version` comma list, `bits`) → per-message `IServiceScopeFactory` scope → resolves scoped `IPlatformUserInfoCache` → `new TwitchDisplayCacheUpdater(cache)` → `adapter.IngestChatAsync`.
- `OnConnected` / `OnDisconnected` publish `PlatformConnectionChangedEvent`.

### 4. EventSub alert source

`Vulperonex.Web.TwitchAuth.TwitchEventSubSource` wraps `EventSubWebsocketClient`:
- Typed handlers map to pure helpers in `TwitchAlertPayloadFactory` (kept socket-free for unit tests) → `TwitchMockPayload` → `adapter.IngestAlertAsync`. Covered: `ChannelFollow`, `ChannelSubscribe` (skip when `IsGift`), `ChannelSubscriptionMessage`, `ChannelSubscriptionGift`, `ChannelCheer`, `ChannelRaid`, `ChannelPointsCustomRewardRedemptionAdd`.
- `WebsocketConnected` → calls `IHelixClient.CreateEventSubSubscriptionAsync(type, version, condition, sessionId)` for each subscription type. Session-id dedup (`_lastSubscribedSessionId`) prevents resubscribing on a seamless reconnect (Twitch carries subscriptions over).
- `WebsocketConnected` / `WebsocketDisconnected` publish `PlatformConnectionChangedEvent`.

### 5. Orchestrator (BackgroundService)

`Vulperonex.Web.TwitchAuth.TwitchConnectionOrchestrator`:
- Always calls `adapter.StartAsync` (event-type registration runs even if ingestion is gated off).
- Gate: `Twitch:EventSub:Enabled` (default true) AND broadcaster resolvable.
- Broadcaster resolution order: `Twitch:BroadcasterId` (legacy override) → `SystemSettingKey.TwitchChannelName` (DB) → `Twitch:ChannelName` (config), then `IHelixClient.LookupUserAsync(channelName)` for the numeric id.
- Retry loop (10s) on auth/connect failure (omni `TwitchMonitorService` pattern). Sequence per attempt: resolve broadcaster → start IRC → start EventSub.

### 6. Scopes + config

- `Endpoints/TwitchAuthEndpoints.cs`: default scope set bumped to the omni-superset — `chat:read chat:edit channel:read:redemptions channel:manage:redemptions channel:read:subscriptions moderation:read channel:read:vips moderator:read:followers bits:read`. Operators must re-grant after upgrade.
- `appsettings.Development.json`: `Twitch:ChannelName` documented.
- New `SystemSettingKey.TwitchChannelName` (`twitch.channel_name`).

### 7. Twitch Reward Cache (SPEC §4.25)

- `IHelixClient.GetCustomRewardsAsync(broadcasterId, ct)` → `TwitchRewardDescriptor`.
- `Vulperonex.Web.TwitchAuth.TwitchRewardCache` singleton + `ITwitchRewardCache` interface. Lazy refresh (no hosted service); refresh hooks: UI `/refresh` endpoint, OAuth `/complete` + `/device/complete`.
- Endpoints: `GET /api/twitch/rewards`, `POST /api/twitch/rewards/refresh` (see `Endpoints/TwitchRewardsEndpoints.cs`).
- Trigger metadata: `FilterFieldDto.OptionsSource` field; `RewardName` declares `OptionsSource = "twitch.rewards"`.
- Frontend: `useTwitchRewardsStore` (Pinia), reward `<select>` in `TriggerEditor.vue` + `SimulateControlsPanel.vue`, refresh button, "(no longer available)" synthetic option for stale stored values.

### 8. DI

`src/Hosts/Vulperonex.Web/DependencyInjection.cs`:
- `services.AddSingleton<TwitchAdapter>()`
- `services.AddTwitchLibEventSubWebsockets()` (provides `EventSubWebsocketClient`)
- `services.AddSingleton<TwitchIrcChatSource>()` + `services.AddSingleton<IPlatformChatSender>(sp => sp.GetRequiredService<TwitchIrcChatSource>())`
- `services.AddSingleton<TwitchEventSubSource>()`
- `services.AddSingleton<ITwitchRewardCache, TwitchRewardCache>()`
- `services.AddHostedService<TwitchConnectionOrchestrator>()`

## Files

| File | Change |
|---|---|
| `Directory.Packages.props` | + TwitchLib.Client, TwitchLib.EventSub.Websockets |
| `src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj` | reference new packages |
| `src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs` | typed `IngestChatAsync` / `IngestAlertAsync` |
| `src/Vulperonex.Application/Twitch/IHelixClient.cs` | + `CreateEventSubSubscriptionAsync` + `GetCustomRewardsAsync` + `TwitchRewardDescriptor` |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchHelixClient.cs` | implement both new Helix calls (409 treated as success for sub create) |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchIrcChatSource.cs` | new — IRC via TwitchLib.Client |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchEventSubSource.cs` | new — EventSub via TwitchLib + typed handlers |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchAlertPayloadFactory.cs` | new — pure mapping helpers (unit-testable) |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchConnectionOrchestrator.cs` | new — BackgroundService orchestrator |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchRewardCache.cs` | new — singleton cache + refresh |
| `src/Hosts/Vulperonex.Web/Endpoints/TwitchRewardsEndpoints.cs` | new — GET + POST refresh |
| `src/Hosts/Vulperonex.Web/Endpoints/TwitchAuthEndpoints.cs` | scope default → omni set; `/complete` + `/device/complete` queue reward refresh |
| `src/Hosts/Vulperonex.Web/Program.cs` + `DependencyInjection.cs` | wire sources + orchestrator + cache + reward endpoints |
| `src/Vulperonex.Application/Workflows/Metadata/{IActionMetadataProvider,ActionMetadataAttribute}.cs` | + `Advanced` flag (§4.22) |
| `src/Vulperonex.Application/Workflows/Metadata/ITriggerMetadataProvider.cs` | + `OptionsSource` on `FilterFieldDto` |
| `src/Vulperonex.Infrastructure/Workflows/Metadata/TriggerMetadataProvider.cs` | `RewardName` gets `OptionsSource = "twitch.rewards"` |
| `src/frontend/src/api/client.ts` | + `optionsSource` + `advanced` on metadata types, + reward fetch helpers, + `rewardTitle` on `SimulateRequestBody` |
| `src/frontend/src/stores/twitchRewards.ts` | new Pinia store |
| `src/frontend/src/components/admin/TriggerEditor.vue` | `<select>` + refresh branch for `twitch.rewards` options source |
| `src/frontend/src/components/admin/SimulateControlsPanel.vue` | reward `<select>` + refresh + sends both `rewardId` and `rewardTitle` |
| `src/frontend/src/components/admin/WorkflowActionsEditor.vue` | basic/advanced field split + `<details>` disclosure |
| `src/frontend/src/components/admin/VariablePicker.vue` | tighten UserId filter to `*.UserId` only |
| `src/frontend/src/i18n/{en-US,zh-TW}.json` | reward strings, advanced options label, no-longer-available suffix |
| `src/frontend/src/styles/app.css` | explicit dark-mode `select`/`option`/`input` styles |
| `tests/Vulperonex.Tests.Unit/Web/TwitchRewardCacheTests.cs` | new — 4 tests for cache resolve + replace |
| `tests/Vulperonex.Tests.Unit/Web/TwitchAlertPayloadFactoryTests.cs` | new — pure mapper coverage |
| `tests/Vulperonex.Tests.Unit/Adapters/Twitch/TwitchAdapterEventTypeTests.cs` | rewritten to typed `IngestChatAsync` / `IngestAlertAsync` |

## Acceptance / verification

1. `dotnet build Vulperonex.sln` — 15 projects, 0 errors.
2. `dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj` — 284/284 passing.
3. `vue-tsc --noEmit` + `vitest run` for touched frontend suites green.
4. **Manual smoke (operator):**
   - Set `Twitch:ChannelName` or `Twitch:BroadcasterId`; complete OAuth with the new scopes (`/twitch` page → Reset Token → re-grant).
   - Run `Vulperonex.Web`. Logs show: broadcaster resolved → IRC connected → EventSub connected → 7 subscriptions registered (or 409 "already exists" treated as success).
   - Send a chat message in the channel → `/monitor` ChatStream shows it; `/overlay/chat` renders it.
   - Trigger a follow / sub / cheer / raid / redemption on Twitch → corresponding alert flows into `/overlay/alerts`.
   - Open Rule Editor → Reward Redeemed trigger → Reward Name dropdown lists rewards; clicking ↻ refreshes; "(no longer available)" appears for stale stored values.
   - Open `/monitor` Simulate → `redeem` alias → Reward dropdown populated; submitting fires a workflow rule filtered by that reward title.
5. **Connection chip:** `/twitch` UI listens for `platform.connection_changed` events; chip reflects live state. Killing network → orchestrator retry + reconnect logged.

## Risks / known follow-ups

- **`auth_failed` reason** — SPEC §4.7 calls for it; current implementation publishes only `irc_disconnected` / `eventsub_disconnected` / `null`. Tracked as a §F.2 follow-up.
- **§6.1 layering exception** — `LookupTwitchUserAction`, `RefundTwitchRedemptionAction`, `ShoutoutAction` still live in `Vulperonex.Application` (pre-existing). Tracked under §6.1 with the (a) rename-platform-neutral or (b) move-to-adapter decision pending.
- **TwitchLib 4.x API surface differs from omni-commander's 3.5.3** — verified via reflection during the session: `e.Payload.Event` (no `.Notification`), Channel args in `TwitchLib.EventSub.Core.EventArgs.Channel`, IRC chat color = `HexColor` (not `ColorHex`), disconnect arg = `OnDisconnectedArgs`. Captured in source comments.
