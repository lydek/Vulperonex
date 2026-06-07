# 功能規格書：工作流規則強型別過濾器與可觀測性

> [← Back to Master Specification](../../SPEC.md)

### 4.26 工作流規則 Typed Filter、Metadata 與可觀測性 (Phase 8)

**背景與動機：**

Twitch `!checkin` 事故揭露了規則管線的系統性根因：trigger filter 是通用、無型別的 `Dictionary<string,string>`，以精確 key/value 比對；NCalc 與 filter 失敗會無聲穿透，無法追溯到 `RuleId`；前後端重複維護 trigger/action metadata。Phase 8 解決這些問題並整併冗餘 schema 欄位。

**1. Schema 整併（`ConsolidateWorkflowRuleSchema` + `WipeWorkflowRules` migration）：**

- `EventTypeKey` 與 `MatchCondition` 自巢狀 `WorkflowTrigger` 提升至 `WorkflowRule` 根層 — 兩者在 schema 中**恰好出現一次**（見 §4.6 與 OQ3）。
- `WorkflowTrigger` 精簡為單一 `Filter: Dictionary<string,string>`，內含 typed、每事件型別的鍵。
- `WorkflowRule.EventTypeKey` 改為 `string?`：sub-workflow rule（`IsSubWorkflow = true`）不帶 `EventTypeKey` 與 `Trigger`；提供任一者回 `400 SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER`。非 sub-workflow rule 的 `EventTypeKey` 為 null/whitespace 仍回 `400 UNKNOWN_EVENT_TYPE_KEY`。
- rule 層級 `PlatformFilter`、`ConcurrencyMode`（更名為 `ExecutionMode`）與 `UpdatedAt` 欄位已移除。開發 DB 已清空並由 `DefaultWorkflowRuleSeedService` 重新植入 typed 範例規則（幂等 — DB 一旦有任何 rule 即跳過 seeding）。舊 JSON 仍可反序列化（接受並忽略 legacy 內層欄位）以保向後相容。

**2. Typed trigger filter matcher registry：**

`TriggerFilterMatcherRegistry`（singleton、frozen 分派字典、無 runtime `Register()`）取代 `WorkflowEngine` 的通用字典比對。每個 `ITriggerFilterMatcher` 為 stateless/immutable singleton，使高頻聊天 fan-out 路徑保持 lock-free。

| EventTypeKey | Matcher | Filter 鍵 |
|---|---|---|
| `user.message` | `MatchChatMessage`（含字界檢查，`!so` 不匹配 `!sorry`） | `CommandName`、`Prefix` |
| `user.donated` | `MatchUserDonated`（最小門檻） | `MinAmount` |
| `user.subscribed` | `MatchUserSubscribed` | `Tier`（`1000`/`2000`/`3000`） |
| `user.gifted_sub` | `MatchUserGiftedSub` | `Tier`、`MinGiftCount` |
| `channel.raided` | `MatchChannelRaided`（最小門檻） | `MinViewers` |
| `reward.redeemed` | `MatchRewardRedeemed`（精確 title；`OptionsSource: "twitch.rewards"`，§4.25） | `RewardName` |
| `workflow.timer` | `MatchWorkflowTimer`（精確 id） | `TimerId` |
| 其他（如 `user.followed`） | 回退至通用字典 + warning log | — |

比對先跑 typed filter；選用的 rule 層級 `MatchCondition` NCalc 閘門於其後評估。

**3. Metadata 為單一真實來源：**

- `ITriggerMetadataProvider` → `GET /api/metadata/triggers` 公開 `AvailableEventTypes`、`FilterFieldsFor(eventTypeKey)`（`{ key, label, type, options?, optionLabels?, optionsSource?, help, required? }`）與 `ValidVariablesFor(eventTypeKey)`。
- `IActionMetadataProvider` → `GET /api/metadata/actions` 以反射讀取 15 個 action record 上的 `[ActionMetadata]` / `[ActionParam]` 屬性產生 typed parameter metadata（驅動 §4.22 動態 action 表單）。新增 action 未附 metadata 屬性時單元測試失敗。
- 前端啟動時拉取這些資料而非硬編碼定義；新增 action 只需後端 record + 屬性。

**4. 可觀測性（無 schema 變更）：**

- `NCalcExpressionEvaluator` 於 parse/eval 失敗記錄 `Warning`，攜帶 `RuleId`、`RuleName`、8 字元 `ExpressionHash` 與 `ErrorClass`（`ParseError` / `EvalError`）— **絕不記錄原始 expression 內文**（PII 保護）。`ExpressionContext` 新增 `RuleId` / `RuleName`（`IExpressionEvaluator` 簽章不變）。
- `WorkflowEngine` 發出結構化 `workflow_rule_skipped` 事件（`RuleId` / `Reason` / `EventTypeKey`）。日誌噪音分級：未知 filter key / action throw → `Warning`；正常 filter 或 `MatchCondition` 不匹配與 throttle deny → `Debug`；`EventTypeKey` 不匹配 → 不記錄。正常聊天流量不產生 `Information` 等級的 skip 噪音。

**5. 驗證與錯誤碼：** filter key 不在該事件型別 metadata 內時，`POST`/`PUT /api/rules` 回 `400 INVALID_FILTER_KEY`（無寬鬆讀取路徑）。`INVALID_FILTER_KEY`、`SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER`、`WORKFLOW_RULE_CONFLICT` 見 OQ4。

**6. 編輯器 UX：** 規則編輯器改為 Drawer + Tabs（Basic / Action Steps / Error Handling）佈局，含 schema 驅動的 `TriggerEditor`（從 `/api/metadata/triggers` 渲染 typed 欄位）、依事件型別過濾的變數選擇器，以及寫入 `UserRoleCondition` 的角色 chip 選擇器。舊的整頁 `RuleEditorView` 保留為 JSON 模式 fallback。

---
