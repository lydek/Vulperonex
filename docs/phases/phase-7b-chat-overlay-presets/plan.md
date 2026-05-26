# Phase 7B Implementation Plan: Chat Output Observability and Overlay Template Presets

> Parent Plan: `tasks/plan.md`
> Parent Todo: `tasks/todo.md`
> Reference Sources: `docs/SPEC.md`, `ref/Omni-Commander/OmniCommander.WebApi/wwwroot/chat.html`
> Prerequisites: Phase 7 runtime/schema parity is complete; Phase 7A editor UX improvements do not block backend observability and overlay preset contract for this slice.
> Goal: Complete the workflow `SendChatMessage` observability gap under simulation/local mode, and promote `/overlay/chat` from a single implementation to a switchable template system.
> Boundaries: OneComme compatibility belongs to the extension/plugin slice and will not be directly integrated into the core runtime.
> Progress Source: Checkboxes in this document are for design/verification draft only; actual completion status is tracked in `docs/phases/phase-7b-chat-overlay-presets/todo.md` and `tasks/todo.md`.

---

## Goals

Currently, Phase 7 has completed workflow parity, but two obvious gaps remain:

1. `SendChatMessage` under the `Simulation` platform lacks a clear observable surface, making it impossible for users to verify directly if messages are successfully sent.
2. `/overlay/chat` is still a single implementation, lacking a preset/template system, which hinders migration and template expansion.

Phase 7B only addresses these two lines of work:

- **Observability Line**: Make workflow chat output queryable, viewable, and verifiable in simulation/local mode.
- **Template Line**: Establish chat overlay preset contracts, support multi-template switching, and reserve hooks for OneComme compatibility extensions.

---

## Scope

### In-Scope

- Simulation/local chat output observable surface
- At least one clear visual or queryable verification surface for Chat Outbox / history / memory receiver
- `/overlay/chat` preset/template contract
- At least two built-in switchable templates
- Configuration-level template switching
- OneComme-compatible extension/import contract

### Out-of-Scope

- Directly embedding the OneComme runtime or full UI into the core
- Emitting any raw HTML / `v-html` payload directly
- Adding new workflow schemas / new executor types
- Graph/canvas workflow editor

---

## Task Breakdown

## Task 41 - Simulation Chat Output Observable Surface

**Description:** Provide an observable surface for the chat sender on the `Simulation` platform. `SendChatMessage` must not be a silent no-op; it should at least display the rendered message, platform, channel, dedupKey, and status in the admin UI, overlay history, or memory receiver.

**Acceptance Criteria:**
- [ ] After executing `SendChatMessage` on the `Simulation` platform, users can query the message results through a visual interface or a clear API.
- [ ] Results include at least `message`, `platform`, `channel`, `dedupKey`, `status`, and timestamp.
- [ ] Workflow chat output verification no longer depends on whether `/overlay/chat` has a bridge.
- [ ] `sent` / `skipped` / `failed` states are distinguishable.

**Implementation Hints:**
- Optional implementation surfaces: admin `Chat Outbox` view, overlay history, or simulation memory receiver.
- Prioritize a query surface that answers "where did the message go" rather than just writing logs.

## Task 42 - Chat Overlay Preset System

**Description:** Upgrade `/overlay/chat` to a preset/template-driven overlay. Provide multiple built-in templates and reserve future import/export capabilities; the core only defines the preset/package contract and does not directly couple with the OneComme runtime.

**Acceptance Criteria:**
- [ ] At least two switchable chat templates are provided: Vulperonex default template + another built-in or installable template.
- [ ] Template switching is accomplished via configuration or admin UI, without modifying frontend source code.
- [ ] Template rendering still complies with DTO whitelists and text binding, without introducing raw `v-html` payload output.
- [ ] The same overlay payload contract can be reused by different presets.

**Implementation Hints:**
- Decouple the `preset metadata + renderer contract` first, before binding the template system to the signal source.
- Templates can refer to the visual structure of Omni chat overlay, but data binding must maintain existing safety boundaries.

## Task 43 - OneComme Compatibility Path

**Description:** Prioritize OneComme as one of the compatibility targets, but integrate it as an extension/plugin. Define the minimum contract for template importers, directory scanners, or adapter packages to reduce migration costs for existing users while maintaining core boundaries.

**Acceptance Criteria:**
- [ ] The document lists the OneComme compatibility strategy: which capabilities are directly compatible via plugins, which are mapped, and which are temporarily unsupported.
- [ ] At least one extension/import path is clearly marked as OneComme-compatible / migration-oriented; core integration is not required.
- [ ] Manually verify and record the identification and import flow of the OneComme template directory structure or package metadata.

**Implementation Hints:**
- The compatibility unit is the "template directory structure / preset package", not the OneComme app itself.
- If implementing an importer, separate the filesystem scanning from the preset contract.

---

## Checkpoint: Phase 7B

- [ ] All sub-tasks for Tasks 41-43 are completed with `[x]` self-check.
- [ ] Workflow `SendChatMessage` results can be directly observed in `Simulation` mode, without guessing if they were sent.
- [ ] `/overlay/chat` can switch between at least two templates, and the core preset contract can accept external/installable templates.
- [ ] `docs/phases/phase-7b-chat-overlay-presets/manual-verification.md` records the observability + preset + extension compatibility PASS/FAIL.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Observability surface only displays logs without user semantics | High | Prioritize message/platform/channel/status query surface |
| Preset system couples too early with OneComme format | Medium | Implement core preset contract first, then importer/extension |
| Overlay templates bypass security boundaries for design freedom | High | Maintain strict DTO whitelist + text binding, do not introduce raw HTML payloads |
| Simulation sender behavior differs too much from real sender | Medium | Keep the same outbox/envelope model, only swap the final transmission surface |

---

## Out-of-Scope

- Direct embedding or hard runtime dependency on OneComme app
- Alerts/member template marketplace outside chat overlay
- Any third-party template script execution
- Workflow editor UX refinements (remains in Phase 7A)
