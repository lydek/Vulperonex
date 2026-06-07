# Vulperonex CLI 指令參考

`Vulperonex.Cli` 是本機 admin 主控台。預設會對 `http://localhost:5000` 發送 loopback Web API 請求；可用 `VULPERONEX_API_URL` 指向其他 loopback base URL。CLI 會自動補上 AdminGuard 需要的 `Origin` / `Referer`，mutation request 會嘗試取得 `/api/overlay/csrf-token` 並帶上 `X-Admin-Csrf`。

> 使用 CLI 前需先啟動 `Vulperonex.Web` 或其他相容 Web host。`VULPERONEX_API_URL` 只允許 `localhost` / loopback HTTP(S)，非 loopback 會回 `CLI_API_URL_NOT_LOOPBACK`。

```powershell
# 開發模式
dotnet run --project src/Hosts/Vulperonex.Cli -- <command> [args]

# 指向自訂 loopback API
$env:VULPERONEX_API_URL = "http://localhost:5001"
dotnet run --project src/Hosts/Vulperonex.Cli -- member list
```

語系切換（預設 `en-US`）：

```powershell
$env:CULTURE = "zh-TW"
dotnet run --project src/Hosts/Vulperonex.Cli -- --help
```

未提供 args 時進入互動模式；也可用 `--interactive` / `-i` 明確進入互動模式。

---

## help

```powershell
Vulperonex.Cli help
Vulperonex.Cli help <group>
```

---

## member - 會員管理

| 子指令 | 用途 |
| --- | --- |
| `member list` | 列出會員 |
| `member show <member-id\|prefix>` | 顯示單一會員 |
| `member seed <platform-user-id> [display-name]` | 透過 `/api/simulate/chat` 建立或更新模擬會員 |
| `member delete <member-id\|prefix> [--yes]` | 取得 delete token 後刪除會員與平台身份 |

範例：

```powershell
Vulperonex.Cli member list
Vulperonex.Cli member show 01H
Vulperonex.Cli member seed test-user "Test User"
Vulperonex.Cli member delete 01H --yes
```

---

## rule - Workflow 規則

| 子指令 | 用途 |
| --- | --- |
| `rule list` | 列出規則 |
| `rule show <rule-id\|prefix\|--name <name>>` | 顯示規則 JSON |
| `rule create <rule.json>` | 從 JSON 檔建立規則 |
| `rule update <rule-id\|prefix\|--name <name>> <rule.json>` | 從 JSON 檔更新規則 |
| `rule enable <rule-id\|prefix\|--name <name>>` | 啟用規則 |
| `rule disable <rule-id\|prefix\|--name <name>> [--yes]` | 停用規則 |
| `rule delete <rule-id\|prefix\|--name <name>> [--yes]` | 刪除規則 |

```powershell
Vulperonex.Cli rule list
Vulperonex.Cli rule show --name "Daily check-in"
Vulperonex.Cli rule create .\rule.json
Vulperonex.Cli rule disable --name "Daily check-in" --yes
```

---

## simulate - 模擬事件

| 子指令 | 用途 |
| --- | --- |
| `simulate chat [message] [--user-id <id>] [--display-name <name>]` | 模擬聊天訊息 |
| `simulate follow [--user-id <id>] [--display-name <name>]` | 模擬追隨事件 |
| `simulate sub [--user-id <id>] [--display-name <name>] [--tier <tier>]` | 模擬訂閱事件 |
| `simulate checkin [--user-id <id>] [--display-name <name>] [--stamp-count <count>] [--skip-cooldown]` | 模擬會員打卡 |

```powershell
Vulperonex.Cli simulate chat "hello" --user-id testuser --display-name "Test User"
Vulperonex.Cli simulate follow --user-id testuser
Vulperonex.Cli simulate sub --user-id testuser --tier 1000
Vulperonex.Cli simulate checkin --user-id testuser --stamp-count 3 --skip-cooldown
```

---

## twitch - OAuth

| 子指令 | 用途 |
| --- | --- |
| `twitch auth start [--no-browser]` | 啟動 Twitch OAuth；依設定走 browser callback 或 public-client device flow |
| `twitch auth reset` | 清除已儲存的 Twitch refresh token |

```powershell
Vulperonex.Cli twitch auth start
Vulperonex.Cli twitch auth start --no-browser
Vulperonex.Cli twitch auth reset
```

---

## timer - 計時器 workflow

| 子指令 | 用途 |
| --- | --- |
| `timer list` | 列出 workflow timers |
| `timer show <timer-id>` | 顯示單一 timer |
| `timer create <rule-id> <interval-seconds> <next-fire-at-iso> [--disabled]` | 建立 timer |
| `timer delete <timer-id> [--yes]` | 刪除 timer |

```powershell
Vulperonex.Cli timer list
Vulperonex.Cli timer create 01H 3600 2026-05-26T12:00:00Z
Vulperonex.Cli timer delete 01H --yes
```

---

## config - SystemSetting

| 子指令 | 用途 |
| --- | --- |
| `config get <key>` | 讀取設定值 |
| `config set <key> <value>` | 寫入設定值 |

```powershell
Vulperonex.Cli config get modules.enabled.checkin
Vulperonex.Cli config set modules.enabled.checkin false
```

---

## 退出碼

| Code | 意義 |
| --- | --- |
| 0 | 成功 |
| 1 | 一般執行錯誤或 HTTP request 失敗 |
| 64 | unknown command |
| 65 | data error；例如 member / rule 找不到 |

---

## 測試

| 指令 | 用途 |
| --- | --- |
| `dotnet test tests/Vulperonex.Tests.Integration --filter "FullyQualifiedName~Cli"` | 執行 CLI 整合測試 |

## 設計約定

- CLI 只允許 loopback API base URL。
- CLI command tree 與 `src/Hosts/Vulperonex.Cli/Resources/I18n/*.json` 的 usage 字串需保持同步。
- 需要確認的 destructive command 支援 `--yes` 時才可略過互動確認。
- CLI 不直接注入 Application service；行為應等同 Web API 入口，包含 loopback gate、CSRF、mutation validation 與 audit side effect。
