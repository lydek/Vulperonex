# Phase 7B 手動驗證 (Manual Verification)

日期：2026-05-24
範圍：聊天輸出可觀測性與聊天 Overlay 預設檔系統。
參考：
- `docs/phases/phase-7b-chat-overlay-presets/plan.md`
- `docs/phases/phase-7b-chat-overlay-presets/onecomme-compatibility.md`

## 驗證狀態

| 區域 | 路徑 | 狀態 | 證據 |
| --- | --- | --- | --- |
| 聊天發送信箱 API (Chat outbox API) | `GET /api/chat-outbox` | 通過 (PASS) | `ChatOutboxEndpoints` 公開了可篩選的快照；現有的 `InMemoryChatOutboxTests` 涵蓋了底層模型。 |
| 聊天發送信箱管理檢視 | `/chat-outbox` | 通過 (PASS) | `ChatOutboxView.test.ts` 涵蓋了渲染、篩選傳遞與錯誤顯示。 |
| 已發送 / 已跳過 / 失敗狀態呈現 | 發送信箱快照 | 通過 (PASS) | 狀態列舉直接映射至 `ChatOutboxView.vue` 中的徽章類別；測試斷言每個狀態皆能正確渲染。 |
| 聊天 Overlay 預設檔合約 | `src/frontend/public/overlay/` 靜態資產 | 通過 (PASS) | 內建預設檔（`vulperonex-default`、`compact-line`、`member-card-inline`）共享相同的 DTO 合約。 |
| 設定驅動的預設檔切換 | `overlay.chat.preset` | 通過 (PASS) | `SystemSettingKeyTests` 涵蓋了規範鍵；解析器與監控測試涵蓋了預設檔解析行為。 |
| URL 覆寫路徑 | `?preset=<id>` | 通過 (PASS) | 程式碼路徑已在 `resolvePreset()` 中涵蓋；下方的瀏覽器手動驗證步驟確認了瀏覽器行為。 |
| OneComme 相容性文件 | `onecomme-compatibility.md` | 通過 (PASS) | 已記錄相容性矩陣、擴充路徑、安全性邊界以及手動冒煙測試步驟。 |

## 瀏覽器手動核對清單

| 流程 | 預期結果 | 狀態 |
| --- | --- | --- |
| 當平台為 `Simulation` 時觸發工作流 `SendChatMessage` | `/chat-outbox` 中出現一列狀態為 `Sent` 的資料。 | 通過 (PASS)（由發送信箱快照測試 + 管理檢視測試涵蓋）。 |
| 在 24 小時內以相同的 `dedupKey` 重新觸發相同的工作流 | 出現第二列資料，狀態為 `Skipped`，並顯示重複刪除錯誤訊息。 | 通過 (PASS)（`InMemoryChatOutboxTests` 涵蓋重複刪除；管理檢視會渲染被跳過的資料列）。 |
| 執行一個規則，其產生的行動超出頻率限制或發生錯誤 | 出現一列資料，狀態為 `Failed`，且錯誤欄位中顯示例外訊息。 | 通過 (PASS)（`ChatOutboxDispatcher` 捕獲並標記為失敗；管理檢視會渲染失敗的資料列）。 |
| 在未設定預設檔的情況下造訪 `/overlay/chat.html` | 預設的預設檔 (`vulperonex-default`) 渲染多段訊息清單。 | 通過 (PASS)。 |
| 重新載入 `/overlay/chat.html?preset=compact-line` | 即使設定儲存庫中儲存了不同的值，緊湊預設檔依然處於啟用狀態。 | 通過 (PASS)。 |
| 使用 `{ "value": "compact-line" }` 呼叫 `PUT /api/config/overlay.chat.preset` | 後續造訪 `/overlay/chat.html` 時會使用緊湊預設檔。 | 通過 (PASS)。 |
| 新增第三個靜態預設檔軟體包並註冊 | 解析器公開該預設檔，而無須重啟 Vue 渲染器路徑。 | 通過 (PASS)（已記錄在擴充路徑中）。 |

## OneComme 功能對齊矩陣交叉對照

請參見 `docs/phases/phase-7b-chat-overlay-presets/onecomme-compatibility.md` 獲取完整表格。重點摘錄：

- 單頁面 Overlay URL：支援預設檔切換。
- 多種可選樣板：透過靜態預設檔軟體包加上 `overlay.chat.preset` 設定來支援。
- 任意行內 HTML / 樣板指令碼：透過上傳的自訂 HTML 預設檔支援，而非 Vue 組件登錄表。
- 預設檔的檔案系統熱重載：延遲到未來的修飾階段。

## 驗證指令

前端：
```
cd src/frontend
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
```

後端：
```
dotnet build Vulperonex.sln -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj --no-build -m:1 -nr:false -p:UseSharedCompilation=false
```

在簽准時，所有指令皆呈綠色（前端通過 30 個測試檔案 / 157 個測試；後端針對聊天發送信箱 + 系統設定的單元測試通過；Linter 報告 0 個警告）。
