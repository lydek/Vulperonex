# Phase 5.5 — CLI ID Resolution & Destructive-Operation Confirmation Decision

> Parent plan: `docs/phases/phase-5_5-rapid-test/plan.md`
> Corresponding todo item:
> - Task 17e (new): CLI ID resolution + missing-arg UX + destructive-operation confirmation

## Status

Proposal — pending implementation. This document freezes the human-factors design for "id as the sole key" operations under the CLI `rule` / `member` groups, avoiding repeated discussion during implementation.

## Problem Scope

The CLI rule/member groups completed in Phase 5 take a "full id" as the sole input:

```
rule disable <ruleId>
rule enable  <ruleId>
rule delete  <ruleId>
member show  <memberId>
member delete <memberId>
```

Observed pain points:

1. **Missing-arg experience**: `rule disable` (no id) → stderr only emits `UNKNOWN_COMMAND`, with no usage and no hint; the user doesn't know what to supply.
2. **Inconvenient id retrieval**: every destructive operation requires first running `rule list` / `member list` to copy the id; ids are GUID/hash strings, hard to recognize and remember by hand.
3. **Ambiguous semantics**: `UNKNOWN_COMMAND` is used for both "no such subcommand" and "subcommand exists but missing arg", so the CLI E2E tests and the cookbook cannot tell them apart.

## Design Principles

- **The REST API is the sole write contract** (SPEC §4.12 unchanged). The CLI-side convenience resolution (prefix / name) is done **only at the CLI layer** and does not expand the API interface.
- **Resolution is visible to the user**. Any "prefix hit → resolved to id" / "name → id" must list the selected full record on stderr; the user confirms before a destructive operation runs.
- **Only interactive mode may omit `--yes`**. one-shot CLI / piped stdin must carry `--yes`, preventing accidental triggering from scripts.
- **Name is not unique** (SPEC states `Name` is non-unique), so the CLI cannot assume name → a single id; multiple hits go down the `AMBIGUOUS_ID` path.

## Decisions

### 1. Missing arg → `MISSING_ARGS` + inline usage

Add error code `MISSING_ARGS`, distinct from `UNKNOWN_COMMAND`:

- `UNKNOWN_COMMAND`: the dispatcher can't find a subcommand by that name.
- `MISSING_ARGS`: the subcommand exists but a required positional arg is missing.

Behavior:

```
$ rule disable
MISSING_ARGS
usage: rule disable <id|prefix|--name <name>> [--yes]
hint: run 'rule list' to see available ids
```

Every subcommand that needs an id applies this format. The usage / hint strings go in the i18n catalog (`command.<group>.<verb>.missing-args.usage`, `.hint`).

### 2. ID resolution order

Before sending to the API, the CLI resolves the positional `<identifier>` in this order:

1. **Full id hit** (exact `Id` match): use it, no list lookup.
2. **Unique id-prefix hit**: GET `/api/rules` or `/api/members` → client-side filter `Id.StartsWith(input, Ordinal)` → a unique hit is used, multiple hits go to `AMBIGUOUS_ID`.
3. **`--name <n>` mode** (`rule` group only): GET `/api/rules` → `Name.Equals(n, OrdinalIgnoreCase)`; multiple hits go to `AMBIGUOUS_ID`, zero hits go to `NOT_FOUND`.

`--name` and the positional id are mutually exclusive; both supplied → `INVALID_ARGS`.

**The `member` group does not support `--name`** — `MemberReadModel` has no `DisplayName` field (only `MemberId` and `Identities[].PlatformUserId`). `member show` / `member delete` accept only "full MemberId | id prefix". If Phase 6 needs to look up a member by `PlatformUserId`, evaluate a separate `--platform-user-id` flag then.

**Why no fuzzy contains**: the user expects `--name foo` to mean "I know the full name", not a search; contains easily triggers destructive operations by mistake. If Phase 6 needs search, add a separate `rule search <keyword>` subcommand.

### 3. `AMBIGUOUS_ID` candidate listing

Format:

```
AMBIGUOUS_ID
candidates:
  abc12345  echo-rule        enabled
  abc99999  echo-rule-v2     disabled
hint: use a longer prefix or the full id
```

- The candidate table is written to stderr (same stream as the error code).
- List at most the first 10 + a truncation hint `(... N more)`, avoiding dumping the whole table when the prefix is too short.
- Column order: `Id (first 8 chars) | Name | Status` (rule) or `Id (first 8 chars) | PlatformUserId | DisplayName` (member).

`NOT_FOUND` lists no candidates, returning only the code.

### 4. Destructive-operation confirmation

Subcommands requiring confirmation:

| Subcommand | Why confirmation |
|--------|-----------|
| `rule delete` | Irreversible, deletes a row |
| `rule disable` | Changes IsEnabled, affects the execution configuration |
| `member delete` | Irreversible, deletes a row |

**No** confirmation needed:

- `rule enable`: reversible (just disable again), no data loss
- `rule show` / `member show`: read-only
- `member seed`: incremental, no overwrite

Confirmation flow:

```
about to disable:
  id:     abc12345-1111-2222-3333-444455556666
  name:   echo-rule
  status: enabled
confirm? [y/N]:
```

- **REPL interactive mode** (`Console.IsInputRedirected == false` and `Console.IsOutputRedirected == false`): print the confirmation prompt, read a single line; `y` / `yes` (case-insensitive) executes, anything else → `CANCELLED` + exit 1.
- **one-shot / piped**: if `--yes` is supplied, execute directly; otherwise `CONFIRMATION_REQUIRED` + print the "about to X" summary + exit 1.
- `--yes` is also accepted in interactive mode (skips the prompt).

### 5. New error codes

| Code | Scenario |
|------|------|
| `MISSING_ARGS` | A required positional arg is missing |
| `AMBIGUOUS_ID` | prefix / name hits multiple |
| `NOT_FOUND` | id / prefix / name hits zero |
| `CONFIRMATION_REQUIRED` | A destructive operation in non-interactive mode without `--yes` |
| `CANCELLED` | The interactive prompt received non-`y` input |

`INVALID_ARGS` (existing): used for mutually exclusive flags, `--name` and positional supplied together, etc.
`UNKNOWN_COMMAND` (retained): used only when the dispatcher can't find a subcommand.

### 6. Races & data consistency

- During resolution GET `/api/rules` → confirm → operate the API. If the id was deleted externally in between → the API returns 404 → the CLI emits `NOT_FOUND` directly, no retry.
- The list result is **not** client-side cached (re-fetched on each resolution), avoiding phantom results from stale cross-command data in the cookbook. Re-evaluate if performance becomes an issue in Phase 6.

## Why Not Other Options

- **Auto-complete the numeric index of the last list** (`rule disable 1`): the REPL hides state across commands; after `rule list`, a third-party write means index 1 is no longer the row originally seen; the risk for destructive operations is too high — **not adopted**.
- **Fuzzy name contains search**: accidental-trigger risk, listed under "not adopted" and to be evaluated as a separate `search` subcommand in Phase 6.
- **Force `--yes` regardless of mode**: degrades the interactive user experience (typing a flag every time), and the REPL already implies "a human is online", so a prompt is more natural than a flag.
- **Omit confirmation, show only a dry-run preview**: violates the "destructive operations must be doubly confirmed" principle; an accidental Enter would execute.

## Impact on the SPEC

Update SPEC §4.13 / §5 examples:

- §4.13 CLI command list adds the `--name` / `--yes` flag descriptions and references this document.
- §5 (CLI examples) `rule disable <ruleId>` etc. expand to `rule disable <ruleId|--name <name>> [--yes]`.
- The error-code table (if the SPEC has an aggregation section) adds `MISSING_ARGS` / `AMBIGUOUS_ID` / `NOT_FOUND` / `CONFIRMATION_REQUIRED` / `CANCELLED`.

`docs/phases/phase-5_5-rapid-test/plan.md` task slices add 17e; `todo.md` in sync.

## Test Thresholds

- Integration tests (StubHandler):
  - missing arg → `MISSING_ARGS` + stderr contains the usage / hint strings
  - full id → correct API path
  - unique prefix → resolved to full id, then sent
  - multiple prefix → `AMBIGUOUS_ID` + stderr lists candidates (≤10)
  - zero prefix → `NOT_FOUND`
  - `--name` exact / multiple / zero — one path each
  - `--name` + positional id → `INVALID_ARGS`
- Destructive confirmation:
  - one-shot without `--yes` → `CONFIRMATION_REQUIRED` + stderr summary
  - one-shot `--yes` → correct API path
  - REPL interactive: simulate stdin input `y` → executes; `n` → `CANCELLED`
  - REPL interactive `--yes` → skips the prompt
- `member` group mirror tests (except where SPEC paths differ)

## Review Notes (to backfill)

- Reviewer:
- Date:
- Decision:
- Follow-up:
