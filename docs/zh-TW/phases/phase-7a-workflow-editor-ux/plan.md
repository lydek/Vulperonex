# Phase 7A 實作計畫：Workflow Editor UX Alignment with Omni-Commander

> 父層計畫：`tasks/plan.md`
> 父層待辦：`tasks/todo.md`
> 對照來源：`ref/Omni-Commander/OmniCommander.UI/src/components/workflow/`
> 前置條件：Phase 7 runtime/schema parity 已完成；本 slice 不擴新 backend schema，不新增新 executor 類型。
> 目標：把 Phase 7 已存在的 workflow schema，以接近 Omni-Commander 的方式提供可操作 Web editor；JSON fallback 保留，但不再是主操作路徑。
> 進度來源：本文件中的 checkbox 僅作設計/驗收草案；實際完成狀態以 `docs/phases/phase-7a-workflow-editor-ux/todo.md` 與 `tasks/todo.md` 為準。

---

## 背景

目前 Vulperonex 的 workflow runtime/schema 已經到位，但 Web editor 仍停留在 Phase 6/7 的「JSON-first + 輕量表單」策略。實際使用上有三個明確問題：

1. `TriggerEditor` 的 filter「新增」按鈕無效，造成 trigger filter 幾乎不可用。
2. `conditions` / `actions` / `onFailureSteps` 仍主要相依 JSON textarea，對非開發者不直覺。
3. 缺少變數選取器與欄位插入體驗；使用者必須自己記憶 `{Trigger.*}` / `{Step.*}` / `{Args.*}` 語法。

Phase 7A 不再討論 runtime parity，而是專注在 editor UX parity。此 slice 目標是讓常見規則編輯流程不需直接手寫 JSON，並把 Omni-Commander 中最有價值的互動能力收斂進來。

---

## 設計原則

- **Schema 不變，UX 補齊**：不修改 Phase 7 backend contract；前端 editor 僅重新組織既有 payload。
- **表單優先，JSON 備援**：主路徑以表單與 step builder 完成；raw JSON 僅供 import/export、未知 action、進階逃生。
- **變數可發現**：不用再要求使用者背樣板語法；可從 picker 插入，但最終仍落回同一套 `{Trigger.*}` / `{Step.*}` / `{Args.*}` / `{Member.*}` contract。
- **Omni 對齊，非照抄**：借用 Omni 的 step list、action form、variable picker、condition builder 思路，但不直接搬完整 drag-and-drop graph editor。
- **未知類型 fail-soft**：若 rule 含未知 action 或暫未支援欄位，editor 不 crash；顯示 fallback 卡片並允許 raw JSON 維護。
- **不新增 Phase 8 scope**：不趁機擴 runtime、新增 graph engine 或重新設計 expression DSL。

---

## 資訊架構

### 基本設定頁

- Rule name / enabled / priority / timeout / sub-workflow toggle
- Trigger 區塊
  - Event type dropdown
  - Filter key/value rows
  - MatchCondition
- Throttle / cooldown 區塊
- Condition builder 區塊
  - condition list
  - condition type selector
  - condition-specific fields
  - variable picker 插入

### 動作步驟頁

- Step list shell
  - 新增 step
  - 刪除 step
  - 上移 / 下移 step
  - 顯示 step index、action display name、輸出變數 badge
- Step detail form
  - action type selector
  - action-specific fields
  - `ExecutionCondition`
  - `OutputVariable`
  - variable picker 插入

### 錯誤處理頁

- 與主流程相同的 step builder shell
- 但資料源為 `onFailureSteps`
- UI 明示：OnFailure steps 不再支援巢狀 OnFailure

### 進階 JSON 頁

- raw JSON import/export
- 無法對應欄位提示
- unknown action fallback surface

---

## Tasks

## Task 36 - Workflow Editor UX Baseline Repair

**描述：** 修復現有互動壞點，避免把 bug 帶進新 builder。

**驗收標準：**
- [ ] `TriggerEditor` 新增 filter row 按鈕按下立即新增一列，且 row 內 key/value 可編輯。
- [ ] `TriggerEditor` row 刪除後 model 即時更新；空 key 不序列化進 payload。
- [ ] `TriggerEditor`、`ThrottleEditor`、`StepConditionInput`、`OnFailureEditor` 各自補 Vitest，鎖住既有互動。

**實作提示：**
- 避免再用只從 `props.filter` 衍生、無法暫存空 row 的 computed-only list。
- row draft state 應獨立於最終 `Record<string, string>` payload，直到同步時再清洗空 key。

## Task 37 - Visual Builder for Conditions, Actions, and OnFailure

**描述：** 以 visual builder 取代 `conditions` / `actions` / `onFailureSteps` 主要 JSON textarea 編輯方式。`conditions` 與 `actions` / `onFailureSteps` 共用一致的新增、刪除、排序、欄位編輯體驗，但各自保留對應 schema。

**驗收標準：**
- [ ] `Conditions` / `Actions` / `OnFailure` 主視圖可直接新增項目，而不是先進 raw JSON。
- [ ] condition shell / step shell 都支援新增、刪除、上下移動、展開/收合。
- [ ] `conditions` 至少支援目前常見類型的表單式編輯；若遇未知 condition type，才退回 raw JSON fallback。
- [ ] 顯示 human-readable action label；至少覆蓋 Phase 7 已驗收樣本與 parity matrix 會用到的常用 action：
  - [ ] `sendChatMessage`
  - [ ] `randomPicker`
  - [ ] `delay`
  - [ ] `stopIf`
  - [ ] `updateCounter`
  - [ ] `invokeSubWorkflow`
  - [ ] `lookupTwitchUser`
  - [ ] `shoutout`
  - [ ] `refundTwitchRedemption`
  - [ ] `emitOverlayWidget`
  - [ ] `emitSystemEvent`
  - [ ] `triggerEffect`
  - [ ] `triggerCheckIn`
  - [ ] `addLotteryTickets`
  - [ ] `invokePluginAction`
- [ ] 每個 step 皆可編輯 `ExecutionCondition` 與 `OutputVariable`。
- [ ] Unknown action type 顯示 fallback 卡片，允許切 raw JSON 維護。
- [ ] OnFailure editor 明示限制：不可巢狀定義第二層 OnFailure。

**實作提示：**
- 建立 action metadata registry，集中描述 `type`、`label`、`summary`、`field schema`、`supportsOutputVariable`。
- 建立 condition metadata registry，集中描述 `type`、`label`、`summary`、`field schema`。
- step builder 只包裝既有 `WorkflowAction` payload，不另發明前端專用 action schema。

## Task 38 - Variable Picker and Visual Text Inputs

**描述：** 提供接近 Omni-Commander 的 variable picker 與條件輸入體驗。

**驗收標準：**
- [ ] variable picker 至少分：
  - [ ] Trigger
  - [ ] Args
  - [ ] Step Outputs
  - [ ] Member
  - [ ] Failure
- [ ] 可插入到 action 參數、filter value、`ExecutionCondition`、`MatchCondition` 等文字欄位。
- [ ] 插入結果仍使用 Phase 7 contract：`{Trigger.*}` / `{Args.*}` / `{Step.*}` / `{Member.*}`。
- [ ] `ExecutionCondition` / `MatchCondition` 提供「視覺化條件」與「原始運算式」兩種模式。
- [ ] 視覺化條件模式至少支援：
  - [ ] 選變數
  - [ ] 選 operator
  - [ ] 輸入單一比較值
  - [ ] 匯出為 NCalc-compatible expression

**實作提示：**
- 可先做 click-to-insert，不要求第一版就支援 drag-and-drop。
- 可參考 Omni 的 `VariablePicker.vue`、`ConditionBuilder.vue`，但簡化成目前架構可承接的版本。

## Task 39 - JSON Fallback Demotion and Import/Export

**描述：** 保留 JSON 能力，但降級成進階 fallback surface。

**驗收標準：**
- [ ] editor 預設打開表單/step builder，不是 raw JSON。
- [ ] rule JSON import 後可對應回表單；對應失敗處明示 unsupported/fallback。
- [ ] 可從表單匯出完整 rule JSON，供 CLI / docs sample round-trip。
- [ ] 維持 1MB guard、parse error、focus/refocus、oversized paste 防護。

**實作提示：**
- 不移除 `RuleJsonEditor`；改為 advanced panel。
- form 與 JSON 間同步要單向清楚，避免雙向 watcher 造成 editor 抖動。

## Task 40 - Omni Parity Review and Manual Verification

**描述：** 用文件固定 Phase 7A 完成定義，避免再次把「有 component 殼」誤判成「UX 完成」。

**驗收標準：**
- [ ] `docs/phases/phase-7a-workflow-editor-ux/manual-verification.md` 記錄 PASS/FAIL。
- [ ] 包含 Omni 對照矩陣：已對齊 / 刻意簡化 / out-of-scope。
- [ ] Browser manual 至少覆蓋：
  - [ ] 新增 / 編輯 / 排序 condition
  - [ ] trigger filter row 新增刪除
  - [ ] 新增 action step
  - [ ] 編輯 random picker
  - [ ] 配置 onFailure step
  - [ ] 插入 step output variable
  - [ ] 切 raw JSON fallback 再回 builder

---

## Checkpoint：Phase 7A

- [ ] 全部 Task 36-40 sub-task `[x]` 完成自檢
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：workflow editor 主流程不需直接手寫 JSON 即可建立與修改常見規則
- [ ] Browser manual：variable picker 插入與 reload round-trip 全部 PASS
- [ ] `docs/phases/phase-7a-workflow-editor-ux/manual-verification.md` 記錄 PASS/FAIL + Omni UX 對照矩陣

---

## Out-of-Scope

- 完整 graph editor / node editor / canvas builder
- 第一版 drag-and-drop 變數 chip 搬運
- 新 backend schema / 新 executor 類型 / 新 expression DSL
- Phase 8 的 lottery persistence、leader election、live Twitch hardening
- 重新設計整個 admin IA；本 slice 僅限 workflow editor
