# Phase 7E TODO

## Backend

- [ ] `ITwitchHelixClient`：新增 `GetGlobalBadgesAsync(ct)` + `GetChannelBadgesAsync(broadcasterId, ct)` + `TwitchBadgeDescriptor(string Key, string SetId, string Version, string ImageUrl1x, string? Title, string? Description)`
- [ ] `TwitchHelixClient`：實作 GET `chat/badges/global` 與 GET `chat/badges?broadcaster_id={id}`；JSON DTO
- [ ] `Application/Twitch/ITwitchBadgeCache.cs`：`Get(key) / ListAll() / SyncGlobalAsync / SyncChannelAsync / IsReady`
- [ ] `Infrastructure/Twitch/TwitchBadgeCache.cs`：`ConcurrentDictionary<string,TwitchBadgeDescriptor>`、thread-safe sync
- [ ] `Hosts/Vulperonex.Web/Twitch/TwitchBadgeSyncHostedService.cs`：StartAsync 嘗試 SyncGlobalAsync + SyncChannelAsync（broadcaster id 取自 `Twitch:BroadcasterId` config）；錯誤吞下 + log
- [ ] DI 註冊（Vulperonex.Web/Program.cs 或 ServiceCollectionExtensions）
- [ ] `OverlayEventForwarder.ForwardChatEventAsync` + `TryResolveMemberSnapshotAsync`：注入 cache，將 `display?.Badges` 經 cache 解析為 URL list；無對應者過濾
- [ ] `SimulationKind.Message` request 增加 `Badges` (string[]) 與 `ColorHex`
- [ ] `SimulateEndpoints`：parse `badges` / `colorHex`；在 publish 前對 sim user 呼叫 `IPlatformUserInfoCache.UpsertAsync` 寫入 badges + colorHex
- [ ] 新 endpoint `GET /api/twitch/badges` → `BadgeListResponse(global: TwitchBadgeDescriptor[], channel: TwitchBadgeDescriptor[])`
- [ ] 反射白名單測試更新（新增 endpoint）

## Frontend

- [ ] `api/client.ts`：`getTwitchBadges()`、`SimulateRequestBody.badges?: string[]`、`colorHex?: string`、`TwitchBadgeDescriptor` type
- [ ] `SimulateControlsPanel.vue`：
  - [ ] onMounted fetch badges → group by setId
  - [ ] 新增「身份徽章」section：chip grid，每 chip = `<img>` + 名稱；點擊 toggle 加入 selectedBadges
  - [ ] 新增「名稱顏色」欄位：hex input + 即時 swatch
  - [ ] submit 帶 `badges` + `colorHex`
  - [ ] 移除 `Streamer Roles` 區（或保留並警示「已被徽章選擇取代」）
- [ ] `ChatPresetDefault.vue`：移除 `chat-role-pill` 渲染段
- [ ] i18n key `simulate.badges` / `simulate.nameColor`

## Tests

- [ ] `TwitchBadgeCacheTests`：sync、get、miss
- [ ] `TwitchHelixClientBadgesTests`：DTO 反序列化
- [ ] `OverlayEventForwarderBadgeResolutionTests`：key→URL、miss 過濾
- [ ] `SimulateEndpointsBadgeTests`：badges 寫入 displayCache、colorHex 透傳
- [ ] frontend `SimulateControlsPanel.test.ts`：fetch、render、submit body

## Docs

- [x] `plan.md`
- [x] `todo.md`
- [ ] `manual-verification.md`：手動驗證步驟（OAuth 後 sim → overlay 截圖）
