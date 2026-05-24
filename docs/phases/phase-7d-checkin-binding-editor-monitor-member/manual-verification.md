# Phase 7D Manual Verification

> Scope：CheckIn→MemberOverlay 綁定、Custom HTML 編輯器、統一監控頁、會員可編輯
> References：
> - `docs/phases/phase-7d-checkin-binding-editor-monitor-member/plan.md`
> - `docs/phases/phase-7d-checkin-binding-editor-monitor-member/todo.md`
> - `docs/SPEC.md` §4.14.2、§4.14.3、§4.18、§4.19

## Verification Status

| Area | Component | Status | Evidence |
| --- | --- | --- | --- |
| MemberCheckedInEvent | `Vulperonex.Domain.Events.MemberCheckedInEvent` | NOT STARTED | Task 50 |
| TriggerCheckIn publish | `TriggerCheckInActionExecutor` | NOT STARTED | Task 51 |
| Forwarder → MemberHub | `OverlayEventForwarder` | NOT STARTED | Task 52 |
| Payload extension | `OverlayMemberPayload` | NOT STARTED | Task 53 |
| CLI checkin publish | `simulate checkin` | NOT STARTED | Task 54 |
| Draft/Production layout | `wwwroot/overlay/custom/{slug}/{draft,production,history}` | NOT STARTED | Task 55 |
| Files API | `/api/overlay/custom-presets/{slug}/files` | NOT STARTED | Task 56 |
| Validation gate | `POST /validate` | NOT STARTED | Task 57 |
| Deploy / Rollback | `POST /deploy`、`POST /rollback` | NOT STARTED | Task 58 |
| Overlay Editor UI | `/admin/overlay-editor` | NOT STARTED | Task 59 |
| Zip upload integration | existing `POST /api/overlay/custom-presets` | NOT STARTED | Task 60 |
| Monitor Dashboard | `/monitor` | NOT STARTED | Task 61 |
| Simulate panel | `SimulateControlsPanel.vue` | NOT STARTED | Task 62 |
| Overlay preview iframe | `MonitorOverlayPanel.vue` | NOT STARTED | Task 63 |
| Chat stream panel | `ChatStreamPanel.vue` | NOT STARTED | Task 64 |
| Audit log infra | `MemberAuditLogs` table | NOT STARTED | Task 65 |
| Member mutation API | `PATCH /loyalty`、`POST /reset`、`DELETE` | NOT STARTED | Task 66 |
| Member edit UI | `AdjustLoyaltyModal` 等 | NOT STARTED | Task 67 |
| Workflow audit | TriggerCheckIn → audit log | NOT STARTED | Task 68 |

## Browser Manual Checklist

| Flow | Expected | Status |
| --- | --- | --- |
| `simulate checkin` 對已有會員觸發 | `/overlay/member` 5s 內顯示卡片，stamp slot 與 round 對應 `checkInCount` | PENDING |
| `simulate checkin` 對非會員觸發 | `/overlay/member` 仍顯示新建會員卡（後端自動建 member） | PENDING |
| `overlay.chat.show_member_card=true` + member 留言 | chat overlay 在留言旁顯示 chip（頭像 + checkInCount） | PENDING |
| `/admin/overlay-editor` 新建 slug | slug 出現在 list；空白 draft 載入；validate 提示 `index.html` 缺失 | PENDING |
| 寫 valid HTML + CSS + JS（含 `OverlayCommon.initSignalRConnection`） | validate 全綠；deploy 後 OBS URL 反映新內容 | PENDING |
| 寫故意 broken HTML（unclosed tag） | validate 報 HTML parse error；deploy 按鈕 disabled | PENDING |
| Deploy 後改 draft、再 deploy | history 出現舊版本；rollback 還原 | PENDING |
| Zip 上傳 OneComme 範例樣板 | 解壓到 draft；validate 自動跑；UI 提示到 Overlay Editor 修正 | PENDING |
| `/monitor` 寬螢幕載入 | 三欄：sider + iframe + chat stream；header 顯示連線狀態 | PENDING |
| `/monitor` 窄螢幕載入 | sider 改 drawer；上下堆疊 | PENDING |
| `/monitor` 中 simulate chat → iframe + chat panel 即時更新 | 無需 reload | PENDING |
| iframe 背景切換 green key | OBS 模擬背景變綠 | PENDING |
| AdjustLoyaltyModal 調 totalLoyalty -100 + reason | mutation 成功；audit drawer 列出該變動 | PENDING |
| 同一會員兩分頁同時 PATCH | 第二個 409；UI 提示 reload | PENDING |
| DeleteConfirmDialog 兩段確認 | 拿 token → 確認 → 會員從 list 消失；audit 標 ActorKind=user | PENDING |
| Token 過 30s 再用 | DELETE 回 400 token expired | PENDING |
| Workflow `TriggerCheckInAction` 觸發 | audit log ActorKind=workflow，ActorId=ruleId | PENDING |

## Security Checklist

| Item | Status | Evidence |
| --- | --- | --- |
| Custom preset path traversal（draft/production/history 三層） | NOT STARTED | Task 55-56 |
| Editor PUT/DELETE 僅可寫 draft，production 唯讀 | NOT STARTED | Task 56 |
| Delete token 一次性 + 30s TTL | NOT STARTED | Task 66 |
| Audit log append-only（無 update/delete API） | NOT STARTED | Task 65 |
| `OverlayMemberPayload` 反射白名單擴充正確（含 RoundIndex / StampSlotInRound） | NOT STARTED | Task 53 |
| Member mutation endpoint loopback-only | NOT STARTED | Task 66 |
| `MemberCheckedInEvent` 不含 `MemberId` / `TotalLoyalty` 等敏感欄位於 overlay payload | NOT STARTED | Task 50 + 53 |
| Validation gate 對外部 URL 警示 | NOT STARTED | Task 57 |
| Concurrency etag check `If-Match` 嚴守 | NOT STARTED | Task 66 |

## Verification Commands

Frontend：
```
cd src/frontend
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
```

Backend：
```
dotnet build Vulperonex.sln -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Vulperonex.sln --no-build -m:1 -nr:false -p:UseSharedCompilation=false
```

## Dated Verification Entries

> Append per project convention（format 見 `phase-6-web-ui/manual-verification.md`）：
> ```
> ## YYYY-MM-DD - Task NN <subject>
> - Verifier: <name>
> - Environment: <OS, runtime, browser>
> - Commands / Steps: ...
> - Expected: ...
> - Actual: ...
> - Result: PASS / FAIL
> - Evidence / commit: <sha>
> ```

(無項目 — Phase 7D 尚未開工)
