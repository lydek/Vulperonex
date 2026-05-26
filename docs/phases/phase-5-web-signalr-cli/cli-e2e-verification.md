# Phase 5 CLI E2E / Twitch OAuth Verification

This gate will block Phase 6 UI development work until the CLI serves as a reliable manual verification tool.

> For interactive REPL workflows, see [`supplemental-cli-repl.md`](supplemental-cli-repl.md) (Task 16g). This document focuses on reproducible verification of one-shot commands.

> Fast entry point during development: `powershell -ExecutionPolicy Bypass -File .\tools\cli.ps1`
>
> - Without arguments: automatically detects currently active `Vulperonex.Web` loopback port, entering the REPL directly
> - With arguments: e.g. `powershell -ExecutionPolicy Bypass -File .\tools\cli.ps1 rule list`
> - Published CLI: append `-Published` to bypass build friction of `dotnet run` every time

## Environment Setup

- Windows 11 + PowerShell 7 (or 5.1).
- .NET SDK installed; `dotnet restore` runs successfully in the project root.
- `rtk proxy powershell -NoProfile -Command "..."` is the author's local token proxy wrapper; non-author environments can execute the `dotnet ...` string inside the `Command` directly with equivalent behavior.
- If you need to set `$env:...` inside `rtk proxy powershell -Command`, the outer PowerShell must wrap the `-Command` content in single quotes, or set environment variables in the current shell beforehand. Do not use `-Command "$env:VULPERONEX_API_URL='...'; ..."` as the outer PowerShell will expand `$env:VULPERONEX_API_URL` first, leading to errors like `http://127.0.0.1:5000=...`.
- If the CLI displays `NotFound`, check if you are targeting the wrong port first. If `Vulperonex.Web` cannot bind to 5000, it binds to 5002/5004/...; `tools/cli.ps1` automatically probes `/health` to prevent this.
- The Twitch Developer Console "OAuth Redirect URLs" must have **all three** registered (CLI `SelectCallbackPort` attempts 7979 → 7980 → 7981 sequentially, choosing the first available; callbacks will fail if any are missing):
  - `http://localhost:7979/auth/callback`
  - `http://localhost:7980/auth/callback`
  - `http://localhost:7981/auth/callback`

### PowerShell UTF-8 Output Note

If localized CLI help or Chinese documentation appears garbled in PowerShell, configure UTF-8 output before diagnosing the i18n JSON files:

```powershell
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001
```

## Mandatory Automated Test Coverage

- Launch `Vulperonex.Web` using a fresh SQLite database without manual EF migrations.
- `GET /api/rules` must return `200 OK`, not `` `SQLite Error 1: no such table: WorkflowRules` ``.
- Execute CLI commands against a live loopback API:
  - `rule list`
  - `config get log.min_level`
  - `member list`
  - `member seed <platform-user-id> [display-name]`
  - `member delete <member-id>`
  - `simulate chat <message>`
  - `simulate follow`
  - `simulate sub`
  - `simulate` (partial help, does not call API)
  - `rule create <rule.json>` / `rule update <rule-id> <rule.json>` / `rule delete <rule-id>`
- Verify that when the CLI encounters errors, it writes strictly the backend `error` code to stderr and exits with exit code `1`.

## Mandatory Twitch OAuth Manual Flow

- Configure local Twitch Client ID (`Twitch:ClientId`) for the Web API. If the Twitch App is a confidential client, additionally configure `Twitch:ClientSecret` to route through the authorization-code callback flow; if it is a public client, omit the secret, and the CLI routes through the device-code flow.
- Execute CLI OAuth commands.
- The browser opens or the CLI prints the Twitch authorization URL.
- The CLI callback listener accepts loopback requests strictly on `/auth/callback`.
- The API exchanges the `code` using the PKCE verifier, storing the refresh token via `IOAuthTokenStore`.
- Due to `OAUTH_CREDENTIAL_NAMESPACE` restrictions, `/api/config/oauth.twitch.refresh_token` remains in a forbidden state.

If `Twitch:ClientId` is not set, non-Twitch CLI commands remain available. The interactive REPL queries `/api/twitch/auth/status` on startup, printing a no-Twitch mode warning and continuing; executing `twitch auth start` inside the REPL prints `TWITCH_CLIENT_ID_MISSING` and setup hints directly, without establishing OAuth sessions or calling `/api/twitch/auth/start`.

## Manual Command Templates

### 1. Configure Environment and Launch Web Host (Terminal A)

```powershell
$env:Twitch__ClientId = "<Your Twitch App Client ID>"
# Optional: Only set for confidential Twitch Apps.
# Omit for public clients; the CLI will use device-code flows.
$env:Twitch__ClientSecret = "<Your Twitch App Client Secret>"

# /m:1 /nr:false /p:UseSharedCompilation=false: Forces single-threaded build and avoids build server reuse, preventing intermittent failures from SQLite file locks and rtk sandbox interactions. If this mitigation is not required, use standard `dotnet build`.
rtk proxy powershell -NoProfile -Command "dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false"

rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Web"
```

> **IMPORTANT:** The Web host defaults to attempting `FirstApiPort = 5000`. If 5000 is occupied, it skips to 5002 / 5004 / ... per `PortAllocationOptions` (`step = 2`, max 5008). **Extract the actual port from the `Now listening on: http://127.0.0.1:<port>` console output on startup**, and insert that port into `VULPERONEX_API_URL` below. **DO NOT** assume it is always 5000.

### 2. Publish Independent CLI Executable (Required for OAuth Flows)

```powershell
rtk proxy powershell -NoProfile -Command "dotnet publish src\Hosts\Vulperonex.Cli -c Release -o artifacts\cli-manual"
```

### 3. Execute One-Shot CLI Commands (Terminal B)

Replace `<api_port>` with the actual port printed in Terminal A's console.

> **Task 16f L46:** This table first uses `dotnet run` to complete functional verification; after passing, repeat the run using `artifacts\cli-manual\Vulperonex.Cli.exe` (the published binary from Step 2) against the same Web host, verifying that exit codes, stdout, and stderr for each command match `dotnet run` exactly. Since the Codex sandbox rejects background Web host launches, published paths must be manually verified in a local terminal to satisfy Gate conditions.

```powershell
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"

rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- rule list"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- config get log.min_level"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- member list"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- member seed manual-user ManualUser"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate chat hello from cli"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate follow"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate sub"
rtk proxy powershell -NoProfile -Command "dotnet run --project src\Hosts\Vulperonex.Cli -- simulate"
```

When setting the API URL and entering the REPL on a single line, use single quotes:

```powershell
rtk proxy powershell -NoProfile -Command '$env:VULPERONEX_API_URL="http://127.0.0.1:<api_port>"; dotnet run --project src\Hosts\Vulperonex.Cli -- --interactive'
```

### 4. Pass / Fail Acceptance

| Command | Expected Exit | Expected Stdout | Expected Stderr |
|------|-----------|-------------|-------------|
| `rule list` | 0 | JSON array (can be empty `[]`), pretty-printed | Empty |
| `config get log.min_level` | 0 | JSON object with `value` field | Empty |
| `config get oauth.twitch.refresh_token` | 1 | Empty | `OAUTH_CREDENTIAL_NAMESPACE` |
| `member list` | 0 | JSON array (default pagination) | Empty |
| `member seed manual-user ManualUser` | 0 | `OK member seed requested: manual-user` and `OK member available: <member-id>` | Empty |
| `member delete <member-id>` | 0 | `OK member deleted: <member-id>` | Empty |
| `simulate` | 0 | Partial help, listing `chat`, `follow`, `sub` | Empty |
| `simulate chat hello from cli` | 0 | JSON ack containing `accepted: true`, `eventTypeKey: user.message`, `eventId` | Empty |
| `simulate follow` | 0 | JSON ack containing `eventTypeKey: user.followed`, `eventId` | Empty |
| `simulate sub` | 0 | JSON ack containing `eventTypeKey: user.subscribed`, `eventId` | Empty |
| `rule create <rule.json>` | 0 | Created rule JSON | Empty |
| `rule update <rule-id> <rule.json>` | 0 | Updated rule JSON | Empty |
| `rule disable <rule-id>` | 0 | `OK rule disabled: <rule-id>` | Empty |
| `rule enable <rule-id>` | 0 | `OK rule enabled: <rule-id>` | Empty |
| `rule delete <rule-id>` | 0 | `OK rule deleted: <rule-id>` | Empty |
| Any command against non-loopback `VULPERONEX_API_URL` | 1 | Empty | `CLI_API_URL_NOT_LOOPBACK` |

### 4b. Phase 5.5 CLI Resolver Verification

These checks cover the resolver contracts added after Phase 5 CLI smoke. If Phase 6 Web UI plans to adopt CLI behaviors for manual verification reference, ensure the following paths are operational.

| Command | Expected Exit | Expected Stdout | Expected Stderr |
|------|-----------|-------------|-------------|
| `rule show <full-rule-id>` | 0 | Rule JSON | Empty |
| `rule show <unique-rule-id-prefix>` | 0 | Resolved Rule JSON | Empty |
| `rule show <ambiguous-prefix>` | 1 | Empty | `AMBIGUOUS_ID` with candidate list |
| `rule show <missing-prefix>` | 1 | Empty | `NOT_FOUND` |
| `rule show --name <exact-rule-name>` | 0 | Resolved Rule JSON | Empty |
| `rule show <id> --name <name>` | 1 | Empty | `INVALID_ARGS` |
| `rule disable <id>` (one-shot) | 1 | Empty | `CONFIRMATION_REQUIRED` with rule summary |
| `rule disable <id> --yes` | 0 | `OK rule disabled: <id>` | Empty |
| `rule delete <id>` (one-shot) | 1 | Empty | `CONFIRMATION_REQUIRED` with rule summary |
| `rule delete <id> --yes` | 0 | `OK rule deleted: <id>` | Empty |
| `member show <full-member-id>` | 0 | Member JSON | Empty |
| `member show <unique-member-id-prefix>` | 0 | Resolved Member JSON | Empty |
| `member show <ambiguous-prefix>` | 1 | Empty | `AMBIGUOUS_ID` with candidate list |
| `member delete <id>` (one-shot) | 1 | Empty | `CONFIRMATION_REQUIRED` with member summary |
| `member delete <id> --yes` | 0 | `OK member deleted: <id>` | Empty |
| Interactive `rule delete <id>` then input `y` | 0 | confirm prompt and success output | Empty |
| Interactive `rule delete <id>` then input `n` | 1 | confirm prompt | `CANCELLED` |

Automated proof: Running `dotnet test tests\Vulperonex.Tests.Integration\Vulperonex.Tests.Integration.csproj --filter FullyQualifiedName~CliCommandTests /m:1 /nr:false /p:UseSharedCompilation=false` on 2026-05-20 resulted in 61 tests passing.

Any single line failure is treated as a Gate blocker, recorded as `FAIL` in the "Status" section below, requiring a task to fix.

### 5. Twitch OAuth Flow (Implemented in Task 16f)

```powershell
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"
rtk proxy powershell -NoProfile -Command ".\artifacts\cli-manual\Vulperonex.Cli.exe twitch auth start"
```

Only for URL generation in non-browser environments (does not start `HttpListener`, prints only `authorizeUrl`, `state`, and `callbackPort`):

```powershell
rtk proxy powershell -NoProfile -Command ".\artifacts\cli-manual\Vulperonex.Cli.exe twitch auth start --no-browser"
```

The browser flow waits on `http://localhost:<callbackPort>/auth/callback`, then POSTs the returned `code` and `state` to `/api/twitch/auth/complete`.

**OAuth Acceptance Criteria:**

| Step | Expected |
|------|------|
| `twitch auth start` (public client) | CLI opens or lists `https://www.twitch.tv/activate` and prints user code; CLI prints `Twitch authorization completed.` after authorization |
| `twitch auth start` (confidential client) | Browser opens Twitch authorization page; CLI prints `Opened Twitch authorization URL. Waiting on ...` |
| User consents in browser and token exchange succeeds | Browser displays `Twitch authorization completed`, prompting return to the CLI |
| CLI completes | exit 0; refresh token written to SQLite |
| Twitch token exchange fails | CLI stderr `TWITCH_OAUTH_EXCHANGE_FAILED`, browser or CLI displays authorization incomplete |
| `twitch auth reset` | exit 0; stored refresh token cleared, subsequent status displays unauthorized, allows repeating `twitch auth start` |
| Repeat `config get oauth.twitch.refresh_token` | exit 1, stderr `OAUTH_CREDENTIAL_NAMESPACE` (**NOT** opened even after authorization) |

### 5b. REPL TTY Interactive Behavior Manual Verification (Task 16f L60 / 16g)

Automated integration tests cannot cover physical TTY key presses; the following flow must be executed at least once in **Windows Terminal** + **PowerShell 7**. `cmd.exe` and PowerShell 5.1 are optional.

**Setup:** Terminal A's Web host remains active (Step 1); Terminal B sets up `VULPERONEX_API_URL` and launches the REPL:

```powershell
$env:VULPERONEX_API_URL = "http://127.0.0.1:<api_port>"
dotnet run --project src\Hosts\Vulperonex.Cli -- --interactive
```

Or using the published executable (recommended for OAuth cancellation tests to prevent `dotnet run` child processes from intercepting Ctrl+C):

```powershell
.\artifacts\cli-manual\Vulperonex.Cli.exe --interactive
```

#### 5b-1 Tab Completion

| Input Sequence | Expected |
|----------|------|
| `ru<Tab>` | displays `rule ` |
| `rule li<Tab>` | displays `rule list` |
| `twitch a<Tab>` | displays `twitch auth ` |
| `twitch auth st<Tab>` | displays `twitch auth start` |
| `xy<Tab>` | buffer unchanged, no noise output |

#### 5b-2 History Navigation (↑ / ↓)

1. Enter sequentially: `rule list<Enter>`, `member list<Enter>`, `config get log.min_level<Enter>`.
2. Press `↑` once → expected display: `config get log.min_level`.
3. Press `↑` twice → expected display: `member list`.
4. Press `↑` three times → expected display: `rule list`.
5. Press `↑` again → buffer unchanged (reached the oldest record).
6. Press `↓` → retreats through `member list`, `config get log.min_level`, and an empty buffer below the last entry.
7. Deduplication rule for new inputs: submitting `rule list` twice consecutively retains only one copy in history (pressing `↑` sequentially retreats `config ... / member list / rule list` without duplicate `rule list` entries).

#### 5b-3 Ctrl+C Clearing Buffer

1. Type a partial line `rule lis` at the prompt (**DO NOT** press Enter).
2. Press `Ctrl+C`.
3. Expected: current line displays `^C`, shifts to a new line, prompt `vulperonex> ` is reprinted, and the REPL remains active.
4. Immediately enter `exit<Enter>` → REPL terminates normally, exit code 0.

#### 5b-4 Ctrl+C Canceling `twitch auth start`

Requires an environment where `Twitch:ClientId` is configured but authorization is **NOT** completed (or environment variable `Twitch__ClientSecret` is removed to force device flows, or directly canceling the browser for confidential flows).

**Confidential client path:**
1. Type `twitch auth start<Enter>` inside the REPL.
2. CLI prints `Opened Twitch authorization URL. Waiting on http://localhost:<port>/auth/callback`, and the browser opens the Twitch authorization page.
3. **DO NOT** consent in the browser; switch back to Terminal B, and press `Ctrl+C`.
4. Expected:
   - stderr prints `TWITCH_OAUTH_CANCELLED`.
   - prompt `vulperonex> ` is reprinted, and the REPL remains active.
   - Terminal A's Web host does not encounter unhandled exceptions.
   - If the browser remains open and authorization completes subsequently, the callback displays connection refused (expected, as the state is invalidated).

**Public client (device flow) path:**
1. Type `twitch auth start<Enter>` inside the REPL.
2. CLI prints `Twitch public-client authorization` + `Open: <url>` + `Code: <user_code>`.
3. **DO NOT** enter the code in the browser; press `Ctrl+C` in Terminal B.
4. Expected: same as the confidential path above (`TWITCH_OAUTH_CANCELLED` + REPL remains active).

#### 5b-5 LineEditor and Ctrl+C Routing Verification

**Ctrl+C when buffer contains text must not cancel dispatch:** typing `rule lis` at the prompt and pressing `Ctrl+C` **must not** print `TWITCH_OAUTH_CANCELLED` or other dispatch error codes in stderr (as dispatch is not entered yet); strictly clears the current buffer.

**Ctrl+C when dispatch is active:** covered in 5b-4. `twitch auth start` is the only command currently implementing Ctrl+C cancellation; other commands (like `rule list`) return quickly, so no observable cancellation behavior is expected.

#### 5b-6 Redirected stdin Fallback

```powershell
"rule list`nexit" | dotnet run --project src\Hosts\Vulperonex.Cli
```

Expected: prints `rule list` JSON then exits with code 0; does not start the LineEditor (no ANSI or key press errors).

#### 5b-7 Verification Fields

Append to the "Status" section of this file in the following format:

```
### <YYYY-MM-DD> REPL Interactive Verification | Verifier: <name>
- Terminal: Windows Terminal vX / PowerShell 7.X
- 5b-1 Tab: PASS / FAIL (record actual output of `twitch auth st`)
- 5b-2 History: PASS / FAIL
- 5b-3 Ctrl+C Clearing Buffer: PASS / FAIL
- 5b-4 Ctrl+C Canceling OAuth: PASS / FAIL (path: confidential / device)
- 5b-5 Routing: PASS / FAIL
- 5b-6 Redirected stdin: PASS / FAIL
- Remarks:
```

### 6. Cleanup

```powershell
Remove-Item Env:Twitch__ClientId
Remove-Item Env:Twitch__ClientSecret
Remove-Item Env:VULPERONEX_API_URL
# Press Ctrl+C in Terminal A to terminate the Web host
```

To permanently set ClientId:

```powershell
[Environment]::SetEnvironmentVariable('Twitch__ClientId', '<id>', 'User')
```

> **Security:** `Twitch:ClientId` is a public OAuth value; `Twitch:ClientSecret` is a secret value required only for confidential clients. Public clients must not set the secret. Secrets must be placed strictly in local ignored development settings or single-use environment variables, not committed to the repo.

## Status

Append entries of each execution in the following format, aligning with `manual-verification.md` conventions (Phase 5 manual verification records from plan.md):

```
### <YYYY-MM-DD> Verifier: <name>
- Environment: Windows 11 / PowerShell 7 / .NET <version> / Twitch ClientId set: yes/no
- Commands: <selected from the table above or all>
- Expected: <reference the table above>
- Observation: <actual stdout/stderr/exit>
- Result: PASS / FAIL
- Remarks: <rtk on/off, actual port value, others>
```

History:

- 2026-05-17 | Recorder: lydek | Action: Initial version of Gate document established | Result: N/A (document only)
- 2026-05-17 | Recorder: lydek | Action: Added automated Phase 5/Cli test coverage for startup migration, CLI smoke tests against live APIs, Twitch OAuth startup URLs, and Twitch OAuth token storage completion | Result: Automated tests PASS
- 2026-05-17 | Recorder: lydek | Pending: CLI published to `artifacts/cli-manual/`, sandbox policies reject background Web host launches, published binary smoke tests remain pending manual execution in a local terminal | Owner: lydek | Due: Before starting Phase 6
- 2026-05-19 | Recorder: Codex | Action: Added CLI manual test UX, automated coverage for `simulate` partial help, `rule create/update`, `member seed/delete`, `twitch auth reset` | Result: Automated tests PASS; published CLI verification remains pending on local terminal
- 2026-05-19 | Recorder: Codex | Action: Added explicit `OK ...` output for empty successful responses, registered Web host member event consumer to ensure `member seed` data appears in `member list` | Result: Automated tests PASS
- 2026-05-19 | Recorder: Codex | Action: `POST /api/simulate/{alias}` returns traceable JSON ack, CLI displays `eventId` / `eventTypeKey` to confirm event publishing on Web API | Result: Automated tests PASS
