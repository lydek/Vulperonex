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

Because the PoC is not routed into the application, the production bundle does not yet represent the final routed drawer cost. Phase D.1 must re-measure after `RuleEditorDrawer.vue` is imported by `RulesView`.

## Consequences

- Continue Phase D.1 with `reka-ui` rather than PrimeVue Dialog/Tabs.
- Keep the legacy full-page `RuleEditorView` as advanced fallback.
- Re-measure gzip delta after D.1 imports the drawer into the real rule list path.
