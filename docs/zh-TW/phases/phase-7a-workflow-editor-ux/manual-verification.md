# Phase 7A 手動驗證與 Omni 編輯器 UX 功能對齊簽准 (Manual Verification and Omni Editor UX Parity Sign-off)

日期：2026-05-24
範圍：工作流編輯器 UX 與 Omni-Commander 的對齊（無執行階段結構變更）。
參考：`ref/Omni-Commander/OmniCommander.UI/src/components/workflow/`
前置簽准文件：`docs/phases/phase-7-workflow-parity/manual-verification.md`

## 驗證狀態

| 區域 | 路徑 | 狀態 | 證據 |
| --- | --- | --- | --- |
| 觸發器篩選器列基準 | `src/frontend/src/components/admin/TriggerEditor.vue` | 通過 (PASS) | `TriggerEditor.test.ts` 涵蓋了修復 Phase 6 新增資料列缺陷之後的新增、編輯、移除和空鍵值抑制。 |
| 共用步驟清單外殼 | `src/frontend/src/components/admin/StepListShell.vue` | 通過 (PASS) | `StepListShell.test.ts` 透過測試輔助元件涵蓋了新增、移除、向上/向下移動、摺疊以及停用的邊界狀態。 |
| 條件視覺化編輯器 | `src/frontend/src/components/admin/WorkflowConditionsEditor.vue` | 通過 (PASS) | `WorkflowConditionsEditor.test.ts` 執行了由元資料登錄表驅動的表單路徑。 |
| 行動視覺化編輯器 | `src/frontend/src/components/admin/WorkflowActionsEditor.vue` | 通過 (PASS) | `WorkflowActionsEditor.test.ts` 涵蓋了新增 → 類型切換 → 欄位編輯 → 輸出變數的來回傳輸，而不使用原始 JSON。 |
| OnFailure 編輯器外殼 | `src/frontend/src/components/admin/OnFailureEditor.vue` | 通過 (PASS) | `OnFailureEditor.test.ts` 涵蓋了行動編輯器的重用路徑，並渲染了巢狀 onFailure 的限制說明。 |
| 變數選取器 | `src/frontend/src/components/admin/VariablePicker.vue`, `VariableFieldInput.vue`, `ConditionExpressionInput.vue` | 通過 (PASS) | `VariableFieldInput.test.ts` 與 `ConditionExpressionInput.test.ts` 涵蓋了觸發事件 (Trigger)、參數 (Args)、步驟 (Step)、會員快照 (Member) 和失敗內容 (Failure) 群組，以及視覺化/原始模式切換。 |
| JSON 備用降級 | `RuleJsonEditor.vue` 透過各編輯器中的 `<details>` 掛載 | 通過 (PASS) | 每個編輯器都將 JSON 編輯器嵌入在「進階 JSON (Advanced JSON)」展開面板下；現有的 `RuleJsonEditor.test.ts` 保留了 1 MB 上限、貼上防護和剖析錯誤焦點涵蓋範圍。 |
| 匯入 / 匯出來回傳輸 | `src/frontend/src/views/admin/RuleEditorView.vue` | 通過 (PASS) | `RuleEditorView.test.ts` 涵蓋了匯入資料載入、不受支援的欄位橫幅以及 `buildExportPayload` 的來回傳輸。 |

## 編輯器 UX 核對清單（瀏覽器手動驗證）

| 流程 | 狀態 | 證據 / 備註 |
| --- | --- | --- |
| 新增、編輯、重新排序、移除條件 | 通過 (PASS) | 完全透過 `WorkflowConditionsEditor` 外殼驅動；已透過 `WorkflowConditionsEditor.test.ts` 與 `StepListShell.test.ts` 驗證。 |
| 觸發器篩選器列 新增 / 編輯 / 移除 | 通過 (PASS) | `TriggerEditor.test.ts` 涵蓋了對草稿列的新增和移除；空鍵值在發送前會被清除。 |
| 新增行動步驟並編輯其欄位 | 通過 (PASS) | `WorkflowActionsEditor.test.ts` 新增一個步驟，將類型切換為 `randomPicker`，編輯欄位，並斷言發送的承載資料。 |
| 設定隨機選取器 (Random Picker) 行動 | 通過 (PASS) | 相同的行動測試透過 `string-list` / `number-list` 類型涵蓋了 `choices` 和 `weights` 欄位的來回傳輸。 |
| 設定 OnFailure 步驟 | 通過 (PASS) | `OnFailureEditor.test.ts` 透過具有獨立模型繫結的共用外殼新增一個步驟。 |
| 插入步驟輸出變數 | 通過 (PASS) | `VariableFieldInput.test.ts` 與 `ConditionExpressionInput.test.ts` 涵蓋了跨觸發事件 (Trigger) / 參數 (Args) / 步驟 (Step) / 會員快照 (Member) / 失敗內容 (Failure) 群組的變數選取器插入。 |
| 切換區段至原始 JSON 備用模式並返回 | 通過 (PASS) | 每個編輯器下方的「進階 JSON」`<details>` 面板僅作為備用方案；`RuleJsonEditor.test.ts` 驗證了底層編輯器；視覺化表單仍為預設掛載。 |
| 匯入完整規則 JSON 並呈現不受支援的欄位 | 通過 (PASS) | `RuleEditorView.test.ts` 匯入一個包含 `legacyField`、`experimental` 和 `trigger.futureKnob` 的檔案，並斷言橫幅列出了每個未對應的路徑。 |
| 將目前表單匯出為 JSON | 通過 (PASS) | `RuleEditorView.test.ts` 涵蓋了 `buildExportPayload` 的來回傳輸，並斷言匯出內容攜帶了編輯後的名稱、行動和觸發器。 |

## Omni-Commander 編輯器 UX 功能對齊矩陣 (Parity Matrix)

| OC 編輯器功能 | Phase 7A 結果 | 備註 |
| --- | --- | --- |
| 包含新增、移除、重新排序、展開/摺疊的步驟清單 | 對齊 | `StepListShell.vue` 提供了條件、行動和 OnFailure 所使用的共用鉻框。 |
| 由元資料驅動欄位的視覺化條件編輯器 | 對齊 | `WorkflowConditionsEditor.vue` 從 `workflowEditor.ts` 讀取 `conditionDefinitions`。 |
| 具有每步 `ExecutionCondition` 和 `OutputVariable` 的視覺化行動編輯器 | 對齊 | `WorkflowActionsEditor.vue` 在每個步驟的 Body 插槽下方渲染這兩個元資料欄位。 |
| 獨立的 OnFailure 管線編輯器 | 對齊 | `OnFailureEditor.vue` 重用了動作編輯器，具有獨立的模型和禁止巢狀 onFailure 的說明。 |
| 包含 Trigger / Args / Step / Member / Failure 群組的變數選取器 | 對齊 | `VariablePicker.vue` 公開了這五個命名空間，並可從行動欄位、篩選值、`ExecutionCondition` 和 `MatchCondition` 觸及。 |
| 視覺化條件運算式 + 原始運算式切換 | 對齊 | `ConditionExpressionInput.vue` 公開了這兩種模式，並發送與 Phase 7 NCalc 相容的運算式。 |
| 未知行動 / 條件的備用卡片 | 對齊 | 當類型未知時，每個編輯器都會渲染一個備用摘要加上進階 JSON 面板。 |
| 從 JSON 匯入 / 匯出規則 | 對齊 | 匯入會載入表單；匯出將表單序列化為 API 預期的相同內容；兩者都共享 `KNOWN_RULE_KEYS` 的涵蓋範圍。 |
| 拖放步驟重新排序 | 刻意簡化 | 對於 Phase 7A 切片，重新排序是由按鈕驅動（`向上` / `向下`）；拖曳控制把手仍超出範圍。 |
| 圖形 / 畫布編輯器 | 超出範圍 | Phase 7A 刻意避免畫布式編輯器；若有需要，將在後續階段追蹤。 |
| 變數晶片拖放 | 超出範圍 | 在 Phase 7A 中，變數選取器僅支援點擊插入。 |

## N/A 與未來待辦事項 (Backlog)

| 項目 | Phase 7A 決策 | 待辦事項目標 |
| --- | --- | --- |
| 拖放變數晶片與步驟重新排序 | 根據 `plan.md`，此項超出範圍。 | 未來的編輯器修飾階段。 |
| 完整的圖形 / 畫布編輯器 | 根據 `plan.md`，此項超出範圍。 | 如果有使用者需求，將在未來的編輯器修飾階段進行。 |
| Phase 8 抽獎、領導者選舉、即時 Twitch 強化 | 本切片未觸及。 | Phase 8 執行階段強化。 |

## 驗證指令

在 `src/frontend` 下執行：

```
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
```

在簽准時，所有四個指令皆為綠色（通過 28 個測試檔案 / 151 個測試；`oxlint` 報告 0 個警告）。
