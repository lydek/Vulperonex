# Phase 5.5 Plan — Rapid-Test Enablement

> Parent plan: `tasks/plan.md`
> Parent checklist: `tasks/todo.md`
> Scope: close the "configuration capability" gap between Phase 5 and Phase 6
> Status: Draft; awaiting review

---

## Motivation

Phase 5 completed the read/dispatch path for Web API + SignalR + CLI, but the **write path** was backend-only:
- The CLI `rule` subcommand only had `list/show/enable/disable/delete` — no `create` / `update`.
- `POST /api/rules` / `PUT /api/rules/{id}` were already implemented but had no CLI entry point → you had to hit curl directly to install a rule.
- The `chat.html` overlay had not been migrated from Omni-Commander into `src/Hosts/Vulperonex.Web/wwwroot/`; the `/overlay/chat` SignalR group already pushed events, but no page consumed them.
- There was no reproducible "from zero to seeing events flow" cookbook, leaving new contributors (including AI agents) with no entry point to core loyalty/check-in features.

Phase 6 was about to expand into the full simulate→workflow→overlay chain plus check-in SystemEvents, but if even "install a chat-reply rule for simulate to trigger" still required manual curl, the setup noise would eat Phase 6's spec review, E2E verification, and regression-test budget.

Phase 5.5 goal: **before Phase 6 starts, the CLI and the existing overlay can already drive a single complete chat→workflow→overlay chain with no external tool dependency**.

---

## Scope

### In Scope

- CLI adds `rule create <file.json>` and `rule update <id> <file.json>`, calling the existing `POST /api/rules` / `PUT /api/rules/{id}`.
- A "dump" output behaviorally equivalent to a hypothetical `rule show --json-only` (to provide a round-trip editing template for `rule update`) — reuse the existing `rule show`; do **not** add a flag. If one becomes necessary, defer it to Phase 6.
- Migrate `ref/Omni-Commander/OmniCommander.WebApi/wwwroot/chat.html` plus shared assets (`chat.css` / `chat.js`) to `src/Hosts/Vulperonex.Web/wwwroot/overlay/chat.html`, adapted to the Phase 5 `OverlayChatPayload` wire key set (`schemaVersion`, `eventId`, `timestamp`, `displayName`, `colorHex`, `segments`, `badges`). The SignalR client uses the `@microsoft/signalr` CDN or a bundled `wwwroot/libs/` (following the Omni-Commander convention; no bundling pipeline introduced).
- After migration, add an `OverlayChatPayload` wire-key-set **architecture test**: every payload field referenced by `chat.html` JS must be in the `OverlayChatPayload` allowlist; catches frontend/backend drift early.
- Add an integration fixture: emit `user.message` via `SimulationAdapter`, first install a `SendChatMessage` workflow rule, then assert:
  - WorkflowEngine trigger count
  - the rendered template received by the SendChatMessage executor
  - the SignalR overlay/chat client receives the payload, including the reply (if the reply also rides a `user.message` domain event) or not (if the reply is a pure Twitch IRC egress)
- Add a cookbook doc `docs/phases/phase-5_5-rapid-test/cookbook-chat-reply.md` covering step by step: start Web host → CLI `rule create` to install a sample rule → CLI `simulate chat` → open browser `/overlay/chat` → observe the event.
- Add Phase 5.5 entries to `tasks/plan.md` / `tasks/todo.md`, clearly marking the boundary with Phase 6.

### Out of Scope (Phase 6 or later)

- **New workflow actions (including AddLoyaltyAction, IncrementCheckInAction)**: these belong to the check-in/loyalty SystemEvent design, which the SPEC marks post-MVP ([`docs/SPEC.md`](../../SPEC.md) §4.8); design the SystemEvent + DTO + plugin boundary in Phase 6 first, then implement.
- **CLI `member loyalty add/set`**: do not open backend PUT paths before the above actions are defined; the CLI does not run ahead of the spec.
- **`member-card.html` migration**: depends on the not-yet-defined `/overlay/member` full DTO and SystemEvent; belongs to Phase 6.
- **CLI rule schema JSON Schema generation**: Phase 5.5 samples are hand-written and gated by the backend validator; auto schema generation is deferred to the OpenAPI/CLI-completion investment.
- **`--json` output flag, structured stderr**: already deferred in Phase 5; stays deferred.
- **Tab-completion fix**: already spawned as an independent task; does not block 5.5.
- **REPL multi-line input / heredoc** (`rule create` from stdin pipe JSON is available, but no inline interactive editor).

---

## Shared Contracts

### CLI `rule create` / `rule update` interface

```
rule create <path-to-json>
rule update <id> <path-to-json>
```

- `<path-to-json>` is a local file path; the CLI reads the file → no schema validation → POSTs/PUTs directly as `application/json`.
- Passes through the backend error code (existing `WriteResponseAsync` logic).
- Successful POST: prints the backend-returned `{ id, ... }` (existing endpoint behavior), exit 0.
- Successful PUT: backend returns 204 → CLI prints nothing, exit 0.

**No stdin pipe** (Phase 5.5 scope reduction): if `<path-to-json>` is `-`, treat it as `UNKNOWN_COMMAND`, avoiding interaction between redirected stdin and the REPL line editor.

### Sample rule JSON

Attached at `docs/phases/phase-5_5-rapid-test/examples/rule-chat-echo.json` — a minimal rule with one chat trigger + a SendChatMessage action, used as a shared fixture for the cookbook and the integration test. From Phase 6 on, more samples will be added; 5.5 ships only the echo one.

### Overlay asset directory

- Stored uniformly under `src/Hosts/Vulperonex.Web/wwwroot/overlay/`.
- `chat.html` references **no** relative paths from `ref/Omni-Commander/`; assets (CSS / JS / SignalR client) are co-located or served via CDN.
- The SignalR client version aligns with the backend SignalR server NuGet major version (SPEC §2 backend table "Real-time Communication = SignalR (10.0)" → use `@microsoft/signalr@10.x`).

### Error codes

5.5 adds no error codes; the CLI only passes through existing backend codes.

---

## Dependency Graph

```text
Task 17a CLI rule create / update
    -> Task 17c E2E fixture
        -> Task 17d cookbook finalized
            -> Checkpoint 5.5

Task 17b chat.html migration + architecture test
    -> Task 17c (shared fixture verifies overlay payload)

Task 17e CLI ID resolution + missing-arg UX + confirmation flow
    -> Task 17d cookbook (cookbook chapter demonstrates --yes and prefix resolution)
```

Tasks 17a / 17b / 17e can run in parallel; 17c must run after 17a + 17b pass; 17d must be written after 17a + 17c + 17e are all green.

---

## Task Slices

### Task 17a — CLI rule create / update

- Add `create` and `update` subcommands to `src/Hosts/Vulperonex.Cli/Commands/RuleCommand.cs`.
- `create`: read file → `client.PostAsync("/api/rules", new StringContent(json, Encoding.UTF8, "application/json"))` → `WriteResponseAsync`.
- `update`: read file → `client.PutAsync($"/api/rules/{id}", ...)`. Handle 204 No Content (existing `WriteResponseAsync` already supports an empty body).
- File missing or not a valid path → `INVALID_ACTION_CONFIG` (CLI-side code) or a new `FILE_NOT_FOUND` — decided during the implementation slice; the doc reserves it for now.
- Integration test: StubHandler verifies method, path, body pass-through.
- Update the `cli-e2e-verification.md` table to add PASS conditions for the create/update commands.

### Task 17b — chat.html migration

- Copy/rewrite `chat.html` to `src/Hosts/Vulperonex.Web/wwwroot/overlay/chat.html`.
- Migrate CSS / JS in sync to `wwwroot/overlay/css/chat.css`, `wwwroot/overlay/js/chat.js`, `wwwroot/overlay/js/overlay-common.js` (keep the Omni-Commander names for cross-reference, but point the paths at the Vulperonex `wwwroot`).
- JS connects to `/overlayHub` (Phase 5 already mounts the SignalR hub; use the actual hub path), subscribes to the chat group and renders.
- Remove the Omni-Commander-specific `?v=1.0.1` cache-bust string and the Twitch-specific emote-URL assumption — render directly from `imageUrl` inside `OverlayChatPayload.segments`.
- **Architecture test**: add `tests/Vulperonex.Tests.Architecture/Overlay/ChatHtmlPayloadKeysTest.cs` that parses `chat.js` to extract the list of `payload.xxx` references and asserts every field is in the `OverlayChatPayload` public property set. In practice grab `\bpayload\.([a-zA-Z_]+)` via regex, avoiding a JS parser.
- Manual verification: OBS browser source → `http://localhost:5001/overlay/chat` (port taken from the Phase 5 OverlayPort allocation).

### Task 17c — E2E fixture chat → workflow → overlay

- Add `RapidTest/ChatReplyChainTests.cs` to `tests/Vulperonex.Tests.Integration/`:
  - Start a test Web host (shared Phase 5 fixture), seed the `examples/rule-chat-echo.json` rule.
  - Send a message via `POST /api/simulate/chat`.
  - The SignalR overlay/chat client receives the payload within 5 seconds, fields matching.
  - Assert `SendChatMessageActionExecutor` was called (via a Test Double or counter).
- This test does **not** assert the IRC egress (no Twitch live connection); it only verifies the domain → SignalR chain is complete.
- Tag the fixture `Phase5_5_ChatReplyChain`; future Phase 6 SystemEvent chains will extend a similar pattern.

### Task 17e — CLI ID resolution + missing-arg UX + destructive-operation confirmation

> Design frozen in [`cli-id-resolution-decision.md`](./cli-id-resolution-decision.md).

- `RuleCommand` / `MemberCommand` `disable` / `enable` / `delete` / `show` subcommands:
  - Missing positional arg → print usage + hint to stderr, exit code `MISSING_ARGS`.
  - Positional accepts "full id | id prefix"; the `rule` group additionally supports `--name <n>` as an alternative input; the two are mutually exclusive (both supplied → `INVALID_ARGS`).
  - prefix / name multiple hits → `AMBIGUOUS_ID` + a candidate table on stderr (up to 10, with a truncation hint beyond that).
  - Zero hits → `NOT_FOUND`.
- Destructive operations (`rule disable` / `rule delete` / `member delete`) all go through `CliExecutionContext.ConfirmAsync(summary)`:
  - Interactive REPL (`!Console.IsInputRedirected && !Console.IsOutputRedirected`): print an "about to X" summary + `[y/N]` prompt; `y` executes, anything else → `CANCELLED`.
  - one-shot / piped: must carry `--yes`, otherwise `CONFIRMATION_REQUIRED` + print the summary.
- New error codes: `MISSING_ARGS` / `AMBIGUOUS_ID` / `NOT_FOUND` / `CONFIRMATION_REQUIRED` / `CANCELLED`.
- i18n catalog (`en-US.json` / `zh-TW.json`) gets missing-args usage / hint and confirm prompt / summary strings.
- Integration tests cover all paths in the decision doc's "test thresholds".
- Shares `ConfirmAsync` with the 17a CLI `rule create` / `update` (those are non-destructive, but `update` can opt into confirmation — listed as a stretch).

### Task 17d — Cookbook doc

- `docs/phases/phase-5_5-rapid-test/cookbook-chat-reply.md`:
  - Chapter 1: start the Web host (reuse Phase 5 §1).
  - Chapter 2: CLI `rule create examples/rule-chat-echo.json` → expect stdout JSON + exit 0.
  - Chapter 3: CLI `simulate chat "hello"`.
  - Chapter 4: open browser `http://localhost:<overlay_port>/overlay/chat` → expect to see a chat segment.
  - Chapter 5: CLI `rule delete <id>` cleanup.
- Each chapter contains a table listing observation points and pass conditions.

---

## Checkpoint 5.5

- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes, including the new fixture.
- [ ] CLI `rule create` / `rule update` integration tests cover both the success path and the backend-error-code pass-through path.
- [ ] `chat.html` architecture test passes: the fields referenced by JS ⊆ the `OverlayChatPayload` public properties.
- [ ] The cookbook has been run end to end by a non-author (including an AI agent), each step's observed values backfilled, with at least one recorded PASS.
- [ ] `tasks/todo.md` and `docs/phases/phase-5_5-rapid-test/todo.md` are updated in sync.

---

## Size

S+ (CLI subcommand expansion within a single host + one static overlay page + one E2E fixture + one cookbook; no change to Domain / Application interfaces).

---

## Open Items

- Whether the sample rule JSON should live under `tools/` rather than `docs/phases/`: if Phase 6 introduces a `tools/seed/`-style seed folder, the 5.5 sample can move in with it; no such structure exists yet, so keep it under `docs/phases/` for now.
- Whether `rule update` should echo `meta` on a PUT failure (validation error): the existing Phase 5 `WriteResponseAsync` only prints the `error` code; if the cookbook needs to show "why the update failed", the stderr format must be extended — listed as an open item, **not** implemented in 5.5.
- Whether to add a dark/light theme toggle during the chat.html migration: Phase 6 OBS use cases come first; 5.5 does no theming and keeps the Omni-Commander default look.

---

## Unlocked After Completion

- The Phase 6 check-in SystemEvent / `/overlay/member` DTO design can apply the Phase 5.5 fixture pattern directly.
- The first onboarding path for a new contributor is "run the cookbook once", without needing the full Phase 1-5 context.
- The CLI is promoted from a "read-only tool" to a "rule-building configuration panel", providing the write entry point for spec-driven development from Phase 6 on.
