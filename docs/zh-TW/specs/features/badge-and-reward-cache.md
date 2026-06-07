# 功能規格書：Twitch 徽章與頻道點數獎勵快取

> [← Back to Master Specification](../../SPEC.md)

### 4.23 Twitch 徽章快取與模擬器徽章 UI Twitch Badge Cache & Simulator Badge UI (Phase 7E)

**背景與動機：**
聊天 overlay 必須能正確顯示 Twitch 原生徽章圖示（VIP / Moderator / Subscriber / Founder / 頻道自訂徽章如「繪師」「贊助者」）。當前實作存在兩個缺陷：

1. **真實 Twitch 路徑徽章圖示壞掉**：IRC parser 將 `badges` IRC tag 解析為 `subscriber/0`、`vip/1` 等 *key* 字串並寫入 `PlatformUserDisplayInfo.Badges`。`OverlayEventForwarder.ForwardChatEventAsync` 直接將這些 key 當作 URL 透過 SignalR 廣播給 overlay，導致 `<img :src>` 永遠為 key 字串而非真實圖片 URL。
2. **模擬器無徽章選擇能力**：`SimulateControlsPanel.vue` 僅能勾選 `Subscriber/Moderator/VIP/Follower` 文字角色，overlay 渲染為 `chat-role-pill` 文字膠囊；無法觸發徽章圖示路徑，也無法選擇自訂頻道徽章。

對照來源：`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchChannelApiService.cs`（`GetGlobalBadgesAsync` / `GetChannelBadgesAsync`）、`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/IdentityService.cs`（`SyncBadgesAsync` / `GetBadgeUrl` 以 7 天 TTL 快取 `badge_{set}_{ver}` → URL）、`ref/Omni-Commander/OmniCommander.Infrastructure/Twitch/TwitchMessageEnricher.cs`（在訊息 enrich 時將 badges KVP 解析為 URL list 寫入 ChatMessage）。

**設計與規格：**

> **Phase 7G 更名（§6.1）：** 下列平台特定名稱已泛化 — `ITwitchHelixClient` → `IHelixClient`、`ITwitchBadgeCache` → `IPlatformBadgeCache`、`TwitchBadgeDescriptor` → `PlatformBadgeDescriptor`（`TwitchBadgeCache` *實作* 類別保留原名）。下方 Phase 7E 名稱請讀作其泛化後的對應型別。

1. **`ITwitchHelixClient` 徽章端點**：
   - 新增 `Task<IReadOnlyDictionary<string, TwitchBadgeDescriptor>> GetGlobalBadgesAsync(CancellationToken)`：對應 `GET helix/chat/badges/global`。
   - 新增 `Task<IReadOnlyDictionary<string, TwitchBadgeDescriptor>> GetChannelBadgesAsync(string broadcasterId, CancellationToken)`：對應 `GET helix/chat/badges?broadcaster_id={id}`。
   - 回傳字典 key 格式為 `{set_id}_{version}`，value 為 descriptor（含 `SetId`, `Version`, `ImageUrl1x`, `Title?`, `Description?`）。

2. **`ITwitchBadgeCache`（Application 介面）+ `TwitchBadgeCache`（Infrastructure 實作）**：
   - 介面：`string? Get(string key)`、`IReadOnlyList<TwitchBadgeDescriptor> ListAll()`、`Task SyncGlobalAsync(CancellationToken)`、`Task SyncChannelAsync(string broadcasterId, CancellationToken)`、`bool IsReady`。
   - 實作：`ConcurrentDictionary<string, TwitchBadgeDescriptor>` thread-safe；sync 失敗時保留舊資料、log warning。
   - 注意：cache 為 in-memory，應用重新啟動需重新同步；不做磁碟持久化。

3. **`TwitchBadgeSyncHostedService`**：
   - `StartAsync` 中 fire-and-forget 呼叫 `SyncGlobalAsync` + `SyncChannelAsync(broadcasterId)`（broadcaster id 來自 `Twitch:BroadcasterId` config，未設定則略過 channel sync）。
   - sync 失敗不阻擋應用啟動。

4. **`OverlayEventForwarder` 徽章解析**：
   - `ForwardChatEventAsync` 與 `TryResolveMemberSnapshotAsync` 注入 `ITwitchBadgeCache`，將 `display?.Badges` 之 key list 透過 `cache.Get` 轉換為 URL list，無對應 URL 之 key 被過濾（避免 overlay 出現破圖）。
   - 廣播至 SignalR 之 `OverlayChatPayload.Badges` 改為已解析之 URL 列表（contract schemaVersion 不變，僅內容語意修正）。
   - `ExtractRoles` 文字角色維持輸出（`event.roles`），供 future preset 使用；本期 `ChatPresetDefault.vue` 不再渲染文字角色 chips。

5. **模擬路徑徽章/顏色透傳**：
   - `SimulationRequest`（`SimulationKind.Message`）新增 `IReadOnlyCollection<string> Badges` 與 `string? ColorHex`。
   - `SimulateEndpoints` 接受 request body 之 `badges: string[]`、`colorHex: string?`；在 `adapter.SimulateAsync` 之前對 sim user (`simulation:{userId}`) 呼叫 `IPlatformUserInfoCache.UpdateAsync`，將 `Badges` + `ColorHex` 寫入快取，使後續 forwarder 之解析路徑與真實 Twitch 路徑統一。
   - 此設計避免在 domain event 上新增徽章欄位污染領域模型。

6. **新 API endpoint `GET /api/twitch/badges`**：
   - 回傳 `{ global: TwitchBadgeDescriptor[], channel: TwitchBadgeDescriptor[] }`，供前端 picker UI 列出可選徽章。
   - cache 未就緒時回傳空 array 並附 `Cache-Control: no-store` header。
   - 受 admin auth 限制（與其他 `/api/twitch/*` 端點一致）。

7. **前端 `SimulateControlsPanel.vue` UI 改造**：
   - onMounted 呼叫 `getTwitchBadges()` 並依 `setId` 分組顯示為徽章 chip grid，每 chip 為 `<img>` + tooltip 標題；點擊 toggle 加入 `selectedBadges`。
   - 新增「名稱顏色」欄位：hex input 配對即時 color swatch（預設 `#FFCA28`）。
   - submit 時於 request body 帶入 `badges: selectedBadges` 與 `colorHex`。
   - 移除「Streamer Roles」文字角色多選區（向後相容：若舊測試傳 `roles` 仍解析為對應 badge key，由後端 derive；本期前端 UI 不曝光）。

8. **前端 `ChatPresetDefault.vue` 調整**：
   - 移除 `chat-role-pill` 文字角色 chip 渲染段；徽章圖示為唯一身份標示。
   - badge `<img>` 渲染保持現狀；新增 `onerror` fallback 隱藏破圖。

**驗收：**
- 模擬器傳送含徽章之聊天訊息，`/overlay/chat` 顯示 Twitch 原生徽章 PNG 而非文字 chips。
- 真實 Twitch IRC 聊天訊息（VIP / Moderator / Subscriber）overlay 圖示正常。
- `GET /api/twitch/badges` 回傳 global 集合（含 broadcaster / moderator / vip / subscriber / founder 等）與 channel 自訂徽章。
- 名稱顏色 hex 套用至 overlay `chat-username` 之 `style="color"`。
- Cache miss 時 overlay 不出現破圖。
- 單元測試覆蓋：`TwitchBadgeCacheTests`、`OverlayEventForwarderBadgeResolutionTests`、`SimulateEndpointsBadgeTests`、frontend `SimulateControlsPanel.test.ts`。

**邊界：**
- 不下載徽章圖檔做 base64 內嵌，全程走 Twitch CDN URL。
- 不支援上傳自訂徽章（僅顯示已於 Twitch 註冊者）。
- 不整合 BTTV / 7TV / FFZ 徽章與表情。
- 不做徽章變更 audit log。
- Cache 無背景定期 refresh（自訂徽章新增後需重新啟動；後續 phase 可加 24h refresh hosted service）。

---


---

### 4.25 Twitch 頻道點數獎勵快取 + 獎勵選單 UI (Phase 7G)

> 完整內容請見英文版 `docs/SPEC.md` §4.25。以下為中文摘要。

**動機：** `reward.redeemed` 觸發器的 `RewardName` 篩選原本是純文字輸入，操作者得自己背獎勵名稱，常打錯也無從查起；模擬面板的 `Reward ID` 文字輸入同樣難用。

**設計重點：**

1. **Helix 查詢** — `IHelixClient.GetCustomRewardsAsync(broadcasterId, ct)` 取 `channel_points/custom_rewards`，回傳 `TwitchRewardDescriptor { Id, Title, Cost, IsEnabled, ImageUrl? }`。所需 scope `channel:read:redemptions` 已含於 Phase 7G 後的預設 scope 集。
2. **`ITwitchRewardCache` 單例**（`Vulperonex.Web.TwitchAuth`）— 純記憶體快取；refresh 流程：UI `/refresh` 端點 + OAuth `/complete` 完成後自動 `QueueRefresh()`。broadcaster 解析鏈與 §4.7 一致（`Twitch:BroadcasterId` → `SystemSettingKey.TwitchChannelName` → `Twitch:ChannelName` → `LookupUserAsync`）。401/403 直接吃掉並以 `ready=false` 表示。
3. **HTTP 端點**：
   - `GET /api/twitch/rewards` — 回傳 `{ ready, lastRefreshedAt, rewards }` 快取快照，不打網路。
   - `POST /api/twitch/rewards/refresh` — 強制重整。未授權狀態回 `200 { ready:false }`，讓 UI 顯示「請先授權」而非錯誤橫幅。
4. **觸發器篩選 dynamic options source** — `FilterFieldDto.OptionsSource` 新欄位；`RewardName` 宣告 `OptionsSource: "twitch.rewards"`。前端 `TriggerEditor.vue` 改用嚴格 `<select>`，含「任何」選項 + 獎勵清單 + 對於資料庫已存但目前快取沒有的舊選擇加上「（已失效）」尾綴選項。
5. **模擬面板共用同一快取** — `SimulateControlsPanel.vue` 將原本的 `rewardId` 文字輸入換成同樣的 `<select>`（value=id, label=title）。送出時同時帶 `rewardId` 與 `rewardTitle`；後端 `SimulateRequest` 新增可選 `RewardTitle`，缺則回退到 `rewardId`。此舉修正了一個潛在 bug：先前 `SimulateEndpoints.ToSimulationRequest` 把 `RewardTitle` 直接複製自 `rewardId`，導致 `MatchRewardRedeemed`（以 `RewardTitle` 比對）在模擬路徑上幾乎不可能正確觸發。

**邊界：**
- 圖示尚未渲染（DTO 帶 `ImageUrl` 但 UI 仍純文字）。
- 無自動排程 refresh（僅手動 + OAuth 完成）。
- Refund action 不在範圍內。

---
