# 階段 5 CLI E2E / Twitch OAuth 驗證

此關卡（Gate）將阻擋階段 6 UI 的開發工作，直到 CLI 可以作為可靠的手動驗證工具為止。

> 互動式 REPL 流程見 [`supplemental-cli-repl.md`](supplemental-cli-repl.md)（任務 16g）。本檔聚焦 one-shot 命令的可重現驗證。

## 環境前置

- Windows 11 + PowerShell 7（或 5.1）。
- .NET SDK 已安裝；專案根目錄可成功 `dotnet restore`。
- `rtk proxy powershell -NoProfile -Command "..."` 為作者本地 token 代理包裝；非作者環境可直接執行該 `Command` 內的 `dotnet ...` 字串，行為等價。
- 若需要在 `rtk proxy powershell -Command` 內設定 `$env:...`，外層 PowerShell 必須使用單引號包住 `-Command` 內容，或先在目前 shell 設定環境變數。不要使用 `-Command "$env:VULPERONEX_API_URL='...'; ..."`，外層 PowerShell 會先展開 `$env:VULPERONEX_API_URL`，造成 `http://127.0.0.1:5000=...` 這類錯誤。
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

- 為 Web API 設定本機 Twitch 用戶端識別碼（`Twitch:ClientId`）。若 Twitch App 是 confidential client，可另外設定 `Twitch:ClientSecret` 走 authorization-code callback flow；若 Twitch App 是 public client，請不要設定 secret，CLI 會走 device-code flow。
- 執行 CLI OAuth 指令。
- 瀏覽器開啟或 CLI 印出 Twitch 授權 URL。
- CLI 回呼監聽器（Callback listener）僅接受 `/auth/callback` 上的本機回環（loopback）請求。
- API 以 PKCE 驗證器（verifier）交換 `code`，並透過 `IOAuthTokenStore` 儲存重新整理權杖（refresh token）。
- 由於 `OAUTH_CREDENTIAL_NAMESPACE` 限制，`/api/config/oauth.twitch.refresh_token` 仍維持禁止存取（forbidden）狀態。

若未設定 `Twitch:ClientId`，非 Twitch CLI 指令仍可使用。互動式 REPL 會在啟動時呼叫 `/api/twitch/auth/status`，輸出 no-Twitch mode 警告後繼續；在 REPL 內執行 `twitch auth start` 會先輸出 `TWITCH_CLIENT_ID_MISSING` 與設定提示，且不會建立 OAuth session 或呼叫 `/api/twitch/auth/start`。

## 手動指令範本

### 1. 設定環境並啟動 Web host（終端機 A）

```powershell
$env:Twitch__ClientId = "<您的 Twitch 應用程式客戶端識別碼>"
# Optional：只有 confidential Twitch App 才設定。
# Public client 請省略，CLI 會使用 device-code flow。
$env:Twitch__ClientSecret = "<您的 Twitch 應用程式客戶端密鑰>"

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

> **任務 16f L46：** 本表先使用 `dotnet run` 完成功能驗證；通過後重複執行一次改用 `artifacts\cli-manual\Vulperonex.Cli.exe`（步驟 2 的 published 二進位）對相同 Web host 驗證每行命令的 exit code / stdout / stderr 與 `dotnet run` 一致。Codex sandbox 拒絕背景 Web host 啟動，published 路徑必須由本機終端機手動跑過一次才視為 Gate 通過。

```powershell
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"

rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- rule list"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- config get log.min_level"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- member list"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate chat hello from cli"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate follow"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate sub"
```

單行設定 API URL 並進入 REPL 時，請使用單引號：

```powershell
rtk proxy powershell -NoProfile -Command '$env:VULPERONEX_API_URL="http://127.0.0.1:<api_port>"; dotnet run --project src\Hosts\Vulperonex.Cli -- --interactive'
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
| `twitch auth start`（public client） | CLI 開啟或列出 `https://www.twitch.tv/activate`，並印 user code；授權後 CLI 印 `Twitch authorization completed.` |
| `twitch auth start`（confidential client） | 瀏覽器開啟 Twitch 授權頁；CLI 印 `Opened Twitch authorization URL. Waiting on ...` |
| 使用者於瀏覽器同意且 token exchange 成功 | 瀏覽器顯示 `Twitch authorization completed`，並提示回到 CLI |
| CLI 完成 | exit 0；refresh token 已寫入 SQLite |
| Twitch token exchange 失敗 | CLI stderr `TWITCH_OAUTH_EXCHANGE_FAILED`，瀏覽器或 CLI 顯示授權未完成 |
| 重複 `config get oauth.twitch.refresh_token` | exit 1，stderr `OAUTH_CREDENTIAL_NAMESPACE`（**不**因已授權而開放） |

### 5b. REPL TTY 互動行為手動驗證（任務 16f L60 / 16g）

自動化整合測試無法覆蓋實體 TTY 按鍵；以下流程需於 **Windows Terminal** + **PowerShell 7** 下執行至少一次。`cmd.exe` 與 PowerShell 5.1 為選擇性。

**前置：** 終端機 A Web host 持續執行（步驟 1）；終端機 B 設妥 `VULPERONEX_API_URL` 並啟 REPL：

```powershell
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"
dotnet run --project src\Hosts\Vulperonex.Cli -- --interactive
```

或以 published 執行檔（OAuth 取消測試建議使用，避免 `dotnet run` 子程序攔截 Ctrl+C）：

```powershell
.\artifacts\cli-manual\Vulperonex.Cli.exe --interactive
```

#### 5b-1 Tab 補全

| 輸入序列 | 預期 |
|----------|------|
| `ru<Tab>` | 顯示 `rule ` |
| `rule li<Tab>` | 顯示 `rule list` |
| `twitch a<Tab>` | 顯示 `twitch auth ` |
| `twitch auth st<Tab>` | 顯示 `twitch auth start` |
| `xy<Tab>` | buffer 不變、無噪音輸出 |

#### 5b-2 歷史巡覽（↑ / ↓）

1. 連續輸入 `rule list<Enter>`、`member list<Enter>`、`config get log.min_level<Enter>`。
2. 按 `↑` 一次 → 預期顯示 `config get log.min_level`。
3. 按 `↑` 兩次 → 預期顯示 `member list`。
4. 按 `↑` 三次 → 預期顯示 `rule list`。
5. 再按 `↑` → buffer 不變（已到最舊一筆）。
6. 按 `↓` → 退回 `member list`、`config get log.min_level`、最後一筆下方為空 buffer。
7. 輸入新命令時去重規則：連續送出兩次相同 `rule list`，歷史只保留一份（按 `↑` 依序回退 `config ... / member list / rule list`，不重複出現 `rule list`）。

#### 5b-3 Ctrl+C 清 buffer

1. 在 prompt 輸入半行 `rule lis`（**不**按 Enter）。
2. 按 `Ctrl+C`。
3. 預期：當前行顯示 `^C`、換到新行、prompt `vulperonex> ` 重印、REPL 仍存活。
4. 立即輸入 `exit<Enter>` → REPL 正常結束、exit code 0。

#### 5b-4 Ctrl+C 取消 `twitch auth start`

需 `Twitch:ClientId` 設定但 **未**完成授權的環境（或刻意 `Remove-Item Env:Twitch__ClientSecret` 走 device flow / 走 confidential flow 後直接取消瀏覽器）。

**Confidential client 路徑：**
1. REPL 內輸入 `twitch auth start<Enter>`。
2. CLI 印 `Opened Twitch authorization URL. Waiting on http://localhost:<port>/auth/callback`，瀏覽器開啟 Twitch 授權頁。
3. **不要**在瀏覽器同意；切回終端機 B，按 `Ctrl+C`。
4. 預期：
   - stderr 出現 `TWITCH_OAUTH_CANCELLED`。
   - `vulperonex> ` prompt 重印，REPL 繼續存活。
   - 終端機 A Web host 不出現未處理例外。
   - 瀏覽器若仍開著、後續完成授權 → callback 已無 listener 在等，瀏覽器顯示 connection refused 屬正常（state 已作廢）。

**Public client (device flow) 路徑：**
1. REPL 內輸入 `twitch auth start<Enter>`。
2. CLI 印 `Twitch public-client authorization` + `Open: <url>` + `Code: <user_code>`。
3. **不要**在瀏覽器輸入 code；終端機 B 按 `Ctrl+C`。
4. 預期同上 confidential 路徑（`TWITCH_OAUTH_CANCELLED` + REPL 存活）。

#### 5b-5 LineEditor 與 Ctrl+C 分流驗證

**Buffer 有內容時的 Ctrl+C 不得取消 dispatch：** 在 prompt 輸入 `rule lis`、按 `Ctrl+C` 後**不應**有 `TWITCH_OAUTH_CANCELLED` 或其他 dispatch error code 出現於 stderr（因為尚未進入 dispatch）；僅當前 buffer 被清。

**Dispatch 已執行時的 Ctrl+C：** 5b-4 涵蓋。`twitch auth start` 為唯一已實作 Ctrl+C 取消的命令；其他命令（`rule list` 等）回應時間短，Ctrl+C 觀察不到取消行為屬於預期。

#### 5b-6 redirected stdin 後備

```powershell
"rule list`nexit" | dotnet run --project src\Hosts\Vulperonex.Cli
```

預期：列出 `rule list` JSON 後 exit 0；不啟動 LineEditor（無 ANSI / 按鍵錯誤）。

#### 5b-7 驗收欄位

於本檔「狀態」段以下列格式追加：

```
### <YYYY-MM-DD> REPL 互動驗證｜驗證者：<name>
- 終端機：Windows Terminal vX / PowerShell 7.X
- 5b-1 Tab：PASS / FAIL（記錄 `twitch auth st` 實際輸出）
- 5b-2 歷史：PASS / FAIL
- 5b-3 Ctrl+C 清 buffer：PASS / FAIL
- 5b-4 Ctrl+C 取消 OAuth：PASS / FAIL（路徑：confidential / device）
- 5b-5 分流：PASS / FAIL
- 5b-6 redirected stdin：PASS / FAIL
- 備註：
```

### 6. 清理

```powershell
Remove-Item Env:Twitch__ClientId
Remove-Item Env:Twitch__ClientSecret
Remove-Item Env:VULPERONEX_API_URL
# 終端機 A 按 Ctrl+C 結束 Web host
```

如需永久設定 ClientId：

```powershell
[Environment]::SetEnvironmentVariable('Twitch__ClientId', '<id>', 'User')
```

> **Security：** `Twitch:ClientId` 為 OAuth 公開值；`Twitch:ClientSecret` 是秘密值，只有 confidential client 需要。Public client 不應設定 secret。若使用 secret，只能放在本機 ignored 的開發設定或一次性環境變數，不能 commit 到 repo。

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
