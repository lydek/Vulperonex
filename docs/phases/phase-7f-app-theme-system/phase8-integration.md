# Phase 7F Theme System — Phase 8 Branch Integration Record

> This document records the integration process and decisions of the `codex/phase8-theme` branch, which merged the `codex/theme-design` theme system port into `codex/phase8-workflow-ui`.
> Design specifications can be found in `plan.md` in the same directory and `docs/SPEC.md` §4.24; page-by-page audit is documented in `manual-verification.md`.

## Background

- `codex/theme-design` completed the theme system design and most migrations (based on the shared base `6af10e8`).
- `codex/phase8-workflow-ui` branched off from the same base and contains 50 commits covering workflow / Twitch / overlay, but has **no theme system** at all (`app.css` remains hardcoded, no `useTheme`, and `main.ts` does not initialize the theme).
- The two branches diverged, necessitating integration in a new branch `codex/phase8-theme`.

## Integration Strategy

1. Branch `codex/phase8-theme` from `codex/phase8-workflow-ui`.
2. The `app.css` / `monitor-tokens.css` files in `phase8` are **completely identical** to the base, and the token migration in `theme-design` started from the same base, so we can adopt the existing `theme-design` changes directly with no conflicts.
3. `SettingsView.vue` / `SettingsView.test.ts` in `phase8` are also identical to the base, so we can directly apply the `theme-design` versions (including the theme toggle UI).
4. Since the i18n catalogs for the two locales diverged in `phase8`, we manually add the `settings.theme.*` keys.

## Completed Items

### Slice 1 — Theme Foundation (commit `c3ce96a`)

- `composables/useTheme.ts`: supports `light` / `dark` / `system`, localStorage persistence, `prefers-color-scheme` monitoring, and applies `data-theme` + `color-scheme`.
- `main.ts` calls `initializeTheme()` at startup.
- `styles/app.css` uses `--vp-*` semantic tokens and introduces the `[data-theme="dark"]` dark palette.
- `styles/monitor-tokens.css` derives from `--vp-*`, removing the independent hardcoded palette.
- `SettingsView.vue` theme toggle control + i18n (en/zh).
- `useTheme.test.ts` / `SettingsView.test.ts` (6 tests in total).

### Slice 2 — workflow editor + shared components (commit `f7a52ff`)

Scoped CSS updated to use `--vp-*` tokens:

- `VariableFieldInput` / `VariablePicker` / `ConditionExpressionInput`
- `WorkflowActionsEditor` / `WorkflowConditionsEditor` / `StepListShell`
- `RoleChipSelector` / `ChatOutboxView` status badges
- `MonitorDashboardView` residual rgba values converged into tokens

### Slice 3 — rule drawer + Twitch auth (commit `8ea08c7`)

- `RuleEditorDrawer`: migrated overlay / drawer / tabs / footer / skeleton.
- `TwitchAuthView`: migrated description / input / success / device hint.

## Intentional Exceptions

- The `#6441a5` (Twitch brand purple) in `TwitchAuthView` is retained as a brand exception and does not use theme tokens.
- ChatOutbox `processing` / `skipped` statuses lack corresponding info tokens and use the neutral surface token instead.
- Overlay presets (`/overlay/*`, `public/overlay/**`) remain preset-scoped, out of scope for the app theme.

## Verification

- `vue-tsc --noEmit`: PASS (run after each slice).
- `vitest run`: 210 passed / 4 failed.
  - The 4 failures are pre-existing issues in `MembersView` (which also failed on the pre-theme baseline `9c8b8db`), unrelated to this theme migration.

## Backlog (Future Slices)

- High-color component migration: `MembersView` (needs light/dark double tokens to preserve its 292-color premium card layout), `OverlayEditorModal`, `SimulateControlsPanel`, `MonitorOverlayPanel`, `ChatStreamPanel`.
- Custom theme foundation: extend `useTheme` to support user token overrides.
- Browser light/dark smoke testing across breakpoints.
