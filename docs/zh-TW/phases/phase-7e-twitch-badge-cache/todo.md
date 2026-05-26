# Phase 7E TODO

## Backend

- [x] `ITwitchHelixClient`：新增 `GetGlobalBadgesAsync(ct)` + `GetChannelBadgesAsync(broadcasterId, ct)` + `TwitchBadgeDescriptor`
- [x] `TwitchHelixClient`：實作 GET `chat/badges/global` 與 GET `chat/badges?broadcaster_id={id}`；JSON DTO
- [x] `Application/Twitch/ITwitchBadgeCache.cs`
- [x] `Hosts/Vulperonex.Web/TwitchAuth/TwitchBadgeCache.cs`：`ConcurrentDictionary`、global/channel 二層、thread-safe sync、normalize `/`→`_`
- [x] `Hosts/Vulperonex.Web/TwitchAuth/TwitchBadgeSyncHostedService.cs`：StartAsync fire-and-forget SyncGlobal + SyncChannel（broadcaster id 取自 `Twitch:BroadcasterId`）
- [x] DI 註冊
- [x] `OverlayEventForwarder`：注入 cache，badge key → URL 解析；http(s) 預先解析者直接通過
- [x] `SimulateEndpoints`：accept `badges` / `colorHex`，在 publish 前 `IPlatformUserInfoCache.UpdateAsync` 寫入 sim user
- [x] 新 endpoint `GET /api/twitch/badges` → `{ ready, global, channel }`

## Frontend

- [x] `api/client.ts`：`getTwitchBadges()`、`SimulateRequestBody.badges` / `.colorHex`、`TwitchBadgeDescriptor` type
- [x] `SimulateControlsPanel.vue`：
  - [x] onMounted fetch badges
  - [x] 「身份徽章」 chip grid + toggle
  - [x] 「名稱顏色」 hex input + color picker
  - [x] submit 帶 `badges` + `colorHex`
  - [x] 移除 `Streamer Roles` UI（程式內 `selectedRoles` 保留但無 UI）
- [x] `ChatPresetDefault.vue`：移除 `chat-role-pill` 渲染段 + 死樣式；img `onerror` 隱藏破圖

## Tests

- [x] `TwitchBadgeCacheTests`：sync、get、miss、channel shadow global、Helix 失敗吞下
- [x] `OverlayEventForwarderTests`：ctor 簽章更新含 badge cache
- [x] `OverlayDtoWhitelistTests`：補上 `roles` key（pre-existing failure 順手修）
- [x] `ExecutorExpansionTests.RecordingTwitchHelixClient`：補上 badge 方法
- [x] frontend `SimulateControlsPanel.test.ts`：fetchMock route by URL，simulate calls 過濾

## Docs

- [x] `plan.md`
- [x] `todo.md`
- [x] `manual-verification.md`
- [x] `docs/SPEC.md` §4.23
