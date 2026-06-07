# 角色運算式 → `UserRoleCondition` 遷移指南

舊版的工作流規則使用 `Conditions[]` 或規則的 `MatchCondition` 中原始的 NCalc 角色運算式來限制執行（例如 `Member.IsModerator == true`）。Phase 8 引入了強型別的 `userRole` 條件，該條件更清晰、經過驗證，且可以直接從 Conditions 頁籤編輯而不需要編寫運算式。

當編輯器偵測到舊有角色運算式時，會呈現橘色的**遷移提示晶片 (Migration Chip)**。該晶片僅具建議性質 — 它會開啟建議對話方塊，絕不會自動重寫規則。請使用下面的對照表進行手動轉換。

---

## 1. `userRole` 條件結構

```json
{ "type": "userRole", "mode": "HasAny", "roles": "Moderator" }
```

- **`mode`** — `HasAny`（擁有列出的任何角色）、`HasAll`（擁有列出的所有角色）或 `NotHave`（不擁有列出的任何角色）。
- **`roles`** — 以逗號分隔的角色名稱：`Broadcaster`、`Moderator`、`Subscriber`、`Vip`、`Follower`。

---

## 2. 常見對照

| 舊有 NCalc 運算式 | 等效的 `userRole` 條件 |
|---|---|
| `Member.IsBroadcaster` | `{ "type": "userRole", "mode": "HasAny", "roles": "Broadcaster" }` |
| `Member.IsModerator` | `{ "type": "userRole", "mode": "HasAny", "roles": "Moderator" }` |
| `Member.IsSubscriber` | `{ "type": "userRole", "mode": "HasAny", "roles": "Subscriber" }` |
| `Member.IsVip` | `{ "type": "userRole", "mode": "HasAny", "roles": "Vip" }` |

---

## 3. 複合運算式

| 舊有 NCalc 運算式 | 等效的 `userRole` 條件 |
|---|---|
| `Member.IsModerator || Member.IsBroadcaster` | `{ "type": "userRole", "mode": "HasAny", "roles": "Moderator, Broadcaster" }` |
| `Member.IsSubscriber && Member.IsVip` | `{ "type": "userRole", "mode": "HasAll", "roles": "Subscriber, Vip" }` |
| `!Member.IsModerator` | `{ "type": "userRole", "mode": "NotHave", "roles": "Moderator" }` |

---

## 4. 遷移步驟

1. 在編輯器中開啟規則，切換至 **Conditions** 頁籤。
2. 如果顯示了遷移提示晶片，點擊它以檢視偵測到的運算式。
3. 新增一個 `userRole` 條件（它會置頂顯示在清單中），並對照上表設定 mode/roles。
4. 從舊有的條件或 `MatchCondition` 中移除原始的 NCalc 運算式。
5. 儲存並在啟用規則前使用 CLI 進行模擬執行驗證。
