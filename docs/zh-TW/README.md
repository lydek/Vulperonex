# Vulperonex

> **Language / 語言**: [English](../../README.md) | [繁體中文](README.md)

直播輔助平台 — 整合 Twitch 事件流、會員忠誠度、Overlay 廣播、Workflow 規則引擎與外掛模組管理。

## 文件語系策略

- 英文為預設文件語言，保留原始檔名。
- 在地化 Markdown 文件放在 `docs/<locale>/` 之下，並與英文來源維持相同的相對路徑與純淨檔名。
- 繁體中文文件使用 `docs/zh-TW/` 目錄樹。
- 不使用 `*.zh-TW.md` 這類語系後綴命名；改採專用語系資料夾策略。

本專案由四個可執行 Host 組成：

| Host | 用途 | OutputType | TargetFramework |
|---|---|---|---|
| `Vulperonex.Web` | ASP.NET Core API + SignalR Hub + 靜態 Overlay 站台 | `Exe` | `net10.0` |
| `Vulperonex.Cli` | 主控台 CLI (member / rule / simulate / twitch / timer / config) | `Exe` | `net10.0` |
| `Vulperonex.Desktop` | Windows 桌面殼 (WebView2 + 內嵌 Web Host) | `WinExe` | `net10.0-windows` |
| `frontend/` | Vue 3 SPA Admin UI (Vite + Pinia + PrimeVue + Monaco) | n/a | n/a |

---

## 系統需求

| 工具 | 版本 | 用途 |
|---|---|---|
| .NET SDK | 10.0+ | 編譯所有 C# 專案 |
| Node.js | 20.x LTS+ | 前端 toolchain |
| pnpm | 9.15.4 | 前端套件管理（`package.json` 內已指定） |
| PowerShell | 5.1+ / 7+ | Windows 開發環境 |
| Git | 2.40+ | 版本控制 |

> Windows 桌面 Host (`Vulperonex.Desktop`) 需要 Windows 10 1809+ 與 WebView2 Runtime。

---

## 取得原始碼

```powershell
git clone <repo-url> Vulperonex
cd Vulperonex
dotnet restore Vulperonex.sln
cd src/frontend
pnpm install --frozen-lockfile
cd ../..
```

---

## 後端 — Vulperonex.Web（API + Overlay）

### 建置

```powershell
dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false
```

> `/m:1 /nr:false /p:UseSharedCompilation=false` 為專案約定旗標，避免 MSBuild node reuse 導致 Windows 上 file lock。

### 執行

```powershell
dotnet run --project src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj
```

預設行為：

- 監聽 loopback (`127.0.0.1`) port pair（API + Overlay）由 `PortPairAllocator` 動態配置；啟動時 console 印出實際 URL。
- 環境變數 `ASPNETCORE_ENVIRONMENT=Development` 啟動 dev 例外頁。
- 第一次啟動會在使用者 AppData 下產生：
  - `machine-key`（HMAC ETag 簽章）
  - `.admin-csrf-token`（per-process 隨機 CSRF token）

### 開發者旗標

| 環境變數 | 預設 | 說明 |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` 開啟例外頁與詳細 log |
| `Security:CsrfTokenPath` | 使用者 AppData | 覆寫 CSRF token 檔案路徑（測試常用） |

### 資料庫遷移

SQLite 自動隨應用啟動 `Migrate()`。手動套用：

```powershell
dotnet ef database update --project src/Vulperonex.Infrastructure --startup-project src/Hosts/Vulperonex.Web
```

新增 migration：

```powershell
dotnet ef migrations add <Name> --project src/Vulperonex.Infrastructure --startup-project src/Hosts/Vulperonex.Web
```

---

## CLI — Vulperonex.Cli

### 建置 + 執行

```powershell
dotnet run --project src/Hosts/Vulperonex.Cli -- <command> [args]
```

或自包式發行：

```powershell
dotnet publish src/Hosts/Vulperonex.Cli -c Release -r win-x64 --self-contained false -o artifacts/cli
artifacts/cli/Vulperonex.Cli.exe --help
```

### 內建指令樹

| 群組 | 子指令 | 用途 |
|---|---|---|
| `member` | `list` / `show` / `adjust-loyalty` / `reset` / `delete` / `audit` | 會員管理 + 審計查詢 |
| `rule` | `list` / `enable` / `disable` / `show` | Workflow 規則切換 |
| `simulate` | `event` / `checkin` | 模擬事件 / 打卡 |
| `twitch` | `login` / `status` / `logout` | OAuth 流程 |
| `timer` | `list` / `trigger` | 計時器 workflow |
| `config` | `get` / `set` / `list` | SystemSetting 鍵值 |

範例：

```powershell
dotnet run --project src/Hosts/Vulperonex.Cli -- member list --limit 20
dotnet run --project src/Hosts/Vulperonex.Cli -- simulate checkin --user testuser --platform twitch
```

### 多語

CLI 透過 `Resources/I18n/{en-US,zh-TW}.json` 載入字串；用 `CULTURE` 環境變數覆寫：

```powershell
$env:CULTURE = "zh-TW"; dotnet run --project src/Hosts/Vulperonex.Cli -- --help
```

---

## Desktop — Vulperonex.Desktop（Windows 殼）

### 建置 + 執行

```powershell
dotnet run --project src/Hosts/Vulperonex.Desktop
```

行為：

- 啟動內嵌 `Vulperonex.Web` host (loopback only)。
- WebView2 載入 admin SPA。
- 關閉 window 即關閉所有背景 service。

### 發行

```powershell
dotnet publish src/Hosts/Vulperonex.Desktop -c Release -r win-x64 --self-contained true -o artifacts/desktop
```

產出 `artifacts/desktop/Vulperonex.Desktop.exe`。

> 僅 Windows 可建（`net10.0-windows` TargetFramework）。在 Linux/Mac 上 `dotnet build` 會跳過此 host。

---

## 前端 UI — `src/frontend/`

### 安裝

```powershell
cd src/frontend
pnpm install --frozen-lockfile
```

### 開發模式

```powershell
pnpm dev
```

- Vite dev server 監聽 `127.0.0.1:5173`（host 鎖 loopback）。
- 透過 vite proxy 轉發 `/api/*` + `/hubs/*` 到後端 Web host。
- HMR 啟用。需要後端先跑起來才能呼叫 admin API。

### 生產建置

```powershell
pnpm build
```

執行兩步：

1. `vue-tsc --noEmit` — 型別檢查（編譯不出 .d.ts，純驗證）
2. `vite build` — 打包至 `src/frontend/dist/`

後端 Web host 啟動時透過 `UseStaticFiles` 直接服務該目錄。

### Lint

```powershell
pnpm lint     # oxlint
pnpm vue-tsc  # 純型別檢查
```

---

## 測試

### 後端 C#

整個方案三個測試專案：

| 專案 | 數量 | 用途 |
|---|---|---|
| `Vulperonex.Tests.Architecture` | 19 | NDepend-style 依賴方向、命名規約、層級邊界 |
| `Vulperonex.Tests.Unit` | 210 | 純邏輯單元測試（無 DB / 無 HTTP） |
| `Vulperonex.Tests.Integration` | 219 | `WebApplicationFactory` + SQLite + 全 HTTP/Hub 端對端 |

執行全套：

```powershell
dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false
```

僅單元：

```powershell
dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj
```

僅整合：

```powershell
dotnet test tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj
```

過濾單一測試：

```powershell
dotnet test --filter "FullyQualifiedName~MemberMutationEndpointTests"
```

> 整合測試的 `CreateClient` 會：
> - 配置 per-test 臨時 `Security:CsrfTokenPath`，避免並發 IO 鎖死
> - 從 DI 取 `AdminCsrfTokenProvider.Token` 設為 `X-Admin-Csrf` 標頭
> - 注入對齊本機 host 的 `Origin` + `Referer` 雙標頭

### 前端 Vitest

```powershell
cd src/frontend
pnpm test
```

執行 `vitest run --coverage`：

- 34 個測試檔，167 個案例
- 覆蓋率報告輸出至 `src/frontend/coverage/`
- 環境 `jsdom`，自動偵測 `MODE === "test"` 略過實際 CSRF token fetch

watch 模式：

```powershell
pnpm vitest
```

### 全測試一鍵

```powershell
dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false
cd src/frontend; pnpm vue-tsc --noEmit; pnpm test; pnpm build; pnpm lint
```

---

## 完整 Checkpoint 流程

phase 收尾必跑：

```powershell
# 1. 後端 build
dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false

# 2. 後端 test
dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false

# 3. 前端 type-check + test + build + lint
cd src/frontend
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
cd ../..
```

四步全綠才可 merge。

---

## 專案結構

```
Vulperonex/
├── Vulperonex.sln
├── Directory.Build.props        # net10.0 + C# 14 + Nullable enable
├── Directory.Packages.props     # 中央套件版本鎖定
├── src/
│   ├── Vulperonex.Domain/                 # 純領域模型 + Event
│   ├── Vulperonex.Application/            # Use case + interface
│   ├── Vulperonex.Infrastructure/         # EF Core + 外部整合
│   ├── Adapters/
│   │   ├── Vulperonex.Adapters.Twitch/
│   │   ├── Vulperonex.Adapters.OneComme/
│   │   └── Vulperonex.Adapters.Simulation/
│   ├── Plugins/
│   │   └── Vulperonex.Plugins.Abstractions/
│   ├── Hosts/
│   │   ├── Vulperonex.Web/                # API + SignalR + Static
│   │   ├── Vulperonex.Cli/                # Console CLI
│   │   └── Vulperonex.Desktop/            # WebView2 殼
│   └── frontend/                          # Vue 3 SPA
├── tests/
│   ├── Vulperonex.Tests.Architecture/
│   ├── Vulperonex.Tests.Unit/
│   └── Vulperonex.Tests.Integration/
└── docs/
    └── phases/                            # 階段 plan + todo + verification
```

---

## 文件

- `CONTRIBUTING.md` — 外掛開發約定
- `docs/SPEC.md` — 系統規格
- `docs/phases/` — 各 phase plan / todo / manual-verification
- `docs/cli.md` — CLI 完整指令參考（若存在）

---

## 疑難排解

| 症狀 | 解法 |
|---|---|
| `dotnet build` 失敗：file is locked | 確認加上 `/m:1 /nr:false /p:UseSharedCompilation=false` |
| 整合測試 CSRF 403 | 確認測試從 DI 取 `AdminCsrfTokenProvider.Token`，未 hardcode `"true"` |
| 前端 dev 連不到 API | 後端先啟動，確認 vite proxy 目標 port 對齊 console 印出 URL |
| `pnpm install` 卡住 | 刪 `src/frontend/node_modules` + `pnpm-lock.yaml` 後重裝 |
| SQLite 遷移錯誤 | 刪 `%LOCALAPPDATA%/Vulperonex/*.db` 重新啟動觸發自動 migrate |
| Desktop 啟動白屏 | 安裝 [WebView2 Runtime Evergreen](https://developer.microsoft.com/microsoft-edge/webview2/) |

---

## 授權

見 repo 根目錄 LICENSE 檔（若有）。
