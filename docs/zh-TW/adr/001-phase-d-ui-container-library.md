# ADR 001: Phase D UI Container Library

## Status

Accepted, 2026-05-29.

## Context

Phase 8 Phase D needs an accessible drawer, tabs, and form shell for the workflow rule editor. The existing frontend uses Vue 3, custom CSS, and a small amount of PrimeVue, but the Phase D plan selected a headless primitive library so visual styling can stay in the Vulperonex CSS system.

## Decision

Use `reka-ui` for Phase D container primitives.

- Version installed: `reka-ui@2.9.8`.
- PoC component: `src/frontend/src/components/admin/RekaPhaseDPoc.vue`.
- PoC test: `src/frontend/src/components/admin/RekaPhaseDPoc.test.ts`.

The PoC uses:

- `DialogRoot`, `DialogTrigger`, `DialogPortal`, `DialogOverlay`, `DialogContent`, `DialogTitle`, `DialogDescription`, and `DialogClose` as a right-side drawer shell.
- `TabsRoot`, `TabsList`, `TabsTrigger`, and `TabsContent` for Basic / Actions / Errors tabs.
- Reka `Label` with native form controls for the first form path.

## Token And Styling Check

Reka emits stable data attributes that can be styled by existing CSS:

- Drawer: `.reka-poc-drawer[data-state="open"]`.
- Tabs: `.reka-poc-tab[data-state="active"]`.
- Dialog overlay/content: ordinary class selectors layered over unstyled slots.

The PoC intentionally uses existing local button/form classes (`primary-button`, `icon-button`, `form-field`, `form-label`) and direct CSS selectors instead of library themes. This confirms Phase D can use current CSS variables/classes without replacing the app token system.

## Bundle And Build Evidence

`reka-ui` was added to `src/frontend/package.json` and `src/frontend/pnpm-lock.yaml`.

Verification run:

- `vitest run src/components/admin/RekaPhaseDPoc.test.ts`: 1 test passed.
- `vue-tsc --noEmit`: passed.
- `vite build --outDir ../../artifacts/vulperonex-phase8-reka-build --emptyOutDir`: passed.

Build output after the install, with the PoC component not routed into production yet:

- CSS bundle: `225.64 kB`, gzip `39.45 kB`.
- Main JS bundle: `4,229.49 kB`, gzip `1,117.17 kB`.

Phase D.1 routed build output after `RuleEditorDrawer.vue` was imported by `RulesView`:

- CSS bundle: `227.48 kB`, gzip `39.75 kB`.
- Main JS bundle: `4,278.37 kB`, gzip `1,132.42 kB`.
- Routed delta versus the unrouted PoC build: CSS gzip `+0.30 kB`, main JS gzip `+15.25 kB`.

The routed delta is within the Phase D budget.

Phase D.2 routed build output after `TriggerEditor.vue` was switched to metadata-driven typed fields:

- CSS bundle: `227.53 kB`, gzip `39.78 kB`.
- Main JS bundle: `4,280.65 kB`, gzip `1,133.06 kB`.
- Delta versus the Phase D.1 routed build: CSS gzip `+0.03 kB`, main JS gzip `+0.64 kB`.

The D.2 delta is still within the Phase D budget.

Phase D.3 routed build output after `VariablePicker.vue` was filtered by trigger metadata:

- CSS bundle: `227.53 kB`, gzip `39.78 kB`.
- Main JS bundle: `4,281.45 kB`, gzip `1,133.23 kB`.
- Delta versus the Phase D.2 routed build: CSS gzip `+0.00 kB`, main JS gzip `+0.17 kB`.

The D.3 delta is still within the Phase D budget.

Phase D.4 routed build output after action editor metadata moved from frontend hardcoded definitions to `/api/metadata/actions`:

- CSS bundle: `227.53 kB`, gzip `39.77 kB`.
- Main JS bundle: `4,278.75 kB`, gzip `1,132.63 kB`.
- Delta versus the Phase D.3 routed build: CSS gzip `-0.01 kB`, main JS gzip `-0.60 kB`.

The D.4 delta is still within the Phase D budget.

## Consequences

- Continue Phase D.1 with `reka-ui` rather than PrimeVue Dialog/Tabs.
- Keep the legacy full-page `RuleEditorView` as advanced fallback.
- Keep measuring gzip deltas when later Phase D tasks add metadata-driven fields and action metadata stores.
