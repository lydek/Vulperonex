# Phase 7F App Theme System Plan

> Parent plan: `tasks/plan.md`
> Parent todo: `tasks/todo.md`
> SPEC section: `docs/SPEC.md` section 4.24

## Goal

Build a global theme system for the Vue admin shell and shared frontend components. The current app has scattered hard-coded colors, a light-only app shell, and a monitor-only token scaffold. Phase 7F makes theme behavior explicit before implementation: token source, persistence, switching UI, migration order, and page-by-page verification.

## Current Findings

- `src/frontend/src/styles/app.css` is light-only and hard-codes shell, card, form, table, badge, alert, and button colors.
- `src/frontend/src/styles/monitor-tokens.css` has monitor-local tokens and a dark block, but comments state it is not a finished global theme.
- `src/frontend/src/main.ts` uses PrimeVue with `unstyled: true`; therefore the app owns all visual tokens.
- Several admin pages and editor components define local hex or rgba values inside SFC styles.
- Overlay routes and static OBS assets are display presets, not the first target for admin app theme switching. They can keep preset-specific tokens unless shown inside the admin shell.

## Design Decisions

1. **Global tokens first**: introduce `--vp-*` tokens in `app.css` as the canonical source for admin UI colors, surfaces, borders, focus, shadows, radii, and status colors.
2. **Data attribute switching**: `document.documentElement.dataset.theme` is the runtime switch. Supported initial values are `light`, `dark`, and `system`.
3. **System preference support**: `system` follows `prefers-color-scheme` and updates when the OS preference changes.
4. **Persist through settings**: user preference is stored in app settings after backend wiring exists. Before that, local storage is allowed only as a frontend bridge and must be isolated behind a theme service/composable.
5. **No new dependency**: theme switching uses Vue, CSS variables, and existing APIs.
6. **Monitor tokens derive from app tokens**: `--monitor-*` should become aliases or derived values from `--vp-*`, not a separate permanent palette.
7. **Page migration is incremental**: shared app shell and common primitives migrate first, then admin pages by risk and color count.
8. **Overlay preset styles stay scoped**: `/overlay/*` and `public/overlay/**` keep preset-specific tokens unless embedded in admin previews, where surrounding chrome follows `--vp-*`.

## Implementation Slices

### Slice 1 - Theme Foundation

- Add `src/frontend/src/styles/theme.css` or extend `app.css` with `--vp-*` tokens.
- Add `src/frontend/src/composables/useTheme.ts` to read, apply, and persist theme preference.
- Add tests for `light`, `dark`, `system`, local persistence fallback, and media-query updates.
- Add a compact theme control in `SettingsView.vue`.

### Slice 2 - Shell and Shared Primitives

- Replace hard-coded app shell colors in `app.css` with `--vp-*`.
- Convert shared classes: buttons, cards, form fields, chips, modal, table, alerts, and code blocks.
- Keep layout unchanged.

### Slice 3 - Monitor Token Integration

- Rewire `monitor-tokens.css` to derive from `--vp-*`.
- Remove obsolete comments that say dark is only scaffold after the UI can toggle themes.
- Verify `/monitor` with both themes and preview background controls.

### Slice 4 - High-Color Admin Pages

Prioritize by color count and user exposure:

1. `MembersView.vue`
2. `OverlayEditorModal.vue`
3. `SimulateControlsPanel.vue`
4. `MonitorOverlayPanel.vue`
5. `ChatStreamPanel.vue`
6. `TwitchAuthView.vue`
7. `SettingsView.vue`
8. `RuleEditorView.vue`
9. `ChatOutboxView.vue`
10. workflow editor components

### Slice 5 - Verification and Cleanup

- Produce a theme audit matrix for all routes.
- Run frontend typecheck, Vitest, build, and lint.
- Browser-smoke desktop and mobile widths for light and dark.
- Confirm no admin text becomes low contrast or invisible.

## Acceptance Criteria

- App supports `light`, `dark`, and `system` preferences.
- Theme preference can be changed from Settings without reload.
- Admin shell, shared controls, and migrated pages use `--vp-*` tokens for surfaces, text, borders, focus, and status colors.
- Monitor page follows app theme and no longer owns a disconnected palette.
- Static overlay preset styles are explicitly documented as preset scope, not app theme scope.
- Theme audit lists every route as `done`, `partial`, `preset-scoped`, or `deferred`.
- No new package dependency is added.

## Verification Commands

Run from `D:\code\Vulperonex-theme` unless noted:

```powershell
rtk corepack pnpm@9.15.4 --dir src/frontend exec vue-tsc --noEmit
rtk corepack pnpm@9.15.4 --dir src/frontend exec vitest run
rtk corepack pnpm@9.15.4 --dir src/frontend build
rtk corepack pnpm@9.15.4 --dir src/frontend lint
```

If the default Vite output path is locked, use:

```powershell
rtk corepack pnpm@9.15.4 --dir src/frontend exec vite build --outDir C:\tmp\vulperonex-frontend-build
```

## Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Large color migration causes visual regressions | High | Commit by slice and use route audit screenshots/manual checks |
| Dark theme contrast gaps | High | Use semantic tokens and check text, controls, borders, focus rings |
| Overlay preset confusion | Medium | Keep app theme and OBS preset themes separate in docs and code comments |
| Settings persistence backend scope expands | Medium | Start with isolated frontend theme service, then wire durable settings in a separate slice |
