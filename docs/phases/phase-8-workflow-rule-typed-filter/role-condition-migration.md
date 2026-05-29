# Role Expression → `UserRoleCondition` Migration Guide

Older workflow rules gated execution with raw NCalc role expressions inside
`Conditions[]` or the rule's `MatchCondition` (e.g. `Member.IsModerator == true`).
Phase 8 introduces a typed `userRole` condition that is clearer, validated, and
editable from the Conditions tab without writing expressions.

The editor surfaces an orange **migration chip** when it detects a legacy role
expression. The chip is advisory only — it opens a suggestion dialog and never
rewrites a rule automatically. Use the mappings below to convert by hand.

---

## 1. `userRole` Condition Shape

```json
{ "type": "userRole", "mode": "HasAny", "roles": "Moderator" }
```

- **`mode`** — `HasAny` (any listed role), `HasAll` (every listed role), or
  `NotHave` (none of the listed roles).
- **`roles`** — comma-separated role names: `Broadcaster`, `Moderator`,
  `Subscriber`, `Vip`, `Follower`.

---

## 2. Common Mappings

| Legacy NCalc Expression | Equivalent `userRole` Condition |
|---|---|
| `Member.IsBroadcaster` | `{ "type": "userRole", "mode": "HasAny", "roles": "Broadcaster" }` |
| `Member.IsModerator` | `{ "type": "userRole", "mode": "HasAny", "roles": "Moderator" }` |
| `Member.IsSubscriber` | `{ "type": "userRole", "mode": "HasAny", "roles": "Subscriber" }` |
| `Member.IsVip` | `{ "type": "userRole", "mode": "HasAny", "roles": "Vip" }` |

---

## 3. Compound Expressions

| Legacy NCalc Expression | Equivalent `userRole` Condition |
|---|---|
| `Member.IsModerator || Member.IsBroadcaster` | `{ "type": "userRole", "mode": "HasAny", "roles": "Moderator, Broadcaster" }` |
| `Member.IsSubscriber && Member.IsVip` | `{ "type": "userRole", "mode": "HasAll", "roles": "Subscriber, Vip" }` |
| `!Member.IsModerator` | `{ "type": "userRole", "mode": "NotHave", "roles": "Moderator" }` |

---

## 4. Migration Steps

1. Open the rule in the editor and switch to the **Conditions** tab.
2. If a migration chip is shown, click it to review the detected expressions.
3. Add a `userRole` condition (it pins to the top of the list) and configure the
   mode/roles per the tables above.
4. Remove the original NCalc expression from the legacy condition or
   `MatchCondition`.
5. Save and verify with a CLI dry-run before enabling the rule.
