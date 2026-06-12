# Vulperonex

> **Language / 語言**: [English](../../README.md) | [繁體中文](README.md)

Vulperonex 是一個直播輔助平台，提供 Twitch 工作流、會員忠誠度、打卡集點卡、OBS Overlay、計時器、規則以及模組化整合等功能。

此儲存庫包含 ASP.NET Core 主機、Vue 後台 UI、桌面端主機、CLI 主機、工作流執行期、測試以及相關輔助文件。

## 快速開始

請使用儲存庫根目錄的開發腳本。

首次設定：

```powershell
.\scripts\dev.ps1 restore
.\scripts\dev.ps1 build
```

日常啟動：

```powershell
.\scripts\dev.ps1 run-web
```

網頁主機將會啟動 API、後台 UI 靜態主機、SignalR 中繼站（SignalR Hubs）以及 Overlay 端點。

啟動後，開啟主控台輸出的本機 URL。API 連接埠通常預設為 `5000`，但如果預設連接埠已被佔用，Vulperonex 會自動選擇下一組可用的連接埠對。

預設的第一個 URL：

```text
http://localhost:5000
```

## 系統需求

- Windows PowerShell 5.1 或 PowerShell 7+
- .NET SDK 10+
- Node.js 20+
- pnpm 9.15.4
- Git

前端套件宣告使用 `pnpm@9.15.4`。如果已安裝 Corepack，請執行一次啟用指令：

```powershell
corepack enable
```

## 開發腳本

主要進入點為 [scripts/dev.ps1](../../scripts/dev.ps1)。它將常用的建置、測試與執行指令整合在同一個地方。

| 任務 | 指令 | 用途 |
| --- | --- | --- |
| 說明 | `.\scripts\dev.ps1 help` | 顯示可用的任務。 |
| 還原 | `.\scripts\dev.ps1 restore` | 還原 NuGet 套件並安裝前端套件。 |
| 安裝前端套件 | `.\scripts\dev.ps1 install` | 僅執行 pnpm install。 |
| 建置全部 | `.\scripts\dev.ps1 build` | 建置後端方案與前端資產。 |
| 建置後端 | `.\scripts\dev.ps1 build-backend` | 建置 `Vulperonex.sln`。 |
| 建置前端 | `.\scripts\dev.ps1 build-frontend` | 在 `src/frontend` 中執行 `pnpm build`。 |
| 測試全部 | `.\scripts\dev.ps1 test` | 執行後端與前端測試。 |
| 測試後端 | `.\scripts\dev.ps1 test-backend` | 執行方案的 `dotnet test`。 |
| 測試前端 | `.\scripts\dev.ps1 test-frontend` | 執行 Vitest 並輸出測試覆蓋率。 |
| 檢查 UI 型別 | `.\scripts\dev.ps1 typecheck` | 執行 Vue TypeScript 檢查。 |
| 檢查 UI 語法 | `.\scripts\dev.ps1 lint` | 執行 oxlint 程式碼檢查。 |
| 執行網頁主機 | `.\scripts\dev.ps1 run-web` | 啟動 `Vulperonex.Web`。 |
| 執行前端開發伺服器 | `.\scripts\dev.ps1 run-frontend` | 在 `127.0.0.1` 啟動 Vite 開發伺服器。 |
| 執行桌面端主機 | `.\scripts\dev.ps1 run-desktop` | 啟動 `Vulperonex.Desktop`。 |

範例：

```powershell
.\scripts\dev.ps1 build -Configuration Release
.\scripts\dev.ps1 test-backend -Filter "WorkflowEngineTests"
.\scripts\dev.ps1 run-web
.\scripts\dev.ps1 run-frontend
```

當 `pnpm` 可用時，此腳本會自動使用它。如果 `PATH` 中沒有 `pnpm`，則會退回使用 `corepack pnpm`
。

後端建置與測試任務使用對 Windows 友善的 MSBuild 旗標，以減少檔案鎖定問題：

```text
/m:1 /nr:false /p:UseSharedCompilation=false
```

## 常見工作流

### 首次設定

```powershell
.\scripts\dev.ps1 restore
.\scripts\dev.ps1 build
```

### 執行應用程式

```powershell
.\scripts\dev.ps1 run-web
```

開啟網頁主機輸出的 URL。通常為 `http://localhost:5000`，但如果 `5000` 連接埠已被佔用，則可能是鄰近的其他連接埠。

### 執行前端開發伺服器

```powershell
.\scripts\dev.ps1 run-frontend
```

這在 API 主機已啟動時，對於進行 UI 迭代開發非常有用。

### 執行測試

```powershell
.\scripts\dev.ps1 test
```

僅執行指定的後端測試：

```powershell
.\scripts\dev.ps1 test-backend -Filter "TriggerMetadataProviderTests"
```

僅執行前端檢查：

```powershell
.\scripts\dev.ps1 typecheck
.\scripts\dev.ps1 test-frontend
.\scripts\dev.ps1 lint
```

## OBS Overlay 連結

請先啟動網頁主機：

```powershell
.\scripts\dev.ps1 run-web
```

接著開啟主控台顯示的後台 UI URL，並從 Overlay 或設定區域複製 OBS 瀏覽器來源的 URL。

當 OBS 與本專案在同一台機器上執行時，請使用本機 URL（local URL）；僅當 OBS 執行於同網路的另一台機器，且已在設定中啟用區域網路 Overlay 存取時，才使用區網 URL（LAN URL）。由於本機 IP 可能會隨時間變動，區網複製動作應根據目前偵測到的 IP 產生 URL。

## 手動指令

建議優先使用腳本。以下指令適用於需要直接偵錯工具鏈時。

```powershell
dotnet restore .\Vulperonex.sln
dotnet build .\Vulperonex.sln -c Debug /m:1 /nr:false /p:UseSharedCompilation=false
dotnet test .\Vulperonex.sln -c Debug /m:1 /nr:false /p:UseSharedCompilation=false
corepack pnpm --dir .\src\frontend install --frozen-lockfile
corepack pnpm --dir .\src\frontend build
dotnet run --project .\src\Hosts\Vulperonex.Web\Vulperonex.Web.csproj
```

## 專案目錄配置

```text
src/
  Hosts/
    Vulperonex.Web/      ASP.NET Core API、SignalR、後台 UI 主機、Overlay
    Vulperonex.Desktop/  桌面端主機
    Vulperonex.Cli/      CLI 主機
  frontend/              Vue 3 後台 UI
  Vulperonex.*           領域、應用程式、基礎設施、外掛模組
tests/
  Vulperonex.Tests.Unit/
  Vulperonex.Tests.Integration/
  Vulperonex.Tests.Architecture/
docs/                    規格、階段說明與在地化文件
scripts/                 本地開發腳本
```

## 疑難排解

### PowerShell 執行原則

如果 PowerShell 封鎖了腳本執行，可在本次呼叫中使用 bypass 參數執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 help
```

### pnpm 儲存庫不符

如果 pnpm 回報非預期的 store 位置，請重新安裝前端相依套件：

```powershell
.\scripts\dev.ps1 install
```

### 鎖定的 .NET 建置檔案

停止任何執行中的 `Vulperonex.Web` 或 `Vulperonex.Desktop` 程式，然後重新執行：

```powershell
.\scripts\dev.ps1 build-backend
```

### Twitch 憑證

Twitch 整合設定是經由後台 UI 管理的。本地開發在沒有生產環境憑證的情況下仍可運作，但與 Twitch 相關的特定功能則需要配置應用程式憑證，以及授權實況主或機器人帳戶。

## 文件

專案規格與階段說明位於 [docs](../../docs/)。繁體中文文件則位於 [docs/zh-TW](./) 下。
