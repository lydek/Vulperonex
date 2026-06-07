# Phase 5.5 To-Do — Rapid-Test Enablement

> Plan: `docs/phases/phase-5_5-rapid-test/plan.md`
> Parent checklist: `tasks/todo.md`

---

## Task 17a — CLI rule create / update

- [x] Task 17a-1: add `create <file>` subcommand to `RuleCommand`; read file → POST `/api/rules`; pass through the backend error code.
- [x] Task 17a-2: add `update <id> <file>` subcommand to `RuleCommand`; read file → PUT `/api/rules/{id}`; handle 204 No Content.
- [x] Task 17a-3: finalize the CLI-side error code: file missing → `FILE_NOT_FOUND` (or reuse an existing code, decided when the slice lands).
- [x] Task 17a-4: integration test covers method/path/body pass-through and backend 4xx error-code pass-through.
- [x] Task 17a-5: extend the `cli-e2e-verification.md` table with PASS conditions for the create/update commands.

## Task 17b — chat.html overlay migration

- [x] Task 17b-1: copy/rewrite `chat.html` to `src/Hosts/Vulperonex.Web/wwwroot/overlay/chat.html`, removing the Omni-Commander-specific cache-bust and emote-URL assumption.
- [x] Task 17b-2: migrate `chat.css` / `chat.js` / `overlay-common.js` to `wwwroot/overlay/css|js/`.
- [x] Task 17b-3: align the SignalR client version with the backend (`@microsoft/signalr@10.x`); no bundling pipeline introduced.
- [x] Task 17b-4: architecture test `Vulperonex.Tests.Architecture/Overlay/ChatHtmlPayloadKeysTest.cs`, asserting the payload fields referenced by `chat.js` ⊆ the `OverlayChatPayload` public property set.
- [x] Task 17b-5: manual verification: open `/overlay/chat` as an OBS browser source, run `simulate chat`, observe segment rendering. Record in `manual-verification.md`.

## Task 17c — E2E fixture chat → workflow → overlay

- [x] Task 17c-1: write a minimal chat→SendChatMessage rule in `docs/phases/phase-5_5-rapid-test/examples/rule-chat-echo.json`.
- [x] Task 17c-2: `tests/Vulperonex.Tests.Integration/RapidTest/ChatReplyChainTests.cs`: seed the rule via the shared Web fixture → `POST /api/simulate/chat` → SignalR client receives the payload within 5 seconds.
- [x] Task 17c-3: the test asserts `SendChatMessageActionExecutor` was triggered (counter or Test Double).
- [x] Task 17c-4: do not hit the Twitch IRC egress; only verify the domain → SignalR chain is complete.

## Task 17e — CLI ID resolution + missing-arg UX + destructive-operation confirmation

> Design frozen: `docs/phases/phase-5_5-rapid-test/cli-id-resolution-decision.md`

- [x] Task 17e-1: add error codes `MISSING_ARGS` / `AMBIGUOUS_ID` / `NOT_FOUND` / `CONFIRMATION_REQUIRED` / `CANCELLED`; add missing-args / confirm string keys to the i18n catalog.
- [x] Task 17e-2: `RuleIdentifierResolver` / `MemberIdentifierResolver` support full id / prefix; the `rule` group additionally supports `--name`. Multiple hits print a candidate table (≤10) via `AMBIGUOUS_ID`; zero hits go to `NOT_FOUND`.
- [x] Task 17e-3: add `CliExecutionContext.ConfirmAsync(messageKey, summaryLines, hasYesFlag)`: interactive mode reads `[y/N]` from `Input`, non-interactive requires `--yes`, otherwise writes `CONFIRMATION_REQUIRED` + summary + hint.
- [x] Task 17e-4: `RuleCommand` `disable` / `enable` / `delete` / `show` / `update` and `MemberCommand` `show` / `delete` switch to the resolver; missing arg prints usage + hint, exit `MISSING_ARGS`.
- [x] Task 17e-5: destructive subcommands (`rule disable` / `rule delete` / `member delete`) all go through `ConfirmAsync`, accepting `--yes` / `-y`.
- [x] Task 17e-6: integration tests cover missing arg / full id / unique prefix / multiple prefix / zero prefix / name exact / `--name` + positional mutually exclusive / `--yes` / non-interactive without `--yes` / interactive y / interactive n.
- [x] Task 17e-7: extend the `cli-e2e-verification.md` table with PASS conditions for the prefix / `--name` / `--yes` / `[y/N]` paths.
- [x] Task 17e-8: SPEC §4.12 / §5 / §10 (D6a) add the `--name` / `--yes` flags and the new error codes, referencing this document.

## Task 17d — Cookbook doc

- [x] Task 17d-1: `cookbook-chat-reply.md` step-by-step chapters (start host → CLI rule create → simulate chat → browser observation → cleanup).
- [x] Task 17d-2: each chapter contains an "observation point / pass condition" table.
- [x] Task 17d-3: run end to end by a non-author (including an AI agent) and backfill observed values; at least one recorded PASS.

## Checkpoint 5.5

- [x] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings.
- [x] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` passes, including the new 17c fixture.
- [x] CLI `rule create` / `rule update` integration tests are green on both the success and 4xx pass-through paths.
- [x] All Task 17e test paths green (missing arg / prefix / `--name` / `--yes` / interactive prompt).
- [x] `chat.html` architecture test passes.
- [x] Cookbook has at least one recorded external PASS.
- [x] `tasks/todo.md` updated in sync.
- [x] Aligned with the Phase 6 spec: check-in/loyalty actions are out of 5.5 scope, and the Phase 6 plan explicitly inherits the 5.5 fixture pattern.

## Phase 5.5 Dependencies

- [x] Phase 5 Task 16f L46 / L60 manual verification complete (the 5.5 cookbook references the Phase 5 §1 start-host flow, which must first be confirmed reproducible locally).

## Phase 6 Unlock Conditions

- [x] All 5.5 checkpoints green.
- [x] A cookbook PASS record exists.
- [x] The Phase 6 plan explicitly continues the 5.5 fixture / overlay asset directory structure.
