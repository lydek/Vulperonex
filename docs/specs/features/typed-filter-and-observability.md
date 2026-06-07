# Feature Spec: Workflow Rule Typed Filter, Metadata & Observability

> [← Back to Master Specification](../../SPEC.md)

### 4.26 Workflow Rule Typed Filter, Metadata & Observability (Phase 8)

**Background & Motivation:**

The Twitch `!checkin` incident exposed systemic root causes in the rule pipeline: trigger filters were a generic, untyped `Dictionary<string,string>` matched by exact key/value; NCalc and filter failures fell through silently with no way to trace them to a `RuleId`; and the frontend and backend double-maintained trigger/action metadata. Phase 8 resolves these and consolidates redundant schema fields.

**1. Schema consolidation (`ConsolidateWorkflowRuleSchema` + `WipeWorkflowRules` migrations):**

- `EventTypeKey` and `MatchCondition` were lifted out of the nested `WorkflowTrigger` to the `WorkflowRule` root — each now appears **exactly once** in the schema (see §4.6 and OQ3).
- `WorkflowTrigger` is reduced to a single `Filter: Dictionary<string,string>` of typed, per-event-type keys.
- `WorkflowRule.EventTypeKey` became `string?`: a sub-workflow rule (`IsSubWorkflow = true`) carries no `EventTypeKey` and no `Trigger`; supplying either returns `400 SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER`. A non-sub-workflow rule with a null/whitespace `EventTypeKey` still returns `400 UNKNOWN_EVENT_TYPE_KEY`.
- Rule-level `PlatformFilter`, `ConcurrencyMode` (renamed `ExecutionMode`), and `UpdatedAt` columns were retired. The development DB was wiped and reseeded with typed sample rules via `DefaultWorkflowRuleSeedService` (idempotent — seeding is skipped once the DB holds any rule). Old JSON still deserializes (legacy inner fields are accepted and ignored) for backward compatibility.

**2. Typed trigger filter matcher registry:**

`TriggerFilterMatcherRegistry` (singleton, frozen dispatch dictionary, no runtime `Register()`) replaces generic dictionary matching in `WorkflowEngine`. Each `ITriggerFilterMatcher` is a stateless/immutable singleton so the high-frequency chat fan-out path stays lock-free.

| EventTypeKey | Matcher | Filter keys |
|---|---|---|
| `user.message` | `MatchChatMessage` (with word-boundary checks so `!so` does not match `!sorry`) | `CommandName`, `Prefix` |
| `user.donated` | `MatchUserDonated` (min-threshold) | `MinAmount` |
| `user.subscribed` | `MatchUserSubscribed` | `Tier` (`1000`/`2000`/`3000`) |
| `user.gifted_sub` | `MatchUserGiftedSub` | `Tier`, `MinGiftCount` |
| `channel.raided` | `MatchChannelRaided` (min-threshold) | `MinViewers` |
| `reward.redeemed` | `MatchRewardRedeemed` (exact title; `OptionsSource: "twitch.rewards"`, §4.25) | `RewardName` |
| `workflow.timer` | `MatchWorkflowTimer` (exact id) | `TimerId` |
| Others (e.g. `user.followed`) | Fallback to generic dict + warning log | — |

The matched typed filter runs first; the optional rule-level `MatchCondition` NCalc gate runs afterward.

**3. Metadata as the single source of truth:**

- `ITriggerMetadataProvider` → `GET /api/metadata/triggers` exposes `AvailableEventTypes`, `FilterFieldsFor(eventTypeKey)` (`{ key, label, type, options?, optionLabels?, optionsSource?, help, required? }`), and `ValidVariablesFor(eventTypeKey)`.
- `IActionMetadataProvider` → `GET /api/metadata/actions` reflects `[ActionMetadata]` / `[ActionParam]` attributes on the 15 action records (§4.6) into typed parameter metadata (drives the §4.22 dynamic action form). A unit test fails if a new action ships without metadata attributes.
- The frontend pulls these on startup instead of hardcoding definitions; adding a new action requires only the backend record + attributes.

**4. Observability (no schema change):**

- `NCalcExpressionEvaluator` logs a `Warning` on parse/eval failure carrying `RuleId`, `RuleName`, an 8-char `ExpressionHash`, and an `ErrorClass` (`ParseError` / `EvalError`) — **never the raw expression body** (PII protection). `ExpressionContext` gained `RuleId` / `RuleName` (no signature change to `IExpressionEvaluator`).
- `WorkflowEngine` emits a structured `workflow_rule_skipped` event (`RuleId` / `Reason` / `EventTypeKey`). Log noise is graded: unknown filter key / action throw → `Warning`; normal filter or `MatchCondition` no-match and throttle deny → `Debug`; `EventTypeKey` mismatch → no log. Normal chat traffic produces no `Information`-level skip noise.

**5. Validation & error codes:** A filter key absent from the event type's metadata returns `400 INVALID_FILTER_KEY` on `POST`/`PUT /api/rules` (no lenient read path). See OQ4 for `INVALID_FILTER_KEY`, `SUB_WORKFLOW_MUST_NOT_HAVE_TRIGGER`, and `WORKFLOW_RULE_CONFLICT`.

**6. Editor UX:** The rule editor became a Drawer + Tabs (Basic / Action Steps / Error Handling) layout with a schema-driven `TriggerEditor` (renders typed fields from `/api/metadata/triggers`), an event-type-filtered variable picker, and a role-chip selector that writes `UserRoleCondition`s. The legacy full-page `RuleEditorView` remains as a JSON-mode fallback.

---
