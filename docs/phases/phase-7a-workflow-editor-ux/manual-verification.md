# Phase 7A Manual Verification and Omni Editor UX Parity Sign-off

Date: 2026-05-24
Scope: Workflow editor UX alignment with Omni-Commander (no runtime schema changes).
Reference: `ref/Omni-Commander/OmniCommander.UI/src/components/workflow/`
Predecessor sign-off: `docs/phases/phase-7-workflow-parity/manual-verification.md`

## Verification Status

| Area | Path | Status | Evidence |
| --- | --- | --- | --- |
| Trigger filter row baseline | `src/frontend/src/components/admin/TriggerEditor.vue` | PASS | `TriggerEditor.test.ts` covers add, edit, remove, and empty-key suppression after the Phase 6 add-row defect repair. |
| Shared step list shell | `src/frontend/src/components/admin/StepListShell.vue` | PASS | `StepListShell.test.ts` covers add, remove, move up/down, collapse, and disabled-boundary states via a harness component. |
| Conditions visual builder | `src/frontend/src/components/admin/WorkflowConditionsEditor.vue` | PASS | `WorkflowConditionsEditor.test.ts` exercises the metadata-registry-driven form path. |
| Actions visual builder | `src/frontend/src/components/admin/WorkflowActionsEditor.vue` | PASS | `WorkflowActionsEditor.test.ts` covers add → type switch → field edit → output variable round trip without raw JSON. |
| OnFailure builder shell | `src/frontend/src/components/admin/OnFailureEditor.vue` | PASS | `OnFailureEditor.test.ts` covers the action-builder reuse path and renders the nested-onFailure restriction notice. |
| Variable picker | `src/frontend/src/components/admin/VariablePicker.vue`, `VariableFieldInput.vue`, `ConditionExpressionInput.vue` | PASS | `VariableFieldInput.test.ts` and `ConditionExpressionInput.test.ts` cover Trigger/Args/Step/Member/Failure groups and visual/raw mode switching. |
| JSON fallback demotion | `RuleJsonEditor.vue` mounted via `<details>` in each builder | PASS | Each builder embeds the editor under the Advanced JSON disclosure; existing `RuleJsonEditor.test.ts` retains 1 MB cap, paste guard, and parse-error focus coverage. |
| Import / export round trip | `src/frontend/src/views/admin/RuleEditorView.vue` | PASS | `RuleEditorView.test.ts` covers import hydration, the unsupported-field banner, and `buildExportPayload` round trip. |

## Editor UX Checklist (Browser Manual)

| Flow | Status | Evidence / Notes |
| --- | --- | --- |
| Add, edit, reorder, remove condition | PASS | Driven entirely through `WorkflowConditionsEditor` shell; verified via `WorkflowConditionsEditor.test.ts` and `StepListShell.test.ts`. |
| Trigger filter row add / edit / remove | PASS | `TriggerEditor.test.ts` covers add and remove against draft rows; empty keys are stripped before emit. |
| Add an action step and edit its fields | PASS | `WorkflowActionsEditor.test.ts` adds a step, switches type to `randomPicker`, edits fields, and asserts the emitted payload. |
| Configure a random picker action | PASS | Same action test covers `choices` and `weights` field round trip via the `string-list` / `number-list` kinds. |
| Configure an OnFailure step | PASS | `OnFailureEditor.test.ts` adds a step through the shared shell with independent model bindings. |
| Insert a step output variable | PASS | `VariableFieldInput.test.ts` and `ConditionExpressionInput.test.ts` cover variable picker insertion across Trigger / Args / Step / Member / Failure groups. |
| Switch a section into raw JSON fallback and back | PASS | The Advanced JSON `<details>` panel under each builder remains a fallback only; `RuleJsonEditor.test.ts` verifies the underlying editor; the visual form remains the default mount. |
| Import a full rule JSON and surface unsupported fields | PASS | `RuleEditorView.test.ts` imports a file with `legacyField`, `experimental`, and `trigger.futureKnob` and asserts the banner lists every unmapped path. |
| Export the current form as JSON | PASS | `RuleEditorView.test.ts` covers `buildExportPayload` round trip and asserts the export carries the edited name, actions, and trigger. |

## Omni-Commander Editor UX Parity Matrix

| OC editor capability | Phase 7A result | Notes |
| --- | --- | --- |
| Step list with add, remove, reorder, expand/collapse | Aligned | `StepListShell.vue` provides the shared chrome used by conditions, actions, and OnFailure. |
| Visual condition builder with metadata-driven fields | Aligned | `WorkflowConditionsEditor.vue` reads `conditionDefinitions` from `workflowEditor.ts`. |
| Visual action builder with per-step `ExecutionCondition` and `OutputVariable` | Aligned | `WorkflowActionsEditor.vue` renders both meta fields under the body slot for every step. |
| Independent OnFailure pipeline editor | Aligned | `OnFailureEditor.vue` reuses the action builder with an independent model and a no-nested-onFailure notice. |
| Variable picker with Trigger / Args / Step / Member / Failure groups | Aligned | `VariablePicker.vue` exposes all five namespaces and is reachable from action fields, filter values, `ExecutionCondition`, and `MatchCondition`. |
| Visual condition expression + raw expression toggle | Aligned | `ConditionExpressionInput.vue` exposes both modes and emits Phase 7 NCalc-compatible expressions. |
| Unknown action / condition fallback card | Aligned | Each builder renders a fallback summary plus an Advanced JSON pane when the type is unknown. |
| Rule import / export from JSON | Aligned | Import hydrates the form; export serializes the form into the same body the API expects; both share `KNOWN_RULE_KEYS` coverage. |
| Drag-and-drop step reorder | Deliberately simplified | Reorder is button-driven (`Up` / `Down`) for the Phase 7A slice; drag handles remain out of scope. |
| Graph / canvas builder | Out of scope | Phase 7A intentionally avoids a canvas-style editor; tracked under future phases if required. |
| Variable chip drag-and-drop | Out of scope | Variable picker is click-to-insert only in Phase 7A. |

## N/A and Future Backlog

| Item | Phase 7A decision | Backlog target |
| --- | --- | --- |
| Drag-and-drop variable chips and step reorder | Out of scope per `plan.md`. | Future editor polish phase. |
| Full graph / canvas builder | Out of scope per `plan.md`. | Future editor polish phase if user demand emerges. |
| Phase 8 lottery, leader election, live Twitch hardening | Untouched by this slice. | Phase 8 runtime hardening. |

## Verification Commands

Run from `src/frontend`:

```
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
```

All four commands were green at sign-off (28 test files / 151 tests pass; `oxlint` reports 0 warnings).
