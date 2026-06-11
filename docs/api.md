# Vulperonex Web API Reference

> **Language / 語言**: [English](api.md) | [繁體中文](zh-TW/api.md)

Complete integration surface of the `Vulperonex.Web` host: REST endpoints and SignalR hubs. Intended as the contract reference for admin-UI clients.

The machine-readable spec is always available at runtime: **`GET /openapi/v1.json`** — prefer generating clients from it (e.g. openapi-typescript, orval) over hand-writing request code.

## Conventions

- JSON bodies use **camelCase**; enums serialize as **strings**; property names are case-insensitive on input.
- Errors follow `{ "error": "<ERROR_CODE>", "meta": null }` with a matching HTTP status (see `ErrorCodes` / `ErrorCodeStatusMap`).
- The API port binds **loopback only**. The overlay port may additionally bind a LAN address when `Overlay:Lan:Enabled` is set.

## Authentication & request guards

Enforced by `AdminGuardMiddleware`:

| Surface | Requirement |
|---|---|
| `GET /api/*` (except `/api/overlay/*`) | Loopback origin only |
| Non-GET `/api/*`, and all `/api/overlay/*` | Loopback + `X-Admin-Csrf` header + allow-listed `Host` + matching `Origin`/`Referer` |
| LAN requests | Only overlay static assets (GET), SignalR hubs (access key), and six overlay-safe config keys (access key) |

Flow for an admin client:

1. `GET /api/overlay/csrf-token` → `{ token }`. The token is regenerated **every Kestrel restart**; refetch on 400 `MISSING_OR_INVALID_CSRF_HEADER`.
2. Send `X-Admin-Csrf: <token>` on every mutating request and every `/api/overlay/*` request.

LAN overlay clients (OBS on another machine) authenticate with the access key via `?k=<key>` query or `X-Overlay-Key` header.

## System

| Method | Path | Description |
|---|---|---|
| GET | `/health` | `{ "status": "ok" }` |
| GET | `/openapi/v1.json` | OpenAPI document |
| GET | `/api/overlay/csrf-token` | `{ token }` — admin session CSRF token |

## Members — `/api/members`

| Method | Path | Description |
|---|---|---|
| GET | `/` | List members. Query: `platform`, `limit` (1–200, default 50), `offset`. Each item carries `eTag`. |
| GET | `/{id}` | Single member; responds with `ETag` header. |
| GET | `/{id}/audit` | Audit log entries. Query: `limit`, `offset`. |
| PATCH | `/{id}/loyalty` | Adjust loyalty. Requires `If-Match` (missing → 428, stale → 409). Body: `{ totalLoyalty?, checkInCount?, reason }`. |
| POST | `/{id}/reset` | Reset counters. Requires `If-Match`. Body: `{ resetLoyalty, resetCheckIn, reason }`. |
| POST | `/{id}/delete-token` | Issue one-time delete token → `{ token }`. |
| DELETE | `/{id}` | Two-step delete. Body: `{ token, reason }`. |

## Workflow rules — `/api/rules`

| Method | Path | Description |
|---|---|---|
| GET | `/` | List rule summaries. |
| GET | `/{id}` | Full rule DTO. |
| POST | `/` | Create. Body must **not** contain `id` → 201 with `Location`. |
| PUT | `/{id}` | Update. Optimistic concurrency via stored version → 409 on conflict; cycle detection on `invoke-sub-workflow` actions → 400 `CIRCULAR_WORKFLOW_REFERENCE`. |
| DELETE | `/{id}` | Delete. |
| PUT | `/{id}/enable` | Enable rule. |
| PUT | `/{id}/disable` | Disable rule. |

## Workflow timers — `/api/timers`

DTO: `{ id, ruleId, intervalSeconds, isEnabled, nextFireAt, version }`.

| Method | Path | Description |
|---|---|---|
| GET | `/` | List timers. |
| GET | `/{id}` | Single timer. |
| POST | `/` | Create → 201. Body: `{ ruleId, intervalSeconds, isEnabled, nextFireAt }`. |
| PUT | `/{id}` | Update. Requires `If-Match: "<version>"` (missing → 428, stale → 409). |
| DELETE | `/{id}` | Delete. |

## Rule-editor metadata

| Method | Path | Description |
|---|---|---|
| GET | `/api/event-types` | `[{ key, description, isSimulatable }]` — system events are filtered server-side. |
| GET | `/api/metadata/triggers` | Per event type: `{ key, displayName, description, filterFields, validVariables }`. |
| GET | `/api/metadata/actions` | Available action types with config schema. |

## System config — `/api/config`

Only allow-listed keys are reachable; `security.*` and `oauth.*` namespaces are rejected with 403.

| Method | Path | Description |
|---|---|---|
| GET | `/{key}` | `{ key, value }` (value is a string or null). |
| PUT | `/{key}` | Body: `{ value }`. Value is validated against the key's declared type (int / bool / enum) → 400 `INVALID_CONFIG_VALUE` on mismatch. |

## Plugin modules — `/api/plugins-modules`

| Method | Path | Description |
|---|---|---|
| GET | `/` | `[{ name, displayName, kind, enabled, dependencies, dependents }]` |
| POST | `/{name}/toggle` | Body: `{ enabled }`. Returns toggled module plus cascade-changed modules. |

## Simulation — `/api/simulate`

| Method | Path | Description |
|---|---|---|
| POST | `/checkin` | Body: `{ platformUserId?, displayName?, skipCooldown?, stampCount?, isTest? }` → 202. `isTest: true` skips all persistence. |
| POST | `/{alias}` | Simulate platform events (`message`, `follow`, `subscribe`, `donate`, `gift-sub`, `raid`, `reward-redeem`, …; see `SimulationAliasRegistry`). Body supports `roles`, `badges`, `colorHex`, `bits`, `viewers`, `rewardId`, `rewardTitle`, … → 202. |

## Twitch authentication — `/api/twitch/auth`

| Method | Path | Description |
|---|---|---|
| GET | `/status` | `{ clientIdConfigured, clientSecretConfigured, hasRefreshToken }` |
| POST | `/start` | Authorization Code + PKCE. Body: `{ callbackPort? }` → `{ authorizeUrl, state, callbackPort }`. |
| POST | `/complete` | Body: `{ state, code }` → 204. |
| POST | `/device/start` | Device Code flow → device authorization payload. |
| POST | `/device/complete` | Body: `{ deviceCode }` → 204 done / 202 still pending. |
| DELETE | `/token` | Clear stored refresh token (disconnect). |
| GET | `/auth/callback`, `/api/auth/twitch/callback` | Browser OAuth redirect targets (not AJAX); redirect back to `/?oauth=<result>`. |

## Twitch data

| Method | Path | Description |
|---|---|---|
| GET | `/api/twitch/badges` | `{ ready, global[], channel[] }` |
| GET | `/api/twitch/rewards` | `{ ready, lastRefreshedAt, rewards[] }` |
| POST | `/api/twitch/rewards/refresh` | Force cache refresh. |

## Chat outbox — `/api/chat-outbox`

| Method | Path | Description |
|---|---|---|
| GET | `/` | Observe outgoing chat queue. Query: `status`, `platform`, `limit` (≤500). |

## Overlay administration

| Method | Path | Description |
|---|---|---|
| GET | `/api/overlay/presets` | Built-in preset list. |
| POST | `/api/overlay/assets` | Multipart image upload (validated, size-capped) → `{ url }`. |
| GET | `/api/overlay/lan-info` | Cross-machine OBS info: `{ enabled, bindAddress, overlayPort, accessKey?, suggestedHosts }`. |
| DELETE | `/api/overlay/chat/messages` | Clear chat history; broadcasts `cleared` on the hub. |
| DELETE | `/api/overlay/alerts/messages` | Same for alerts. |
| DELETE | `/api/overlay/member/messages` | Same for member. |
| GET | `/overlay/{chat\|member\|alerts}` | OBS browser-source entry; redirects to the configured preset page. |

## SignalR hubs

All hubs push messages named `event`; history-clearing broadcasts `cleared` with `{ hubName }`.

| Hub | Description |
|---|---|
| `/hubs/events` | General events (`platform.connection_changed`, config-changed forwarding). |
| `/hubs/overlay/chat` | Chat overlay; replays recent history (default 30) on connect. |
| `/hubs/overlay/alerts` | Alerts; replayed items carry `replayed: true`. |
| `/hubs/overlay/member` | Check-in / member card. |
| `/hubs/overlay/effects` | Effect triggers (no history). |
| `/hubs/overlay/widgets` | Custom widgets; replays history on connect. |
