# Vulperonex 快速測試：聊天回應規則除錯指南 (Cookbook)

本指南引導開發者與測試代理進行 E2E 完整聊天回應鏈的除錯，從啟動伺服器、建立規則、訂閱 SignalR Overlay 直到發送模擬訊息並觀察回應結果。

---

## 1. 快速除錯流程

### 步驟 1：啟動 Web 主機 (Server)
啟動本機 API 與雙埠宿主。伺服器將自動套用 SQLite 資料庫移轉，無需手動執行移轉命令。
* **命令**：
  ```powershell
  # 切換至專案根目錄
  dotnet run --project src/Hosts/Vulperonex.Web --launch-profile "http"
  ```
* **觀察點與通過條件**：
  | 觀察物件 | 通過條件 / 預期結果 |
  | :--- | :--- |
  | 主控台日誌 | 顯示 Kestrel 成功綁定至 Loopback 雙端點（例如 `http://127.0.0.1:5000` 與 `http://[::1]:5000`） |
  | 資料庫狀態 | 若是新環境，自動在資料庫路徑建立 SQLite `Vulperonex.db` 並完成 table 初始化 |

---

### 步驟 2：使用 CLI 建立聊天回應規則 (Create Rule)
使用 CLI 工具載入預設的最小回應規則檔案 `rule-chat-echo.json`。
* **命令**：
  ```powershell
  $env:VULPERONEX_API_URL = "http://127.0.0.1:5000"
  .\artifacts\cli-manual\Vulperonex.Cli.exe rule create docs/phases/phase-5_5-rapid-test/examples/rule-chat-echo.json
  ```
* **觀察點與通過條件**：
  | 觀察物件 | 通過條件 / 預期結果 |
  | :--- | :--- |
  | CLI 輸出 | 輸出建立成功的 Rule JSON 摘要，包含新生成的 `id`（例如 `01H...` ULID） |
  | 結束程式碼 | `exit 0` (成功) |

---

### 步驟 3：在瀏覽器中訂閱 Overlay 聊天框 (Overlay Subscription)
開啟瀏覽器或在 OBS 中加入瀏覽器來源，連接至 SignalR Chat Hub 頁面。
* **網址**：
  `http://127.0.0.1:5000/overlay/chat.html`
* **觀察點與通過條件**：
  | 觀察物件 | 通過條件 / 預期結果 |
  | :--- | :--- |
  | 瀏覽器主畫面 | 頁面載入正常，顯示為乾淨的透明或深色背景，Vue 用戶端成功載入 |
  | 開發者工具 (Console) | `SignalR connected` 無連線失敗或 4xx/5xx 錯誤日誌 |

---

### 步驟 4：模擬聊天訊息輸入 (Simulate Chat)
透過 CLI 模擬平台發布一條使用者聊天訊息，觸發工作流引擎。
* **命令**：
  ```powershell
  .\artifacts\cli-manual\Vulperonex.Cli.exe simulate chat "hello world from cookbook"
  ```
* **觀察點與通過條件**：
  | 觀察物件 | 通過條件 / 預期結果 |
  | :--- | :--- |
  | CLI 輸出 | 輸出包含 `eventId` 與 `accepted: true` 的 JSON 響應 |
  | Overlay 瀏覽器畫面 | 在 **0.5 秒內** 動態浮現一條包含 `Echo: hello world from cookbook` 的聊天訊息，且樣式美觀 |

---

### 步驟 5：清理測試規則 (Cleanup)
測試完成後，刪除建立的臨時回應規則，以維持環境乾淨。
* **命令**：
  ```powershell
  .\artifacts\cli-manual\Vulperonex.Cli.exe rule delete <rule-id> --yes
  ```
* **觀察點與通過條件**：
  | 觀察物件 | 通過條件 / 預期結果 |
  | :--- | :--- |
  | CLI 輸出 | 顯示 `OK rule deleted: <rule-id>` |
  | 結束程式碼 | `exit 0` |

---

## 2. 除錯與防禦記錄 (Verification Record)

### 2026-05-24 - AI Agent 自動化整合鏈防禦驗證 (Equivalence & SignalR Integration)
* **驗證者**：Antigravity (AI Coding Agent)
* **測試環境**：Windows 11, SQLiteInMemory 整合測試沙盒, 隨機分配 Socket 埠 Kestrel 實例
* **測試方法**：
  執行 `tests/Vulperonex.Tests.Integration/RapidTest/ChatReplyChainTests.cs`。該測試自動化複製了上述步驟 1 至 5 的全套流程，並對 SignalR 即時回應負載、ULID 與 outbox side-effects 進行強斷言。
* **驗證命令**：
  ```powershell
  rtk dotnet test --filter "FullyQualifiedName=Vulperonex.Tests.Integration.RapidTest.ChatReplyChainTests"
  ```
* **預期結果**：
  SignalR Client 在 5 秒內精確捕獲 `"Echo: hello"` 訊息，且 outbox 儲存此回應。
* **實際結果**：
  `ok dotnet test: 1 tests passed`
* **驗證狀態**：**PASS**

### 2026-05-20 - 人類物理 E2E 手動整合與 Twitch OAuth 驗收
* **驗證者**：lydek (專案負責人)
* **測試環境**：Windows PowerShell, 本地 `Vulperonex.Web` 主機 (`http://127.0.0.1:5000`)
* **測試方法**：
  人肉啟動 Web API，透過 PowerShell 連接 REPL 互動終端機，輸入 `rule create`、`simulate chat`，並在瀏覽器中人眼觀察 Overlay 渲染；同時成功完成 Twitch PKCE OAuth 授權與 SQLite 加密儲存。
* **驗證狀態**：**PASS** (詳見 `manual-verification.md` 歷史記錄)
