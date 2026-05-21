# ADR 001: Frontend Stack Selection

## Status

Approved for Phase 6 planning.

## Context

Phase 6 moves the already verified loopback Web API, SignalR overlay, CLI manual-test flows, and Twitch OAuth status into a local Web UI and later wraps that UI in the Photino desktop shell. The first screen must be an operational control surface, not a landing page.

The stack must support:

- Vue single-file components with TypeScript.
- A dense local admin UI for repeated streamer operations.
- Overlay routes that stay isolated from admin state.
- SignalR state handling with deterministic tests.
- `zh-TW` and `en-US` i18n.
- Static build output served by `src/Hosts/Vulperonex.Web/wwwroot`.
- Ask-first dependency installation.

## Decision

Use the following frontend stack:

- **Vue 3.5** with Composition API and SFCs.
- **Vite 7.3** for dev server and production build.
- **PrimeVue 4 Unstyled** for accessible component primitives without imposing a visual theme.
- **UnoCSS Preset Wind 4** for utility styling.
- **Pinia Setup Stores** for admin state, with readonly exposed state and mutations through actions.
- **vue-i18n** with `zh-TW` as default and `en-US` as the second required locale.
- **Vitest + Vue Test Utils** for component and composable tests.
- **vue-tsc** for Vue SFC type checking.
- **oxlint** as the configured linter; do not add ESLint unless the plan is explicitly revised.
- **pnpm 9.15.4**, pinned through `packageManager`.

## Constraints

- First dependency installation still requires ask-first approval.
- pnpm 9.15.4 does not support `pnpm install --dry-run`; run `corepack pnpm@9.15.4 install --lockfile-only --ignore-scripts` before installing the stack to catch version conflicts without running lifecycle scripts.
- API base URL must use same-origin relative paths by default and support `VITE_API_URL` override for dev.
- Overlay pages must not share admin Pinia state; they connect directly to their dedicated overlay hubs.
- Overlay rendering must use text binding only; do not use `v-html` for external event content.
- OAuth `code` is consumed by the backend callback endpoint and must not be exposed to Web UI routes.

## Consequences

- Task 19 owns frontend skeleton, build output, i18n manifest, API client, stores, SignalR composables, and overlay route skeletons.
- Task 20 can build admin panels on top of that foundation without adding a second state pattern.
- Task 21 can load the built UI through the desktop shell without changing frontend architecture.
