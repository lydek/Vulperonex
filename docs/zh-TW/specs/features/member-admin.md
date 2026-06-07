# 功能規格書：會員管理編輯介面

> [← Back to Master Specification](../../SPEC.md)

### 4.19 會員管理可編輯介面 Member Admin Editable Surface（Phase 7D）

**背景：** Phase 6 `/admin/members` 為唯讀檢視，理由是 MVP 安全防呆。但實況主真實使用情境需要：

- 手動調整某會員 loyalty / checkin 次數（補錯誤、活動補償）
- 查特定會員的歷史變動軌跡（誰、何時、為何改）
- 重設特定會員的 loyalty 不刪除身份
- 完全刪除測試會員

CLI 雖能做但實況中切視窗成本高。

**Phase 7D 設計：** Member admin 改為可編輯。**所有變動寫 audit log**，保留可追溯性。

**新增 endpoint：**

| Method | Path | 用途 | 必填 body |
|--------|------|------|----------|
| `PATCH` | `/api/members/{memberId}/loyalty` | 調整 totalLoyalty / checkInCount | `{ totalLoyalty?: int, checkInCount?: int, reason: string }` |
| `POST` | `/api/members/{memberId}/reset` | 重設 loyalty 歸零（保留 identity）| `{ resetLoyalty: bool, resetCheckIn: bool, reason: string }` |
| `DELETE` | `/api/members/{memberId}` | 完全刪除會員（含 identity）| `{ reason: string }` |
| `GET` | `/api/members/{memberId}/audit` | 取會員變動歷史 | query: `?limit=50&offset=0` |

**Audit table：** 新增 `MemberAuditLogs` SQLite table：

```
MemberAuditLogs:
  Id              ULID PK
  MemberId        ULID FK         -- 主體 id（會員 id；SubjectKind='module' 時為模組名稱）
  SubjectKind     string          -- 'member'（預設） | 'module' — 此稽核表由會員與模組切換事件共用（Phase 7D）
  OccurredAt      DateTimeOffset
  ActorKind       string          -- 'user' | 'workflow' | 'cli' | 'system'
  ActorId         string?         -- workflow rule id / cli session id / null for user
  Operation       string          -- 會員：'adjust_loyalty' | 'checkin' | 'reset' | 'delete'；模組：'enable_module' | 'disable_module'（開放字串，非封閉 DB enum）
  BeforeJson      string?         -- snapshot before
  AfterJson       string?         -- snapshot after
  Reason          string          -- required, non-empty
```

**Concurrency：** 所有 mutation endpoint 採 `If-Match` header 帶 `etag`（基於 `MemberRecord.UpdatedAt` ticks hash）。版本不符 → 409 Conflict。前端遇 409 → 提示 reload。

**Validation：**
- `totalLoyalty >= 0`, `checkInCount >= 0`
- `reason.Length in [3, 500]`
- DELETE 需 confirm token：先 `POST /api/members/{id}/delete-token` 拿 30s token，DELETE body 必帶 token，防誤點

**前端 UI：**

| 元件 | 對應 endpoint | 模式 |
|------|--------------|------|
| AdjustLoyaltyModal | `PATCH /loyalty` | 表單：新數值 + 變更原因。顯示 before/after diff。 |
| ResetModal | `POST /reset` | 確認 dialog：重設 loyalty / checkIn checkboxes + 原因 |
| DeleteConfirmDialog | `DELETE /` | 二段確認：第一段拿 token，第二段確認執行 |
| AuditLogDrawer | `GET /audit` | 右側 drawer，timeline 列變動歷史，含 actor + before/after + reason |

**Workflow integration：** `TriggerCheckInAction` 在 increment 後寫一筆 audit log，ActorKind='workflow'，ActorId=ruleId。`TriggerAdjustLoyaltyAction`（若 Phase 7D 新增）同理。

**安全：**
- 沿用 loopback-only。
- DELETE token + reason required 防誤刪。
- Audit log 不可刪改（append-only），保留期沿用 `log.db_retention_days`（但會員 audit 獨立計算，預設 365 天）。
- 反射測試：endpoint 回傳 DTO 不包含 `MemberId` 以外的內部 PK。
