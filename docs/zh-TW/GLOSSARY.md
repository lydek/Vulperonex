# 統一語言與術語對照表：Vulperonex

本文件定義了 Vulperonex 的核心領域名詞（Domain Terms）。為維護系統的概念完整性，避免在英文程式碼、API Payload、前端語系資源（i18n）與中文文件之間產生語意偏差，所有開發者與翻譯者皆須嚴格遵守以下名詞對照與定義。

---

## 核心領域名詞定義

### 1. Member / 會員
* **英文術語**：`Member` / `MemberRecord`
* **中文翻譯**：`會員`
* **領域定義**：在系統中擁有唯一跨平台身分，並可累積忠誠度點數與打卡簽到次數的註冊使用者。
* **程式碼型別**：`Vulperonex.Domain.Members.MemberRecord`

### 2. Platform Identity / 平台身分
* **英文術語**：`PlatformIdentity`
* **中文翻譯**：`平台身分`
* **領域定義**：會員在具體串流平台（如 Twitch、YouTube）上的帳號繫結，由 `Platform`（平台名）與 `PlatformUserId`（平台使用者 ID）複合組成。
* **程式碼型別**：`Vulperonex.Domain.Members.PlatformIdentity`

### 3. Workflow Rule / 工作流規則
* **英文術語**：`WorkflowRule` / `Rule`
* **中文翻譯**：`工作流規則` / `規則`
* **領域定義**：系統執行自動化任務的核心單元，包含一個觸發器（Trigger）、多個執行條件（Conditions）以及一序列執行動作（Actions）。
* **程式碼型別**：`Vulperonex.Domain.Workflows.WorkflowRule`

### 4. Trigger / 觸發器
* **英文術語**：`WorkflowTrigger` / `Trigger`
* **中文翻譯**：`觸發器`
* **領域定義**：規則執行的起動點，負責訂閱特定的事件類型鍵值 `EventTypeKey`（如 `user.message`）。
* **程式碼型別**：`Vulperonex.Domain.Workflows.WorkflowTrigger`

### 5. Execution Condition / 執行條件
* **英文術語**：`ExecutionCondition` / `Condition`
* **中文翻譯**：`執行條件` / `前置條件`
* **領域定義**：在具體動作執行前負責評估與篩選的過濾邏輯（如 Cooldown 冷卻時間、UserRole 身分組、MessageContent 訊息內容）。
* **程式碼型別**：`Vulperonex.Domain.Workflows.Conditions.ExecutionCondition`

### 6. Workflow Action / 工作流動作
* **英文術語**：`WorkflowAction` / `Action`
* **中文翻譯**：`工作流動作` / `動作`
* **領域定義**：當規則被觸發且前置條件全數通過後執行的具體工作（如 SendChatMessage 傳送聊天訊息、InvokeSubWorkflow 呼叫子工作流）。
* **程式碼型別**：`Vulperonex.Domain.Workflows.Actions.WorkflowAction`

### 7. Simulation / 模擬
* **英文術語**：`Simulation` / `Simulate`
* **中文翻譯**：`模擬` / `模擬器`
* **領域定義**：於本機開發與測試環境中，用以模擬串流平台事件傳入並觸發後端匯流排的核心子系統與測試端點。
* **程式碼型別**：`Vulperonex.Adapters.Simulation.SimulationAdapter`

### 8. Overlay / 疊加幕
* **英文術語**：`Overlay`
* **中文翻譯**：`疊加幕`
* **領域定義**：供 OBS 或其他實況軟體經由瀏覽器來源（Browser Source）載入渲染的 Web 檢視（如聊天室疊加幕 `/overlay/chat`、會員卡疊加幕 `/overlay/member`、訂閱特效疊加幕 `/overlay/alerts`）。
* **程式碼型別**：`Vulperonex.Application.Overlay.OverlayModule`

### 9. Preset / 預設配置
* **英文術語**：`Preset` / `Template`
* **中文翻譯**：`預設配置` / `樣板`
* **領域定義**：疊加幕檢視的渲染樣式與版面配置，分為核心內建的 Vue Preset，以及使用者上傳的靜態自訂 HTML Preset。
* **程式碼型別**：`Vulperonex.Application.Overlay.Dtos.ChatOverlayPreset`

### 10. Transient Delivery Queue (TDQ) / 瞬態遞送佇列
* **英文術語**：`TransientDeliveryQueue` / `TDQ`
* **中文翻譯**：`瞬態遞送佇列` / `瞬態佇列`
* **領域定義**：基於 SQLite 的瞬態緩衝佇列。當記憶體事件匯流排通道（Channel）滿載溢出時負責落地儲存，並於系統重新啟動時自動重播，以保證「至少遞送一次（At-Least-Once）」的可靠性。
* **程式碼型別**：`Vulperonex.Infrastructure.EventBus.TransientDeliveryQueue`

### 11. Deduplication (Dedup) / 重複抑制
* **英文術語**：`Deduplication` / `Dedup`
* **中文翻譯**：`重複抑制` / `去重`
* **領域定義：** 透過 `ActionExecutionLog` 持久化比對，防止同一個事件（EventId）在重播或並行時被重複執行同一動作的安全防護機制。
* **程式碼型別**：`Vulperonex.Infrastructure.EventBus.ActionExecutionLog`

### 12. Audit Log / 稽核日誌
* **英文術語**：`AuditLog` / `MemberAuditLog`
* **中文翻譯**：`稽核日誌`
* **領域定義**：基於 SQLite 的僅可追加（Append-only）歷史紀錄表格，用以唯讀性追蹤與記錄管理員手動調整、或工作流自動變更會員資料（如 loyalty）的異動歷史。
* **程式碼型別**：`Vulperonex.Infrastructure.Members.MemberAuditLog`

### 13. Loyalty / 忠誠點數
* **英文術語**：`Loyalty` / `LoyaltyInfo`
* **中文翻譯**：`忠誠度` / `忠誠點數`
* **領域定義**：會員透過與實況互動、簽到或打卡所累積積攢的忠誠點數與次數資訊。
* **程式碼型別**：`Vulperonex.Domain.Members.LoyaltyInfo`

### 14. Check-In / 打卡簽到
* **英文術語**：`Check-In` / `CheckIn`
* **中文翻譯**：`打卡` / `簽到`
* **領域定義**：會員參與實況互動的簽到或印章累積行為，可增量 loyalty 次數並驅動會員疊加幕渲染。
* **程式碼型別**：`Vulperonex.Domain.Workflows.Executors.TriggerCheckInActionExecutor`

---

## 語系翻譯對齊矩陣

| 情境 / 階層 | 英文術語 (程式碼/API) | zh-TW 繁體中文翻譯 | 避免使用的詞彙 |
| --- | --- | --- | --- |
| 領域核心層 | `Member` | `會員` | `成員`（過於泛化，缺乏商業語境） |
| 領域核心層 | `PlatformIdentity` | `平台身分` | `平台帳號`（語意不夠精確，非 DDD VO） |
| 前端與 API | `Overlay` | `疊加幕` | `覆蓋層` / `OBS畫面`（過於口語或非標準） |
| 前端與 API | `Preset` | `預設配置` | `預設樣式` / `主題`（無法表達 Preset 契約） |
| 前端與 API | `Check-In` | `打卡` | `登入` / `報到`（容易與系統登入混淆） |
| 前端與 API | `Loyalty` | `忠誠度` | `積分` / `分數`（缺乏領域色彩） |
| 系統架構 | `Transient Delivery Queue` | `瞬態遞送佇列` | `臨時佇列`（無法體現 At-Least-Once 特性） |
| 系統架構 | `Deduplication` | `重複抑制` | `去重`（簡語，不夠專業正式） |
