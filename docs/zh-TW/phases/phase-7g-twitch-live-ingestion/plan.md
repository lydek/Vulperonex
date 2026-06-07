# Phase 7G — Twitch 即時接入 (TwitchLib 混合) + 訂閱與兌換快取 (Twitch Live Ingestion + Reward Cache)

> **狀態：** 已於 2026-06-01 交付。此計畫是對 2026-05/06 月份開發輪次中已落地實作內容的**追溯**記錄 — 作為 2026-06-01 規格對比實作審查（參見 `docs/spec-vs-impl-2026-06-01.md`，原始報告位於 `.claude/worktrees/spec-review-2026-06-01/docs/spec-vs-impl-2026-06-01.md`）的後續追蹤。
> **此階段更新的 SPEC 章節：** §2 (技術棧)、§4.4 (系統事件)、§4.6 (行動目錄)、§4.7 (傳輸拓撲 + auth_failed)、§4.13.1 (API 表格 + 模擬別名 + 裝置流程)、§4.22 (進階旗標 + 篩選感知變數選取器)、§4.25 (Twitch 兌換快取 — 新章節)、§6.1 (分層例外)。

## 上下文 (Context)

在 Phase 7G 之前，`Vulperonex.Adapters.Twitch` 已經提供了完整的網域管線（包含剖析器、重複刪除快取、事件映射器、OAuth、Helix 用戶端），但**沒有即時接入來源 (Live Ingestion Source)**：`TwitchAdapter.StartAsync` 僅註冊了事件類型便停止，`PublishIrcMessageAsync` / `PublishMockPayloadAsync` 為 `internal` 且僅由測試呼叫，轉接器甚至未在 DI 中註冊。生產環境的接入完全依賴 `/api/simulate/*`。

在開發輪次中，首次嘗試使用手寫的 `ClientWebSocket` EventSub 接入。在經過開發者審查後，重新調整設計以遵循已在 `ref/omni-commander` 中驗證過的模式：

- 使用 **TwitchLib** 代替手寫的 WebSocket — 提供經過驗證的連線/心跳/重新連線與強型別事件處理器。
- **混合傳輸模式**：IRC (`TwitchLib.Client`) 用於聊天（同時涵蓋了 EventSub 聊天所缺少的禁言/逾時/清除聊天等管理事件）；EventSub WebSocket (`TwitchLib.EventSub.Websockets`) 用於警報（追隨/訂閱等）。
- **自動解析實況主**：從頻道登入名稱 (Channel Login Name) 解析，而不是強制操作者輸入數值型的 `BroadcasterId`。
- 具有「驗證失敗時重試」的 **Orchestrator** 背景服務。
- **正確的 OAuth 作用域 (Scopes)** + **連線狀態事件**，使 UI 狀態晶片能正常運作。

此階段還新增了 **Twitch 頻道點數兌換快取 (Twitch Reward Cache)** 與兌換選取器 UI（因操作者回饋表示 `RewardName` 篩選器以前是純文字輸入，沒有人知道該輸入什麼）。

## 方法（已交付）

所有 TwitchLib 程式碼都位於 `Vulperonex.Web`（主機專案）。轉接器專案 (Adapter Project) 保持不引入第三方 SDK — 主機處理器將 TwitchLib 事件轉換為轉接器現有的 `TwitchIrcMessage` / `TwitchMockPayload` 型別，並呼叫強型別接入方法。測試保持不依賴 TwitchLib。

### 1. NuGet 套件（僅主機）

`src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj`：
- `TwitchLib.Client 4.0.1`
- `TwitchLib.EventSub.Websockets 0.8.0`

版本固定在 `Directory.Packages.props` 中。刻意**不**新增 `TwitchLib.Api` — 實況主解析與 EventSub 訂閱建立重用現有的 `IHelixClient`。

### 2. 轉接器 — 強型別接入進入點

`src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs`：
- `public Task IngestChatAsync(TwitchIrcMessage, TwitchDisplayCacheUpdater? override, CancellationToken)` — 路由至 `PublishIrcMessageAsync`。
- `public Task IngestAlertAsync(TwitchMockPayload, TwitchDisplayCacheUpdater? override, CancellationToken)` — 路由至 `PublishMappedEventAsync(TwitchEventMapper.Map(payload))`。
- 可選的 `displayCacheOverride` 允許背景主機提供一個限於 scope 範圍的 `TwitchDisplayCacheUpdater`；Singleton 的轉接器本身無法直接存取限於 scope 範圍的使用者資訊快取。

### 3. IRC 聊天來源

`Vulperonex.Web.TwitchAuth.TwitchIrcChatSource` 封裝了 `TwitchLib.Client.TwitchClient`：
- `ConnectAsync(channelLogin, accessToken, ct)` 初始化 `ConnectionCredentials(channelLogin, accessToken)` 並連線。
- `OnMessageReceived` → 從聊天中建構 `TwitchIrcMessage` 標籤（`msg-id`、`user-id`、`display-name`、來自 `HexColor` 的 `color`、以逗號分隔的 `set/version` 徽章列表 `badges`、`bits`） → 針對每條訊息透過 `IServiceScopeFactory` 建立 scope → 解析 scope 中的 `IPlatformUserInfoCache` → `new TwitchDisplayCacheUpdater(cache)` → `adapter.IngestChatAsync`。
- `OnConnected` / `OnDisconnected` 發布 `PlatformConnectionChangedEvent`。

### 4. EventSub 警報來源

`Vulperonex.Web.TwitchAuth.TwitchEventSubSource` 封裝了 `EventSubWebsocketClient`：
- 強型別處理器映射至 `TwitchAlertPayloadFactory` 中的純協助工具（保持無通訊端，以便進行單元測試） → `TwitchMockPayload` → `adapter.IngestAlertAsync`。涵蓋範圍：`ChannelFollow`、`ChannelSubscribe`（當 `IsGift` 為真時跳過）、`ChannelSubscriptionMessage`、`ChannelSubscriptionGift`、`ChannelCheer`、`ChannelRaid`、`ChannelPointsCustomRewardRedemptionAdd`。
- `WebsocketConnected` → 針對每個訂閱類型呼叫 `IHelixClient.CreateEventSubSubscriptionAsync(type, version, condition, sessionId)`。連線工作階段 ID 重複消除 (`_lastSubscribedSessionId`) 避免在無縫重新連線時重複訂閱（Twitch 會保留訂閱狀態）。
- `WebsocketConnected` / `WebsocketDisconnected` 發布 `PlatformConnectionChangedEvent`。

### 5. 連線協調器 (BackgroundService)

`Vulperonex.Web.TwitchAuth.TwitchConnectionOrchestrator`：
- 一律呼叫 `adapter.StartAsync`（即使接入被關閉，事件類型註冊仍會執行）。
- 閘門條件：`Twitch:EventSub:Enabled`（預設為 true）且實況主可解析。
- 實況主解析順序：`Twitch:BroadcasterId`（舊有覆寫） → `SystemSettingKey.TwitchChannelName`（資料庫） → `Twitch:ChannelName`（設定檔），然後透過 `IHelixClient.LookupUserAsync(channelName)` 查詢數值識別碼。
- 驗證/連線失敗時每 10 秒重試一次（遵循 omni-commander `TwitchMonitorService` 的重試模式）。每次重試的順序：解析實況主 → 啟動 IRC → 啟動 EventSub。

### 6. 作用域與設定

- `Endpoints/TwitchAuthEndpoints.cs`：將預設的 OAuth 作用域提升至與 omni-commander 相同的超集 — `chat:read chat:edit channel:read:redemptions channel:manage:redemptions channel:read:subscriptions moderation:read channel:read:vips moderator:read:followers bits:read`。升級後，操作者必須重新授予權限。
- `appsettings.Development.json`：記錄了 `Twitch:ChannelName`。
- 新增 `SystemSettingKey.TwitchChannelName` (`twitch.channel_name`)。

### 7. Twitch 頻道點數兌換快取 (SPEC §4.25)

- `IHelixClient.GetCustomRewardsAsync(broadcasterId, ct)` → `TwitchRewardDescriptor`。
- `Vulperonex.Web.TwitchAuth.TwitchRewardCache` 作為 Singleton 實作，並提供 `ITwitchRewardCache` 介面。採延遲重新整理（無託管背景服務）；重新整理掛鉤點：UI `/refresh` 端點、OAuth `/complete` 及 `/device/complete`。
- 端點：`GET /api/twitch/rewards`、`POST /api/twitch/rewards/refresh`（參見 `Endpoints/TwitchRewardsEndpoints.cs`）。
- 觸發器元資料：`FilterFieldDto.OptionsSource` 欄位；`RewardName` 宣告了 `OptionsSource = "twitch.rewards"`。
- 前端：`useTwitchRewardsStore` (Pinia)、`TriggerEditor.vue` 與 `SimulateControlsPanel.vue` 中的兌換項目 `<select>` 下拉選單、重新整理按鈕、針對過期快取值提供「(已無法使用)」的合成選項。

### 8. 相依性注入 (DI)

`src/Hosts/Vulperonex.Web/DependencyInjection.cs`：
- `services.AddSingleton<TwitchAdapter>()`
- `services.AddTwitchLibEventSubWebsockets()`（提供 `EventSubWebsocketClient`）
- `services.AddSingleton<TwitchIrcChatSource>()` + `services.AddSingleton<IPlatformChatSender>(sp => sp.GetRequiredService<TwitchIrcChatSource>())`
- `services.AddSingleton<TwitchEventSubSource>()`
- `services.AddSingleton<ITwitchRewardCache, TwitchRewardCache>()`
- `services.AddHostedService<TwitchConnectionOrchestrator>()`

## 檔案策略

| 檔案 | 變更 |
|---|---|
| `Directory.Packages.props` | 新增 TwitchLib.Client, TwitchLib.EventSub.Websockets |
| `src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj` | 參考新的 NuGet 套件 |
| `src/Adapters/Vulperonex.Adapters.Twitch/TwitchAdapter.cs` | 實作強型別 `IngestChatAsync` / `IngestAlertAsync` |
| `src/Vulperonex.Application/Twitch/IHelixClient.cs` | 新增 `CreateEventSubSubscriptionAsync` + `GetCustomRewardsAsync` + `TwitchRewardDescriptor` |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchHelixClient.cs` | 實作兩個新的 Helix 呼叫（建立訂閱時，409 已存在視為成功） |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchIrcChatSource.cs` | 新增 — 透過 TwitchLib.Client 建立 IRC 連線 |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchEventSubSource.cs` | 新增 — 透過 TwitchLib 建立 EventSub 與強型別處理器 |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchAlertPayloadFactory.cs` | 新增 — 純映射協助工具（具備可單元測試性） |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchConnectionOrchestrator.cs` | 新增 — 連線協調器 BackgroundService |
| `src/Hosts/Vulperonex.Web/TwitchAuth/TwitchRewardCache.cs` | 新增 — Singleton 快取與重新整理邏輯 |
| `src/Hosts/Vulperonex.Web/Endpoints/TwitchRewardsEndpoints.cs` | 新增 — GET + POST 重新整理端點 |
| `src/Hosts/Vulperonex.Web/Endpoints/TwitchAuthEndpoints.cs` | 將預設作用域改為超集；於 `/complete` + `/device/complete` 排入兌換快取更新 |
| `src/Hosts/Vulperonex.Web/Program.cs` + `DependencyInjection.cs` | 連線來源、協調器、快取及兌換端點 |
| `src/Vulperonex.Application/Workflows/Metadata/{IActionMetadataProvider,ActionMetadataAttribute}.cs` | 新增 `Advanced` 進階欄位標記 (§4.22) |
| `src/Vulperonex.Application/Workflows/Metadata/ITriggerMetadataProvider.cs` | 於 `FilterFieldDto` 新增 `OptionsSource` |
| `src/Vulperonex.Infrastructure/Workflows/Metadata/TriggerMetadataProvider.cs` | `RewardName` 取得 `OptionsSource = "twitch.rewards"` |
| `src/frontend/src/api/client.ts` | 新增元資料型別的 `optionsSource` 與 `advanced`、兌換查詢協助工具，於 `SimulateRequestBody` 中新增 `rewardTitle` |
| `src/frontend/src/stores/twitchRewards.ts` | 新增 Pinia store |
| `src/frontend/src/components/admin/TriggerEditor.vue` | 針對 `twitch.rewards` 選項來源新增 `<select>` 與重新整理分支 |
| `src/frontend/src/components/admin/SimulateControlsPanel.vue` | 兌換項 `<select>` + 重新整理按鈕，且同時傳送 `rewardId` 和 `rewardTitle` |
| `src/frontend/src/components/admin/WorkflowActionsEditor.vue` | 基礎/進階欄位分離與 `<details>` 折疊顯示 |
| `src/frontend/src/components/admin/VariablePicker.vue` | 將 UserId 篩選收緊為僅限 `*.UserId` |
| `src/frontend/src/i18n/{en-US,zh-TW}.json` | 新增兌換用文字、進階選項標籤、已無法使用後綴等 |
| `src/frontend/src/styles/app.css` | 明確的深色模式 `select`/`option`/`input` 樣式 |
| `tests/Vulperonex.Tests.Unit/Web/TwitchRewardCacheTests.cs` | 新增 — 4 個針對快取解析與取代的測試 |
| `tests/Vulperonex.Tests.Unit/Web/TwitchAlertPayloadFactoryTests.cs` | 新增 — 純映射器的單元測試覆蓋 |
| `tests/Vulperonex.Tests.Unit/Adapters/Twitch/TwitchAdapterEventTypeTests.cs` | 重寫為對強型別 `IngestChatAsync` / `IngestAlertAsync` 的測試 |

## 驗收 / 驗證

1. 執行 `dotnet build Vulperonex.sln` — 15 個專案，0 個錯誤。
2. 執行 `dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj` — 284/284 通過。
3. 執行 `vue-tsc --noEmit` + `vitest run` 針對修改過的前端測試套件皆呈綠色。
4. **手動冒煙測試（實作操作）：**
   - 設定 `Twitch:ChannelName` 或 `Twitch:BroadcasterId`；使用新的作用域完成 OAuth 授權（在 `/twitch` 頁面 → 重設 Token → 重新授予權限）。
   - 執行 `Vulperonex.Web`。日誌顯示：實況主已解析 → IRC 已連線 → EventSub 已連線 → 註冊了 7 個訂閱（或者 409 "已存在" 視為成功）。
   - 在頻道中傳送聊天訊息 → `/monitor` 的 ChatStream 會顯示該訊息；`/overlay/chat` 也能成功渲染。
   - 在 Twitch 上觸發追隨 / 訂閱 / 小額贊助 (Cheer) / 主辦 (Raid) / 點數兌換 → 對應的警報會流入 `/overlay/alerts` 中。
   - 開啟規則編輯器 (Rule Editor) → 兌換點數觸發器 (Reward Redeemed Trigger) → 兌換名稱下拉選單會列出兌換項目；點擊 ↻ 會更新快取；過期值會出現「(已無法使用)」。
   - 開啟 `/monitor` 模擬面板 (Simulate) → `redeem` 別名 → 兌換下拉選單有資料；送出後會觸發由該兌換標題篩選的工作流規則。
5. **連線狀態晶片：** `/twitch` UI 會接聽 `platform.connection_changed` 事件；狀態晶片會反映即時連線狀態。中斷網路連線 → 記錄協調器重試與重新連線日誌。

## 風險與已知後續

- **`auth_failed` 原因** — SPEC §4.7 要求提供此欄位；目前實作僅發布 `irc_disconnected` / `eventsub_disconnected` / `null`。列為 §F.2 的後續追蹤。
- **§6.1 分層例外** — `LookupTwitchUserAction`、`RefundTwitchRedemptionAction`、`ShoutoutAction` 仍然存在於 `Vulperonex.Application` 中（先前已存在）。在 §6.1 底下進行追蹤，目前仍待決定 (a) 重新命名為平台中立名稱，或 (b) 移至轉接器中。
- **TwitchLib 4.x API 表面與 omni-commander 的 3.5.3 不同** — 已於實作輪次中透過反射進行驗證：`e.Payload.Event`（而非 `.Notification`）、`TwitchLib.EventSub.Core.EventArgs.Channel` 中的頻道參數、IRC 聊天顏色為 `HexColor`（而非 `ColorHex`）、斷開連線參數為 `OnDisconnectedArgs`。已記錄在原始碼註釋中。
