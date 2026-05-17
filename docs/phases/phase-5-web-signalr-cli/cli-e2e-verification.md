# 階段 5 CLI E2E / Twitch OAuth 驗證

此關卡（Gate）將阻擋階段 6 UI 的開發工作，直到 CLI 可以作為可靠的手動驗證工具為止。

> 互動式 REPL 流程見 [`supplemental-cli-repl.md`](supplemental-cli-repl.md)（任務 16g）。本檔聚焦 one-shot 命令的可重現驗證。

## 環境前置

- Windows 11 + PowerShell 7（或 5.1）。
- .NET SDK 已安裝；專案根目錄可成功 `dotnet restore`。
- `rtk proxy powershell -NoProfile -Command "..."` 為作者本地 token 代理包裝；非作者環境可直接執行該 `Command` 內的 `dotnet ...` 字串，行為等價。
- Twitch 開發者主控台「OAuth Redirect URLs」必須**三條全部**註冊（CLI `SelectCallbackPort` 依序試 7979 → 7980 → 7981 取第一個可用埠；缺其一則該埠回呼會失敗）：
  - `http://localhost:7979/auth/callback`
  - `http://localhost:7980/auth/callback`
  - `http://localhost:7981/auth/callback`

## 必要的自動化測試覆蓋範圍

- 使用全新的 SQLite 資料庫啟動 `Vulperonex.Web`，且不進行手動 EF 遷移（EF migration）。
- `GET /api/rules` 必須回傳 `200 OK`，而非 `` `SQLite Error 1: no such table: WorkflowRules` ``。
- 針對執行中的本機回環（live loopback）API 執行 CLI 指令：
  - `rule list`
  - `config get log.min_level`
  - `member list`
  - `simulate chat <message>`
  - `simulate follow`
  - `simulate sub`
- 驗證當 CLI 發生錯誤時，仍僅將後端 `error` 代碼寫入 stderr，並以代碼 `1` 退出（exit code 1）。

## 必要的 Twitch OAuth 手動流程

- 為 Web API 設定本機 Twitch 用戶端識別碼（`Twitch:ClientId`，可透過 `Twitch__ClientId` 環境變數）。
- 執行 CLI OAuth 指令。
- 瀏覽器開啟或 CLI 印出 Twitch 授權 URL。
- CLI 回呼監聽器（Callback listener）僅接受 `/auth/callback` 上的本機回環（loopback）請求。
- API 以 PKCE 驗證器（verifier）交換 `code`，並透過 `IOAuthTokenStore` 儲存重新整理權杖（refresh token）。
- 由於 `OAUTH_CREDENTIAL_NAMESPACE` 限制，`/api/config/oauth.twitch.refresh_token` 仍維持禁止存取（forbidden）狀態。

## 手動指令範本

### 1. 設定環境並啟動 Web host（終端機 A）

```powershell
$env:Twitch__ClientId = "<您的 Twitch 應用程式客戶端識別碼>"

# /m:1 /nr:false /p:UseSharedCompilation=false：強制單執行緒 build 且不重用 build server，避免並行 build server 對 SQLite 檔鎖與 rtk sandbox 互動產生間歇性失敗。如不需此緩解，可改用一般 `dotnet build`。
rtk proxy powershell -NoProfile -Command "dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false"

rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Web"
```

> **重要：** Web host 預設嘗試 `FirstApiPort = 5000`，若 5000 已被佔用則依 `PortAllocationOptions`（`step = 2`，最大 5008）跳至 5002 / 5004 / ...。**啟動後請從 console 輸出的 `Now listening on: http://127.0.0.1:<port>` 取得實際 port**，並於下方 `VULPERONEX_API_URL` 填入該 port。**禁止**假設一定是 5000。

### 2. 發布獨立 CLI 執行檔（OAuth 流程需用）

```powershell
rtk proxy powershell -NoProfile -Command "dotnet publish src\Hosts\Vulperonex.Cli -c Release -o artifacts\cli-manual"
```

### 3. 執行 one-shot CLI 命令（終端機 B）

將 `<api_port>` 替換為終端機 A console 印出的實際 port。

```powershell
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"

rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- rule list"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- config get log.min_level"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- member list"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate chat hello from cli"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate follow"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate sub"
```

### 4. 通過 / 失敗驗收

| 命令 | 預期 exit | 預期 stdout | 預期 stderr |
|------|-----------|-------------|-------------|
| `rule list` | 0 | JSON array（可為空 `[]`），pretty-printed | 空 |
| `config get log.min_level` | 0 | JSON 物件含 `value` 欄位 | 空 |
| `config get oauth.twitch.refresh_token` | 1 | 空 | `OAUTH_CREDENTIAL_NAMESPACE` |
| `member list` | 0 | JSON array（依分頁預設） | 空 |
| `simulate chat hello from cli` | 0 | 空（端點 204）或 JSON ack | 空 |
| `simulate follow` | 0 | 同上 | 空 |
| `simulate sub` | 0 | 同上（端點接受空 body） | 空 |
| 任意命令對非 loopback `VULPERONEX_API_URL` | 1 | 空 | `CLI_API_URL_NOT_LOOPBACK` |

任何一行失敗即視為 Gate 阻擋，於下方「狀態」段以 `FAIL` 紀錄並開 task 修復。

### 5. Twitch OAuth 流程（任務 16f 已實作）

```powershell
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"
rtk proxy powershell -NoProfile -Command ".\artifacts\cli-manual\Vulperonex.Cli.exe twitch auth start"
```

僅用於非瀏覽器環境的 URL 生成（不啟 `HttpListener`，僅印 `authorizeUrl` / `state` / `callbackPort`）：

```powershell
rtk proxy powershell -NoProfile -Command ".\artifacts\cli-manual\Vulperonex.Cli.exe twitch auth start --no-browser"
```

瀏覽器流程會等待 `http://localhost:<callbackPort>/auth/callback`，然後將傳回的 `code` 與 `state` 以 POST 方式傳送至 `/api/twitch/auth/complete`。

**OAuth 驗收條件：**

| 步驟 | 預期 |
|------|------|
| `twitch auth start`（瀏覽器模式） | 瀏覽器開啟 Twitch 授權頁；CLI 印 `Opened Twitch authorization URL. Waiting on ...` |
| 使用者於瀏覽器同意 | 瀏覽器顯示 `Twitch authorization received. You can close this window.` |
| CLI 完成 | exit 0；refresh token 已寫入 SQLite |
| 重複 `config get oauth.twitch.refresh_token` | exit 1，stderr `OAUTH_CREDENTIAL_NAMESPACE`（**不**因已授權而開放） |

### 6. 清理

```powershell
Remove-Item Env:Twitch__ClientId
Remove-Item Env:VULPERONEX_API_URL
# 終端機 A 按 Ctrl+C 結束 Web host
```

如需永久設定 ClientId：

```powershell
[Environment]::SetEnvironmentVariable('Twitch__ClientId', '<id>', 'User')
```

> **Security：** 目前 `Twitch:ClientId` 為 OAuth 公開值，可放環境變數。Phase 6+ 若引入 `Twitch:ClientSecret`，**禁止**寫入 shell history / env var；改用 DPAPI 或 SQLite credential store。

## 狀態

每次執行請以下列格式追加條目，對齊 `manual-verification.md` 規範（plan.md 第 5 階段手動驗證記錄）：

```
### <YYYY-MM-DD> 驗證者：<name>
- 環境：Windows 11 / PowerShell 7 / .NET <version> / Twitch ClientId set: yes/no
- 命令：<從上表挑選或全部>
- 預期：<引用上表>
- 觀察：<實際 stdout/stderr/exit>
- 結果：PASS / FAIL
- 備註：<rtk on/off、port 實際值、其他>
```

歷史：

- 2026-05-17 ｜ 紀錄者：lydek ｜ 動作：Gate 文件初版建立 ｜ 結果：N/A（僅文件）
- 2026-05-17 ｜ 紀錄者：lydek ｜ 動作：為啟動遷移（startup migration）、執行中 API 的 CLI 冒煙測試、Twitch OAuth 啟動 URL、Twitch OAuth 完成權杖儲存新增自動化的階段 5/Cli 測試覆蓋 ｜ 結果：自動化測試 PASS
- 2026-05-17 ｜ 紀錄者：lydek ｜ 待辦：CLI 已發布至 `artifacts/cli-manual/`，沙盒政策拒絕背景 Web 主機啟動，已發布程式的冒煙測試仍待本機終端機執行 ｜ Owner：lydek ｜ Due：開始 Phase 6 之前
