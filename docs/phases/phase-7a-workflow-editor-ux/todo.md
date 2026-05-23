# Phase 7A Workflow Editor UX Alignment with Omni-Commander

> 對應規劃：`docs/phases/phase-7a-workflow-editor-ux/plan.md`
> 父層追蹤：`tasks/plan.md` / `tasks/todo.md`
> 參考來源：`ref/Omni-Commander/OmniCommander.UI/src/components/workflow/`

---

## Task 36 - Workflow Editor UX Baseline Repair

- [x] Task 36a：修復 `TriggerEditor` filter row「新增無反應」問題，允許空白 draft row 存在於本地編輯狀態。
- [x] Task 36b：filter row 支援新增、刪除、重新載入既有資料；空 key 不寫入 payload。
- [x] Task 36c：補 `TriggerEditor` / `ThrottleEditor` / `StepConditionInput` / `OnFailureEditor` regression Vitest。

## Task 37 - Visual Builder for Conditions, Actions, and OnFailure

- [x] Task 37a：建立 condition metadata registry（`type`、label、summary、field schema）。
- [x] Task 37b：Conditions 頁以 visual builder 取代主要 JSON 編輯路徑。
- [x] Task 37c：建立 action metadata registry（`type`、label、summary、field schema、outputVariables）。
- [ ] Task 37d：建立共用 step list shell，支援新增、刪除、上下移動、展開/收合。
- [x] Task 37e：Actions 頁以 step builder 取代主要 JSON 編輯路徑。
- [x] Task 37f：OnFailure 頁以相同 step builder shell 實作，但資料源獨立。
- [x] Task 37g：unknown action/condition type 顯示 fallback 卡片，可切 raw JSON 維護。
- [x] Task 37h：至少落地 Phase 7 常用 actions 表單：`sendChatMessage`、`randomPicker`、`delay`、`stopIf`、`updateCounter`、`invokeSubWorkflow`、`lookupTwitchUser`、`shoutout`、`refundTwitchRedemption`、`emitOverlayWidget`、`emitSystemEvent`、`triggerEffect`、`triggerCheckIn`、`addLotteryTickets`、`invokePlugin`。

## Task 38 - Variable Picker and Visual Text Inputs

- [x] Task 38a：建立 variable picker 資訊來源，分 `Trigger` / `Args` / `Step Outputs` / `Member` / `Failure`。
- [x] Task 38b：至少在 action 參數、filter value、`ExecutionCondition`、`MatchCondition` 提供插入入口。
- [x] Task 38c：建立條件輸入 mode toggle（visual mode / raw expression mode）。
- [x] Task 38d：visual condition builder 至少支援單一變數、operator、字面值，並產出 Phase 7 NCalc expression。

## Task 39 - JSON Fallback Demotion and Import/Export

- [ ] Task 39a：`RuleJsonEditor` 降為 advanced fallback，而不是主編輯路徑。
- [ ] Task 39b：補 form -> JSON export round-trip。
- [ ] Task 39c：補 JSON import -> form hydration，並標示 unsupported/fallback 區塊。
- [ ] Task 39d：維持 1MB guard、parse error、focus/refocus、oversized paste 防護。

## Task 40 - Omni Parity Review and Manual Verification

- [ ] Task 40a：建立 `docs/phases/phase-7a-workflow-editor-ux/manual-verification.md`。
- [ ] Task 40b：整理 editor UX checklist、PASS/FAIL、Omni parity 對照。
- [ ] Task 40c：Browser manual 覆蓋 conditions、trigger filter、action step、onFailure、變數插入、JSON fallback。

## Phase 7A Checkpoint

- [ ] 全部 Task 36-40 sub-task `[x]`
- [ ] `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`
- [ ] Browser manual：workflow editor 主流程可不用手寫 JSON 完成常見配置
- [ ] Browser manual：variable picker 插入與 reload round-trip PASS
- [ ] `docs/phases/phase-7a-workflow-editor-ux/manual-verification.md` 完整填寫 PASS/FAIL + Omni UX 對照
