# Feature Spec: Global App Theme System

> [← Back to Master Specification](../../SPEC.md)

### 4.24 Global App Theme System (Phase 7F)

**Goal:** Vulperonex admin UI must support a global theme model instead of relying on scattered hard-coded light colors. The theme system covers the Vue admin shell, shared admin components, monitor chrome, settings UI, and embedded preview chrome. OBS overlay presets remain preset-scoped unless they are rendered as admin chrome.

**Theme modes:**

| Mode | Behavior |
| --- | --- |
| `light` | Force light theme tokens. |
| `dark` | Force dark theme tokens. |
| `system` | Follow `prefers-color-scheme` and update when OS preference changes. |

**Token contract:**

- `--vp-*` tokens are the canonical app-level tokens for admin UI surfaces, text, borders, focus rings, shadows, status colors, and interactive states.
- `src/frontend/src/styles/app.css` owns the default app token definitions and app shell usage.
- Feature-local token files may exist, but they must derive from `--vp-*` unless they are explicitly preset-scoped.
- `src/frontend/src/styles/monitor-tokens.css` must not remain a disconnected permanent palette; monitor tokens derive from app tokens after Phase 7F.

**Runtime contract:**

- Runtime theme is applied through `document.documentElement.dataset.theme`.
- Stored user preference is one of `light`, `dark`, or `system`.
- Settings UI exposes theme selection.
- Durable persistence uses system settings when backend wiring is available. A frontend local-storage bridge is allowed only behind a theme service/composable so it can be replaced without changing page components.
- No new frontend dependency is required for theme switching.

**Scope split:**

- Admin app scope: `/monitor`, `/settings`, `/simulate`, `/events`, `/members`, `/overlay-presets`, `/rules`, `/timers`, `/chat-outbox`, `/twitch`, shared components under `src/frontend/src/components/admin`, and admin preview chrome.
- Preset scope: static assets under `src/frontend/public/overlay/**` for chat/member, plus `/overlay/alerts` and its Vue components under `src/frontend/src/views/overlay` for alerts. Preset scope may use its own brand or OBS-specific tokens and does not have to follow the app theme unless an explicit preset setting is added later.

**Acceptance criteria:**

- Switching theme updates admin UI without a page reload.
- `system` mode follows OS preference.
- App shell and common primitives use `--vp-*` tokens instead of raw app palette values.
- Monitor page follows the active app theme.
- Every route has a Phase 7F theme verification status in `docs/phases/phase-7f-app-theme-system/manual-verification.md`.
- Frontend typecheck, tests, build, and lint pass or document unrelated pre-existing blockers.

---
