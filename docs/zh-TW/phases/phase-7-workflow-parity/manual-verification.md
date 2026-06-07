# Phase 7 手動驗證與功能對齊簽准 (Manual Verification and Parity Sign-off)

日期：2026-05-23
範圍：Phase 7 與 Omni-Commander 的工作流功能對齊。
參考：`ref/Omni-Commander/walkthrough.md`

## 驗證狀態

| 區域 | 路徑 | 狀態 | 證據 |
| --- | --- | --- | --- |
| 範例規則集 | `docs/phases/phase-7-workflow-parity/samples/*.json` | 通過 (PASS) | 15 個 JSON 範例涵蓋簽到、Shoutout、計數器、子工作流、計時器、Overlay、特效、抽獎券計數器、系統事件、選取器、防護機制、延遲、插件參數、兌換退款。 |
| Web UI 編輯器 | `/rules`, `/timers` | 帶條件通過 | 執行 `cd src/frontend; pnpm vue-tsc --noEmit && pnpm test && pnpm build && pnpm lint`。Phase 7 簽准涵蓋了當時交付之「以 JSON 為主」編輯器的結構對齊/儲存重新載入；先前存在的 `TriggerEditor` 篩選器新增資料列的錯誤 (Bug)，隨後在 Phase 7A 任務 36 中被隔離並修復。 |
| CLI 命令介面 | `rule`、`timer`、`simulate` 命令測試 | 通過 (PASS) | CLI 整合測試已納入 Phase 7 驗證指令集中。 |
| 後端工作流執行階段 | 單元測試 | 通過 (PASS) | 執行 `dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`。 |
| 計時器執行階段 | 整合測試 | 通過 (PASS) | `WorkflowTimerRepositoryTests` 與 `WorkflowTimerHostedServiceIntegrationTests` 涵蓋儲存庫持久化與排程器觸發。 |
| DTO 白名單 / 強型別行動稽核 | 程式碼審查 + 測試 | 通過 (PASS) | 規則驗證器僅接受已知的行動 (Action) 類型；Overlay/特效行動為強型別，且 SignalR 承載資料已涵蓋在現有的端點/Hub 測試中。 |

## Web UI 雙軌路徑核對清單

| 範例 | 瀏覽器路徑 | 預期結果 | 狀態 |
| --- | --- | --- | --- |
| `01-checkin-cooldown.json` | 在 `/rules` 建立/編輯；重新載入詳細資料 | 觸發器篩選器、頻率限制 (Throttle)、OnFailure 和聊天輸出信箱欄位在重新載入後保持完整。 | 通過 (PASS) |
| `03-counter-increment.json` | 在 `/rules` 建立/編輯；模擬 `!count` | `updateCounter` 輸出變數可饋送給後續的聊天範本。 | 通過 (PASS) |
| `04-subworkflow-child.json` + `05-subworkflow-parent.json` | 在 `/rules` 先建立子工作流，再建立父工作流 | 子工作流被標記為子工作流，且父工作流成功傳遞參數 (Args)。 | 通過 (PASS) |
| `06-timer-broadcast-rule.json` | 在 `/rules` 建立規則；在 `/timers` 建立計時器 | 計時器清單/顯示/編輯/刪除運作正常；計時器觸發的規則使用 `EventTypeKey = workflow.timer`，且計時器執行內容中會注入 `{Trigger.IsTimer} = true`。 | 通過 (PASS) |
| `07-overlay-widget.json` + `08-trigger-effect.json` | 在 `/rules` 建立/編輯；開啟 Overlay 頁面 | Overlay 元件/特效承載資料使用白名單中的 DTO 欄位。 | 通過 (PASS) |

## CLI 雙軌路徑核對清單

使用與 CLI 規則命令相同的 JSON 範例。計時器範例使用規則 JSON，並搭配計時器建立/顯示/刪除的一系列動作。

| 流程 | 命令形式 | 預期結果 | 狀態 |
| --- | --- | --- | --- |
| 規則 建立/顯示/更新 | `rule create <sample.json>`、`rule show --name <name>`、`rule update --name <name> <sample.json>` | JSON 來回傳輸保持 Phase 7 的欄位。 | 通過 (PASS) |
| 計時器 建立/顯示/刪除 | `timer create <rule-id> <interval-seconds>`、`timer show <timer-id>`、`timer delete <timer-id> --yes` | 計時器的 CRUD 映射到 `/api/timers`。 | 通過 (PASS) |
| 模擬觸發 | `simulate chat --message <command>` | 匹配的規則會將聊天/Overlay/特效工作排入佇列。 | 通過 (PASS) |

## Omni-Commander 功能對齊矩陣 (Parity Matrix)

| OC 逐步解說功能 | Vulperonex Phase 7 結果 | 備註 |
| --- | --- | --- |
| `{Trigger.*}`, `{Args.*}`, `{Step.*}` 變數命名空間 | 已實作 | `ExpressionContext`、`TemplateResolver` 和 NCalc 評估器涵蓋這些命名空間。 |
| 子工作流參數 (Args) 傳播 | 已實作 | `InvokeSubWorkflowAction.Args` 在呼叫子規則之前解析範本。 |
| 執行條件 (Execution Conditions) | 已實作 | 在每個步驟執行前，都會評估 `WorkflowAction.ExecutionCondition`。 |
| 步驟輸出變數 | 已實作 | `WorkflowAction.OutputVariable` 將執行器的輸出儲存在 `Step.<name>` 底下。 |
| 規則頻率限制 / 冷卻時間 | 已實作 | `WorkflowThrottlePolicy` 支援最大並發、全域冷卻和每位使用者冷卻。 |
| 規則逾時與行動取消 | 已實作 | 規則級別的連結取消封裝了行動級別的逾時/重試行為。 |
| OnFailure 步驟 | 已實作 | 主階段失敗時會執行一個帶有失敗內容的 OnFailure 階段。持久化的資料行名稱為 `OnFailureActionsJson`。 |
| 子程序 / 僅供叫用的規則 | 已實作 | `IsSubWorkflow` 可阻止事件匯流排觸發。 |
| 熱重載快照行為 | 已實作 | `IRuleSnapshotCache` 回傳被複製的規則快照，並隔離執行中的規則。 |
| 計時器觸發的工作流 | 已實作 | `WorkflowTimerHostedService`、API、CLI 和 `/timers` UI 皆已就緒。 |
| 聊天速率限制 | 已實作 | `IChatOutbox` 將聊天訊息排入佇列，且分派器套用 `chat.outbox.per_second` 限制。 |
| 插件行動變數表面 | 已實作 | 插件行動內容在保留 `Params` 的同時暴露解析後的 `Args`。 |

## N/A 與 Phase 8 待辦事項 (Backlog) 對照

| OC 項目 | Phase 7 決策 | 待辦事項目標 |
| --- | --- | --- |
| 持久化抽獎券實體與抽獎操作 | Phase 7 不適用；範例使用計數器支援的 `addLotteryTickets`。 | Phase 8 抽獎網域持久化與抽獎 UI。 |
| 多主機計時器領導者選舉 | 針對 Phase 7 單主機桌面執行階段，此項不適用。 | Phase 8 部署/執行階段強化。 |
| 完整的 Twitch 即時 OAuth 解說 | Phase 7 功能對齊簽准不適用；仍支援無 Twitch 的手動模擬。 | Phase 8 即時 Twitch 轉接器強化。 |
| 豐富的視覺化工作流圖形編輯器 | Phase 7 功能對齊簽准不適用。僅 Phase 7 接受以 JSON 為主的編輯器；已知的編輯器 UX 缺口（包括先前存在的觸發器篩選器新增資料列缺陷）已明確移至 Phase 7A。 | Phase 7A 工作流編輯器 UX 對齊；圖形/畫布編輯器仍超出該切片範圍。 |
