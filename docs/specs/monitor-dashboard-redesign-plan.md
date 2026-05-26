# Plan: Monitor Dashboard Redesign

> Source spec: `docs/specs/monitor-dashboard-redesign.md`
> Tracking parent: this plan
> Target branch: `codex/phase7-workflow-parity` (or follow-up branch if isolated slice preferred)

This document turns the spec into a sequenced implementation plan with dependencies, parallelism, risks, and verification gates. It accounts for the fact that Phase 1 of the spec is already partially landed in the current diff.

---

## Current-State Audit (as of plan start)

| Spec Task | State | Evidence |
|---|---|---|
| Task 1 — Layout contract | ✅ Done | `MonitorDashboardView.vue` has `WIDE_BREAKPOINT = 1280`, narrow/desktop/mobile branches documented |
| Task 2 — Shell tokens | ⚠️ Partial | `styles/monitor-tokens.css` exists with 22 `--monitor-*` tokens, but values are hard-coded light hex instead of being derived from the active Vulperonex theme (violates Decision 1) |
| Task 3 — Header | ✅ Done | Glass class, ⚙️ icon, eyebrow, 3-state chip with pulse-dot animation |
| Task 4 — Controls rail + drawer + SimulateControlsPanel ref-parity | ⚠️ Shell done, panel internals untouched | Rail collapsible + drawer landed; `SimulateControlsPanel.vue` still a single alias-driven form (no test-mode toggle, no visual 4-section rhythm, no batch checkin parity confirmed) |
| Task 5 — Preview workspace | ⚠️ Partial | Existing `monitor-controls-header` row in `MonitorOverlayPanel.vue` has tabs/preset/env/reload; no eyebrow/stronger toolbar framing yet |
| Task 6 — Chat feed reframe | ❌ Open | `ChatStreamPanel.vue` not yet reframed against the new shell |
| Task 7 — Responsive transitions | ⚠️ Partial | Resize listener correct, but transition stale-state edges only lightly tested |
| Task 8 — i18n + a11y | ⚠️ Partial | Header/chip/drawer keys done; new keys for restructured controls/feed/preview not yet added |
| Task 9 — Final regression | ⚠️ Partial | 7 vitest cases added (172 passing locally); Task 4/5/6 work uncovered |

Conclusion: Phase 1 mostly landed, but Decision 1 (theme-derived tokens) is not honored, and Phase 2 still needs real work — especially Task 4 (`SimulateControlsPanel` ref-parity push) which is the spec's largest behavior delta.

---

## Strategy

Vertical slicing per the spec's recommended execution order:

1. Close Phase 1 gaps before Phase 2 (Decision 1 token fix).
2. Land Phase 2 zones in dependency order: Task 4 first (highest user-visible surface area), then Tasks 5 and 6 in parallel.
3. Phase 3 ties down transitions, a11y, and regression net.

Each batch ends with the standard four-step checkpoint (vue-tsc, vitest, build, lint).

---

## Batches

### Batch 0 — Close Phase 1 Token Decision Gap

Fix the one decision violation before continuing.

| Task | Action | Files | Scope |
|---|---|---|---|
| 0.1 | Replace hard-coded light hex in `--monitor-*` with values derived from `styles/app.css` theme variables (or expose a `--vp-theme-*` source). Use `light-dark()` or fallback chain so the monitor tokens follow the app theme instead of forcing always-light. | `src/frontend/src/styles/monitor-tokens.css`, `styles/app.css` (read for canonical names) | S |
| 0.2 | Manual smoke: switch app theme (if toggle exists) and confirm monitor surfaces follow. If no theme toggle exists yet, document via comment that dark branch is wired but un-toggleable for now. | (no file) | XS |

Verification: vue-tsc + vitest (smoke); no behavior change expected.

Dependencies: none.
Parallelizable: no — gate for Phase 2.

---

### Batch 1 — Phase 2 Zone 1: Controls Rail Ref-Parity (Spec Task 4)

This is the spec's heaviest task. Decision 2 explicitly asks for functional + layout parity with ref `MonitorControls.vue`, not just visual reorganization.

| Task | Action | Files | Scope |
|---|---|---|---|
| 1.1 | Section the form into 4 fieldsets matching ref rhythm: **(a) Test Mode toggle**, **(b) Chat Simulation**, **(c) Checkin Simulation (single + batch)**, **(d) Other Event Aliases (sub/giftsub/bits/redeem/follow)**. Keep current `alias` ref but render the relevant fieldset based on alias *plus* allow direct chat/checkin entry without dropdown. | `SimulateControlsPanel.vue` | M |
| 1.2 | Add `isTestMode` ref (mirrors ref `isProductionMode` inverted). Wire to existing simulate API call as a no-op flag for now (backend support is out of scope per spec). UI toggle visible at top of rail. | `SimulateControlsPanel.vue` | S |
| 1.3 | Add batch checkin section (count + skipCooldown + stamps). The existing panel already has `batchSize` ref — confirm wired to `/api/simulate/checkin` loop with progress display. | `SimulateControlsPanel.vue` | S |
| 1.4 | Style fieldsets as dense control cards using `--monitor-*` tokens. Section header + thin divider per ref. | `SimulateControlsPanel.vue` | S |
| 1.5 | Drawer presentation: ensure same fieldset rhythm renders inside the narrow-width drawer body (no separate layout). | (already inherited) | XS |
| 1.6 | i18n: add keys `monitor.controls.section.{testMode,chat,checkin,other}.title`, `monitor.controls.testMode.label`, `monitor.controls.batch.{count,run,progress}`. Bilingual. | `i18n/{en-US,zh-TW}.json` | S |
| 1.7 | Vitest: add cases covering (a) test-mode toggle ARIA + state, (b) batch checkin progress emit, (c) section render under each alias. Existing `SimulateControlsPanel.test.ts` 2-case file extends to ~6 cases. | `SimulateControlsPanel.test.ts` | S |

Verification: vue-tsc + vitest + manual desktop/narrow check.

Dependencies: Batch 0.
Parallelizable with Batch 2/3: NO — Task 4 is independent in spec but uses tokens fixed in Batch 0. Within batch, 1.1 must precede 1.2-1.5; 1.6 can run with 1.4-1.5; 1.7 last.

---

### Batch 2 — Phase 2 Zone 2: Preview Workspace Reframe (Spec Task 5)

Presentational only. No new live/draft product mode (spec scope guard).

| Task | Action | Files | Scope |
|---|---|---|---|
| 2.1 | Add a header eyebrow + title row above existing `monitor-controls-header` so the preview reads as "the workspace" (e.g. `SCENE PREVIEW` / `t('monitor.preview.title')`). | `MonitorOverlayPanel.vue` | XS |
| 2.2 | Promote `monitor-controls-header` to a 2-row toolbar: row 1 = hub tabs + preset selector + env toggle + reload; row 2 = background controls (transparent/green/pink/color/url). Use grid/flex breakpoints to wrap gracefully. | `MonitorOverlayPanel.vue` | M |
| 2.3 | Frame the iframe canvas with stronger surface: border, inner shadow, min-height that prevents collapse on narrow widths. Use `--monitor-*` tokens. | `MonitorOverlayPanel.vue` | S |
| 2.4 | i18n: `monitor.preview.title`, `monitor.preview.eyebrow`, `monitor.preview.toolbar.{reload,env.draft,env.production}` keys (rename existing inline strings). | `i18n/*` | S |
| 2.5 | Vitest existing `MonitorOverlayPanel` test (if any) regression. If none, add minimal smoke for title/toolbar render. | `MonitorOverlayPanel.test.ts` (new) | S |

Verification: vue-tsc + vitest + manual at 1440/1280 widths.

Dependencies: Batch 0.
Parallelizable with Batch 1 and 3: YES (different file, no shared state). Recommend running concurrently with Batch 3.

---

### Batch 3 — Phase 2 Zone 3: Chat Feed Reframe + SignalR State Composable (Spec Task 6)

Scope guard: keep current `ChatStreamPanel` contract; only reframe. Adds one new composable for connection state (used by chat panel + dashboard header chip).

| Task | Action | Files | Scope |
|---|---|---|---|
| 3.0 | Create `useHubConnectionState(connection)` composable per "SignalR Connection State Pattern" section. Layer 1 callbacks + Layer 3 30s passive polling. **No** Layer 2 retry inside composable; caller wires manual reconnect. | `src/frontend/src/composables/useHubConnectionState.ts` (new), `useHubConnectionState.test.ts` (new) | M |
| 3.1 | Add a stronger feed header: title + live-state chip driven by `useHubConnectionState` + clear/refresh button. Tokens from `--monitor-*`. | `ChatStreamPanel.vue` | S |
| 3.2 | Tighten item framing: avatar/name/message rhythm, subtle row divider, hover state. Maintain list semantics (existing test selectors). | `ChatStreamPanel.vue` | S |
| 3.3 | Stable desktop width: ensure column min-width `260px`, max-width `360px` honored by parent grid. | `MonitorDashboardView.vue` (chat-panel rules), `ChatStreamPanel.vue` | XS |
| 3.4 | Narrow-screen stack: confirm `< 1024px` chat sits below preview with min-height that doesn't clip header. | (CSS only) | XS |
| 3.5 | Deferred from current slice: replace dashboard header health chip data source: `getHealth` 10s polling → `useHubConnectionState` (SignalR) + keep `/api/health` at 30s as deep-health auxiliary chip OR merge worst-of-two strategy. | `MonitorDashboardView.vue` | S |
| 3.6 | Add manual reconnect button: visible only when `state === Disconnected`. Wires to a guarded `manualReconnect()` (re-entry guard + visibility trigger). | `ChatStreamPanel.vue` or shared header | S |
| 3.7 | i18n: `monitor.chat.title`, `monitor.chat.clear`, `monitor.chat.live.{connected,reconnecting,disconnected,connecting}`, `monitor.chat.reconnect` keys. | `i18n/*` | S |
| 3.8 | Vitest: existing chat tests stay green; add (a) header render + state chip class for each `HubConnectionState`, (b) clear-button click, (c) `useHubConnectionState` unit tests covering callback sync + 30s poll sync + dispose cleanup, (d) manualReconnect re-entry guard. | `ChatStreamPanel.test.ts` + `useHubConnectionState.test.ts` | M |

Verification: vue-tsc + vitest + manual at 1440/1200/800 + chaos test (DevTools throttle network → offline → online to confirm chip reflects state without infinite reconnect spam).

Dependencies: Batch 0.
Parallelizable: YES with Batch 2.

---

### Batch 4 — Phase 3: Transitions + a11y + Final Regression

| Task | Action | Files | Scope |
|---|---|---|---|
| 4.1 | Tighten resize handling: when crossing breakpoint mid-session, ensure `showDrawer` resets only when entering wide, and `isSiderOpen` resets only when entering narrow. Add a `prefers-reduced-motion` guard on transitions. | `MonitorDashboardView.vue` | S |
| 4.2 | a11y: keyboard pass through header → sider toggle → drawer close. Confirm tab order. Add focus trap to drawer (basic: focus on close button on open, return to toggle on close). | `MonitorDashboardView.vue` | S |
| 4.3 | i18n sweep: confirm no hard-coded strings remain in dashboard shell + 3 panels. | grep + manual | S |
| 4.4 | Vitest: add transition-state cases (resize wide→narrow with sider open closes correctly; narrow→wide with drawer open closes correctly). Update a11y test if present. | `MonitorDashboardView.test.ts`, `a11y.test.ts` | S |
| 4.5 | Final 4-step checkpoint: vue-tsc + vitest + build + lint. | (commands) | XS |
| 4.6 | Manual verification matrix at 1440/1280/1024/800. Note in `docs/phases/phase-7d-*/manual-verification.md` (or new file) with dated entry. | docs/phases | XS |

Verification: all four commands green; manual matrix recorded.

Dependencies: Batches 1, 2, 3.
Parallelizable: 4.1 + 4.2 + 4.3 internal parallel; 4.4-4.6 sequential at end.

---

## Risk Register

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | Token theme-derivation rabbit hole (no global theme variables exist yet in `app.css`) | High | Medium | If `app.css` lacks theme tokens, scope Batch 0 to documenting that monitor tokens are intentionally light-only for this slice + emit a TODO for a future global theming spec. Do not block Phase 2. |
| R2 | `SimulateControlsPanel` ref-parity push breaks existing tests / API contracts | Medium | High | Keep `alias` ref + emit contract; section visually only; backend simulate API unchanged. Test changes additive. |
| R3 | Preview toolbar 2-row at medium widths wraps awkwardly | Medium | Medium | Define explicit grid template per breakpoint; flex-wrap last; test at 1280 first. |
| R4 | Chat panel min-width clamps too aggressive on 1280 | Low | Medium | Use `clamp(260px, 22vw, 360px)`; manual check. |
| R5 | Responsive transition flaps on resize jitter | Low | Medium | Debounce `updateLayout` with `requestAnimationFrame`. |
| R6 | Scope creep into ref settings tabs (Module/Sound/Lottery) | High | High | Hard guard via spec "Out of scope"; reject sub-tasks that don't map to Tasks 1-9. |
| R7 | New i18n keys land without zh-TW counterpart | Medium | Low | Each batch's i18n step touches both files in same commit; vue-tsc + missing-key warn catches drift. |
| R8 | SignalR reconnect storm (`start()` called from watcher/interval) | Low (with composable contract) | High | Composable hides `start()` behind `manualReconnect()` with re-entry guard. Lint review on Batch 3 PR enforces "no `connection.start()` outside composable". |
| R9 | Memory leak from undisposed timers when monitor view unmounts mid-reconnect | Low | Medium | `onUnmounted` clears both `pollTimer` + `reconnectTimer`; test 3.8 covers. |
| R10 | Background tab throttling delays L1 callbacks indefinitely → stale chip | High | Low | L3 30s poll wakes during tab focus; visibility listener triggers reconnect attempt on return. |

---

## SignalR Connection State Pattern

Driving the live chip / reconnect UX from `useOverlayHub` directly tends to oscillate between two failure modes: (a) infinite reconnect storms when retry loops live inside watchers, and (b) silent stale state when callbacks are throttled (background tab, suspended worker). The pattern below removes both.

### Three layers, strict separation

| Layer | Mechanism | What it does | What it MUST NOT do |
|---|---|---|---|
| L1 — Passive callbacks | `onclose` / `onreconnecting` / `onreconnected` from `HubConnection` | Sync ref immediately on event | Trigger `start()`, retry, or any network call |
| L2 — Incremental reconnect | Manual `setTimeout` with backoff array, gated by re-entry flag + visibility | Try `start()` once per user/visibility trigger when state is `Disconnected` | Schedule next attempt automatically without an external trigger |
| L3 — Defensive polling sync | `setInterval` at 30s reading `connection.state` and updating ref if changed | Compensate for missed callbacks (background throttling, OS sleep) | Call `start()`, allocate per-tick, or trigger network |

### Composable contract

```ts
// src/frontend/src/composables/useHubConnectionState.ts
import { ref, onMounted, onUnmounted } from "vue";
import { HubConnectionState, type HubConnection } from "@microsoft/signalr";

const POLL_INTERVAL_MS = 30_000;
const RECONNECT_BACKOFF_MS = [0, 2_000, 10_000, 30_000, 60_000];

export function useHubConnectionState(connection: HubConnection) {
  const state = ref<HubConnectionState>(connection.state);
  const lastChangedAt = ref<number>(Date.now());
  const reconnectAttempt = ref<number>(0);
  let pollTimer: number | null = null;
  let reconnectTimer: number | null = null;

  function syncFromConnection(): void {
    const next = connection.state;
    if (next !== state.value) {
      state.value = next;
      lastChangedAt.value = Date.now();
      if (next === HubConnectionState.Connected) reconnectAttempt.value = 0;
    }
  }

  // L1 — passive
  connection.onreconnecting(syncFromConnection);
  connection.onreconnected(syncFromConnection);
  connection.onclose(syncFromConnection);

  // L2 — manual reconnect (caller invokes)
  async function manualReconnect(): Promise<void> {
    if (connection.state !== HubConnectionState.Disconnected) return;
    if (reconnectTimer !== null) return; // re-entry guard

    const idx = Math.min(reconnectAttempt.value, RECONNECT_BACKOFF_MS.length - 1);
    const delay = RECONNECT_BACKOFF_MS[idx];

    reconnectTimer = window.setTimeout(async () => {
      reconnectTimer = null;
      try {
        await connection.start();
        syncFromConnection();
      } catch {
        reconnectAttempt.value++;
        // intentionally do NOT auto-schedule next attempt
      }
    }, delay);
  }

  // L3 — defensive 30s poll (read-only)
  function startPolling(): void {
    if (pollTimer !== null) return;
    pollTimer = window.setInterval(syncFromConnection, POLL_INTERVAL_MS);
  }

  function stopPolling(): void {
    if (pollTimer !== null) {
      clearInterval(pollTimer);
      pollTimer = null;
    }
  }

  function stopReconnect(): void {
    if (reconnectTimer !== null) {
      clearTimeout(reconnectTimer);
      reconnectTimer = null;
    }
  }

  function onVisibilityChange(): void {
    if (!document.hidden && connection.state === HubConnectionState.Disconnected) {
      void manualReconnect();
    }
  }

  onMounted(() => {
    startPolling();
    document.addEventListener("visibilitychange", onVisibilityChange);
  });

  onUnmounted(() => {
    stopPolling();
    stopReconnect();
    document.removeEventListener("visibilitychange", onVisibilityChange);
  });

  return { state, lastChangedAt, reconnectAttempt, manualReconnect };
}
```

### Memory + CPU budget

| Cost | Per tick | Per day (2,880 ticks) |
|---|---|---|
| `connection.state` getter | ~10 ns | ~28 µs |
| `!==` compare + maybe `ref.value =` | ~50 ns | ~150 µs |
| Allocation | 0 bytes | 0 bytes |

Compare: current `getHealth` 10s polling = 8,640 `fetch` + JSON parse per day. New pattern is ~300× cheaper and zero allocation per tick.

### Hard rules (lint-equivalent)

1. **Never** call `connection.start()` from L1 callbacks
2. **Never** call `connection.start()` from L3 interval body
3. **Never** schedule the next reconnect from inside a previous reconnect's failure handler
4. **Always** dispose timers in `onUnmounted` / `onScopeDispose`
5. **Always** guard `manualReconnect` with `reconnectTimer !== null` re-entry check
6. Reset `reconnectAttempt` only on successful `Connected` transition
7. Visibility-driven reconnect must check `document.hidden` AND `state === Disconnected`

### Test coverage (Batch 3.8)

| Case | Asserts |
|---|---|
| Initial mount syncs ref to current `connection.state` | ref matches |
| `onclose` callback flips ref to `Disconnected` | ref + `lastChangedAt` updated |
| 30s poll picks up state when callbacks suppressed (mock fires no events but `connection.state` changes) | ref reflects current state |
| `manualReconnect` while `Connected` is no-op | `start()` not called |
| `manualReconnect` re-entry while timer pending is no-op | only one `setTimeout` scheduled |
| Successful reconnect resets `reconnectAttempt` to 0 | counter reset |
| Failed reconnect increments `reconnectAttempt` | counter increment, no auto-reschedule |
| `onUnmounted` clears both timers + listener | no leak, no further state writes |

---

## Parallelization Map

```
Batch 0 (gate)
  │
  ▼
Batch 1 ──── (independent file: SimulateControlsPanel.vue)
  ║
  ╠═══ Batch 2 (independent file: MonitorOverlayPanel.vue) ─┐
  ║                                                          │
  ╚═══ Batch 3 (independent file: ChatStreamPanel.vue) ──────┤
                                                              │
                                            All converge ─────▼
                                                       Batch 4 (final)
```

Three engineers can take Batches 1, 2, 3 simultaneously after Batch 0 lands. Single-engineer order: 0 → 1 → 2 → 3 → 4 (smallest cognitive context-switch cost) or 0 → 2 → 3 → 1 → 4 (smaller batches first to build momentum).

---

## Verification Gates

After every batch:

```powershell
cd src/frontend
.\node_modules\.bin\vue-tsc.CMD --noEmit
.\node_modules\.bin\vitest.CMD run
.\node_modules\.bin\vite.CMD build
.\node_modules\.bin\oxlint.CMD --config oxlint.json
```

After Batch 4 (final): also run full backend test suite to confirm no integration regression from any chat/simulate test stub drift:

```powershell
dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false
```

Manual verification matrix recorded in dated entry under `docs/phases/phase-7d-checkin-binding-editor-monitor-member/manual-verification.md` (or sibling for monitor-only slice).

---

## Estimated Effort

| Batch | Engineer-hours (focused) |
|---|---|
| Batch 0 | 1-2h (or 30min if R1 mitigation applied) |
| Batch 1 | 4-6h |
| Batch 2 | 3-4h |
| Batch 3 | 4-5h (composable + chaos test add ~2h vs prior estimate) |
| Batch 4 | 2-3h |
| **Total** | **14-20h** single-engineer; **10-14h** parallelized 3-way |

---

## Open Questions Resolved

| # | Resolution | Action |
|---|---|---|
| Q1 | `app.css` has **no** CSS variables (grep confirmed). Apply R1 mitigation. | Keep `--monitor-*` as light-only with `[data-theme="dark"]` block wired; add TODO comment referencing future global theming spec. |
| Q2 | Use determinate PrimeVue ProgressBar. | Batch 1 imports `primevue/progressbar` (or equivalent) with `:value` driven by `batchProgress / batchSize * 100`. |
| Q3 | Backend has **no** `isTest`/`IsTest`/`testMode` flag (grep confirmed across `src/`). | Add UI-only toggle in Batch 1 with comment `// TODO: backend support pending — currently UI label only`. Do not pass the flag in the request body until backend lands it. |
| Q4 | Three-layer state pattern: **(L1) passive callbacks** + **(L2) incremental reconnect** + **(L3) defensive 30s polling sync (read-only)**. See "SignalR Connection State Pattern" section below. | Batch 3 introduces a new `useHubConnectionState` composable. **Forbidden**: writing retry loops, calling `start()` from watchers/intervals, or auto-restarting on `Disconnected` without user/visibility trigger. |

---

## Recommended First Move

1. Read `src/frontend/src/styles/app.css` to resolve Q1.
2. If theme tokens exist: do Batch 0.1 properly (10 min).
3. If not: do R1 mitigation (5 min) + proceed to Batch 1.
4. Land Batch 1 fully (single largest batch) before opening Batches 2-3.

Plan is ready for execution.
