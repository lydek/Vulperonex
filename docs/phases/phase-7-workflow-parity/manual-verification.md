# Phase 7 Manual Verification and Parity Sign-off

Date: 2026-05-23
Scope: Phase 7 workflow parity with Omni-Commander.
Reference: `ref/Omni-Commander/walkthrough.md`

## Verification Status

| Area | Path | Status | Evidence |
| --- | --- | --- | --- |
| Sample rule set | `docs/phases/phase-7-workflow-parity/samples/*.json` | PASS | 15 JSON samples cover check-in, shoutout, counter, sub-workflow, timer, overlay, effect, lottery ticket counter, system event, picker, guard, delay, plugin args, redemption refund. |
| Web UI builder | `/rules`, `/timers` | PASS | `npm --prefix src/frontend run vue-tsc -- --noEmit`; `npm --prefix src/frontend test`; `npm --prefix src/frontend run build`; `npm --prefix src/frontend run lint`. |
| CLI surface | `rule`, `timer`, `simulate` command tests | PASS | CLI integration tests are part of the Phase 7 verification command set. |
| Backend workflow runtime | Unit tests | PASS | `dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`. |
| Timer runtime | Integration tests | PASS | `WorkflowTimerRepositoryTests` and `WorkflowTimerHostedServiceIntegrationTests` cover repository persistence and scheduler firing. |
| DTO whitelist / strong action audit | Code review + tests | PASS | Rule validator accepts only known action types; overlay/effect actions are strong typed and SignalR payloads are covered by existing endpoint/hub tests. |

## Web UI Dual-path Checklist

| Sample | Browser path | Expected result | Status |
| --- | --- | --- | --- |
| `01-checkin-cooldown.json` | Create/edit in `/rules`; reload detail | Trigger filter, throttle, OnFailure, and chat outbox fields survive reload. | PASS |
| `03-counter-increment.json` | Create/edit in `/rules`; simulate `!count` | `updateCounter` output variable feeds follow-up chat template. | PASS |
| `04-subworkflow-child.json` + `05-subworkflow-parent.json` | Create child then parent in `/rules` | Child is marked sub-workflow and parent passes Args. | PASS |
| `06-timer-broadcast-rule.json` | Create rule in `/rules`; create timer in `/timers` | Timer list/show/edit/delete works and rule is invoked by `workflow.timer`. | PASS |
| `07-overlay-widget.json` + `08-trigger-effect.json` | Create/edit in `/rules`; open overlays | Overlay widget/effect payloads use whitelisted DTO fields. | PASS |

## CLI Dual-path Checklist

Use the same JSON samples with the CLI rule command. The timer sample uses a rule JSON plus a timer create/show/delete sequence.

| Flow | Command shape | Expected result | Status |
| --- | --- | --- | --- |
| Rule create/show/update | `rule create <sample.json>`, `rule show --name <name>`, `rule update --name <name> <sample.json>` | JSON round trip preserves Phase 7 fields. | PASS |
| Timer create/show/delete | `timer create <rule-id> <interval-seconds>`, `timer show <timer-id>`, `timer delete <timer-id> --yes` | Timer CRUD maps to `/api/timers`. | PASS |
| Simulate trigger | `simulate chat --message <command>` | Matching rules enqueue chat/overlay/effect work. | PASS |

## Omni-Commander Parity Matrix

| OC walkthrough capability | Vulperonex Phase 7 result | Notes |
| --- | --- | --- |
| `{Trigger.*}`, `{Args.*}`, `{Step.*}` variable namespaces | Implemented | `ExpressionContext`, `TemplateResolver`, and NCalc evaluator cover these namespaces. |
| Child workflow Args propagation | Implemented | `InvokeSubWorkflowAction.Args` resolves templates before invoking child rules. |
| Execution conditions | Implemented | `WorkflowAction.ExecutionCondition` is evaluated before each step. |
| Step output variables | Implemented | `WorkflowAction.OutputVariable` stores executor outputs under `Step.<name>`. |
| Rule throttle / cooldown | Implemented | `WorkflowThrottlePolicy` supports max concurrent, global cooldown, and per-user cooldown. |
| Rule timeout and action cancellation | Implemented | Rule-level linked cancellation wraps action-level timeout/retry behavior. |
| OnFailure steps | Implemented | Main phase failures execute one OnFailure phase with failure context. |
| Sub-process / invoke-only rules | Implemented | `IsSubWorkflow` prevents event-bus triggering. |
| Hot reload snapshot behavior | Implemented | `IRuleSnapshotCache` returns copied rule snapshots and isolates inflight execution. |
| Timer-triggered workflows | Implemented | `WorkflowTimerHostedService`, API, CLI, and `/timers` UI are in place. |
| Chat rate limiting | Implemented | `IChatOutbox` queues chat messages and dispatcher applies `chat.outbox.per_second`. |
| Plugin action variable surface | Implemented | Plugin action context exposes resolved `Args` while preserving `Params`. |

## N/A and Phase 8 Backlog Cross-reference

| OC item | Phase 7 decision | Backlog target |
| --- | --- | --- |
| Durable lottery ticket entity and prize draw operations | N/A for Phase 7; sample uses counter-backed `addLotteryTickets`. | Phase 8 lottery domain persistence and draw UI. |
| Multi-host timer leader election | N/A for Phase 7 single-host desktop runtime. | Phase 8 deployment/runtime hardening. |
| Full Twitch live OAuth walkthrough | N/A for Phase 7 parity sign-off; no-Twitch/manual simulation remains supported. | Phase 8 live Twitch adapter hardening. |
| Rich visual workflow graph editor | N/A for Phase 7; JSON-first builder fields are complete. | Phase 8 UX expansion. |
