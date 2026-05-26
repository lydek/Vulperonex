# Spec: Monitor Dashboard Redesign

## Objective

Redesign `src/frontend/src/views/admin/MonitorDashboardView.vue` so its layout, visual hierarchy, and operator workflow closely follow `ref/Omni-Commander/OmniCommander.UI/src/components/monitor/` while preserving the current Vulperonex theme colors and frontend stack.

This is a planning spec for implementation. It defines the target UI, boundaries, task slices, verification checkpoints, and open questions before code changes continue.

## Product Goal

The redesigned Monitor Dashboard should feel like a real-time operations console instead of a generic admin page.

The page should optimize for three operator loops:

1. Trigger or simulate an event quickly from the control panel.
2. Watch the overlay preview react immediately in the center workspace.
3. Confirm result and context from the live chat stream on the right.

## Reference Baseline

Primary visual and interaction baseline:

- `ref/Omni-Commander/OmniCommander.UI/src/components/monitor/MonitorDashboard.vue`
- `ref/Omni-Commander/OmniCommander.UI/src/components/monitor/MonitorControls.vue`
- `ref/Omni-Commander/OmniCommander.UI/src/components/monitor/MonitorOverlay.vue`
- `ref/Omni-Commander/OmniCommander.UI/src/components/monitor/MonitorChat.vue`

What to copy from the reference:

- Dark control-room composition and density
- Three-zone layout: left controls, center preview, right event/chat feed
- Strong header bar with immediate system status
- Embedded preview toolbar instead of a plain card header
- High-contrast control groups with compact labels and clear call-to-action buttons

What not to copy literally:

- `naive-ui`
- SCSS mixin system
- Legacy store/navigation structure
- Features outside the current Vulperonex scope such as settings submodules, native-only bridge behavior, or unused monitor widgets

## Current State

The current `MonitorDashboardView.vue` already has the basic three-panel shell:

- Header with title and server health
- Collapsible left simulation panel
- Center overlay preview panel
- Right chat stream panel
- Narrow-screen drawer behavior

However, it still differs from the intended reference in several important ways:

- The overall page still reads as a light admin layout instead of an operations surface.
- The center preview area lacks the stronger toolbar framing and workspace emphasis seen in the reference.
- The left control panel hierarchy is functional but not yet visually aligned with the denser control-card rhythm from the reference.
- The right feed area needs stronger event-stream framing and more deliberate width behavior.
- The responsive behavior exists, but the spec should lock down exact breakpoint expectations and interaction states.

## Design Direction

### Visual direction

Preserve Vulperonex brand colors and follow the active Vulperonex theme. The monitor page should feel specialized without becoming a literal clone of Omni Commander.

Required traits:

- The monitor workspace should derive its surface treatment from the main theme instead of hard-coding an always-dark or always-light shell
- Bright accent usage reserved for action buttons, status, and focus
- Strong panel separation using borders, glow, subtle gradients, and layered surfaces
- Compact typography for controls, larger focal treatment for header and preview workspace

### Layout model

Desktop target:

- Left fixed/collapsible controls rail
- Center dominant preview workspace
- Right fixed chat/event feed

Responsive target:

- `>= 1280px`: three-column layout with collapsible left rail
- `1024px - 1279px`: controls move to drawer, preview and chat remain side-by-side with a fixed two-column main area
- `< 1024px`: preview stacked above chat, controls remain drawer-only

### Theme rule

Do not import Omni Commander colors directly. Re-express the design through Vulperonex-owned tokens under `--monitor-*`.

## Scope

### In scope

- Redesign of `MonitorDashboardView.vue`
- Supporting style-token updates for monitor dashboard surfaces
- Refined layout contracts between dashboard shell and existing child panels
- Minor presentational updates in existing monitor child components when needed to match the new composition
- Test updates for layout, accessibility, and responsive behavior
- i18n additions required by the redesigned shell

### Out of scope

- New backend endpoints
- Replacing existing preview/chat/control business logic
- Rewriting the simulation workflows themselves
- Porting all reference-only monitor features
- Introducing new UI libraries or utility dependencies

## Architecture Decisions

- Keep Vue 3 + TypeScript + PrimeVue stack. No `naive-ui`, no SCSS migration.
- Preserve current child-component boundaries unless a boundary directly blocks the redesign.
- Drive styling through monitor-specific CSS variables instead of one-off hex values.
- Treat the dashboard shell as the owner of responsive layout state.
- Keep the redesign vertical: shell first, then preview chrome, then control density, then verification.
- Keep the right-side panel chat-first for this slice. Visual reframing is allowed; changing it into a broader mixed-event product surface is not.
- Keep preview toolbar changes presentational for this slice. Re-layout existing controls, but do not introduce a new live/draft product mode beyond what `MonitorOverlayPanel.vue` already supports.

## Target UI Breakdown

### Header

The header should become an operations bar:

- Left: icon, eyebrow, title
- Right: control-panel toggle action and system health chip
- Optional subtle live/status pulse treatment

Acceptance notes:

- Must immediately communicate page identity and service state
- Must remain usable at narrow widths without wrapping into a broken layout

### Left controls rail

The controls rail should visually resemble the reference rhythm:

- Sticky-feeling utility section at the top
- Dense grouped control cards
- Strong primary action buttons
- Drawer version on narrower widths
- `SimulateControlsPanel.vue` should be brought as close as practical to the reference in both layout and exposed functionality for this slice

Acceptance notes:

- The rail must feel intentional when open and unobtrusive when collapsed
- Collapse/expand behavior must not break preview width calculations
- The first redesign slice should prefer reference parity over conservative content pruning inside `SimulateControlsPanel.vue`

### Center preview workspace

The preview area should become the visual focal point:

- Toolbar row for hub tabs, preset selection, existing environment controls, and reload
- Secondary row for preview background controls
- Large preview canvas area with stronger workspace framing

Acceptance notes:

- The center panel should read as the primary workspace from first glance
- Toolbar elements should stay usable without horizontal overflow at supported widths
- This slice re-frames existing preview controls; it does not add a new live/draft product contract

### Right chat/event feed

The right panel should read as a live operational feed, but this slice keeps the existing `ChatStreamPanel` contract instead of widening it into a new mixed-event component:

- Clear section header
- Tighter item framing
- Stable width on desktop
- Natural stacking behavior on narrow screens

Acceptance notes:

- The panel must remain readable even when the preview dominates the layout
- Feed framing should visually support the "watch result and confirm" workflow
- Existing chat-focused behavior must remain intact

## File Strategy

Primary files likely touched during implementation:

- `src/frontend/src/views/admin/MonitorDashboardView.vue`
- `src/frontend/src/views/admin/MonitorDashboardView.test.ts`
- `src/frontend/src/components/admin/MonitorOverlayPanel.vue`
- `src/frontend/src/components/admin/ChatStreamPanel.vue`
- `src/frontend/src/components/admin/SimulateControlsPanel.vue`
- `src/frontend/src/styles/monitor-tokens.css`
- `src/frontend/src/i18n/en-US.json`
- `src/frontend/src/i18n/zh-TW.json`

## Implementation Plan

### Phase 1: Lock Shell Structure

#### Task 1: Define final layout contract for desktop, tablet, and mobile

Description:
Specify the exact behavior of header, controls rail, drawer, preview area, and chat feed across the supported breakpoints so implementation stops drifting.

Acceptance criteria:

- [ ] Desktop, medium, and narrow layout rules are explicitly documented and reflected in component state design.
- [ ] Left-rail collapse, drawer open/close, and stacked mobile layout each have a single clear owner in the shell.
- [ ] The resulting layout contract does not require backend or child-logic changes.

Verification:

- [ ] Manual reasoning check against current `MonitorDashboardView.vue`
- [ ] Spec review confirms no conflicting breakpoint behavior remains

Dependencies: None

Estimated scope: S

#### Task 2: Redesign shell surfaces and monitor-specific tokens

Description:
Refine the dashboard page background, panel surfaces, border language, shadows, and spacing tokens so the page shifts from generic admin styling to monitor-console styling while keeping Vulperonex colors.

Acceptance criteria:

- [ ] `--monitor-*` tokens cover page background, panel backgrounds, border colors, shadows, and key accents used by the dashboard shell.
- [ ] No new hard-coded colors are introduced into the shell where tokens should apply.
- [ ] The overall page reads closer to the reference mood without copying reference colors literally.

Verification:

- [ ] Visual check in browser at desktop width
- [ ] Grep or review confirms shell styling uses tokens instead of scattered literals

Dependencies: Task 1

Estimated scope: M

### Phase 2: Bring the Three Main Zones in Line

#### Task 3: Rebuild the operations header

Description:
Turn the current header into a stronger monitor bar with clearer hierarchy, tighter action placement, and a more deliberate system health chip.

Acceptance criteria:

- [ ] Header identity, toggle action, and health state are visually scannable within one glance.
- [ ] Health chip supports `healthy`, `unhealthy`, and `checking` without layout shift.
- [ ] Narrow-width layout keeps header actions usable.

Verification:

- [ ] `MonitorDashboardView.test.ts` covers header render and health-state class behavior
- [ ] Manual browser check at 1440px and 768px

Dependencies: Task 2

Estimated scope: S

#### Task 4: Redesign the controls rail and drawer presentation

Description:
Adjust the left control zone so its card rhythm, density, call-to-action hierarchy, and visible feature set more closely resemble the reference monitor controls while preserving existing simulation capabilities.

Acceptance criteria:

- [ ] The rail visually reads as a dedicated operations sidebar.
- [ ] Drawer presentation on narrower screens feels like the same feature, not a different page.
- [ ] Existing simulation interactions remain available without functional regression.
- [ ] `SimulateControlsPanel.vue` moves toward reference parity in both control grouping and exposed feature coverage where the current Vulperonex data/contracts already support it.

Verification:

- [ ] Manual comparison against reference layout intent
- [ ] Responsive manual check for rail vs drawer behavior
- [ ] Manual comparison confirms the left-side controls are closer to the reference in both structure and available actions, not just styling

Dependencies: Task 2

Estimated scope: M

#### Task 5: Reframe the preview workspace as the visual focal point

Description:
Update `MonitorOverlayPanel.vue` presentation so the preview toolbar and canvas feel like the main work surface, closer to the reference monitor preview composition. This task is presentational and compositional; it does not introduce a new live/draft product mode beyond the controls that already exist today.

Acceptance criteria:

- [ ] Toolbar hierarchy is stronger than the current card header treatment.
- [ ] Preview background controls remain available and readable.
- [ ] Canvas area becomes the dominant visual region in the dashboard.
- [ ] Existing preview environment controls continue to work after the visual redesign.

Verification:

- [ ] Manual preview inspection at desktop and medium widths
- [ ] Existing overlay panel interactions still function

Dependencies: Task 2

Estimated scope: M

#### Task 6: Reframe the chat/event feed as a live verification lane

Description:
Update the right-side panel framing so the existing `ChatStreamPanel` behaves like an operator confirmation feed instead of a plain content pane. This task does not expand the panel into a new cross-event stream product surface.

Acceptance criteria:

- [ ] Desktop width remains stable enough that feed items are readable.
- [ ] Narrow layout stacks below preview without clipped controls or broken scroll behavior.
- [ ] Feed header and list area visually match the new dashboard shell.
- [ ] Existing chat-focused behavior remains intact without requiring a broader event-stream contract change.

Verification:

- [ ] Manual check at 1440px, 1200px, and 800px widths
- [ ] Existing feed rendering remains functional

Dependencies: Task 2

Estimated scope: S

### Checkpoint: After Phase 2

- [ ] The page clearly resembles the reference composition at a high level
- [ ] Theme still feels like Vulperonex, not a direct Omni Commander skin copy
- [ ] Desktop, medium, and mobile layouts all remain usable
- [ ] No child panel lost core functionality

### Phase 3: Interaction, Accessibility, and Polish

#### Task 7: Tighten responsive state handling and transitions

Description:
Stabilize resize behavior, collapse/drawer transitions, and stacked-mode layout so users do not hit inconsistent states while switching viewport widths.

Acceptance criteria:

- [ ] Desktop-to-tablet and tablet-to-mobile transitions do not leave stale open-state behavior behind.
- [ ] Rail collapse and drawer open states are predictable and testable.
- [ ] Layout transitions do not hide primary controls unexpectedly.

Verification:

- [ ] Unit tests cover wide and narrow toggle behavior
- [ ] Manual resize testing across breakpoint edges

Dependencies: Tasks 3-6

Estimated scope: S

#### Task 8: Complete i18n and accessibility pass for the shell

Description:
Finalize labels, status semantics, drawer semantics, and keyboard-targeted behaviors needed by the redesigned shell.

Acceptance criteria:

- [ ] All new shell copy uses i18n keys.
- [ ] Status chip, drawer, and toggle semantics remain valid.
- [ ] Keyboard interaction remains workable for header controls and drawer close flow.

Verification:

- [ ] `MonitorDashboardView.test.ts` covers key accessibility attributes
- [ ] Manual keyboard pass through header, drawer trigger, and drawer close action

Dependencies: Task 7

Estimated scope: S

#### Task 9: Finish regression tests and manual verification script

Description:
Update tests and the final verification checklist so the redesign can be reviewed and implemented confidently in later slices.

Acceptance criteria:

- [ ] Tests cover header shell, wide layout, narrow drawer, and key responsive branches.
- [ ] Manual verification steps are explicit for the final reviewer.
- [ ] The redesign is represented as a finished slice rather than an informal visual tweak.

Verification:

- [ ] `pnpm test`
- [ ] `pnpm vue-tsc --noEmit`
- [ ] `pnpm build`
- [ ] `pnpm lint`

Dependencies: Task 8

Estimated scope: S

## Verification Checklist

Implementation should not be considered complete until all of the following are true:

- [ ] Desktop layout clearly shows left controls, dominant center preview, and right feed
- [ ] Medium layout uses drawer controls without breaking preview/feed usability
- [ ] Mobile layout stacks preview over feed cleanly
- [ ] Header shows a strong monitor identity and readable health state
- [ ] Preview area is the main visual focal point
- [ ] Left controls and right feed feel visually related to the reference design
- [ ] All new shell text is localized
- [ ] Tests, type-check, build, and lint all pass

## Suggested Verification Commands

```powershell
cd src/frontend
corepack pnpm@9.15.4 test
corepack pnpm@9.15.4 vue-tsc --noEmit
corepack pnpm@9.15.4 build
corepack pnpm@9.15.4 lint
```

Manual verification target widths:

- `1440x900`
- `1280x800`
- `1024x768`
- `800x600`

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| The redesign drifts into a pixel-copy of the reference and loses Vulperonex identity | Medium | Keep layout and UX patterns, but express them through Vulperonex tokens and existing component structure |
| Styling changes leak into child panels and create scattered overrides | Medium | Keep shell-owned tokens centralized and limit child changes to presentation seams |
| Responsive logic becomes brittle around breakpoint edges | High | Treat `MonitorDashboardView.vue` as the single source of layout state and test both wide and narrow transitions |
| Preview toolbar grows too wide for medium layouts | Medium | Prioritize action order and collapse lower-priority controls before forcing overflow |
| Scope expands into unrelated monitor settings from the reference | High | Keep this spec limited to the live monitor dashboard experience only |

## Decisions Captured

1. The monitor page should follow the current Vulperonex main theme rather than forcing a permanently dark or permanently light shell.
2. The first redesign slice should push `SimulateControlsPanel.vue` toward reference parity in both functionality and layout instead of limiting the work to visual reorganization.

## Recommended Execution Order

1. Lock shell layout contract and tokens.
2. Redesign header, left rail, preview workspace, and right feed.
3. Stabilize responsive state behavior.
4. Finish i18n, accessibility, tests, and verification.

This order keeps the work vertical and reviewable, with the highest visual-risk items addressed early.
