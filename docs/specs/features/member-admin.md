# Feature Spec: Member Admin Editable Surface

> [← Back to Master Specification](../../SPEC.md)

### 4.19 Member Admin Editable Surface (Phase 7D)

**Background:** Phase 6 `/admin/members` was read-only to prevent accidental administrative modifications. However, real streamer workflows require:
- Manually adjusting member loyalty / check-in counts (correcting errors, activity compensation).
- Inspecting modification history for a specific member (who, when, why).
- Resetting specific member loyalty without deleting their identities.
- Deleting test members completely.
Using the CLI during a stream has high window-switching costs.

**Phase 7D Design:** Member admin becomes editable. **All changes write to an audit log** to retain traceability.

**New Endpoints:**

| Method | Path | Purpose | Required Body |
|---|---|---|---|
| `PATCH` | `/api/members/{memberId}/loyalty` | Adjusts totalLoyalty / checkInCount | `{ totalLoyalty?: int, checkInCount?: int, reason: string }` |
| `POST` | `/api/members/{memberId}/reset` | Resets loyalty to zero (retains identity) | `{ resetLoyalty: bool, resetCheckIn: bool, reason: string }` |
| `DELETE` | `/api/members/{memberId}` | Deletes member completely (including identity) | `{ reason: string }` |
| `GET` | `/api/members/{memberId}/audit` | Retrieves member change history | query: `?limit=50&offset=0` |

**Audit Table:** Adds `MemberAuditLogs` SQLite table:

```
MemberAuditLogs:
  Id              ULID PK
  MemberId        ULID FK         -- the subject id (member id, or module name when SubjectKind='module')
  SubjectKind     string          -- 'member' (default) | 'module' — the audit log is shared by member + module-toggle events (Phase 7D)
  OccurredAt      DateTimeOffset
  ActorKind       string          -- 'user' | 'workflow' | 'cli' | 'system'
  ActorId         string?         -- workflow rule id / cli session id / null for user
  Operation       string          -- member: 'adjust_loyalty' | 'checkin' | 'reset' | 'delete' ; module: 'enable_module' | 'disable_module' (open-ended string, not a closed DB enum)
  BeforeJson      string?         -- snapshot before
  AfterJson       string?         -- snapshot after
  Reason          string          -- required, non-empty
```
> **Note:** `SubjectKind` / `ActorKind` / `Operation` are stored as plain `TEXT` (not DB-enforced enums); the values above are the conventions the app writes. Module enable/disable audit entries (§4.20) use `SubjectKind='module'` with `MemberId` holding the module name.

**Concurrency:** All mutation endpoints adopt `If-Match` headers carrying an `etag` (derived from `MemberRecord.UpdatedAt` ticks hash). Version mismatches → 409 Conflict. Frontend prompts reload upon receiving 409.

**Validation:**
- `totalLoyalty >= 0`, `checkInCount >= 0`
- `reason.Length in [3, 500]`
- DELETE requires a confirmation token: request 30s token via `POST /api/members/{id}/delete-token` first, which must be passed in the DELETE body (guarding against accidental clicks).

**Frontend UI:**

| Component | Target Endpoint | Mode |
|---|---|---|
| AdjustLoyaltyModal | `PATCH /loyalty` | Form: new values + adjustment reason. Displays before/after diff. |
| ResetModal | `POST /reset` | Confirmation dialog: reset loyalty / checkIn checkboxes + reason |
| DeleteConfirmDialog | `DELETE /` | Two-stage confirmation: stage 1 retrieves token, stage 2 executes |
| AuditLogDrawer | `GET /audit` | Right drawer: timeline displaying change history, including actor, before/after snapshots, and reasons |

**Workflow Integration:** `TriggerCheckInAction` writes an audit log entry after incrementing, with ActorKind='workflow' and ActorId=ruleId. `TriggerAdjustLoyaltyAction` (if introduced in Phase 7D) follows the same pattern.

**Security:**
- Adheres to loopback-only.
- DELETE token + required reason prevent accidental deletions.
- Audit log is append-only (undeletable/unmodifiable) and retention inherits `log.db_retention_days` (but member audit is tracked independently, defaulting to 365 days).
- Reflection Tests: DTOs returned by endpoints exclude internal PKs except `MemberId`.

---
