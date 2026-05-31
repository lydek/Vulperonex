# Phase 7F TODO

## Docs

- [x] Add global theme system scope to `docs/SPEC.md`.
- [x] Add Phase 7F plan.
- [x] Add Phase 7F todo.
- [x] Add Phase 7F manual verification template.
- [x] Link Phase 7F from `tasks/plan.md` and `tasks/todo.md`.

## Audit

- [x] Inventory all admin routes, shared components, overlay previews, and static overlay assets.
- [x] Count hard-coded color usage by file and classify each route as `done`, `partial`, `preset-scoped`, or `deferred`.
- [x] Record migration priority and blockers in `manual-verification.md`.

## Theme Foundation

- [x] Define canonical `--vp-*` tokens for light and dark theme.
- [x] Add runtime theme preference model: `light`, `dark`, `system`.
- [x] Add a theme composable/service with local fallback persistence.
- [x] Add unit tests for theme preference application and system preference changes.
- [x] Add Settings UI control for theme selection.

## Shared Shell Migration

- [x] Convert `app.css` shell, cards, forms, tables, buttons, chips, modals, alerts, and focus styles to `--vp-*`.
- [ ] Verify default light theme visually matches current app closely.
- [ ] Verify dark theme has usable contrast.

## Monitor Integration

- [x] Rewire `monitor-tokens.css` to derive from `--vp-*`.
- [ ] Convert monitor-related components that still hard-code app chrome colors.
- [/] Verify `/monitor` in light and dark mode.

## Page Migration (phase8-theme branch status)

> 此分支整合詳見 `phase8-integration.md`。狀態以 `codex/phase8-theme` 為準。

- [x] workflow editor components（VariableFieldInput / VariablePicker /
  ConditionExpressionInput / WorkflowActionsEditor / WorkflowConditionsEditor /
  StepListShell / RoleChipSelector）
- [x] `ChatOutboxView.vue`
- [x] `MonitorDashboardView.vue`（殘留 rgba 收斂）
- [x] `RuleEditorDrawer.vue`（phase8 以 drawer 取代 RuleEditorView）
- [x] `TwitchAuthView.vue`（保留 Twitch 品牌紫例外）
- [x] `SettingsView.vue`
- [ ] `MembersView.vue`（292 色 premium card，需 light/dark 雙 token，延後）
- [ ] `OverlayEditorModal.vue`
- [ ] `SimulateControlsPanel.vue`
- [ ] `MonitorOverlayPanel.vue`
- [ ] `ChatStreamPanel.vue`

## Custom Theme（後續）

- [ ] useTheme 擴充支援使用者自訂 token 覆寫。
- [ ] 自訂主題最小 UI。

## Checkpoint

- [x] `rtk corepack pnpm@9.15.4 --dir src/frontend exec vue-tsc --noEmit` - 2026-05-27 via local `.\\node_modules\\.bin\\vue-tsc.cmd --noEmit`, PASS
- [x] `rtk corepack pnpm@9.15.4 --dir src/frontend exec vitest run` - 2026-05-27 via local `.\\node_modules\\.bin\\vitest.cmd run`, 201/201 PASS
- [x] `rtk corepack pnpm@9.15.4 --dir src/frontend build` - 2026-05-27 via local `.\\node_modules\\.bin\\vite.cmd build --outDir C:\\tmp\\vulperonex-theme-frontend-build`, PASS
- [x] `rtk corepack pnpm@9.15.4 --dir src/frontend lint` - 2026-05-27 via local `.\\node_modules\\.bin\\oxlint.cmd --config oxlint.json`, 0 warnings / 0 errors
- [ ] Browser smoke: light theme at 320px, 768px, 1024px, 1440px.
- [ ] Browser smoke: dark theme at 320px, 768px, 1024px, 1440px.
- [ ] `manual-verification.md` contains route matrix and PASS/FAIL evidence.
