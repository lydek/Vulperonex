# Phase 7B Todo: Chat Output Observability and Overlay Template Presets

> Corresponding Plan: `docs/phases/phase-7b-chat-overlay-presets/plan.md`
> Parent Todo: `tasks/todo.md`

## Task 41 - Simulation Chat Output Observable Surface

- [x] Task 41a: Confirm the final observable surface for `SendChatMessage` under the `Simulation` platform (admin view / history / memory receiver).
- [x] Task 41b: Write rendered message, platform, channel, dedupKey, status, and timestamp to a queryable model.
- [x] Task 41c: Provide a minimal UI or API view to directly answer "whether the message was sent".
- [x] Task 41d: Add unit/integration/manual verification covering `sent` / `skipped` / `failed`.

## Task 42 - Chat Overlay Preset System

- [x] Task 42a: Define chat overlay preset metadata / renderer contract.
- [x] Task 42b: Implement at least two presets: Vulperonex default + a second built-in or installable template.
- [x] Task 42c: Provide configuration-level switching endpoints without modifying frontend source code.
- [x] Task 42d: Verify that all presets still use only the DTO whitelist + text binding.

## Task 43 - OneComme Compatibility Path

- [x] Task 43a: Document the OneComme compatibility strategy.
- [x] Task 43b: Define extension/import path mapping with the template directory structure or package metadata.
- [x] Task 43c: Add manual verification workflow to record identification, importing, and switching results.

## Checkpoint: Phase 7B

- [x] All sub-tasks for Tasks 41-43 are completed with `[x]` self-check.
- [x] Workflow `SendChatMessage` results can be directly observed in `Simulation` mode, without guessing if they were sent.
- [x] `/overlay/chat` can switch between at least two templates, and the core preset contract can accept external/installable templates.
- [x] `docs/phases/phase-7b-chat-overlay-presets/manual-verification.md` records the observability + preset + extension compatibility PASS/FAIL.
