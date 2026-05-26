# Phase 7E 手動驗證

## 前置

1. Twitch OAuth 已完成（`/api/twitch/auth/status` `hasRefreshToken: true`）。
2. `appsettings.json` 或環境變數 `Twitch__BroadcasterId` 設為真實 broadcaster id（取得：`curl -H "Authorization: Bearer <token>" -H "Client-Id: <id>" https://api.twitch.tv/helix/users?login=<your_login>`）。
3. 啟動 `dotnet run --project src/Hosts/Vulperonex.Web`，並等待 log 出現：
   - `Synced N global Twitch badges.`
   - `Synced M channel Twitch badges for {BroadcasterId}.`（若有設 BroadcasterId）

## 1. `/api/twitch/badges` 端點

```bash
curl http://localhost:5000/api/twitch/badges | jq '.ready, (.global | length), (.channel | length)'
```

期望：
- `ready: true`
- `global` 約 25–35 個（含 broadcaster / moderator / vip / subscriber / founder / premium 等）
- `channel` 為頻道自訂徽章數量（如「繪師」「贊助者」），未設 BroadcasterId 則為 0

## 2. 模擬器徽章 picker

1. 開 `/admin/simulate`（或 `/monitor`）。
2. 「身份徽章」section 顯示徽章 chip grid，每個 chip 為徽章圖示 + 名稱。
3. 點擊勾選 `VIP` + `Moderator` + 一個自訂徽章（如「繪師」）。
4. 「名稱顏色」改為 `#FFCA28`，色塊即時更新。
5. alias 維持 `chat`，輸入訊息「測試徽章顯示」，送出。

## 3. Chat overlay 顯示

1. 另開分頁 `/overlay/chat`。
2. 訊息出現時驗證：
   - 暱稱前出現所選徽章的真實圖示（PNG），而非文字 chip。
   - 暱稱文字為金黃色 `#FFCA28`。
   - 無 `SUBSCRIBER` / `MODERATOR` 文字膠囊。
3. 開 DevTools Network 確認 `<img>` 載入了 `static-cdn.jtvnw.net/badges/v1/...png`。

## 4. 真實 Twitch IRC 訊息

1. 用 VIP / Moderator / 訂閱者帳號於實際 Twitch 聊天室發送訊息。
2. `/overlay/chat` 顯示對應徽章圖示，無破圖（cache miss 應靜默隱藏 `<img>`）。

## 5. Cache miss 防護

1. 在 Sim picker 不開放的情況下，用 `curl` POST 一筆 `badges: ["bogus_99"]`：
   ```bash
   curl -X POST http://localhost:5000/api/simulate/chat \
     -H "Content-Type: application/json" \
     -d '{"displayName":"Sim","message":"hi","badges":["bogus_99"]}'
   ```
2. Overlay 訊息出現但無徽章圖示，且 console 無 404 圖片錯誤。

## 6. 重啟同步

1. 停止 Web、重啟。
2. log 重新出現 `Synced N global Twitch badges`。
3. `GET /api/twitch/badges` 立刻有資料（非首次請求才查）。
