# OneComme Compatibility Strategy

> Scope: Phase 7B chat overlay preset slice. This document covers how Vulperonex relates to OneComme without embedding OneComme runtime or UI.

> **⚠️ 部分被取代（參見 SPEC §4.14.3）：** 下文提到的 **自訂預設檔管線 (custom preset pipeline)** / `custom` 預設類型 / `wwwroot/overlay/custom/` 已被**移除**。**內建 (built-in)** 預設合約（`OverlayPresetStore` 內建元資料、靜態 `chat.html` + `chat-overlay.js`、`OverlayChatPayload`、`overlay.chat.preset` 設定）仍然有效。任何相依自訂管線的 OneComme 匯入路徑目前皆無落地目標（參見 [onecomme-bridge.md](../../plugins/onecomme-bridge.md)）。下文中有關自訂管線的引用僅具歷史參考價值。

## Goals and Non-Goals

- **Goal:** Reduce migration friction for existing OneComme users by exposing a stable, documented preset contract that an extension can target.
- **Goal:** Keep OneComme integration on an extension / plugin path so the core runtime stays free of OneComme-specific code.
- **Non-goal:** Embedding OneComme runtime, OneComme renderer code, or the OneComme admin UI into Vulperonex.
- **Non-goal:** Executing unreviewed third-party renderer code inside the SPA. Chat/member overlays are static HTML entrypoints; arbitrary HTML is only loaded through the custom preset pipeline and must stay within the overlay sandbox.

## Contract Surface

The chat overlay preset contract is defined by:

1. Built-in/static preset metadata exposed by `OverlayPresetStore`:
   - `key` (kebab-case, stable across releases)
   - `label` (display name)
   - `kind` (`builtin` / `custom`)
   - `relativeUrl` (served static HTML entrypoint)
2. Static HTML renderer contract under `src/frontend/public/overlay/`:
   - `chat.html` bootstraps the overlay shell
   - `js/chat-overlay.js` binds payloads from `/hubs/overlay/chat`
   - `css/*.css` provides per-preset styling selected by `?preset=<id>`
3. `OverlayChatPayload` / `OverlayHubEvent` payload contract:
   - `eventId`, `sentAt`, `displayName`, `eventType`, `segments[]`, `replayed`
   - All values are text or enum-like keys. No HTML, no raw payload.
4. Settings selector: `overlay.chat.preset` (canonical, lower-case) read via `GET /api/config/overlay.chat.preset` and written via `PUT /api/config/overlay.chat.preset`.

## Compatibility Matrix

| OneComme capability | Vulperonex Phase 7B result | Strategy |
| --- | --- | --- |
| Single-page chat overlay served from a known URL | Supported | Canonical OBS URL is `/overlay/chat.html`; compatibility alias `/overlay/chat` redirects there. Query `?preset=<id>` overrides settings. |
| Multiple selectable templates per overlay | Supported | Preset list + settings key + dropdown. |
| Per-template HTML/CSS | Supported | Each preset resolves to a static HTML/CSS/JS bundle under the overlay sandbox. |
| Per-template JS hooks executed on render | Supported only through reviewed overlay bundles | We do not execute arbitrary SPA plugin code; renderer logic lives in static overlay assets or validated custom preset bundles. |
| Template `package.json` metadata | Mapped | Extensions/importers can map package metadata into `OverlayPresetDescriptor` entries and generated static bundles. |
| Template assets (images, fonts) | Mapped | Static assets ship beside the HTML bundle and are referenced relatively from the overlay root. |
| Subscription / follower / cheer rendering | Out of scope for Phase 7B | Other overlays (alerts, member) keep their own preset slice for future phases. |
| Real-time chat / IRC bridges | Two ingestion paths | **Primary:** the native Twitch adapter feeds `IStreamEventBus` directly via EventSub WebSocket (requires Vulperonex OAuth). **Fallback (planned):** when the operator has not authorized Vulperonex, OneComme can perform platform ingestion + auth itself and relay comments into the bus through a OneComme bridge source. See [Dual Ingestion Strategy](#dual-ingestion-strategy). |
| Per-template hot reload via filesystem watcher | Deferred | The first extension path is build-time registration; filesystem hot reload is tracked under a future polish phase. |
| Sound / TTS pipeline | Out of scope | Tracked separately; not part of chat overlay preset contract. |

## Dual Ingestion Strategy

Vulperonex supports two ways to feed live platform events into `IStreamEventBus`. Both terminate at the same bus contract, so every downstream module (Workflow, Overlay, Member) is unaware of which source produced an event.

| Path | Auth requirement | Source | Status |
| --- | --- | --- | --- |
| **Native TwitchLib (IRC + EventSub)** | Vulperonex holds Twitch OAuth (broadcaster grant) | Host wraps TwitchLib: IRC (`TwitchLib.Client`) for chat, EventSub WebSocket (`TwitchLib.EventSub.Websockets`) for alerts (follow/sub/cheer/raid/gift/redemption). Broadcaster auto-resolved from `Twitch:ChannelName`. Maps to the adapter's existing parser/event types. | Implemented. Gated by `Twitch:EventSub:Enabled` + `Twitch:ChannelName` (or `Twitch:BroadcasterId`) + a valid token. |
| **OneComme bridge** | None for Vulperonex — OneComme owns the platform auth/session | OneComme ingests the platform itself; a bridge source relays OneComme comments into the bus through the same `IStreamEventSource` contract. | Planned. No bridge ingestion adapter ships yet; this row records the intended design. |

Rationale for keeping OneComme as a first-class fallback:

- **No-authorization onboarding.** An operator who has not (or cannot) complete the Vulperonex OAuth grant can still get live chat by pointing an existing OneComme install at Vulperonex. This removes the hard OAuth gate as a blocker for first-run.
- **Core stays platform-agnostic.** The bridge is an adapter/plugin, never core code. The OneComme path must publish the same Domain Events (`UserSentMessageEvent`, etc.) the native adapter produces — no OneComme-specific types leak past the bus boundary.
- **Mutually exclusive at runtime.** Operators pick one source per platform to avoid duplicate events; the native adapter's dedup cache only protects against EventSub replay, not cross-source duplication. Document selection as an explicit operator choice when the bridge ships.

When implemented, the OneComme bridge source should:

1. Consume OneComme's comment stream (its local HTTP/WebSocket API), not its renderer or admin UI (per the Non-Goals above).
2. Map `comment.name` → `displayName`, `comment.message` → chat segment text (consistent with [OneComme Bridge](../../plugins/onecomme-bridge.md)).
3. Publish through `IStreamEventSource` so it reuses the existing parse/dedup/display-cache pipeline.

## Extension / Import Path

The migration-oriented path for a OneComme user is:

1. Inspect the existing OneComme template's HTML structure and CSS, and identify the display fields it relies on (`displayName`, message text, badges).
2. Translate that template into a Vue SFC that consumes `events: OverlayHubEvent[]`. The DTO whitelist guarantees the same data is available.
3. Package the converted HTML/CSS/JS as a static overlay bundle rooted at `index.html`.
4. Import the bundle through the custom preset pipeline (`POST /api/overlay/custom-presets`) or ship it as a built-in asset under `src/frontend/public/overlay/`.
5. Register or select the preset through `overlay.chat.preset`, then open `/overlay/chat.html?preset=<id>`.

## Manual Verification Steps

1. With Vulperonex running, navigate to `/overlay/chat.html`. Confirm the default preset (`vulperonex-default`) renders.
2. Reload the page with `?preset=compact-line`. Confirm the compact preset loads from the same static entrypoint.
3. Issue `PUT /api/config/overlay.chat.preset` with `{ "value": "compact-line" }` and reload `/overlay/chat.html` without the query string. Confirm the setting persists.
4. Upload a custom preset bundle, set `overlay.chat.preset=custom:<slug>`, and confirm `/overlay/chat` redirects to the generated custom HTML.

## Security Boundaries

- Built-in chat/member presets are static overlay assets shipped with the application. They are subject to the same review as any other frontend asset.
- Presets must render text from the DTO contract only. Raw HTML from event payloads is forbidden.
- Preset metadata cannot expand the DTO whitelist; new fields require a backend change reviewed for DTO leakage.
- Settings keys are routed through `ConfigEndpoints`. `security.*` and `oauth.*` namespaces remain blocked.
