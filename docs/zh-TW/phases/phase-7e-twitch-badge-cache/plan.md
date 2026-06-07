# Phase 7E 實作計畫：Twitch 徽章快取與模擬器徽章 UI

> 父層計畫：`tasks/plan.md`
> SPEC 對應：`docs/SPEC.md` §4.23
> 對照來源：`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchChannelApiService.cs`、`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/IdentityService.cs`（`SyncBadgesAsync` / `GetBadgeUrl`）、`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchMessageEnricher.cs`
> 前置條件：Phase 7D 完成。Twitch OAuth 已可取得 BroadcasterId（`Twitch:BroadcasterId` config）。
> 目標：模擬器傳送的聊天訊息與真實 Twitch IRC 訊息，在 chat overlay 上均能正確顯示 Twitch 原生徽章圖示（含 VIP / Moderator / Subscriber / Founder 等 global 徽章，以及頻道自訂徽章如「繪師」「贊助者」）。

---

## 問題

1. **真實 Twitch 路徑徽章顯示壞掉**：[OverlayEventForwarder.cs:88](../../../src/Hosts/Vulperonex.Web/SignalR/OverlayEventForwarder.cs:88) 將 `PlatformUserDisplayInfo.Badges`（IRC parser 解析出的 `subscriber/0`、`vip/1` 等 *key*）直接當 URL 丟給 overlay。`ChatPresetDefault.vue:37` 用 `<img :src="badgeUrl">` 渲染，導致 src 永遠是 `"subscriber/0"` 而非圖片。
2. **模擬器無法顯示徽章**：`SimulateControlsPanel.vue` 只能選 `Subscriber/Moderator/VIP/Follower` 文字角色，overlay 渲染為 `chat-role-pill` 文字膠囊（畫面範例：黑色 `SUBSCRIBER` `MODERATOR` `VIP` `FOLLOWER` chips），與真實 Twitch 徽章圖示不一致。
3. **缺乏自訂頻道徽章支援**：實況主自製徽章（繪師 / 贊助者 / Founder 升級版）無管道進入模擬器選擇。

## 範圍

### 內含

- `ITwitchHelixClient` 新增 `GetGlobalBadgesAsync()` 與 `GetChannelBadgesAsync(broadcasterId)`，回傳 `IReadOnlyDictionary<string, string>` key = `{set}_{ver}`、value = `image_url_1x`
- `ITwitchBadgeCache`（Application）+ `TwitchBadgeCache`（Infrastructure，in-memory `ConcurrentDictionary`）
  - `Get(string key) → string? url`
  - `SyncGlobalAsync(ct)`、`SyncChannelAsync(broadcasterId, ct)`
  - `ListAll() → IReadOnlyList<TwitchBadgeDescriptor>`（供 picker UI 列出）
- `TwitchBadgeSyncHostedService`：應用啟動 + Twitch token ready 後同步 global；channel sync 在 broadcaster id 可用時觸發
- `OverlayEventForwarder.ForwardChatEventAsync` 解析 badge key → URL；無對應 URL 時略過該 key（不再把 key 當 URL）
- `SimulationRequest` / `SimulationKind.Message` 增加 `Badges`（IReadOnlyCollection<string> keys）與 `ColorHex`（可為 null）
- `SimulateEndpoints` 接受 `badges`（string[]）與 `colorHex`；在 publish 前更新 `IPlatformUserInfoCache` 將 sim 使用者的 `Badges` 與 `ColorHex` 寫入快取，使 `OverlayEventForwarder` 解析路徑統一
- 新 endpoint `GET /api/twitch/badges`：回傳 global + channel descriptor 給前端 picker
- 前端
  - `api/client.ts`：`getTwitchBadges()`、`SimulateRequestBody` 加 `badges`、`colorHex`
  - `SimulateControlsPanel.vue` 改造：
    - 新增「身份徽章」分組，按 set group 顯示 chip 圖片，多選
    - 新增「名稱顏色」hex input + color swatch
    - 隱藏舊 `Streamer Roles` 多選框（以 badge key 派生 `StreamRole` 維持向後相容）
- 前端 overlay `ChatPresetDefault.vue` 移除 `chat-role-pill` 段（徽章與文字角色二擇一；本期改為徽章呈現）；保留 `event.roles` 欄位供 future presets。

### 不含

- 徽章圖示之檔案下載 / base64 化（仍走 CDN URL，與 Omni-Commander `GetBadgeUrl` 同策略）
- 自訂徽章上傳（僅顯示 Twitch 已註冊者）
- BTTV / 7TV emote / badge 整合
- 徽章歷史變更 audit log

## 架構

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

模擬流程：
1. 使用者於 Sim panel 勾 `vip/1` + `painter/1`，輸入 `#FFCA28`
2. POST `/api/simulate/chat` body 含 `badges: ["vip/1", "painter/1"]`, `colorHex: "#FFCA28"`
3. Endpoint 先 `IPlatformUserInfoCache.UpsertAsync(sim user, badges, colorHex)`
4. SimulationAdapter publish `UserSentMessageEvent`
5. `OverlayEventForwarder` 從 DisplayCache 讀 badges keys → `ITwitchBadgeCache.Get(...)` → URLs → 廣播
6. Overlay 顯示 `<img src="https://static-cdn.jtvnw.net/badges/...">`

## 驗收

- [ ] Sim chat 訊息含 badges 時，`/overlay/chat` 顯示 Twitch 原生徽章圖示而非文字 chips
- [ ] 真實 Twitch IRC chat 訊息（VIP / 訂閱者）overlay 圖示正常
- [ ] `GET /api/twitch/badges` 回傳 global 28 個 + channel 自訂徽章
- [ ] Picker UI 顯示徽章圖示與名稱（hover 顯示 set/version）
- [ ] 名稱顏色 hex 套用至 overlay `chat-username`
- [ ] Cache miss（key 無對應 URL）時 overlay 不出現破圖（過濾掉）
- [ ] 單元測試覆蓋：BadgeCache.Get、ForwarderResolveBadges、SimulateEndpoint upsert displayCache

## 風險

- **Helix 401**：尚未授權時 sync 失敗 → log warning，不阻擋 app 啟動；picker UI 顯示 fallback empty
- **Channel badges 無 broadcaster id**：Twitch:BroadcasterId 未設 → 僅 global 可用，picker 顯示 hint
- **Cache 過時**：自訂徽章新增後需重新啟動才可見（Phase 7E 不做後台 refresh job；Phase 7F 可加 24h refresh hosted service）
