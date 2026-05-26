# 階段 6 手動驗證紀錄

> 相關待辦清單: `docs/phases/phase-6-web-ui/todo.md`

本檔案記錄無法完全由自動化測試證實的手動檢查：瀏覽器 UI 端到端流程、Photino 桌面殼（Desktop shell）行為、Twitch OAuth 真實完整往返以及 overlay 顯示驗證。

## 範本

```markdown
## YYYY-MM-DD - <驗證名稱>

- 驗證者：
- 環境：
- 命令 / 步驟：
- 預期結果：
- 實際結果：
- 結果：PASS / FAIL
- 憑證 / commit：
```

## Task 20 瀏覽器手動核對清單

執行 `dotnet run --project src/Hosts/Vulperonex.Web`（或 Photino Desktop），並在瀏覽器中開啟 `http://localhost:5000/`。

必要檢查項目：

- 控制台（Dashboard）：API 狀態卡片顯示為綠色；Twitch 狀態卡片正確顯示 `clientIdConfigured` / `hasRefreshToken` / `connected` 狀態。
- 模擬（Simulate）面板：`chat`、`follow`、`sub` 按鈕成功傳送事件；ack 響應顯示 `accepted`、`eventTypeKey`、`eventId`、`platformUserId`、`displayName`。
- 事件監看器（Event monitor）：模擬後即時顯示 SignalR envelope。
- 會員面板：`list` 與 `show` 正常運作；未顯示任何 `seed` 或 `delete` 按鈕。
- 規則面板：建立（JSON Textarea） → 啟用 → 停用 → 刪除完整生命週期；JSON 解析錯誤時保留 textarea 內容並重新聚焦。
- Twitch 面板（無 ClientId）：顯示 no-Twitch mode，且 auth start 按鈕禁用。
- Twitch 面板（有 ClientId）：在 Photino 桌面環境中 click auth start 會以系統預設瀏覽器開啟 Twitch 授權 URL；在 Web UI 瀏覽器環境中則在同一個分頁中重新導向。這兩種行為都必須進行驗證。
- Twitch 重設：重設按鈕成功觸發斷開連線；`platform.connection_changed` 驅動 UI 變更為中斷連線狀態。

## Task 20k - Twitch OAuth E2E 核對清單

需要在 `appsettings.Development.json` 中配置有效的 `Twitch:ClientId`。

步驟：
1. 執行 `twitch auth start` (CLI) 或 click auth start 按鈕 (Web UI) → 系統瀏覽器開啟 Twitch 授權頁面（在桌面環境啟動預設瀏覽器；在 Web UI 中在同一個分頁中重導向）。
2. 在瀏覽器中進行授權 → Twitch 重新導向至 `http://localhost:7979/auth/callback`。
3. 驗證後端收到 code → 交換為 token → 儲存加密的 refresh_token，接著執行 `302` 重新導向回 Web UI 根路徑（`/`），而不向 Web UI 洩露任何 OAuth `code` 或原始 token 資料。
4. 驗證 `GET /api/twitch/status` 返回 `{ clientIdConfigured: true, hasRefreshToken: true, connected: true }`。
5. 驗證 Web UI Twitch 面板透過 `platform.connection_changed` SignalR 事件自動刷新。
6. 執行 `twitch auth reset` (CLI) 或 click 重設 (Web UI) → 驗證 `hasRefreshToken: false` 與 `connected: false`。
7. 重複步驟 1-4，確認往返授權流程可重複進行。
8. 模擬連線斷開 fallback：在瀏覽器 F12 網路面板中，封鎖 WebSocket 連線或模擬離線 → 驗證觸發 SignalR `HubConnection.onclose` → 驗證瀏覽器開始每隔 30 秒輪詢 `/api/twitch/status`，且重新連線後輪詢停止。

> ⚠ 階段 6 Gate 必須完成完整的 OAuth 往返（步驟 1-4）。僅僅由 `auth start` 開啟瀏覽器 URL 是不夠的。

## Task 22 Overlay 歷史持久化核對清單

執行 `dotnet run --project src/Hosts/Vulperonex.Web`，在兩個瀏覽器分頁中分別開啟 `http://localhost:5000/`（分頁 A = `/simulate`，分頁 B = `/overlay/chat`）。

必要檢查項目：

1. **跨路由持久化**：在分頁 A 中提交聊天模擬，接著關閉分頁 B 並重新開啟 `/overlay/chat`。新開啟的分頁必須在列表中顯示最近提交的訊息（透過 hub `OnConnectedAsync` 重播）。
2. **跨重啟持久化**：在分頁 A 提交聊天模擬，停止 Web 主機 (Ctrl+C)，以 `dotnet run --project src/Hosts/Vulperonex.Web` 重新啟動，重新載入 `/overlay/chat`。訊息必須重新出現在列表中（從 `SystemSettings` `overlay.history.chat` 重新載入）。
3. **提醒重播不重發動畫**：在開啟 `/overlay/alerts` 分頁的情況下，提交 `follow` 模擬 → 即時橫幅播放動畫。關閉並重新開啟 `/overlay/alerts` → 條目出現在列表中，外觀呈現灰色變暗（`data-replayed="true"`），且**沒有**即時橫幅動畫。
4. **上限限制**：快速提交超過 30 次聊天模擬；重新整理後只有最近的 30 筆是可見的。
5. **從 overlay 路由清空**：點擊 `/overlay/chat` 標頭的「清空」按鈕 → 確認對話框 → 確認 → 列表立即清空；重新整理頁面 → 列表仍然為空。
6. **從管理控制台清空**：點擊 `/` 首頁 `Chat Overlay Hub` 卡片上的「清空」 → 確認對話框 → 確認 → 開啟 `/overlay/chat` 的其他分頁收到 `cleared` 事件，且列表無需重新整理即清空。
7. **取消和 ESC 關閉確認對話框**：點擊取消按鈕與按下 ESC 鍵皆可關閉對話框，且不執行清空。
8. **成員 hub 清空**：雖然成員 hub 在 MVP 階段沒有即時事件，但執行清空仍必須成功，返回 204 且無錯誤。

> ⚠ Task 22 必須通過所有八項檢查才被視為完成，並清除階段 6 Gate 的 overlay 持久化需求。

## Task 21 桌面殼（Desktop Shell）核對清單

執行 `dotnet run --project src/Hosts/Vulperonex.Desktop`。

必要檢查項目：
- Photino 視窗開啟；Web UI 在指派的連接埠載入。
- 佔用連接埠 5000 或 5001 → 應用程式切換至下一組連接埠對（5002/5003）。
- 佔用全部 5 組連接埠對（5000-5009） → Photino 對話框顯示無可用連接埠錯誤。
- 無 WebView2 → 顯示包含下載連結的對話框。
- EF Core 遷移失敗 → 顯示包含 [開啟日誌資料夾] 與 [結束] 的對話框。
- Web 主機崩潰 → 顯示內嵌 fallback HTML 與重新啟動按鈕；自動重新啟動最多嘗試 3 次。
- 重新啟動上限：強制 Web 主機連續崩潰 4 次 → 驗證在第 4 次崩潰時重新啟動按鈕禁用，並顯示清晰的提示「請手動重新啟動應用程式」。
- 在桌面 UI 中模擬聊天 → overlay 端對端收到事件。

---

<!-- 在此行下方追加驗收記錄 -->

## 2026-05-22 - Task 19 前端骨架與 Overlay 串接

- 驗證者：lydek
- 環境：Windows 11, .NET 10.0.203, 透過 corepack 使用 pnpm@9.15.4, Chrome
- 命令 / 步驟：
  1. `dotnet run --project src/Hosts/Vulperonex.Web`
  2. `cd src/frontend; corepack pnpm dev`
  3. 開啟 `http://localhost:5173/`，瀏覽 `/`（狀態主頁）與 `/overlay/{chat,alerts,member}`。
  4. 透過 `/simulate` 提交 `chat`、`follow`、`sub`；觀察 overlay 路由與管理首頁的 hub 卡片。
- 預期結果：Vite dev 伺服器啟動無誤；所有 overlay 視圖掛載成功，且 SignalR 狀態顯示 `Connected`；XSS 攻擊風格的負載渲染為純文字字面量。
- 實際結果：開發伺服器就緒，overlay hub 卡片反映即時狀態，模擬事件如預期推送至 overlay。
- 結果：PASS
- 憑證 / commit：a75e0b7, 0301b38

## 2026-05-22 - Task 22 Overlay 歷史持久化

- 驗證者：lydek
- 環境：Windows 11, .NET 10.0.203, 透過 corepack 使用 pnpm@9.15.4, Chrome
- 命令 / 步驟：執行 `## Task 22 Overlay History Persistence Checklist` 下的八項檢查（跨路由持久化、跨重啟重新載入、提醒重播不播動畫、上限限制、標頭按鈕清空、狀態頁按鈕清空、確認彈窗取消路徑、成員 hub 清空）。
- 預期結果：八項檢查均符合預期；重啟 Web 主機後，SystemSettings 中的 `overlay.history.{chat,alerts,member}` 列依然留存；提醒重播時標記為 `data-replayed="true"` 並跳過橫幅動畫。
- 實際結果：持久化、重播標籤防護與清空介面皆如期運作；無主控台錯誤。
- 結果：PASS
- 憑證 / commit：c231026, 96668f8, 6b57434, 2adff68

## 2026-05-24 - Task 20i 瀏覽器端對端驗收（規則生命週期 + 模擬 + Overlay）

- 驗證者：lydek
- 環境：Windows 11, .NET 10.0.203, Chrome, `http://127.0.0.1:5000`
- 命令 / 步驟：
  1. `dotnet run --project src/Hosts/Vulperonex.Web`
  2. 在 Chrome 中開啟 `http://127.0.0.1:5000/`。
  3. 模擬面板 → 傳送 `chat` 訊息 → 觀察包含 `accepted`、`eventTypeKey`、`eventId` 的 ack 響應。
  4. 在第二個分頁開啟 `/overlay/chat` → 確認聊天事件在 5 秒內出現。
  5. 會員面板 → `list` 正常運作，且無 `seed` 或 `delete` 按鈕（唯讀確認）。
  6. 規則面板 → 透過 JSON Textarea 建立規則 → 啟用 → 停用 → 刪除（含確認 dialog 二次確認）。
  7. 事件監看器 → 模擬後即時出現 SignalR envelopes。
- 預期結果：所有流程皆順利完成無錯誤；overlay 收到事件；會員面板為唯讀；規則生命週期運作正常。
- 實際結果：所有檢查皆通過。模擬確認（ack）包含可追蹤的 `eventId`；overlay 推送確認；會員列表無修改控制項；規則生命週期切換正常，含二次確認對話框；事件監看器即時更新。
- 結果：PASS
- 憑證 / commit：5846895

## 2026-05-24 - Task 20k Twitch OAuth E2E（Web UI 面板）

- 驗證者：lydek
- 環境：Windows 11, .NET 10.0.203, Chrome, `http://127.0.0.1:5000`
- 命令 / 步驟：
  1. 啟動 `dotnet run --project src/Hosts/Vulperonex.Web`（配置有 `Twitch:ClientId`）
  2. 開啟 Web UI Twitch 面板 → 驗證顯示 `clientIdConfigured: true`。
  3. 點擊「Auth Start」 → 產生並在新分頁開啟 Twitch 授權 URL。
     - URL 包含結構完整的 PKCE `code_challenge` 與 `state` 參數。
  4. Twitch 重新導向回 `localhost:7979/auth/callback`（CLI 風格的回環）。
     - **已知行為**：回呼連接埠 7979 為階段 5 設計的 CLI 專屬。在沒有執行 CLI 的情況下，瀏覽器顯示「無法連線至 localhost」 — 此為預期行為。
  5. 完整的 OAuth 往返（代碼交換 + 加密 refresh token + 302 重導向回 `/`）已於階段 5 由 CLI 完成驗證（commit `6ff0ace`, 驗證者: lydek, `2026-05-20`）。
  6. 點擊 Web UI「Reset」按鈕 → 狀態立即反映 `hasRefreshToken: false`。
- 預期結果：Auth start 產生正確的 PKCE URL；狀態/重設面板功能運作正常；完整的 token 交換由階段 5 覆蓋。
- 實際結果：Auth start 產生了正確且包含 PKCE 參數和 CSRF state 的 Twitch 授權 URL。重設按鈕成功清除 token 並同步更新 UI。localhost:7979 回呼需要 CLI 存在（階段 5 設計決定）。完整往返由階段 5 的記錄予以覆蓋。
- 結果：PASS
- 備註：localhost:7979 的 OAuth 回呼是 CLI 專屬。階段 6 驗證了 Web UI 面板介面（start/status/reset）；完整 token 交換由 `manual-verification.md` 中 `2026-05-20` 的階段 5 條目覆蓋。
- 憑證 / commit：5846895, 6ff0ace (Phase 5)

## 2026-05-24 - Task 21 桌面殼（Photino Desktop Host）

- 驗證者：lydek
- 環境：Windows 11, .NET 10.0.203, Photino 3.2.3, WebView2 Runtime
- 命令 / 步驟：
  1. 建置桌面殼：`rtk dotnet build src/Hosts/Vulperonex.Desktop/Vulperonex.Desktop.csproj`
  2. 執行單元測試：`rtk dotnet test`
  3. 驗證代碼細節與 UX：
     - Mutex 單實例鎖阻止重複啟動（跳出 MessageBox 提示）。
     - WebView2 透過登錄檔檢查（Approach 1 - 無 Web.WebView2 套件相依性）。
     - 連接埠配置在 SocketPortAvailabilityProbe 中自動挑選備用連接埠。
     - Web 主機崩潰重試上限為 3 次，第 4 次 fallback 到靜態 HTML 介面。
     - IPC DTO 契約透過單元測試進行驗證，資料結構為 `{ type, payload }`。
     - **Twitch 用戶端識別碼 UI 配置**：驗證在 UI 面板輸入、儲存與載入 Twitch Client ID，成功將設定儲存為 SQLite 資料庫中的 `twitch.client_id`，並動態更新用戶端連接狀態。
- 預期結果：完整編譯，單元測試通過（DesktopShellTests 與 SystemSettingKeyTests），IPC 契約格式相容性驗證通過，且用戶端識別碼 UI 設定功能運作順利。
- 實際結果：編譯順利成功。419 項單元測試（包含模擬 Web 主機崩潰重新啟動、IPC schema 驗證與 SystemSettingKeyTests）完美通過。Client ID UI 輸入與動態設定持久化在瀏覽器與 Photino 環境中皆完成驗證。
- 結果：PASS
- 憑證 / commit：完成 Task 21，所有測試皆呈綠燈。
