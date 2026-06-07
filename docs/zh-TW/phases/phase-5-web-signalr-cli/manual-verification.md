# 階段 5 手動驗證紀錄

> 相關待辦清單: `docs/phases/phase-5-web-signalr-cli/todo.md`

本檔案記錄無法完全由自動化測試證實的手動檢查，特別是終端機 UX、本地 OAuth 瀏覽器流程以及 overlay/瀏覽器行為。

## 範本

```markdown
## YYYY-MM-DD - <驗證名稱>

- 驗證者：
- 環境：
- 命令：
- 預期結果：
- 實際結果：
- 結果：PASS / FAIL
- 憑證 / commit：
```

## CLI 手動測試核對清單

在執行中的本地 Web 主機上執行，並將 `VULPERONEX_API_URL` 設定為作用中的 API 連接埠。

```powershell
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"
.\artifacts\cli-manual\Vulperonex.Cli.exe --interactive
```

使用 `tools/cli.ps1` 時，設定 `$env:VULPERONEX_API_PORT = "<api_port>"` 以強制單一連接埠探測，或傳遞 `-ApiUrl http://127.0.0.1:<api_port>`。

如果本地化的 CLI 說明在 PowerShell 中顯示亂碼，請在完成上述 UTF-8 設定後再次驗證，切勿直接將 i18n JSON 視為損壞。

規則 CRUD 手動驗證可以使用已簽入的範例負載：

```text
docs/phases/phase-5-web-signalr-cli/examples/manual-cli-rule.json
```

必要檢查項目：

- `help` 顯示分類命令群組與別名。
- `simulate` 無子命令時顯示本地 `chat|follow|sub` 說明，而非 `UNKNOWN_COMMAND`。
- `rule create <rule.json>` 從 JSON 檔案建立工作流規則。
- `rule update <rule-id> <rule.json>` 從 JSON 檔案更新相同的規則。
- `rule disable <rule-id>` 印出 `OK rule disabled: <rule-id>`。
- `rule enable <rule-id>` 印出 `OK rule enabled: <rule-id>`。
- `rule delete <rule-id>` 印出 `OK rule deleted: <rule-id>` 並移除規則以清理。
- `simulate chat|follow|sub` 印出包含 `accepted`、`eventTypeKey`、`eventId`、`platformUserId` 和 `displayName` 的 JSON 確認信號；使用 `eventId` 來與 Web/SignalR 日誌進行關聯。
- `member seed <platform-user-id> [display-name]` 透過模擬管線建立測試會員資料，並在該會員可被列出時印出 `OK member available: <member-id>`。
- `member list` 顯示植入的會員。
- `member delete <member-id>` 印出 `OK member deleted: <member-id>` 並移除植入的會員及其平台身分。
- `twitch auth start` 在配置 `Twitch:ClientId` 時啟動 Twitch OAuth。
- `twitch auth reset` 印出 `OK Twitch authorization reset` 並清除儲存的重新整理權杖，以便重複執行 `twitch auth start`。
- `config get oauth.twitch.refresh_token` 仍返回 `OAUTH_CREDENTIAL_NAMESPACE`。

## 2026-05-16 - CLI 模擬聊天推送至 overlay SignalR

- 驗證者：Codex 自動化整合測試
- 環境：Windows, 本地回環 Kestrel 測試主機
- 命令：透過測試 HTTP 用戶端發送 `POST /api/simulate/chat`，SignalR 用戶端連線至 `/hubs/overlay/chat`
- 預期結果：overlay 聊天 hub 在 5 秒內收到聊天負載。
- 實際結果：`Given_OverlayChatHub_When_ChatIsSimulated_Then_EventArrivesWithinFiveSeconds` 通過。
- 結果：PASS
- 憑證 / commit：Phase 5 實作 commit

## 2026-05-19 - CLI 手動測試 UX 自動化覆蓋

- 驗證者：Codex 自動化整合測試
- 環境：Windows, Release 測試建置
- 命令：`simulate`, `rule create/update/enable/disable/delete`, `member seed/delete`, `twitch auth reset`
- 預期結果：本地說明與手動測試命令正確路由，不呼叫未預期的端點。
- 實際結果：`CliCommandTests` 通過 45 項測試；排除固定連接埠耗盡的 `Phase5EndpointTests` 通過 40 項測試。
- 結果：PASS
- 憑證 / commit：待定

## 2026-05-19 - CLI 空成功回應回饋與會員植入修正

- 驗證者：Codex 自動化整合測試
- 環境：Windows, Release 測試建置
- 命令：`simulate chat`, `simulate follow`, `simulate sub`, `member seed`, `rule enable`, `rule disable`, `rule delete`, `twitch auth reset`
- 預期結果：模擬命令印出帶有 `eventId` 的可追蹤 JSON 確認；其他空 HTTP 成功主體的命令印出明確的 `OK ...` 輸出；會員模擬事件由 Web 主機消費，並在 `member list` 中變為可見。
- 實際結果：`CliCommandTests` 通過 49 項測試；排除固定連接埠耗盡的 `Phase5EndpointTests` 通過 40 項測試。
- 結果：PASS
- 憑證 / commit：待定

## 2026-05-19 - 模擬事件確認

- 驗證者：Codex 自動化整合測試
- 環境：Windows, Release 測試建置
- 命令：`simulate chat hello from cli`
- 預期結果：Web API 返回 `202 Accepted`，且 JSON 確認包含 `accepted`、`eventTypeKey`、`eventId`、`platformUserId`、`displayName` 和 `occurredAt`。
- 實際結果：`CliCommandTests` 與 `Phase5EndpointTests` 驗證了可追蹤的模擬輸出。
- 結果：PASS
- 憑證 / commit：待定

## 2026-05-20 - 階段 5 CLI 手動驗證與 Twitch OAuth 真實往返

- 驗證者：lydek
- 環境：Windows PowerShell, 本地 `Vulperonex.Web` 執行於 `http://127.0.0.1:5000`，應用程式內瀏覽器開啟於 `http://127.0.0.1:5000/`
- 命令：`help`, `simulate`, `simulate chat hello from cli`, `simulate follow`, `simulate sub`, `member seed manual-user ManualUser`, `member list`, `rule create docs/phases/phase-5-web-signalr-cli/examples/manual-cli-rule.json`, `rule disable <rule-id>`, `rule enable <rule-id>`, `rule delete <rule-id>`, `twitch auth reset`, `twitch auth start`（並在瀏覽器回呼中完成真實授權）
- 預期結果：CLI 命令提供可見的成功輸出或 JSON 確認，模擬事件包含可追蹤的 `eventId`，會員植入透過 `member list` 變為可見，規則生命週期命令順利完成。關於 Twitch OAuth：`twitch auth start` 成功啟動瀏覽器，在 Twitch 上授權完成，後端 `7979` 回呼收到並驗證 CSRF state 參數，透過開發權杖端點進行程式碼交換，並使用 AES-256-GCM 信封安全地加密及儲存重新整理權杖至 SQLite `SystemSettings` 中，透過 `twitch auth status` 返回配置與連接狀態來驗證。
- 實際結果：使用者回報目前所有功能皆正常。Twitch 真實 OAuth 授權完成端到端：瀏覽器順利開啟、授權並重新導向回 `http://localhost:7979/auth/callback`，主控台記錄了成功的 state 驗證與權杖交換。重新整理權杖在 SQLite DB 中被驗證為 AES-256-GCM 加密（在 DB 中無法以純文字讀取，格式符合 `"v1:" + Base64`），且 CLI 順利執行了後續的自動化查詢。
- 結果：PASS
- 憑證 / commit：`6ff0ace`, `2d9fe4e`, `00cfe8b`
