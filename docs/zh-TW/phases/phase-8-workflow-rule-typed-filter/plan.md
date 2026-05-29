# Phase 8 · Workflow Rule Typed Filter & Observability — Implementation Plan

## Progress Snapshot (2026-05-28)

| Task | Status | Notes |
|---|---|---|
| Task 0.1 — `workflow.timer` Bootstrapper | ✅ Done | `WorkflowInternalEventTypeBootstrapper` + integration test |
| Task A.1 — ExpressionContext RuleId/RuleName | ✅ Done | nullable props, all call sites pass rule |
| Task A.2 — NCalc structured log | ✅ Done | 3 silent paths, SHA1 hash, no PII; unit tested |
| Task A.3 — WorkflowEngine log tiers | ✅ Done | warn/debug split + WarnedFilterKeys rate-limit |
| Task A.4 — known-filter-keys.md | ✅ Done | placeholder pre-Phase B |
| Task A5.1 — drop `WorkflowTrigger.EventTypeKey` | ✅ Done | + DB scrub via consolidate migration |
| Task A5.2 — drop `WorkflowTrigger.MatchCondition` | ✅ Done | engine `??` fallback removed |
| Task A5.3 — `EventTypeKey` → `string?` + validator branch | ✅ Done | + integration test (sub-workflow 201) + SQLite migration test |
| Phase C.1a — registry + 4 matchers | ✅ Done | All 7 matchers + DI + boundary check; FrozenDictionary singleton |
| **Phase C.1b — §1 sample end-to-end test** | ⏳ Blocked on B.3 | Needs Phase B.3 typed sample reseed |
| **Phase B.1 — ITriggerMetadataProvider** | ⏳ Next | endpoint + 7 event types |
| Phase B.2 — IActionMetadataProvider | ⏳ Pending | attribute + reflection |
| Phase B.3 — DB wipe + typed sample reseed | ⏳ Pending | 7 sample rules (Plan §B.3) |
| Phase B.4 — strict validator | ⏳ Pending | `InvalidFilterKey` 400 |
| Phase D — Reka UI Drawer + schema-driven editor | ⏳ Gate D pending | PoC + bundle measure |
| Phase E — Role chip UX | ⏳ Pending | |

**Test counts**: 250 unit + 231 integration = 481 passing. 0 failing.

**Review C-1/C-2/C-3/C-4 + I-1..I-6 status**: all resolved or false-alarm. See review history in session.

---



> **Spec 基準**：[`docs/zh-TW/specs/workflow-rule-design-comparison.md`](../../specs/workflow-rule-design-comparison.md)
>
> **狀態**：Draft · **建立日**：2026-05-28 · **作者**：Codex
>
> **產出範圍**：Spec 第 6 節 Phase A → Phase E 之全部實作切片、依賴、驗證、Checkpoint。

---

## Overview

修復 Twitch chat `!checkin` 事故的**系統性根因**（trigger filter 缺乏 typed
semantic、NCalc silent fallback、metadata FE/BE 雙寫）並完成 Vulperonex 自身
schema 內部冗餘清理。最終達成：

1. NCalc / filter 失敗有結構化 log，operator 能直接定位到 RuleId。
2. `EventTypeKey` / `MatchCondition` 在 schema 中各只出現一次。
3. 後端為 trigger / action metadata 單一事實來源，前端動態拉取。
4. Trigger filter 走 per-event-type typed matcher（OC 風格），杜絕 generic dict 誤設。
5. Editor UX 升級為 Drawer + Tabs，TriggerEditor 變 schema-driven。
6. Role gating UX 強化（沿用 `UserRoleCondition`，不改 schema）。

---

## Architecture Decisions（採 Spec §8 已決議項）

| # | 決策 | 理由 |
|---|---|---|
| AD-1 | metadata 放後端，由 action record attribute + provider 反射產生 | 單一事實來源，杜絕雙寫 |
| AD-2 | `ExpressionContext` 加 `RuleId/RuleName` 屬性（非改 `IExpressionEvaluator` 簽名） | 接口穩定，相容既有測試 |
| AD-3 | Phase A.5 §5b.1/§5b.2/§5b.3 合併一次 release | 共用 DB migration window，避免複合風險 |
| AD-4 | engine-internal event 註冊走獨立 `WorkflowInternalEventTypeBootstrapper IHostedService` | 與 adapter 註冊路徑對稱 |
| AD-5 | Phase B 直接 `DELETE FROM workflow_rules` + `DefaultWorkflowRuleSeedService` reseed typed 範例 | Dev 階段、無真實 operator 資料，原 legacy_filter_blob 設計撤回 |
| AD-6 | Phase D 容器庫採 **Reka UI** (headless) + 既有自家 CSS | bundle 最小 (~30 KB gzip)、a11y 最佳、CSS 零衝突 |

**Fallback 順序**（AD-6 PoC 超標時）：Naive UI tree-shake ＞ 純手刻。

---

## Dependency Graph

```
Phase A (observability)
    │
    └── Phase A.5 (schema cleanup §5b.1/5b.2/5b.3)
            │
            ├── Phase B (metadata provider + legacy scrub)
            │       │
            │       ├── Phase C (typed matcher registry)
            │       │       │
            │       │       └── §1 sample rule end-to-end 觸發
            │       │
            │       └── Phase D (UI: Drawer + schema-driven TriggerEditor)
            │
            └── Phase E (role gating UX — 可與 D 並行)

Phase §5b.5 (workflow.timer registry) — 獨立支線，可與 A 並行
```

---

## Task List

### Phase 0 · 前置（與 Phase A 並行）

#### Task 0.1: 註冊 `workflow.timer` 至 event type registry

**Description**：解 Spec §5b.5。新增 `WorkflowInternalEventTypeBootstrapper`
`IHostedService`，啟動時將 engine-internal event key（先含 `workflow.timer`）
透過 `IStreamEventTypeRegistry.Register()` 寫入。`StreamEventDescriptions`
同步補 entry（非「修復」用途，僅一致性）。

**Acceptance criteria**：
- [ ] `curl /api/event-types` 回傳含 `{ "key": "workflow.timer", "isSimulatable": false, ... }`
- [ ] `POST /api/rules` 帶 `eventTypeKey="workflow.timer"` 不再 400 `UnknownEventTypeKey`
- [ ] UI dropdown 出現 timer 選項
- [ ] 既有 `WorkflowTimerHostedService.InvokeAsync` 行為不變

**Verification**：
- [ ] `dotnet test --filter Category=EventTypeRegistry`
- [ ] 手動：`curl http://localhost:5000/api/event-types | jq '.[] | select(.key=="workflow.timer")'`

**Dependencies**：None（獨立支線）

**Files**：
- `src/Vulperonex.Application/Workflows/Timers/WorkflowInternalEventTypeBootstrapper.cs` (新)
- `src/Hosts/Vulperonex.Web/DependencyInjection.cs`
- `src/Vulperonex.Domain/Events/StreamEventDescriptions.cs`
- `tests/Vulperonex.Tests.Integration/Web/EventTypeEndpointTests.cs`

**Scope**：S

---

### Phase A · 立即止血（observability，不改 schema）

#### Task A.1: `ExpressionContext` 加 `RuleId` / `RuleName`（解 §5b.4）

**Description**：record 加兩個 nullable string 屬性；
`WorkflowEngine.BuildExpressionContext` 帶入當前 rule 資料。既有
`IExpressionEvaluator.Evaluate` 簽名不動（AD-2）。

**Acceptance criteria**：
- [ ] `ExpressionContext` 新增 `RuleId` / `RuleName`（可選；default null 保相容）
- [ ] 所有 `new ExpressionContext(...)` 呼叫點皆帶入 rule 資料（engine 路徑必填、測試路徑可 null）
- [ ] 既有單元測試 0 修改通過

**Verification**：
- [ ] `dotnet test`
- [ ] Grep：`new ExpressionContext` 全部呼叫點檢視

**Dependencies**：None

**Files**：
- `src/Vulperonex.Application/Expressions/ExpressionContext.cs`
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- 測試：既有 ExpressionContext / WorkflowEngine 測試套件

**Scope**：S

---

#### Task A.2: `NCalcExpressionEvaluator` 注入 `ILogger` + 結構化失敗 log

**Description**：解 Spec §1 三條 silent 路徑。eval throw / `HasErrors` →
`LogWarning(... {RuleId} {RuleName} {ExpressionHash} {ErrorClass})`。
`ExpressionHash` = SHA1 前 8 碼。**不寫完整 expression body**（PII 防護）。

**Acceptance criteria**：
- [ ] `null/whitespace` 路徑：不 log（仍為合法 short-circuit）
- [ ] `HasErrors()`：`LogWarning` 含 `ErrorClass=ParseError`
- [ ] `catch`：`LogWarning` 含 `ErrorClass=EvalError` 與 exception type
- [ ] log 不含完整 expression 文字（grep 測試守住）
- [ ] 一個刻意 typo 的 rule 觸發 event → log 顯示 `RuleId=... ExpressionHash=... ErrorClass=...`

**Verification**：
- [ ] `dotnet test --filter NCalcExpressionEvaluator`
- [ ] 新 unit test：`Given_InvalidExpression_When_Evaluate_Then_LogsWarningWithoutBody`
- [ ] 手動：staging 跑帶 typo rule，觀察 log

**Dependencies**：A.1

**Files**：
- `src/Vulperonex.Infrastructure/Expressions/NCalcExpressionEvaluator.cs`
- `src/Vulperonex.Infrastructure/DependencyInjection.cs`（注入 logger）
- `tests/Vulperonex.Tests.Unit/Expressions/NCalcExpressionEvaluatorTests.cs`

**Scope**：S

---

#### Task A.3: `WorkflowEngine` log 分級 + structured event log

**Description**：依 Spec Phase A 噪音原則表分級：

| 事件 | 等級 |
|---|---|
| Expression parse/eval throw | Warning（由 A.2 提供） |
| Filter key 不在 metadata 合法清單 | Warning |
| Action executor throw | Warning |
| Filter value mismatch（正常 no-match） | Debug |
| `MatchCondition` false（正常 no-match） | Debug |
| Throttle deny | Debug |
| `EventTypeKey` 不匹配 | 不 log |

Structured log event：`workflow_rule_skipped` 含 `RuleId / Reason / EventTypeKey` 欄位。

**Acceptance criteria**：
- [ ] `MatchesTriggerFilter` 對 unknown key（Phase B 前以「已知合法清單」短表暫代）發 Warning，對 value mismatch 發 Debug
- [ ] `MatchesTrigger` MatchCondition false 發 Debug
- [ ] Throttle deny 發 Debug
- [ ] `Information` log level 下高頻聊天流量不出現 no-match 噪音

**Verification**：
- [ ] `dotnet test --filter WorkflowEngine`
- [ ] 手動壓測：1000 個 user.message 事件，`Information` log 不含 `rule_skipped`
- [ ] `Debug` 模式下能看到完整 fan-out

**Dependencies**：A.2

**Files**：
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs`
- 測試：`tests/Vulperonex.Tests.Unit/Workflows/WorkflowEngineLogTests.cs`(新)

**Scope**：M

---

#### Task A.4: 文件補「filter key 已知合法清單」短表

**Description**：Phase B metadata endpoint 上線前的人工維護版本。放在
`docs/zh-TW/phases/phase-8-workflow-rule-typed-filter/known-filter-keys.md`，
Phase B 完成後 archive。

**Acceptance criteria**：
- [ ] 對齊現存 event types：`user.message` / `user.donated` / `user.subscribed` /
      `user.gifted_sub` / `channel.raided` / `reward.redeemed` / `workflow.timer`
- [ ] 每筆列出合法 filter key + 型別 + 範例

**Verification**：
- [ ] Code review

**Dependencies**：None

**Files**：
- `docs/zh-TW/phases/phase-8-workflow-rule-typed-filter/known-filter-keys.md`

**Scope**：XS

---

### Checkpoint α · Phase A 收尾

- [ ] 所有 unit + integration 測試通過
- [ ] Build 無 warning
- [ ] Manual：刻意 typo rule restart Web → log 一眼定位 `RuleId`
- [ ] Manual：正常聊天流量下無 log 噪音
- [ ] 與人類 review Phase A 結果再進 A.5

---

### Phase A.5 · Schema 內部清理（§5b.1 / §5b.2 / §5b.3，AD-3 同 release）

> **整體風險**：DB migration。對策：獨立 transaction、dry-run mode、JSON diff log、backup table 保留一 release cycle。
>
> **⚠ SQLite 限制（全 Phase A.5 通用約束）**：專案採 SQLite（`vulperonex.db`）。
> SQLite 對 `ALTER TABLE` 限制嚴格（特別是 NOT NULL ↔ NULL 變更涉及索引重建時，
> EF Core 會生成 table-rebuild 操作）。**所有 A.5 migration integration test
> 必須跑真實 SQLite provider**（非 InMemory / 非 Sqlite InMemory mode），
> 確保 EF Core 生成之 SQL 在 production SQLite engine 下執行成功。

#### Task A5.1: §5b.1 廢除 `WorkflowTrigger.EventTypeKey`

**Description**：domain model 移除欄位；`JsonConstructor` 接舊欄位但忽略；
DB migration 把 `trigger_json.eventTypeKey` 搬到外層（兩者不一致則外層優先 + warning log）。

**Acceptance criteria**：
- [ ] `WorkflowTrigger` 無 `EventTypeKey` property
- [ ] 既有 DB row 經 migration 後 `trigger_json` 不再含內層 `eventTypeKey`
- [ ] JSON deserialize 舊格式不 throw（向後相容）
- [ ] `WorkflowRuleJsonMapper.NormalizeTrigger` 簡化（不再雙寫）

**Verification**：
- [ ] `dotnet test`
- [ ] Migration dry-run：跑 staging DB，產出 diff log
- [ ] Round-trip test：`Given_LegacyRuleWithInnerEventTypeKey_When_LoadAndSave_Then_OuterPreservedInnerDropped`

**Dependencies**：Checkpoint α

**Files**：
- `src/Vulperonex.Application/Workflows/WorkflowTrigger.cs`
- `src/Hosts/Vulperonex.Web/Workflows/WorkflowRuleJsonMapper.cs`
- `src/Vulperonex.Infrastructure/Persistence/Migrations/[N]_StripInnerEventTypeKey.cs`(新)
- `src/frontend/src/views/admin/RuleEditorView.vue`（移除雙寫）

**Scope**：M

---

#### Task A5.2: §5b.2 廢除 `WorkflowTrigger.MatchCondition`

**Description**：移除內層欄位；DB migration 內層搬外層（兩者皆有外層優先 + warning）；
`WorkflowEngine.MatchesTrigger` 移除 `??` fallback。

**Acceptance criteria**：
- [ ] `WorkflowTrigger` 無 `MatchCondition` property
- [ ] `WorkflowEngine.MatchesTrigger` 只讀 `rule.MatchCondition`
- [ ] 既有 rule 經 migration 後 MatchCondition 統一在外層
- [ ] 前端 RuleEditorView 移除內層雙寫

**Verification**：
- [ ] `dotnet test`
- [ ] Migration dry-run + diff log
- [ ] Integration test：`Given_LegacyRuleWithInnerMatchCondition_When_Migrated_Then_OuterCarriesValue`

**Dependencies**：A5.1（共用 migration window）

**Files**：
- `src/Vulperonex.Application/Workflows/WorkflowTrigger.cs`
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs:237`
- `src/Vulperonex.Infrastructure/Persistence/Migrations/[N+1]_LiftInnerMatchCondition.cs`(新)
- `src/frontend/src/views/admin/RuleEditorView.vue`

**Scope**：M

---

#### Task A5.3: §5b.3 `WorkflowRule.EventTypeKey` 改 `string?`（嚴格三步序）

**Description**：BE 先 FE 後。

**[1] Validator 分支**：
- `IsSubWorkflow == true` ⇒ `EventTypeKey is null && Trigger is null`
- `IsSubWorkflow == false` ⇒ `EventTypeKey is not null and not whitespace`

**[2] DB migration**：
- **預檢**：確認 EF Core 實體映射 nullable 與 column constraint 同步調整
- **SQLite 鎖表風險**：SQLite NOT NULL → NULL 觸發 EF Core table-rebuild
  （`CREATE new → INSERT SELECT → DROP old → RENAME`）。Integration test 必須
  跑真實 SQLite file provider（**停用 InMemory provider 或 Sqlite in-memory**），
  驗證生成 SQL 在 production engine 下執行成功
- **鎖表緩解**：dev 階段資料量小可一次 migration；未來資料量大時分兩 release
  （先 drop NOT NULL，後續 release 再 backfill）
- backfill：sub-workflow rule 之 `event_type_key = ''` → NULL

**[3] 前端 payload**：sub-workflow 模式時 omit `eventTypeKey`

**Acceptance criteria**：
- [ ] `WorkflowRule.EventTypeKey` 為 `string?`
- [ ] Web API：新建 sub-workflow rule（payload 不含 eventTypeKey）回 200
- [ ] Web API：新建非 sub-workflow rule 缺 eventTypeKey 回 400 `UnknownEventTypeKey`
- [ ] §5b.6 short-circuit 仍存在但改為「非 sub-workflow 且 null/whitespace」才 reject
- [ ] DB 內既有 sub-workflow rule 之 `event_type_key` = NULL

**Verification**：
- [ ] `dotnet test --filter WorkflowRuleValidator`
- [ ] 新 integration test（**真實 SQLite file provider**）：
  - `Given_SubWorkflowRule_WithoutEventTypeKey_When_Create_Then_Returns200`
  - `Given_NonSubWorkflowRule_WithoutEventTypeKey_When_Create_Then_Returns400`
  - `Given_LegacyRows_When_RunMigration_Then_SqliteTableRebuildSucceeds`
- [ ] Migration dry-run

**Dependencies**：A5.1 / A5.2（同 release window）

**Files**：
- `src/Vulperonex.Application/Workflows/WorkflowRule.cs`
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Workflows/WorkflowRuleDto.cs`
- `src/Vulperonex.Infrastructure/Persistence/Migrations/[N+2]_NullableEventTypeKey.cs`(新)
- `src/frontend/src/views/admin/RuleEditorView.vue`

**Scope**：M

---

### Checkpoint β · Phase A.5 收尾（DB migration window 出貨前 gate）

- [ ] 三項 migration dry-run 全通過、diff log 已 review
- [ ] Staging DB 跑完 migration 後既有 rule 100% round-trip
- [ ] Backup table 保留至下一 release
- [ ] 新建 sub-workflow rule 不需填 trigger
- [ ] `EventTypeKey` / `MatchCondition` 在 schema 各只出現一次
- [ ] 與人類 review、確認可進 release window

---

### Phase B · Metadata 服務層 + Legacy Scrub

#### Task B.1: `ITriggerMetadataProvider` + endpoint

**Description**：新增 provider 回傳：
- `AvailableEventTypes`: `[{ key, displayName, description }]`
- `FilterFieldsFor(eventTypeKey)`: `[{ key, label, type, options?, help, required? }]`
- `ValidVariablesFor(eventTypeKey)`: `string[]`

Endpoint：`GET /api/metadata/triggers`。

**Acceptance criteria**：
- [ ] Provider 覆蓋 7 個 event types（對齊 Task 0.1 註冊清單）
- [ ] `FilterFieldsFor("user.message")` 至少回 `CommandName`、`Prefix`
- [ ] `ValidVariablesFor("user.message")` 含 `MessageText`、`UserLogin` 等
- [ ] endpoint 回 200 JSON

**Verification**：
- [ ] `dotnet test --filter TriggerMetadataProvider`
- [ ] `curl /api/metadata/triggers | jq` 結構驗證

**Dependencies**：Checkpoint β

**Files**：
- `src/Vulperonex.Application/Workflows/Metadata/ITriggerMetadataProvider.cs`(新)
- `src/Vulperonex.Infrastructure/Workflows/Metadata/TriggerMetadataProvider.cs`(新)
- `src/Hosts/Vulperonex.Web/Endpoints/MetadataEndpoints.cs`(新)
- `tests/Vulperonex.Tests.Unit/Workflows/Metadata/TriggerMetadataProviderTests.cs`

**Scope**：M

---

#### Task B.2: `IActionMetadataProvider` + attribute（AD-1 反射）

**Description**：每個 `WorkflowAction` record 加 attribute（如
`[ActionMetadata(DisplayName=..., Description=...)]`，property 加
`[ActionParam(IsRequired=..., DefaultValue=...)]`）。Provider 反射產生
metadata。Endpoint：`GET /api/metadata/actions`。

**Acceptance criteria**：
- [ ] 15 種 action records 全部加 attribute
- [ ] Provider 自動產出 15 entry，每 entry 含 `parameters[]`
- [ ] endpoint 回 200 JSON
- [ ] 單元測試：「加新 action 但漏 attribute」會 fail

**Verification**：
- [ ] `dotnet test --filter ActionMetadata`
- [ ] `curl /api/metadata/actions | jq 'length'` = 15

**Dependencies**：B.1（可並行，但邏輯歸 Phase B）

**Files**：
- `src/Vulperonex.Application/Workflows/Metadata/ActionMetadataAttribute.cs`(新)
- `src/Vulperonex.Application/Workflows/Actions/*.cs`（15 個 record 加 attribute）
- `src/Vulperonex.Infrastructure/Workflows/Metadata/ActionMetadataProvider.cs`(新)
- `tests/Vulperonex.Tests.Unit/Workflows/Metadata/ActionMetadataProviderTests.cs`

**Scope**：M

---

#### Task B.3: DB wipe + seed typed 範例 rule 目錄（取代舊 legacy_filter_blob 設計）

**Description**：AD-5。Dev 階段直接清空既有 rule，由
`DefaultWorkflowRuleSeedService` 擴充版補種典型 typed 範例。

**Migration（一次性）**：
```sql
DELETE FROM workflow_rules;
-- 或保留欄位定義，僅清資料；下次啟動 seed service 自動補
```

**`DefaultWorkflowRuleSeedService` 擴充**（seed 條件：DB 內無任一 rule 時才執行，避免覆寫 operator 後續手建資料）：

| Sample Rule | Trigger | Filter (typed) | Conditions | Actions |
|---|---|---|---|---|
| `!checkin` 打卡 | `user.message` | `CommandName: "!checkin"` | – | `triggerCheckIn` + `sendChatMessage` |
| `!so` 喊話（mod-only） | `user.message` | `CommandName: "!so"` | `userRole: [broadcaster, moderator]` | `shoutout` + `sendChatMessage` |
| Bits 100+ 特效 | `user.donated` | `MinAmount: 100` | – | `triggerEffect` + `emitOverlayWidget` |
| 新訂閱歡迎 | `user.subscribed` | – | – | `sendChatMessage` |
| 50+ gifted sub 警報 | `user.gifted_sub` | `MinGiftCount: 50` | – | `emitOverlayWidget(severity=warning)` |
| Raid 歡迎 | `channel.raided` | `MinViewers: 5` | – | `shoutout` + `sendChatMessage` |
| 抽獎 reward 兌換 | `reward.redeemed` | `RewardName: "Lottery Ticket"` | – | `addLotteryTickets` |

**Acceptance criteria**：
- [ ] Migration `DELETE FROM workflow_rules` 執行成功
- [ ] Seed service 啟動補種 7 條 typed 範例
- [ ] 每條 seed rule 通過 Phase B.4 strict validator
- [ ] 既有 seed (boot-seed `!checkin`) 邏輯併入新版（避免重複）
- [ ] 第二次啟動不重複建（idempotent check：DB 已有 rule 則 skip）

**Verification**：
- [ ] `dotnet test --filter DefaultWorkflowRuleSeed`
- [ ] 整合測試：`Given_EmptyDb_When_AppStart_Then_SevenTypedRulesSeeded`
- [ ] 整合測試：`Given_DbHasRules_When_AppStart_Then_SeedSkipped`
- [ ] 手動：DB 清空 → 啟動 → `curl /api/rules | jq 'length'` = 7

**Dependencies**：B.1（需 metadata 清單以驗證 typed filter 合法性）+ B.4（strict validator 上線後 seed 才能通過）

**Files**：
- `src/Vulperonex.Infrastructure/Persistence/Migrations/[N+3]_WipeWorkflowRules.cs`(新)
- `src/Hosts/Vulperonex.Web/DefaultWorkflowRuleSeedService.cs`(擴充)
- `tests/Vulperonex.Tests.Integration/Web/DefaultWorkflowRuleSeedTests.cs`(擴充)

**Scope**：M

---

#### Task B.4: Validator 全 strict（無 lenient 路徑）

**Description**：開發階段 DB 已 wipe，無遺留違規 rule → validator 不需
lenient 讀取路徑。`WorkflowRuleValidator` 寫入路徑加 filter key
metadata-clean check；讀取路徑無需改。

**Acceptance criteria**：
- [ ] POST `/api/rules` 帶非法 filter key → 400 `InvalidFilterKey`
- [ ] PUT `/api/rules/{id}` 同上
- [ ] GET 路徑無 `migrationWarnings` 欄位（不採用舊 legacy_filter_blob 相容設計）
- [ ] 不引入 `legacy_filter_blob` 欄位

**Verification**：
- [ ] `dotnet test --filter WorkflowRuleValidator`
- [ ] 整合測試：`Given_RuleWithUnknownFilterKey_When_Post_Then_Returns400`

**Dependencies**：B.1

**Files**：
- `src/Hosts/Vulperonex.Web/Validation/WorkflowRuleValidator.cs`
- `src/Hosts/Vulperonex.Web/Errors/ErrorCodes.cs`（新 `InvalidFilterKey`）

---

### Checkpoint γ · Phase B 收尾

- [ ] `/api/metadata/triggers` + `/api/metadata/actions` 各上線
- [ ] DB wipe + reseed 完成，`/api/rules` 回 7 條 typed 範例
- [ ] 新 rule 寫入非法 filter key 一律 400
- [ ] 第二次啟動 seed idempotent（不重複建）
- [ ] 與人類 review

---

### Phase C · Filter Typed Dispatch（後端）

#### Task C.1: `TriggerFilterMatcherRegistry` + 內建 matchers

**Description**：依 EventTypeKey 註冊 matcher，replace `WorkflowEngine.MatchesTriggerFilter`。

**架構約束（執行緒安全）**：
- `TriggerFilterMatcherRegistry` 註冊為 **Singleton**
- 各 `ITriggerFilterMatcher` 實作為 **Singleton + Stateless / Immutable**
- 內部 dispatch dictionary（`Dictionary<string, ITriggerFilterMatcher>`）於 DI
  bootstrap 階段反射載入完畢後封凍為 `FrozenDictionary` 或 `IReadOnlyDictionary`
- **執行期禁止任何寫入操作**（無 `Register()` public method 暴露至 runtime）
- 高頻聊天並行路徑無 lock contention，純讀取 dict

理由：聊天高頻 fan-out，registry 在 hot path 上；任何 `ConcurrentDictionary`
寫入或 `lock` 都會在尖峰時段成為瓶頸。

| EventTypeKey | Matcher |
|---|---|
| `user.message` | `MatchChatMessage`（CommandName / Prefix + **邊界檢查**防 `!so`誤匹`!sorry`） |
| `user.donated` | `MatchMinThreshold(MinAmount)` |
| `user.subscribed` | `MatchSubFilter(Tier)` |
| `user.gifted_sub` | `MatchSubFilter(Tier) + MatchMinThreshold(MinGiftCount)` |
| `channel.raided` | `MatchMinThreshold(MinViewers)` |
| `reward.redeemed` | `MatchExactString(RewardName)` |
| `workflow.timer` | `MatchExactString(TimerId)` |
| 其他 | fallback generic dict + warn log（向後相容） |

**Acceptance criteria**：
- [ ] Registry 提供 `TryMatch(eventTypeKey, filter, triggerValues) → bool`
- [ ] `WorkflowEngine.MatchesTrigger` 改呼 registry，不再直接 dict 等值比對
- [ ] Registry / matchers 全部 Singleton 註冊；dispatch dict 為 `FrozenDictionary` / `IReadOnlyDictionary`
- [ ] 無 public `Register()` runtime method（只可於 DI bootstrap 注入）
- [ ] 邊界檢查單元測試：`!so` 不匹 `!sorry`、`MinAmount: 100` 不匹 `Bits=50`
- [ ] 並行測試：1000 thread × 100 次 `TryMatch` 不出現 race / deadlock

**Verification**：
- [ ] `dotnet test --filter TriggerFilterMatcher`
- [ ] 整合測試：seed 範例 `!checkin` rule 在 Twitch chat 觸發 `triggerCheckIn`（end-to-end）
- [ ] 整合測試：seed 範例 `!so` mod-only rule 對非 mod 不觸發、對 mod 觸發
- [ ] 整合測試：seed 範例 Bits 100+ 特效對 Bits=50 不觸發、對 Bits=100 觸發

**Dependencies**：Checkpoint γ

**Files**：
- `src/Vulperonex.Application/Workflows/Filters/ITriggerFilterMatcher.cs`(新)
- `src/Vulperonex.Application/Workflows/Filters/TriggerFilterMatcherRegistry.cs`(新)
- `src/Vulperonex.Application/Workflows/Filters/Matchers/*.cs`(7 個 + fallback)
- `src/Vulperonex.Application/Workflows/WorkflowEngine.cs:242` 改寫
- `tests/Vulperonex.Tests.Unit/Workflows/Filters/*.cs`
- `tests/Vulperonex.Tests.Integration/Workflows/CheckInEndToEndTests.cs`(新)

**Scope**：L → 拆兩個 PR：(C.1a) registry + 4 個 matchers (user.message / donated / subscribed / reward.redeemed)；(C.1b) 剩餘 3 個 matchers + fallback + seed-rule 整合測試

---

### Checkpoint δ · Phase C 收尾

- [ ] Seed 範例 `!checkin` / `!so` / Bits 100+ 等 7 條 rule end-to-end 觸發行為符合預期
- [ ] 邊界檢查（`!so` ≠ `!sorry`、`MinAmount: 100` ≠ `Bits=50`）通過
- [ ] 全部 unit + integration 綠
- [ ] 與人類 review

---

### Phase D · WebUI UX 強化（Gate 通過後）

#### Gate D · 開工前 checklist

- [x] **AQ-1 已決**：採 Reka UI（headless）+ 自家 CSS（AD-6）
- [x] **Reka UI PoC**：`pnpm add reka-ui` 後實裝最小 Drawer + Tabs + Form 範例
  - 量測 bundle 數字（before/after gzip，目標 +<200 KB，預估 ~30 KB）
  - **Design Tokens 對接驗證**：確認 Reka primitive 之 unstyled slot / data-attribute
    （`data-state`、`data-orientation` 等）可被既有 CSS 變數選擇器 hook 住，
    主題配色（dark/light、accent color）能完整繼承，無需重寫 token 系統
  - 列舉至少 3 個關鍵元件 (Drawer / Tabs / Dialog) 的 styling integration sample
- [x] PoC 結果落 ADR `docs/zh-TW/adr/[N]-phase-d-ui-container-library.md`
- [x] PoC 若 bundle 超標 → 改 Naive UI tree-shake → 仍超標則純手刻（N/A：routed delta 仍在預算內）
- [x] PoC 若 token 摩擦超預期 → 評估 Inspira UI styled recipe 作為 baseline（N/A：data-state styling 可行）
- [x] Phase B endpoint 上線（D 依賴 B）

#### Task D.1: `RuleEditorDrawer.vue` 容器（替換整頁 view）

**Description**：Drawer + 三 tab（基本 / 動作步驟 / 錯誤處理），embed 既有
`WorkflowActionsEditor` / `WorkflowConditionsEditor`。

**Acceptance criteria**：
- [x] List 頁加「Edit (New)」按鈕開啟 Drawer
- [x] 舊「Edit」按鈕保留 → 進 `RuleEditorView` (Advanced JSON Mode fallback)
- [x] Drawer 內三 tab 切換不丟資料

**Verification**：
- [x] `pnpm test` (frontend)
- [x] unit flow 等效驗證：開 Drawer → 編輯 → 存檔 → reload detail；browser manual 留到 Phase D checkpoint

**Dependencies**：Gate D

**Files**：
- `src/frontend/src/components/admin/RuleEditorDrawer.vue`(新)
- `src/frontend/src/views/admin/RuleListView.vue`

**Scope**：M

---

#### Task D.2: `TriggerEditor` 改 schema-driven

**Description**：拉 `/api/metadata/triggers` `FilterFieldsFor(eventTypeKey)`
依 schema 渲染 typed 欄位，取代 generic key/value rows。

**Acceptance criteria**：
- [x] 切到 `user.message` → 出現 `CommandName` / `Prefix` typed input
- [x] 切到 `user.donated` → 出現 `MinAmount` number input
- [x] 編輯器只渲染 metadata 定義的 typed 欄位，不依賴 `migrationWarnings` 或任何 legacy filter 清理流程

**Verification**：
- [x] frontend unit test
- [ ] 手動：每個 event type 切換驗證欄位

**Dependencies**：D.1 + Phase B.1

**Files**：
- `src/frontend/src/components/admin/TriggerEditor.vue`
- `src/frontend/src/stores/triggerMetadata.ts`(新)

**Scope**：M

---

#### Task D.3: `VariablePicker` per-event-type 過濾

**Description**：依當前 `eventTypeKey` 從 metadata 拉 `ValidVariablesFor()` 過濾顯示。

**Acceptance criteria**：
- [x] `user.message` 下 picker 只列該 event 合法變數
- [x] 切換 event type 後 picker 列表同步更新

**Verification**：
- [x] frontend unit test
- [ ] 手動切換驗證

**Dependencies**：D.2

**Files**：
- `src/frontend/src/components/admin/VariablePicker.vue`
- `src/frontend/src/stores/triggerMetadata.ts`

**Scope**：S

---

#### Task D.4: `actionDefinitions` / `conditionDefinitions` 改從後端拉

**Description**：移除 `workflowEditor.ts` 內 hardcode，改為啟動時拉
`/api/metadata/actions`。保留 fallback（API 失敗時用最小硬編集合避免 UI 全空）。

**Acceptance criteria**：
- [x] `workflowEditor.ts` 不含 15 個 action 的 hardcode definition
- [x] 加新 action 只需動 BE record + attribute，FE 自動拉取無需改
- [x] API 失敗時 UI 仍可開啟（顯示 fallback warning）

**Verification**：
- [x] frontend unit test
- [ ] 手動：mock API 失敗 → 確認 fallback

**Dependencies**：D.1 + Phase B.2

**Files**：
- `src/frontend/src/components/admin/workflowEditor.ts`
- `src/frontend/src/stores/actionMetadata.ts`(新)
- `src/frontend/src/components/admin/WorkflowActionsEditor.vue`
- `src/frontend/src/components/admin/WorkflowConditionsEditor.vue`

**Scope**：M

---

### Checkpoint ε · Phase D 收尾

- [ ] Streamer 在新 Drawer 內無需 JSON 即可建出 §1 sample rule
- [ ] 加新 action 僅動 BE → FE 自動拉取（drift test 通過）
- [ ] Bundle size 數字符合預算
- [ ] 舊 `RuleEditorView` 保留為 fallback
- [ ] 與人類 review

---

### Phase E · 角色 Gating UX 強化（可與 Phase D 並行）

#### Task E.1: Editor 基本 tab 加「常用角色限制」捷徑

**Description**：在 Drawer 基本 tab 加 role chip checkbox（broadcaster /
moderator / subscriber / vip / everyone），勾選即 push 一個 `userRole`
Condition 到 `Conditions[]`。

**Acceptance criteria**：
- [x] 基本 tab 出現 role chip 區
- [x] 勾選 chip → `Conditions[]` 多一筆 `userRole` Condition
- [x] 取消勾選 → 該 Condition 移除

**Verification**：
- [x] frontend unit test
- [ ] 手動驗證

**Dependencies**：Phase D.1（embed 在 Drawer 基本 tab）

**Files**：
- `src/frontend/src/components/admin/RuleEditorDrawer.vue`
- `src/frontend/src/components/admin/RoleChipSelector.vue`(新)

**Scope**：S

---

#### Task E.2: Conditions tab `userRole` 視覺化置頂 + 遷移提示 chip

**Description**：Conditions tab 把 `userRole` Condition 置頂顯示。掃描既有
rule 之 `Conditions[]` / `MatchCondition` 含 `Member.IsModerator` /
`Member.IsSubscriber` 等表示式，UI 顯示「可改用 `UserRoleCondition`」chip
（不自動轉，操作者確認）。

**Acceptance criteria**：
- [ ] `userRole` Condition 渲染在 Conditions list 第一位
- [ ] 含 `Member.IsModerator` 表示式之 rule 顯示橘 chip
- [ ] chip 點選不自動執行，僅彈出建議對話方塊

**Verification**：
- [ ] frontend unit test
- [ ] 手動：建立含 `Member.IsModerator` 表示式之 rule，確認 chip 出現

**Dependencies**：E.1

**Files**：
- `src/frontend/src/components/admin/WorkflowConditionsEditor.vue`
- `src/frontend/src/lib/legacyRoleExpressionDetector.ts`(新)

**Scope**：S

---

#### Task E.3: 文件補對照範例

**Description**：寫一篇短文件列舉常見 NCalc role 表示式 → 對應
`UserRoleCondition` 配置。

**Acceptance criteria**：
- [ ] 至少 4 個對照範例（broadcaster / mod / sub / vip）

**Verification**：
- [ ] Code review

**Dependencies**：None

**Files**：
- `docs/zh-TW/phases/phase-8-workflow-rule-typed-filter/role-condition-migration.md`(新)

**Scope**：XS

---

### Checkpoint ζ · Phase E 收尾

- [ ] Role chip UX 流暢
- [ ] 既有 NCalc role 表示式 rule 顯示遷移 chip
- [ ] 文件對照範例可用
- [ ] 與人類 review

---

## Final Checkpoint · 全部上線

- [ ] Seed 範例 `!checkin` rule end-to-end 觸發成功（原 §1 bug 模式被根除）
- [ ] `EventTypeKey` / `MatchCondition` schema 各只一次
- [ ] 後端為 metadata 單一事實，FE 動態拉取
- [ ] Filter typed dispatch 取代 generic dict
- [ ] Drawer + Tabs UX 上線
- [ ] Role chip 可用
- [ ] 全測試綠 + bundle 預算內
- [ ] CHANGELOG / release notes 標 breaking（內層欄位移除 + DB wipe）
- [ ] DefaultWorkflowRuleSeedService idempotent（DB 有 rule 則 skip）

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Phase A.5 migration 失敗，既有 rule 讀取錯亂 | High | 獨立 transaction + dry-run + JSON diff log + backup table 保留 1 release cycle |
| Phase A.5 breaking 既有 API 用戶端（CLI / plugin 用內層欄位） | Med | `JsonConstructor` 接舊欄位但忽略 + warn；release notes 標 breaking |
| Phase C matcher dispatch break 既有奇技淫巧 rule | Med | fallback generic 比對 + warning log，相容窗口期 |
| Phase D Reka UI PoC 超 bundle 預算 / styling 工程量超預期 | Low | Fallback 順序：Naive UI tree-shake ＞ 純手刻；Inspira UI 之 styled recipe 可大幅省 styling 工 |
| Metadata schema 漂移（加 action 漏更新 attribute） | Low | 單元測試守住「reflection-derived count == validator allow-list count」|
| §5b.3 ALTER COLUMN 長鎖 | Med | 分兩 migration（drop NOT NULL 先，backfill 後） |
| **SQLite ALTER TABLE 限制**：EF Core 生成 table-rebuild 在 InMemory provider 通過但 production SQLite 失敗 | High | 所有 A.5 migration test 強制跑真實 SQLite file provider；CI pipeline 加 SQLite e2e migration gate |
| **Matcher Registry runtime lock contention** | Med | Singleton + `FrozenDictionary` + 無 public `Register()` runtime API；並行測試守住 |
| **Reka UI design tokens 整合摩擦** | Low-Med | Gate D PoC 含 3 個元件 styling sample；fallback Inspira UI styled recipe |
| boot-seed 與 Phase B/C 互動 | Low | Final checkpoint 複驗 seed 不重複建 |

---

## Parallelization Map

| 可並行 | 對象 |
|---|---|
| ✅ | Task 0.1 與 Phase A 全部 |
| ✅ | A5.1 / A5.2 / A5.3（同 release window，技術上獨立但綁同梯次 migration） |
| ✅ | B.1 與 B.2（不同 provider） |
| ✅ | Phase D 與 Phase E（D.1 完成後 E 即可開工） |
| ❌ | Phase B → Phase C（C 依賴 metadata 合法清單） |
| ❌ | Checkpoint β 前不得開 Phase B（migration window 隔離） |
