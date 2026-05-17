# 第 5 階段待辦事項 - Web Host + SignalR + CLI

> 計畫：`docs/phases/phase-5-web-signalr-cli/plan.md`
> 父核對清單：`tasks/todo.md`

---

## 任務 14a - Minimal API WorkflowRule CRUD + EventTypes

- [x] 任務 14a-0：實作 Web host 組合、`JsonSerializerDefaults.Web`、端點擴充方法註冊、OpenAPI JSON、中央錯誤碼常數/狀態對應、`Database:Path` 原始讀取架構測試，以及 API 整合測試基礎建設。
- [x] 任務 14a-1：對齊 WorkflowRule 持久化、查詢 DTO 以及 CQRS 讀取/寫入路徑。
- [x] 任務 14a-2：實作 WorkflowRule 驗證與機器可讀的錯誤代碼。
- [x] 任務 14a-3：實作 WorkflowRule CRUD/啟用/停用/刪除端點，並附帶 CQRS 互動測試。
- [x] 任務 14a-4：實作 `GET /api/event-types`，並包含來自共享模擬別名註冊表的 `isSimulatable`。

## 任務 14b - Minimal API 模擬 / 配置 / 成員

- [x] 任務 14b-1：僅針對 `chat`，`follow`，與 `sub` 實作 `POST /api/simulate/{alias}`，並固定 chat/follow/sub request body schema。
- [x] 任務 14b-2：在 registry 查找前實作帶有 `security.*` 與 `oauth.*` 字首拒絕清單的 `GET|PUT /api/config/{key}`。
- [x] 任務 14b-3：透過 `IMemberQueryService` 實作成員列表/顯示查詢端點；Phase 5 目前以 `MemberId ASC` 作為穩定排序。

## 任務 15 - SignalR Hub + Overlay 推送 + 雙連接埠 Kestrel

- [x] 任務 15a：實作 SignalR 管理/overlay hub 合約與 host 註冊。
- [x] 任務 15b：記錄合成 `eventId` 語意，實作 overlay 事件轉寄、明確 alert event 集合、精確 SignalR JSON 金鑰集測試，以及 SC-5 延遲量測。
- [x] 任務 15c：實作雙連接埠 loopback Kestrel 繫結、`IPortAvailabilityProbe.IsAvailable(int port)`、連接埠對分配與耗盡行為。

## 任務 16 - CLI

- [x] 任務 16a：實作 CLI HTTP 基礎、`VULPERONEX_API_URL` 優先的 base URL 解析、stdout/stderr 規則以及後端錯誤透傳。
- [x] 任務 16b：實作 CLI `rule list|show|enable|disable|delete`。
- [x] 任務 16c：實作 CLI `config get|set` 與 `member list|show`。
- [x] 任務 16d：實作 CLI `simulate chat|follow|sub` 與共享資料庫路徑覆蓋保護。
- [x] 任務 16e：完成 第 5 階段檢查點審查與手動 overlay 交付。
- [ ] 任務 16f：補齊 CLI 端到端手動測試 gate：Web API 啟動後自動套用 SQLite migration、真實 CLI 程序可打到本機 API/DB、並提供 Twitch OAuth PKCE 授權入口。

## 任務 16f - CLI E2E / Twitch OAuth 收尾

- [x] Web host 啟動時執行 `DatabaseBootstrapper.MigrateAsync()`，新環境第一次執行 `rule list` 不得因缺少 `WorkflowRules` table 回 500。
- [x] Web startup project 可支援 `dotnet ef database update --project src\Vulperonex.Infrastructure --startup-project src\Hosts\Vulperonex.Web`。
- [x] 自動化測試覆蓋「未預先 migrate 的 temp SQLite DB 啟動後，`GET /api/rules` 回 200」。
- [x] 自動化測試覆蓋真實 Web host + `VulperonexCli.RunAsync` 對本機 API 的 rule/config/member/simulate smoke path。
- [x] CLI 提供 `twitch auth start` 或等效命令，使用 loopback-only OAuth callback、PKCE `state`/`code_verifier`，並把 refresh token 交由 API 透過 `IOAuthTokenStore` 加密保存。
- [x] Twitch OAuth 授權流程不得透過 `/api/config/oauth.*` 讀寫 token；`oauth.*` protected namespace 規則維持不變。
- [x] 手動驗證命令與預期結果記錄於 `cli-e2e-verification.md`，包含 build、啟動 API、CLI smoke、Twitch 授權前置設定與 callback URI。
- [ ] 本機 terminal 實跑 published CLI (`artifacts/cli-manual/Vulperonex.Cli.exe`) 對獨立 Web API process 的 smoke；Codex sandbox 已拒絕背景 Web host 啟動，需人工執行。

## 任務 16g - CLI Interactive REPL

- [x] 抽出 CLI command tree：`IConsoleCommand`、`ICommandDispatcher`、`CompositeConsoleCommand`、`CliExecutionContext`，one-shot 與 REPL 共用 dispatch。
- [x] 保持既有 one-shot `rule/config/member/simulate/twitch` 行為，並新增 `help` / `?` 內建命令。
- [x] 新增 `GET /api/twitch/auth/status`，只回傳 `clientIdConfigured` / `hasRefreshToken` boolean，不回傳 client id 或 token。
- [x] 新增最小 REPL 入口：無參數、`--interactive`、`-i` 讀取 stdin 逐行 dispatch，支援 `exit` / `quit` / EOF。
- [x] REPL 中 API 錯誤只寫 stderr error code，session 繼續。
- [ ] REPL 啟動 banner：依 `/api/twitch/auth/status` 提示 ClientId/OAuth 狀態，且不得產生 authorize URL。
- [ ] REPL `twitch auth start` 支援 Ctrl+C 取消並輸出 `TWITCH_OAUTH_CANCELLED`。
- [ ] Line editor：TTY 模式 history、Tab prefix completion、Backspace、Ctrl+C 清 buffer；redirected stdin 不走 line editor。
- [ ] 手動驗證 REPL：Windows Terminal / PowerShell 下 one-shot、REPL、OAuth、exit/EOF 行為。

## 第 5 階段依賴項

- [x] 任務 13f：強化第 4 階段 SC-6a/SC-6b 等效性，包含 follow/sub/donate 負載，或在第 5 階段關閉前明確豁免。

## 檢查點 5

- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 以 0 個警告通過。
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [x] SC-2、SC-5、SC-8、SC-9 通過。
- [x] WorkflowRule CRUD 與循環引用偵測端到端通過。
- [x] `GET /api/event-types` 排除 `platform.connection_changed`，且 `isSimulatable` 只對 `chat`，`follow`，`sub` 回傳 true。
- [x] 第五階段錯誤碼已集中管理，並由 HTTP 狀態對應表涵蓋。
- [x] `UNKNOWN_*` 與 `INVALID_*` 錯誤碼命名規則已記錄並遵守。
- [x] 配置受保護命名空間檢查通過：`security.*` -> `CONFIG_KEY_SECURITY_NAMESPACE`；`oauth.*` -> `OAUTH_CREDENTIAL_NAMESPACE`。
- [x] 未知的受保護設定金鑰（如 `oauth.unknown.refresh_token`）回傳 `OAUTH_CREDENTIAL_NAMESPACE`，確認字首拒絕清單優先於 registry 查找。
- [x] 成員端點涵蓋列表/顯示、分頁限制、`INVALID_QUERY_PARAM` 與 `MEMBER_NOT_FOUND`。
- [x] SignalR overlay 聊天在 5 秒內收到事件以符合 SC-5，且本地延遲測量暴露了 <1s P95 目標。
- [x] SignalR 序列化精確金鑰集測試涵蓋聊天、提醒與成員 overlay DTO。
- [x] SignalR 金鑰集測試反序列化線路負載，不依賴僅反射的 DTO 檢查。
- [x] 合成 `eventId` 決策記錄於 `docs/phases/phase-5-web-signalr-cli/event-id-decision.md`，在 overlay 轉寄實作前完成。
- [x] `event-id-decision.md` 審查筆記欄位（審查者/日期/決定）填寫完成，方可進入 Task 15b。
- [x] 兩個 Kestrel 連接埠繫結至 IPv4 loopback `127.0.0.1` 與 IPv6 loopback `::1`；非 loopback socket 測試被拒絕。
- [x] 連接埠對分配嘗試 `5000/5001` 至 `5008/5009`，耗盡時回傳 null，再由 host 丟出 `PortExhaustedException`。
- [x] CLI rule/config/member/simulate 命令通過整合測試。
- [x] CLI 4xx/5xx 處理僅將後端 `error` 代碼寫入 stderr 並以退出碼 `1` 退出。
- [x] CLI 模擬聊天固定規則透過 HTTP API 路徑觸發虛擬傳送者。
- [x] 手動確認：CLI 模擬聊天到達 overlay SignalR，結果記錄於 `docs/phases/phase-5-web-signalr-cli/manual-verification.md`。
- [x] CLI 端到端 smoke：新 DB 啟動 API 後不需手動 migration，真實 CLI 可成功執行 `rule list`、`config get`、`member list`、`simulate chat|follow|sub`。
- [x] CLI Twitch OAuth smoke：`twitch auth start` 產生 Twitch authorize URL，loopback callback 收到 code 後由 API exchange token，refresh token 加密保存。
- [x] `ASPNETCORE_ENVIRONMENT=Development` 加上 `appsettings.Development.json` 與環境變數不會覆蓋主 `appsettings.json` 的 `Database:Path`。
- [x] 架構測試證明原始 `Configuration["Database:Path"]` 讀取僅限於 `IDatabasePathResolver`。
- [x] 任務 13f 第 4 階段 SC-6a/SC-6b 後續已完成或明確豁免。
- [x] Git 暫存集限於任務範圍；無關的未追蹤檔案保持不提交。
