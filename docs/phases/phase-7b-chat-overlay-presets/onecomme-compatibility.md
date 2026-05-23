# OneComme Compatibility Strategy

> Scope: Phase 7B chat overlay preset slice. This document covers how Vulperonex relates to OneComme without embedding OneComme runtime or UI.

## Goals and Non-Goals

- **Goal:** Reduce migration friction for existing OneComme users by exposing a stable, documented preset contract that an extension can target.
- **Goal:** Keep OneComme integration on an extension / plugin path so the core runtime stays free of OneComme-specific code.
- **Non-goal:** Embedding OneComme runtime, OneComme renderer code, or the OneComme admin UI into Vulperonex.
- **Non-goal:** Executing third-party template scripts. Templates are Vue components shipped with Vulperonex or a signed extension; arbitrary HTML strings are never bound through `v-html`.

## Contract Surface

The chat overlay preset contract is defined by:

1. `ChatOverlayPreset` metadata in `src/frontend/src/views/overlay/chatPresets.ts`:
   - `id` (kebab-case, stable across releases)
   - `label` (display name)
   - `description` (1-line summary)
   - `component` (Vue component reference)
2. The renderer component prop shape:
   - `events: readonly OverlayHubEvent[]`
   - `emptyLabel: string`
3. `OverlayHubEvent` payload contract from `src/frontend/src/composables/useOverlayHub.ts`:
   - `eventId`, `sentAt`, `displayName`, `eventType`, `segments[]`, `replayed`
   - All values are text or enum-like keys. No HTML, no raw payload.
4. Settings selector: `overlay.chat.preset` (canonical, lower-case) read via `GET /api/config/overlay.chat.preset` and written via `PUT /api/config/overlay.chat.preset`.

## Compatibility Matrix

| OneComme capability | Vulperonex Phase 7B result | Strategy |
| --- | --- | --- |
| Single-page chat overlay served from a known URL | Supported | `/overlay/chat` returns the active preset; URL query `?preset=<id>` overrides settings. |
| Multiple selectable templates per overlay | Supported | Preset list + settings key + dropdown. |
| Per-template HTML/CSS | Mapped through Vue components | Each preset is a sandboxed Vue component bundled with the core or extension; styles are scoped. |
| Per-template JS hooks executed on render | Supported only through code-reviewed components | We do not execute arbitrary template scripts. Extensions must ship Vue SFCs that pass the same security review as built-in presets. |
| Template `package.json` metadata | Mapped | Extensions can ship metadata that resolves into `ChatOverlayPreset` entries; importer scans a presets directory and registers entries that match the contract. |
| Template assets (images, fonts) | Mapped | Extension static assets must be co-located with the SFC and referenced through component imports, not raw `<img src>` to filesystem paths. |
| Subscription / follower / cheer rendering | Out of scope for Phase 7B | Other overlays (alerts, member) keep their own preset slice for future phases. |
| Real-time chat / IRC bridges | Out of scope | OneComme handles platform ingestion itself; Vulperonex pulls from `IStreamEventBus` which is fed by the Twitch adapter. |
| Per-template hot reload via filesystem watcher | Deferred | The first extension path is build-time registration; filesystem hot reload is tracked under a future polish phase. |
| Sound / TTS pipeline | Out of scope | Tracked separately; not part of chat overlay preset contract. |

## Extension / Import Path

The migration-oriented path for a OneComme user is:

1. Inspect the existing OneComme template's HTML structure and CSS, and identify the display fields it relies on (`displayName`, message text, badges).
2. Translate that template into a Vue SFC that consumes `events: OverlayHubEvent[]`. The DTO whitelist guarantees the same data is available.
3. Package the SFC plus optional CSS into an extension under `src/frontend/src/views/overlay/presets/<extension>/` (built-in path) or, for out-of-tree extensions, under an `extensions/<name>/chat-presets/` directory whose contents are statically imported into `chatPresets.ts` at build time.
4. Register the preset entry in `chatPresets.ts`:
   ```ts
   {
     id: "onecomme-style-board",
     label: "OneComme style board",
     description: "OneComme-inspired chat board ported to Vulperonex.",
     component: ImportedOneCommePreset
   }
   ```
5. Flip the active preset by setting `overlay.chat.preset` via the existing config endpoint or by appending `?preset=onecomme-style-board` to the overlay URL.

## Manual Verification Steps

1. With Vulperonex running, navigate to `/overlay/chat`. Confirm the default preset (`vulperonex-default`) renders.
2. Switch the dropdown to `compact-line`. Confirm the layout swaps without reloading the page.
3. Reload the page with `?preset=compact-line`. Confirm the query parameter wins regardless of the stored setting.
4. Issue `PUT /api/config/overlay.chat.preset` with `{ "value": "compact-line" }` and reload `/overlay/chat` without the query string. Confirm the setting persists.
5. (Migration smoke) Drop a third preset SFC under `src/frontend/src/views/overlay/presets/` that reads only `events` and `emptyLabel`, register it in `chatPresets.ts`, rebuild the frontend, and confirm it appears in the dropdown alongside the two built-ins.

## Security Boundaries

- Presets are Vue components shipped with the application bundle. They are subject to the same review as any other UI code.
- Presets must use template interpolation for text. `v-html` is forbidden.
- Preset metadata cannot expand the DTO whitelist; new fields require a backend change reviewed for DTO leakage.
- Settings keys are routed through `ConfigEndpoints`. `security.*` and `oauth.*` namespaces remain blocked.
