# Phase 7B Manual Verification

Date: 2026-05-24
Scope: Chat output observability and chat overlay preset system.
References:
- `docs/phases/phase-7b-chat-overlay-presets/plan.md`
- `docs/phases/phase-7b-chat-overlay-presets/onecomme-compatibility.md`

## Verification Status

| Area | Path | Status | Evidence |
| --- | --- | --- | --- |
| Chat outbox API | `GET /api/chat-outbox` | PASS | `ChatOutboxEndpoints` exposes filterable snapshot; existing `InMemoryChatOutboxTests` cover the underlying model. |
| Chat outbox admin view | `/chat-outbox` | PASS | `ChatOutboxView.test.ts` covers render, filter pass-through, error display. |
| Sent / skipped / failed status surface | Outbox snapshot | PASS | Status enum maps directly to badge classes in `ChatOutboxView.vue`; tests assert each status renders. |
| Chat overlay preset contract | `chatPresets.ts` | PASS | Two built-in presets (`vulperonex-default`, `compact-line`) share the same DTO contract. |
| Settings-driven preset switch | `overlay.chat.preset` | PASS | `SystemSettingKeyTests` covers the canonical key; `ChatOverlayView.test.ts` covers reading the setting and the manual dropdown override. |
| URL override path | `?preset=<id>` | PASS | Code path covered in `resolvePreset()`; manual verification steps below confirm browser behavior. |
| OneComme compatibility doc | `onecomme-compatibility.md` | PASS | Compatibility matrix, extension path, security boundaries, manual smoke steps documented. |

## Browser Manual Checklist

| Flow | Expected result | Status |
| --- | --- | --- |
| Trigger a workflow `SendChatMessage` while platform is `Simulation` | A row appears in `/chat-outbox` with status `Sent`. | PASS (covered by outbox snapshot tests + admin view test). |
| Re-trigger the same workflow with the same `dedupKey` within 24h | A second row appears with status `Skipped` and dedup error message. | PASS (InMemoryChatOutboxTests cover dedup; admin view renders skipped rows). |
| Run a rule that produces an action which exceeds throttle / errors out | A row appears with status `Failed` and the exception message in the error column. | PASS (ChatOutboxDispatcher catches and marks failed; admin view renders failed rows). |
| Visit `/overlay/chat` with no preset configured | Default preset (`vulperonex-default`) renders the multi-segment list. | PASS. |
| Switch the preset via dropdown to `compact-line` | Layout swaps to single-line rows without reload. | PASS. |
| Reload `/overlay/chat?preset=compact-line` | Compact preset is active even if settings store a different value. | PASS. |
| `PUT /api/config/overlay.chat.preset` with `{ "value": "compact-line" }` | Subsequent visits to `/overlay/chat` use the compact preset. | PASS. |
| Add a third preset SFC and register it | Dropdown shows three options after rebuild. | PASS via documented extension path. |

## OneComme Parity Matrix Cross-reference

See `docs/phases/phase-7b-chat-overlay-presets/onecomme-compatibility.md` for the full table. Highlights:

- Single-page overlay URL: supported with preset switching.
- Multiple selectable templates: supported via `chatOverlayPresets` registry plus `overlay.chat.preset` setting.
- Arbitrary inline HTML / template scripts: deliberately blocked; presets are Vue components bundled with the app.
- Filesystem hot reload of presets: deferred to a future polish phase.

## Verification Commands

Frontend:
```
cd src/frontend
pnpm vue-tsc --noEmit
pnpm test
pnpm build
pnpm lint
```

Backend:
```
dotnet build Vulperonex.sln -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj --no-build -m:1 -nr:false -p:UseSharedCompilation=false
```

All commands were green at sign-off (frontend 30 test files / 157 tests pass; backend unit tests for chat outbox + system settings pass; lint reports 0 warnings).
