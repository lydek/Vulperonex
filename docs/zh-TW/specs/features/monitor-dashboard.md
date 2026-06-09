# 功能規格書：統一監控面板

> [← Back to Master Specification](../../SPEC.md)

### 4.18 統一即時監控頁 Unified Monitor Page（Phase 7D）

**背景：** 目前 simulate 事件、看 chat overlay、看 member-card 各在不同分頁/路由。實況主 debug 流程要：admin simulate → 切到 `/overlay/chat` 看結果 → 切到 `/overlay/member` 看 member 卡 → 回 simulate 再試。Context switch 多，driver friction 高。

**Phase 7D 設計：** 新增 `/monitor` 統一頁，舊 simulate / overlay 獨立頁保留作 debug / e2e 用途。

**版型（寬螢幕 ≥1280px）：**

```
┌─────────────────────────────────────────────────────────────┐
│ Header: 平台連線狀態 + SignalR 狀態 + Live/Settings 切換     │
├──────────────┬──────────────────────────────┬───────────────┤
│              │                              │               │
│  Simulate    │  Overlay Preview (iframe)    │  Chat Stream  │
│  Controls    │                              │  (live)       │
│  (sider)     │  ┌────────────────────────┐  │               │
│              │  │ 預覽切換: chat / member│  │  使用者: 訊息 │
│  • chat      │  │                        │  │  ...          │
│  • follow    │  │  iframe                │  │               │
│  • sub       │  │                        │  │               │
│  • giftsub   │  │                        │  │               │
│  • raid      │  └────────────────────────┘  │               │
│  • bits      │  背景: transparent/green/    │               │
│  • redeem    │       pink/color/image       │               │
│  • checkin   │  Preset 切換 dropdown        │               │
│  • batch     │  Reload 按鈕                 │               │
│              │                              │               │
└──────────────┴──────────────────────────────┴───────────────┘
```

**版型（窄螢幕 <1280px）：**

- Simulate controls 改為右側 drawer，header 加開關按鈕。
- Overlay preview + chat stream 上下堆疊。

**功能：**

1. **Simulate controls sider**：包含所有 simulate event subcommand UI（chat / follow / sub / giftsub / raid / bits / redeem / checkin），對應後端既有 `/api/simulate/*` 端點。新增「批次模擬打卡」工具單鍵發 N 個 checkin。
2. **Overlay preview iframe**：
   - 動態 iframe `src` 切換 chat / member / alerts，內含 `?preset={key}&t={ts}` query。
   - 預覽背景切換（transparent / green key / pink key / 純色 / 自訂背景圖 URL），給 OBS 預先看不同背景下視覺。
   - Reload 按鈕（bump query timestamp）。
3. **Chat stream panel**：訂閱 `/hubs/overlay/chat`，列最新 N 則訊息（純文字、含 member chip 預覽），不渲染 preset CSS（純表格樣式），讓實況主看「資料層」是否正確（與 overlay 視覺解耦）。
4. **Header 狀態**：平台連線狀態（Twitch ✅/❌）、SignalR 連線狀態、目前 preset 設定摘要。

**路由保留：**

- `/simulate` 既有獨立 simulate 頁不刪（CLI E2E、自動化 test 用）。
- `/overlay/chat`、`/overlay/member`、`/overlay/alerts` 既有獨立 overlay route 不刪（OBS browser source 仍直接走這幾個 URL）。
- `/monitor` 為**新預設 landing**（取代目前 `/` 預設）；既有 admin 入口仍在 sidebar。

**事件 → UI 即時反應：** SignalR 已連線時，simulate 動作觸發後，預覽 iframe（透過 hub 反向通知）+ chat stream 同步更新，無須手動 reload。

**i18n：** 完整 zh-TW + en-US。

**a11y：** sider 開關有 `aria-label`，drawer focus trap 沿用 ConfirmDialog 模式。

---


---

### 4.20 模組與外掛程式管理系統 Module & Plugin Management (Phase 7D)

**背景與動機：**
目前核心服務（打卡、計數器、抽獎點數、音效畫面、外部 OneComme Bridge 等）雖然作為 Hosted Services 或外掛程式執行，但沒有提供集中管理其啟用/停用（ON/OFF）狀態的頁面。當特定模組關閉時，其相依之模組仍盲目執行可能導致狀態漂移。

**設計與規格：**
1. **模組/外掛程式開關狀態儲存**：
   - 透過 `ISystemSettingsService` 將模組啟用狀態儲存在資料庫/系統設定檔中，使用鍵名 `modules.enabled.{moduleName}`。
   - 所有核心 Hosted Services 在 `ExecuteAsync` 或 `StartAsync` 時需動態偵測該設定值，若為 `false` 則略過註冊、攔截或動作執行（No-Op 狀態），已在執行的 Hosted Services 在偵測到設定變更時應即時切換狀態。
   - 對於 `IWorkflowActionExecutor`（例如 `TriggerCheckInActionExecutor`），若關聯的模組（如打卡模組）已關閉，則應拒絕執行動作並拋出對應的 `WorkflowExecutionException`。

2. **模組相依性解析 (Dependency Resolution)**：
   - **模組登錄表（`ModuleStateService.Definitions`）— 名稱 / 顯示名 / 類別 / 相依**：
     - `workflow`「Workflow Engine」(core) -> 無相依
     - `member`「Member Module」(core) -> 無相依
     - `checkin`「Check-In Module」(core) -> 相依於 `workflow` + `member`
     - `lottery`「Lottery Module」(core) -> 相依於 `workflow` + `member`
     - `onecommebridge`「OneComme Bridge」(plugin) -> 無相依，但需 Core Event Bus
     - （無獨立可切換的 `OverlayModule`；疊層推送為 `OverlayEventForwarder`，恆開。啟用狀態以 `modules.enabled.{name}` 經 `ISystemSettingsService` 儲存。）
   - **拓撲聯鎖關閉 (Cascading Disable)**：
     - 當使用者在 UI 上**停用**一個被其他模組相依的模組時（例如停用 `MemberModule`），系統**必須**觸發拓撲相依關閉。
     - **UI 聯鎖警告閘門**：前端將跳出警告確認：「停用『會員核心模組』將一併關閉以下相依模組：打卡模組，抽獎模組。是否確認關閉？」。
     - 使用者確認後，API 將同時對相依模組寫入 `false`，並在系統 Audit Log 留下 `ActorKind = 'user'`、`Operation = 'disable_module'` 記錄。
   - **自動聯鎖啟用 (Cascading Enable)**：
     - 當使用者在 UI 上**啟用**一個具有相依性的模組時（例如啟用 `CheckInModule`），若其相依模組（例如 `MemberModule`）為關閉狀態，系統應**自動一併開啟**其相依模組，或**彈出警示並拒絕啟動**。

3. **模組管理端點 (API)**：
   - `GET /api/plugins-modules`：列出所有模組/外掛名稱、中文顯示名稱、說明、目前是否執行中 (`IsActive`) 以及相依清單。
   - `POST /api/plugins-modules/{name}/toggle`：參數為 `enabled: bool`。觸發相依性計算，成功執行後回傳拓撲變動後的所有模組狀態清單。

4. **UI 管理頁面**：
   - 於 `/admin/settings` 下新增「功能模組與外掛程式」分頁，以卡片化 Grid 呈現各功能之 ON/OFF 開關、分類標籤（核心服務、互動功能、視聽媒體、外部外掛程式）以及相依性圖示提示。

---

### 4.21 事件與忠誠度模擬功能擴充 Event & Loyalty Simulation (Phase 7D)

**背景與動機：**
現存事件模擬僅限於 `chat`, `follow`, `sub` 等基礎行為，對於關鍵的「身分組（Custom Roles/StreamRole）」與「忠誠點數/打卡」缺乏 UI 與 API 的模擬支援，導致實況主無法直接在 Admin 面板驗證與除錯複雜的工作流（例如：只允許 Moderator 參與的打卡獎勵、只對 VIP 觸發的特效等）。

**設計與規格：**
1. **身分組模擬 (StreamRole flags)**：
   - 擴充 `/api/simulate/*` 所接受的 `SimulateRequest` DTO：其 `Roles` 屬性不僅可為 single string/number，亦支援字串陣列（如 `["subscriber", "moderator", "vip"]`），允許將多重 `StreamRole` 標誌包裝進模擬事件的 User payload 中。
   - 模擬器 UI 提供勾選框 (Checkbox Group)，讓實況主可任意勾選與疊加身份別。

2. **打卡與忠誠度模擬端點**：
   - 新增 `POST /api/simulate/checkin` 端點。
   - 接收參數：
     - `platformUserId`: string (要模擬打卡的會員 ID，預設隨機)
     - `displayName`: string (要模擬打卡的會員顯示名稱，預設隨機)
     - `skipCooldown`: bool (是否繞過打卡冷卻限制，**預設 false**；CLI 以 `--skip-cooldown` 開啟)
     - `stampCount`: int (本次要直接累積的印章/打卡次數，預設 1)
   - **行為**：端點直接呼叫 `IMemberResolver` 與 `IMemberStreamStateRepository` 將打卡次數增量，並在 SQLite 中成功變更後，發布 `MemberCheckedInEvent` 事件到事件匯流排 (Event Bus)，以便 OBS Overlay 與預覽 Hub 能即時觸發集點卡視覺效果。

---

### 4.22 視覺化與直覺化工作流設定 UI Intuitive Workflow Rule Editor (Phase 7D)

**背景與動機：**
現有的工作流規則設定介面要求使用者手動輸入特定 JSON 或純文字運算式，對不熟悉技術的實況主而言極不直覺。此階段將引進視覺化引導編輯介面，徹底摒棄低效的自由文字設定。

**設計與規格：**
1. **條件建構器 (Condition Builder)**：
   - 摒棄純手寫 NCalc 文字，改用視覺化規則列表（Row-based list）。
   - 每筆條件由三大下拉選單組成：`[變數選擇器]` -> `[比較運算子]` -> `[目標值/常數]`。
   - 變數選擇器將動態讀取 `StreamEventTypeRegistry` 及 Workflow 預先提供的 Context 變數列表（如 `user.name`, `message.text`, `member.stamps`），以點選下拉式清單的方式防呆。
   - 前端元件最終自動將視覺化設定轉換並輸出成標準的 NCalc 運算式（例如：`member.stamps >= 10`）傳給後端 API。

2. **動態動作表單 (Dynamic Action Form)**：
   - 針對每個 Action 類型（例如 `TriggerCheckIn`、`RefundTwitchRedemption`、`TriggerEffect` 等），根據後端註冊的 `ActionParameterMetadata`（型別含 string, number, boolean, select, text）動態產生對應的強型別輸入控制項。
   - 元件內整合「變數選擇器浮動面板」，使用者於輸入框內游標點選或輸入 `{` 時，即時跳出可用變數列表，點選即可插入變數範本字串（如 `{user.displayName}`），避免拼寫錯誤。
