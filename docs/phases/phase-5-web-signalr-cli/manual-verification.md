# 第 5 階段手動驗證

> 父待辦事項：`docs/phases/phase-5-web-signalr-cli/todo.md`

手動檢查補充了針對瀏覽器、OBS 與本地 SignalR 行為的自動化測試。它們不能取代自動化驗收測試。

## 條目範本

```markdown
## YYYY-MM-DD - 場景名稱

- 驗證者：
- 環境：
- 指令/瀏覽器/OBS 設定：
- 預期行為：
- 觀察到的行為：
- 結果：通過 | 失敗
- 後續問題/commit：
```

## 條目範例

```markdown
## 2026-05-16 - CLI 模擬聊天到達 overlay SignalR

- 驗證者：<姓名>
- 環境：Windows，本機 loopback，API 連接埠 <port>，overlay 連接埠 <port>
- 指令/瀏覽器/OBS 設定：`vulperonex simulate chat --user test --message "hello"`，overlay 聊天用戶端已連線至 `http://localhost:<overlayPort>/overlay/chat`
- 預期行為：overlay 用戶端收到一個帶有預期顯示名稱與訊息的聊天 payload。
- 觀察到的行為：<觀察到的結果>
- 結果：通過 | 失敗
- 後續問題/commit：<連結或 commit>
```

## 2026-05-16 - CLI 模擬聊天到達 overlay SignalR

- 驗證者：Codex automated integration test
- 環境：Windows，本機 loopback，Kestrel 隨機測試連接埠
- 指令/瀏覽器/OBS 設定：`POST /api/simulate/chat` 透過測試 HTTP client 觸發，SignalR client 連線至 `/hubs/overlay/chat`
- 預期行為：overlay chat hub 在 5 秒內收到包含 `schemaVersion`、`eventId`、`segments[].value` 的聊天 payload。
- 觀察到的行為：`Given_OverlayChatHub_When_ChatIsSimulated_Then_EventArrivesWithinFiveSeconds` 通過，並驗證本機延遲小於 1 秒。
- 結果：通過
- 後續問題/commit：Phase 5 implementation commit
